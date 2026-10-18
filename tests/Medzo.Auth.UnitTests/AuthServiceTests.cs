using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Medzo.Auth.Application.DTOs;
using Medzo.Auth.Application.Exceptions;
using Medzo.Auth.Application.Interfaces;
using Medzo.Auth.Application.Services;
using Medzo.Auth.Domain.Entities;
using Moq;
using Xunit;

namespace Medzo.Auth.UnitTests;

public class AuthServiceTests
{
    private readonly Mock<IJwtService> _jwtServiceMock = new();
    private readonly Mock<IPasswordHasher> _passwordHasherMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IRoleRepository> _roleRepositoryMock = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock = new();
    private readonly Mock<IStaffInvitationRepository> _staffInvitationRepositoryMock = new();
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _authService = new AuthService(
            _jwtServiceMock.Object,
            _passwordHasherMock.Object,
            _userRepositoryMock.Object,
            _roleRepositoryMock.Object,
            _refreshTokenRepositoryMock.Object,
            _staffInvitationRepositoryMock.Object);
    }

    [Fact]
    public void AuthService_ShouldBeCreated()
    {
        Assert.NotNull(_authService);
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ShouldReturnToken()
    {
        var user = ExistingUser();
        var accessExpiry = DateTime.UtcNow.AddHours(1);
        var refreshExpiry = DateTime.UtcNow.AddDays(7);
        RefreshToken? savedRefreshToken = null;
        _userRepositoryMock.Setup(repository => repository.GetByStaffIdAsync("P1001"))
            .ReturnsAsync(user);
        _passwordHasherMock.Setup(hasher => hasher.VerifyPassword("Strong1!", user.PasswordHash))
            .Returns(true);
        ConfigureGeneratedTokens(accessExpiry, refreshExpiry);
        _refreshTokenRepositoryMock
            .Setup(repository => repository.AddAsync(It.IsAny<RefreshToken>()))
            .Callback<RefreshToken>(token => savedRefreshToken = token)
            .Returns(Task.CompletedTask);

        var result = await _authService.LoginAsync(new LoginRequest
        {
            Identifier = " p1001 ",
            Password = "Strong1!"
        });

        result.Token.Should().Be("access-token");
        result.RefreshToken.Should().Be("raw-refresh-token");
        result.ExpiresAt.Should().Be(accessExpiry);
        result.User.Id.Should().Be(user.Id);
        savedRefreshToken.Should().NotBeNull();
        savedRefreshToken!.UserId.Should().Be(user.Id);
        savedRefreshToken.TokenHash.Should().Be(Hash("raw-refresh-token"));
        savedRefreshToken.ExpiresAt.Should().Be(refreshExpiry);
        _jwtServiceMock.Verify(service => service.GenerateAccessToken(
            It.Is<IEnumerable<Claim>>(claims =>
                claims.Any(claim => claim.Type == ClaimTypes.NameIdentifier && claim.Value == user.Id.ToString()) &&
                claims.Any(claim => claim.Type == "staff_id" && claim.Value == "P1001") &&
                claims.Any(claim => claim.Type == ClaimTypes.Role && claim.Value == "Pharmacist"))), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_WithInvalidCredentials_ShouldThrow()
    {
        var user = ExistingUser();
        _userRepositoryMock.Setup(repository => repository.GetByStaffIdAsync("P1001"))
            .ReturnsAsync(user);
        _passwordHasherMock.Setup(hasher => hasher.VerifyPassword("wrong-password", user.PasswordHash))
            .Returns(false);

        var action = () => _authService.LoginAsync(new LoginRequest
        {
            Identifier = "P1001",
            Password = "wrong-password"
        });

        await action.Should().ThrowAsync<UnauthorizedAccessException>();
        _jwtServiceMock.Verify(service => service.GenerateAccessToken(
            It.IsAny<IEnumerable<Claim>>()), Times.Never);
        _refreshTokenRepositoryMock.Verify(repository => repository.AddAsync(
            It.IsAny<RefreshToken>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_WithUnknownIdentifier_ShouldThrowWithoutCheckingPassword()
    {
        _userRepositoryMock.Setup(repository => repository.GetByStaffIdAsync("P4040"))
            .ReturnsAsync((User?)null);
        _userRepositoryMock.Setup(repository => repository.GetByUsernameAsync("P4040"))
            .ReturnsAsync((User?)null);
        _userRepositoryMock.Setup(repository => repository.GetByEmailAsync("p4040"))
            .ReturnsAsync((User?)null);

        var action = () => _authService.LoginAsync(new LoginRequest
        {
            Identifier = "P4040",
            Password = "Strong1!"
        });

        await action.Should().ThrowAsync<UnauthorizedAccessException>();
        _passwordHasherMock.Verify(hasher => hasher.VerifyPassword(
            It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Pharmacist")]
    [InlineData("InventoryManager")]
    public async Task LoginAsync_WithAnySupportedRole_AllowsAccessAmongUnrelatedRoles(string roleName)
    {
        var user = ExistingUser();
        user.Roles = new List<Role>
        {
            new() { Id = "LEG", Name = "Guest" },
            new() { Id = "STAFF", Name = roleName }
        };
        _userRepositoryMock.Setup(repository => repository.GetByStaffIdAsync("P1001"))
            .ReturnsAsync(user);
        _passwordHasherMock.Setup(hasher => hasher.VerifyPassword("Strong1!", user.PasswordHash))
            .Returns(true);
        _refreshTokenRepositoryMock.Setup(repository => repository.AddAsync(It.IsAny<RefreshToken>()))
            .Returns(Task.CompletedTask);
        ConfigureGeneratedTokens(DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddDays(7));

        await _authService.LoginAsync(new LoginRequest
        {
            Identifier = "P1001",
            Password = "Strong1!"
        });

        _jwtServiceMock.Verify(service => service.GenerateAccessToken(
            It.Is<IEnumerable<Claim>>(claims => claims.Any(claim =>
                claim.Type == ClaimTypes.Role && claim.Value == roleName))), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_WithNullStaffId_EmitsEmptyStaffIdClaim()
    {
        var user = ExistingUser();
        user.StaffId = null;
        _userRepositoryMock.Setup(repository => repository.GetByUsernameAsync("valid.user"))
            .ReturnsAsync(user);
        _passwordHasherMock.Setup(hasher => hasher.VerifyPassword("Strong1!", user.PasswordHash))
            .Returns(true);
        _refreshTokenRepositoryMock.Setup(repository => repository.AddAsync(It.IsAny<RefreshToken>()))
            .Returns(Task.CompletedTask);
        ConfigureGeneratedTokens(DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddDays(7));

        await _authService.LoginAsync(new LoginRequest
        {
            Identifier = "valid.user",
            Password = "Strong1!"
        });

        _jwtServiceMock.Verify(service => service.GenerateAccessToken(
            It.Is<IEnumerable<Claim>>(claims => claims.Any(claim =>
                claim.Type == "staff_id" && claim.Value == string.Empty))), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_WithInactiveAccount_ShouldThrow()
    {
        var user = ExistingUser();
        user.IsActive = false;
        _userRepositoryMock.Setup(repository => repository.GetByStaffIdAsync("P1001"))
            .ReturnsAsync(user);

        var action = () => _authService.LoginAsync(new LoginRequest
        {
            Identifier = "P1001",
            Password = "Strong1!"
        });

        await action.Should().ThrowAsync<UnauthorizedAccessException>();
        _jwtServiceMock.Verify(service => service.GenerateAccessToken(
            It.IsAny<IEnumerable<Claim>>()), Times.Never);
    }

    [Fact]

    public async Task LoginAsync_WithLegacyGuestRole_ShouldThrow()
    {
        var user = ExistingUser();
        user.Roles = new List<Role> { new() { Id = "LEG", Name = "User" } };
        _userRepositoryMock.Setup(repository => repository.GetByStaffIdAsync("P1001"))
            .ReturnsAsync(user);
        _passwordHasherMock.Setup(hasher => hasher.VerifyPassword("Strong1!", user.PasswordHash))
            .Returns(true);

        var action = () => _authService.LoginAsync(new LoginRequest
        {
            Identifier = "P1001",
            Password = "Strong1!"
        });

        await action.Should().ThrowAsync<UnauthorizedAccessException>();
        _jwtServiceMock.Verify(service => service.GenerateAccessToken(
            It.IsAny<IEnumerable<Claim>>()), Times.Never);
    }

    [Fact]
    public async Task RefreshTokenAsync_AfterRoleChange_IssuesSessionWithCurrentRole()
    {
        var user = ExistingUser();
        user.Roles = new List<Role>
        {
            new() { Id = "003", Name = "InventoryManager" }
        };
        var currentToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            TokenHash = "stored-token-hash",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        };
        _refreshTokenRepositoryMock.Setup(repository => repository.GetByHashAsync(Hash("current-refresh-token")))
            .ReturnsAsync(currentToken);
        _refreshTokenRepositoryMock.Setup(repository => repository.RotateAsync(
                It.IsAny<RefreshToken>(), It.IsAny<RefreshToken>()))
            .ReturnsAsync(true);
        ConfigureGeneratedTokens(DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddDays(7));

        var result = await _authService.RefreshTokenAsync("current-refresh-token");

        result.User.Roles.Should().ContainSingle().Which.Should().Be("InventoryManager");
        currentToken.RevokedAt.Should().NotBeNull();
        currentToken.ReplacedByTokenHash.Should().Be(Hash("raw-refresh-token"));
        _jwtServiceMock.Verify(service => service.GenerateAccessToken(
            It.Is<IEnumerable<Claim>>(claims =>
                claims.Any(claim => claim.Type == ClaimTypes.Role && claim.Value == "InventoryManager") &&
                claims.All(claim => claim.Type != ClaimTypes.Role || claim.Value != "Pharmacist"))), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_WithNewUser_ShouldReturnToken()
    {
        var role = new Role { Id = "002", Name = "Pharmacist" };
        User? savedUser = null;
        _roleRepositoryMock.Setup(repository => repository.GetByNameAsync("Pharmacist"))
            .ReturnsAsync(role);
        _staffInvitationRepositoryMock.Setup(repository => repository.GetByStaffIdAsync("P2001"))
            .ReturnsAsync(new StaffInvitation { Id = Guid.NewGuid(), StaffId = "P2001", Role = "Pharmacist" });
        _staffInvitationRepositoryMock.Setup(repository => repository.MarkClaimedAsync(It.IsAny<StaffInvitation>()))
            .Returns(Task.CompletedTask);
        _passwordHasherMock.Setup(hasher => hasher.HashPassword("Strong1!"))
            .Returns("password-hash");
        _userRepositoryMock.Setup(repository => repository.AddAsync(It.IsAny<User>()))
            .Callback<User>(user => savedUser = user)
            .ReturnsAsync((User user) => user);
        _refreshTokenRepositoryMock.Setup(repository => repository.AddAsync(It.IsAny<RefreshToken>()))
            .Returns(Task.CompletedTask);
        ConfigureGeneratedTokens(DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddDays(7));

        var result = await _authService.RegisterAsync(new RegisterUserRequest
        {
            Username = " new.user ",
            StaffId = " p2001 ",
            Email = "NEW.USER@EXAMPLE.COM ",
            Password = "Strong1!",
            ConfirmPassword = "Strong1!",
            FirstName = " New ",
            LastName = " User "
        });

        result.Token.Should().Be("access-token");
        result.User.Username.Should().Be("new.user");
        result.User.Email.Should().Be("new.user@example.com");
        result.User.StaffId.Should().Be("P2001");
        result.User.Roles.Should().ContainSingle().Which.Should().Be("Pharmacist");
        savedUser.Should().NotBeNull();
        savedUser!.PasswordHash.Should().Be("password-hash");
        savedUser.IsActive.Should().BeTrue();
        savedUser.FirstName.Should().Be("New");
        savedUser.LastName.Should().Be("User");
        _staffInvitationRepositoryMock.Verify(repository => repository.MarkClaimedAsync(
            It.Is<StaffInvitation>(invitation => invitation.StaffId == "P2001")), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_WithoutInvitation_ShouldThrow()
    {
        _staffInvitationRepositoryMock.Setup(repository => repository.GetByStaffIdAsync("P2001"))
            .ReturnsAsync((StaffInvitation?)null);

        var action = () => _authService.RegisterAsync(ValidRegistrationRequest());

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("This Staff ID has not been approved or has already been used.");
        _userRepositoryMock.Verify(repository => repository.AddAsync(It.IsAny<User>()), Times.Never);
    }

    [Theory]
    [InlineData("P2001", "Pharmacist", true)]
    [InlineData("P2001", "Admin", false)]
    [InlineData("A2001", "Pharmacist", false)]
    public async Task RegisterAsync_WithIneligibleInvitation_ShouldThrow(
        string staffId, string role, bool isClaimed)
    {
        var request = ValidRegistrationRequest();
        request.StaffId = staffId;
        _staffInvitationRepositoryMock.Setup(repository => repository.GetByStaffIdAsync(staffId))
            .ReturnsAsync(new StaffInvitation
            {
                Id = Guid.NewGuid(),
                StaffId = staffId,
                Role = role,
                IsClaimed = isClaimed
            });

        var action = () => _authService.RegisterAsync(request);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("This Staff ID has not been approved or has already been used.");
        _userRepositoryMock.Verify(repository => repository.AddAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_WithDuplicateStaffId_ShouldThrow()
    {
        ConfigureValidInvitation();
        _userRepositoryMock.Setup(repository => repository.GetByStaffIdAsync("P2001"))
            .ReturnsAsync(ExistingUser());

        var action = () => _authService.RegisterAsync(ValidRegistrationRequest());

        await action.Should().ThrowAsync<UserConflictException>()
            .WithMessage("A user with this Staff ID already exists.");
    }

    [Fact]
    public async Task RegisterAsync_WithDuplicateEmail_ShouldThrow()
    {
        ConfigureValidInvitation();
        _userRepositoryMock.Setup(repository => repository.GetByEmailAsync("new.user@example.com"))
            .ReturnsAsync(ExistingUser());

        var action = () => _authService.RegisterAsync(ValidRegistrationRequest());

        await action.Should().ThrowAsync<UserConflictException>()
            .WithMessage("A user with this email address already exists.");
    }

    [Fact]
    public async Task RegisterAsync_WithoutConfiguredRole_ShouldThrow()
    {
        ConfigureValidInvitation();
        _roleRepositoryMock.Setup(repository => repository.GetByNameAsync("Pharmacist"))
            .ReturnsAsync((Role?)null);

        var action = () => _authService.RegisterAsync(ValidRegistrationRequest());

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("The Pharmacist role is not configured.");
        _passwordHasherMock.Verify(hasher => hasher.HashPassword(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RefreshTokenAsync_WithBlankToken_ShouldThrowBeforeRepositoryLookup(string? value)
    {
        var action = () => _authService.RefreshTokenAsync(value!);

        await action.Should().ThrowAsync<InvalidRefreshTokenException>();
        _refreshTokenRepositoryMock.Verify(repository => repository.GetByHashAsync(
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RefreshTokenAsync_WithUnknownToken_ShouldThrow()
    {
        _refreshTokenRepositoryMock.Setup(repository => repository.GetByHashAsync(Hash("unknown")))
            .ReturnsAsync((RefreshToken?)null);

        var action = () => _authService.RefreshTokenAsync("unknown");

        await action.Should().ThrowAsync<InvalidRefreshTokenException>();
    }

    [Theory]
    [InlineData("revoked")]
    [InlineData("expired")]
    [InlineData("inactive")]
    [InlineData("unauthorized-role")]
    public async Task RefreshTokenAsync_WithInvalidStoredSession_ShouldThrow(string reason)
    {
        var token = ValidStoredRefreshToken();
        if (reason == "revoked") token.RevokedAt = DateTime.UtcNow.AddMinutes(-1);
        if (reason == "expired") token.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        if (reason == "inactive") token.User.IsActive = false;
        if (reason == "unauthorized-role")
            token.User.Roles = new List<Role> { new() { Id = "LEG", Name = "Guest" } };
        _refreshTokenRepositoryMock.Setup(repository => repository.GetByHashAsync(Hash("current")))
            .ReturnsAsync(token);

        var action = () => _authService.RefreshTokenAsync("current");

        await action.Should().ThrowAsync<InvalidRefreshTokenException>();
        _refreshTokenRepositoryMock.Verify(repository => repository.RotateAsync(
            It.IsAny<RefreshToken>(), It.IsAny<RefreshToken>()), Times.Never);
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenAtomicRotationFails_ShouldThrow()
    {
        var token = ValidStoredRefreshToken();
        _refreshTokenRepositoryMock.Setup(repository => repository.GetByHashAsync(Hash("current")))
            .ReturnsAsync(token);
        _refreshTokenRepositoryMock.Setup(repository => repository.RotateAsync(
                It.IsAny<RefreshToken>(), It.IsAny<RefreshToken>()))
            .ReturnsAsync(false);
        ConfigureGeneratedTokens(DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddDays(7));

        var action = () => _authService.RefreshTokenAsync("current");

        await action.Should().ThrowAsync<InvalidRefreshTokenException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RevokeTokenAsync_WithBlankToken_ReturnsWithoutRepositoryLookup(string? value)
    {
        await _authService.RevokeTokenAsync(value!);

        _refreshTokenRepositoryMock.Verify(repository => repository.GetByHashAsync(
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RevokeTokenAsync_WithUnknownToken_DoesNotRevoke()
    {
        _refreshTokenRepositoryMock.Setup(repository => repository.GetByHashAsync(Hash("unknown")))
            .ReturnsAsync((RefreshToken?)null);

        await _authService.RevokeTokenAsync("unknown");

        _refreshTokenRepositoryMock.Verify(repository => repository.RevokeAsync(
            It.IsAny<RefreshToken>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task RevokeTokenAsync_WithAlreadyRevokedToken_DoesNotRevokeAgain()
    {
        var token = ValidStoredRefreshToken();
        token.RevokedAt = DateTime.UtcNow.AddMinutes(-1);
        _refreshTokenRepositoryMock.Setup(repository => repository.GetByHashAsync(Hash("revoked")))
            .ReturnsAsync(token);

        await _authService.RevokeTokenAsync("revoked");

        _refreshTokenRepositoryMock.Verify(repository => repository.RevokeAsync(
            It.IsAny<RefreshToken>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task RevokeTokenAsync_WithActiveToken_RevokesUsingCurrentTimestamp()
    {
        var token = ValidStoredRefreshToken();
        var before = DateTime.UtcNow;
        _refreshTokenRepositoryMock.Setup(repository => repository.GetByHashAsync(Hash("active")))
            .ReturnsAsync(token);

        await _authService.RevokeTokenAsync("active");

        var after = DateTime.UtcNow;
        _refreshTokenRepositoryMock.Verify(repository => repository.RevokeAsync(
            token, It.Is<DateTime>(timestamp => timestamp >= before && timestamp <= after)), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_WithDuplicateUsername_ShouldThrow()
    {
        _staffInvitationRepositoryMock.Setup(repository => repository.GetByStaffIdAsync("P3001"))
            .ReturnsAsync(new StaffInvitation { Id = Guid.NewGuid(), StaffId = "P3001", Role = "Pharmacist" });
        _userRepositoryMock.Setup(repository => repository.GetByUsernameAsync("existing.user"))
            .ReturnsAsync(ExistingUser());

        var action = () => _authService.RegisterAsync(new RegisterUserRequest
        {
            Username = "existing.user",
            StaffId = "P3001",
            Email = "new@example.com",
            Password = "Strong1!",
            ConfirmPassword = "Strong1!",
            FirstName = "Existing",
            LastName = "User"
        });

        await action.Should().ThrowAsync<UserConflictException>()
            .WithMessage("*username*");
        _userRepositoryMock.Verify(repository => repository.AddAsync(It.IsAny<User>()), Times.Never);
        _passwordHasherMock.Verify(hasher => hasher.HashPassword(It.IsAny<string>()), Times.Never);
        _refreshTokenRepositoryMock.Verify(repository => repository.AddAsync(
            It.IsAny<RefreshToken>()), Times.Never);
    }

    private void ConfigureGeneratedTokens(DateTime accessExpiry, DateTime refreshExpiry)
    {
        _jwtServiceMock.Setup(service => service.GenerateAccessToken(
                It.IsAny<IEnumerable<Claim>>()))
            .Returns("access-token");
        _jwtServiceMock.Setup(service => service.GenerateRefreshToken())
            .Returns("raw-refresh-token");
        _jwtServiceMock.Setup(service => service.GetAccessTokenExpiration())
            .Returns(accessExpiry);
        _jwtServiceMock.Setup(service => service.GetRefreshTokenExpiration())
            .Returns(refreshExpiry);
    }

    private void ConfigureValidInvitation()
    {
        _staffInvitationRepositoryMock.Setup(repository => repository.GetByStaffIdAsync("P2001"))
            .ReturnsAsync(new StaffInvitation
            {
                Id = Guid.NewGuid(),
                StaffId = "P2001",
                Role = "Pharmacist"
            });
    }

    private static RegisterUserRequest ValidRegistrationRequest() => new()
    {
        Username = "new.user",
        StaffId = "P2001",
        Email = "new.user@example.com",
        Password = "Strong1!",
        ConfirmPassword = "Strong1!",
        FirstName = "New",
        LastName = "User"
    };

    private static RefreshToken ValidStoredRefreshToken()
    {
        var user = ExistingUser();
        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            TokenHash = Hash("current"),
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        };
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static User ExistingUser() => new()
    {
        Id = Guid.NewGuid(),
        Username = "valid.user",
        StaffId = "P1001",
        Email = "valid@example.com",
        PasswordHash = "password-hash",
        FirstName = "Valid",
        LastName = "User",
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        Roles = new List<Role> { new() { Id = "002", Name = "Pharmacist" } }
    };
}
