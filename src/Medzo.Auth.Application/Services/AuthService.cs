using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Medzo.Auth.Application.DTOs;
using Medzo.Auth.Application.Exceptions;
using Medzo.Auth.Application.Interfaces;
using Medzo.Auth.Domain.Entities;

namespace Medzo.Auth.Application.Services;

public class AuthService : IAuthService
{
    private readonly IJwtService _jwtService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IStaffInvitationRepository _staffInvitations;

    public AuthService(
        IJwtService jwtService,
        IPasswordHasher passwordHasher,
        IUserRepository users,
        IRoleRepository roles,
        IRefreshTokenRepository refreshTokens,
        IStaffInvitationRepository staffInvitations)
    {
        _jwtService = jwtService;
        _passwordHasher = passwordHasher;
        _users = users;
        _roles = roles;
        _refreshTokens = refreshTokens;
        _staffInvitations = staffInvitations;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var identifier = request.Identifier.Trim();
        User? user = null;
        if (StaffRoleResolver.IsValid(identifier))
            user = await _users.GetByStaffIdAsync(StaffRoleResolver.Normalize(identifier));

        user ??= await _users.GetByUsernameAsync(identifier);
        user ??= await _users.GetByEmailAsync(identifier.ToLowerInvariant());

        if (user is null || !user.IsActive ||
            !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException();
        }

        return await CreateLoginResponseAsync(user);
    }

    public async Task<LoginResponse> RegisterAsync(RegisterUserRequest request)
    {
        var username = request.Username.Trim();
        var staffId = StaffRoleResolver.Normalize(request.StaffId);
        var email = request.Email.Trim().ToLowerInvariant();

        var invitation = await _staffInvitations.GetByStaffIdAsync(staffId);
        if (invitation is null || invitation.IsClaimed || invitation.Role == "Admin" ||
            !StaffRoleResolver.IsSignupEligible(staffId))
            throw new InvalidOperationException("This Staff ID has not been approved or has already been used.");

        if (await _users.GetByStaffIdAsync(staffId) is not null)
            throw new UserConflictException("A user with this Staff ID already exists.");

        if (await _users.GetByUsernameAsync(username) is not null)
            throw new UserConflictException("A user with this username already exists.");

        if (await _users.GetByEmailAsync(email) is not null)
            throw new UserConflictException("A user with this email address already exists.");

        var roleName = invitation.Role;
        var role = await _roles.GetByNameAsync(roleName)
            ?? throw new InvalidOperationException($"The {roleName} role is not configured.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            StaffId = staffId,
            Email = email,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Roles = new List<Role> { role }
        };

        var savedUser = await _users.AddAsync(user);
        await _staffInvitations.MarkClaimedAsync(invitation);
        return await CreateLoginResponseAsync(savedUser);
    }

    public async Task<LoginResponse> RefreshTokenAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new InvalidRefreshTokenException();

        var currentToken = await _refreshTokens.GetByHashAsync(HashToken(refreshToken));
        var now = DateTime.UtcNow;
        if (currentToken is null || currentToken.RevokedAt.HasValue ||
            currentToken.ExpiresAt <= now || !currentToken.User.IsActive)
        {
            throw new InvalidRefreshTokenException();
        }

        var replacementValue = _jwtService.GenerateRefreshToken();
        var replacementToken = CreateRefreshToken(currentToken.UserId, replacementValue);
        currentToken.RevokedAt = now;
        currentToken.ReplacedByTokenHash = replacementToken.TokenHash;

        if (!await _refreshTokens.RotateAsync(currentToken, replacementToken))
            throw new InvalidRefreshTokenException();

        return BuildLoginResponse(currentToken.User, replacementValue);
    }

    public async Task RevokeTokenAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return;

        var storedToken = await _refreshTokens.GetByHashAsync(HashToken(refreshToken));
        if (storedToken is null || storedToken.RevokedAt.HasValue)
            return;

        await _refreshTokens.RevokeAsync(storedToken, DateTime.UtcNow);
    }

    private async Task<LoginResponse> CreateLoginResponseAsync(User user)
    {
        var refreshTokenValue = _jwtService.GenerateRefreshToken();
        await _refreshTokens.AddAsync(CreateRefreshToken(user.Id, refreshTokenValue));
        return BuildLoginResponse(user, refreshTokenValue);
    }

    private LoginResponse BuildLoginResponse(User user, string refreshTokenValue)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            new("staff_id", user.StaffId ?? string.Empty)
        };

        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role.Name)));

        return new LoginResponse
        {
            Token = _jwtService.GenerateAccessToken(claims),
            RefreshToken = refreshTokenValue,
            ExpiresAt = _jwtService.GetAccessTokenExpiration(),
            User = Map(user)
        };
    }

    private RefreshToken CreateRefreshToken(Guid userId, string tokenValue) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        TokenHash = HashToken(tokenValue),
        CreatedAt = DateTime.UtcNow,
        ExpiresAt = _jwtService.GetRefreshTokenExpiration()
    };

    private static string HashToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private static UserResponse Map(User user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        StaffId = user.StaffId,
        Email = user.Email,
        FirstName = user.FirstName,
        LastName = user.LastName,
        IsActive = user.IsActive,
        Roles = user.Roles.Select(role => role.Name).ToArray(),
        CreatedAt = user.CreatedAt
    };
}
