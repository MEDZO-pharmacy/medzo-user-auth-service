using System.Security.Claims;
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
        savedRefreshToken.TokenHash.Should().NotBe("raw-refresh-token");
        savedRefreshToken.ExpiresAt.Should().Be(refreshExpiry);
        _jwtServiceMock.Verify(service => service.GenerateAccessToken(
            It.Is<IEnumerable<Claim>>(claims =>
                claims.Any(claim => claim.Type == ClaimTypes.NameIdentifier && claim.Value == user.Id.ToString()) &&
                claims.Any(claim => claim.Type == ClaimTypes.Role && claim.Value == "User"))), Times.Once);
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
    public async Task RegisterAsync_WithNewUser_ShouldReturnToken()
    {
        var role = new Role { Id = Guid.NewGuid(), Name = "Pharmacist" };
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
        Roles = new List<Role> { new() { Id = Guid.NewGuid(), Name = "User" } }
    };
}
