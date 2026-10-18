using System.Security.Cryptography;
using System.Text;
using Medzo.Auth.Application.Exceptions;
using Medzo.Auth.Application.Interfaces;
using Medzo.Auth.Application.Services;
using Medzo.Auth.Domain.Entities;
using Moq;
using Xunit;

namespace Medzo.Auth.UnitTests;

public sealed class RefreshTokenSecurityTests
{
    private readonly Mock<IRefreshTokenRepository> tokens = new(MockBehavior.Strict);
    private readonly Mock<IJwtService> jwt = new(MockBehavior.Strict);
    private AuthService Service() => new(jwt.Object, Mock.Of<IPasswordHasher>(),
        Mock.Of<IUserRepository>(), Mock.Of<IRoleRepository>(), tokens.Object, Mock.Of<IStaffInvitationRepository>());
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static RefreshToken ValidToken() => new()
    {
        UserId = Guid.NewGuid(), TokenHash = Hash("old-token"), ExpiresAt = DateTime.UtcNow.AddDays(1),
        User = new User { IsActive = true, Roles = [new Role { Id = "002", Name = "Pharmacist" }] }
    };

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task Blank_refresh_token_is_rejected_without_repository_access(string? value)
    {
        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => Service().RefreshTokenAsync(value!));
        tokens.VerifyNoOtherCalls();
        jwt.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("expired")]
    [InlineData("revoked")]
    [InlineData("inactive-user")]
    [InlineData("unauthorized-role")]
    public async Task Unusable_session_cannot_issue_or_rotate_tokens(string reason)
    {
        var token = ValidToken();
        if (reason == "expired") token.ExpiresAt = DateTime.UtcNow.AddDays(-1);
        if (reason == "revoked") token.RevokedAt = DateTime.UtcNow.AddMinutes(-1);
        if (reason == "inactive-user") token.User.IsActive = false;
        if (reason == "unauthorized-role") token.User.Roles = [];
        tokens.Setup(r => r.GetByHashAsync(Hash("old-token"))).ReturnsAsync(reason == "missing" ? null : token);

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => Service().RefreshTokenAsync("old-token"));

        tokens.Verify(r => r.RotateAsync(It.IsAny<RefreshToken>(), It.IsAny<RefreshToken>()), Times.Never);
        jwt.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Concurrent_rotation_failure_does_not_issue_access_token()
    {
        var token = ValidToken();
        tokens.Setup(r => r.GetByHashAsync(Hash("old-token"))).ReturnsAsync(token);
        jwt.Setup(j => j.GenerateRefreshToken()).Returns("replacement-secret");
        jwt.Setup(j => j.GetRefreshTokenExpiration()).Returns(DateTime.UtcNow.AddDays(7));
        tokens.Setup(r => r.RotateAsync(token, It.Is<RefreshToken>(t =>
            t.TokenHash == Hash("replacement-secret") && t.UserId == token.UserId))).ReturnsAsync(false);

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => Service().RefreshTokenAsync("old-token"));

        tokens.Verify(r => r.RotateAsync(token, It.IsAny<RefreshToken>()), Times.Once);
        jwt.Verify(j => j.GenerateAccessToken(It.IsAny<IEnumerable<System.Security.Claims.Claim>>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Blank_logout_token_is_a_no_op(string value)
    {
        await Service().RevokeTokenAsync(value);
        tokens.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Logout_revokes_the_hashed_token_once()
    {
        var token = ValidToken();
        tokens.Setup(r => r.GetByHashAsync(Hash("old-token"))).ReturnsAsync(token);
        var before = DateTime.UtcNow;
        tokens.Setup(r => r.RevokeAsync(token, It.Is<DateTime>(t => t >= before))).ReturnsAsync(true);

        await Service().RevokeTokenAsync("old-token");

        tokens.Verify(r => r.RevokeAsync(token, It.IsAny<DateTime>()), Times.Once);
        jwt.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Repeated_logout_does_not_revoke_again()
    {
        var token = ValidToken();
        token.RevokedAt = DateTime.UtcNow.AddMinutes(-1);
        tokens.Setup(r => r.GetByHashAsync(Hash("old-token"))).ReturnsAsync(token);

        await Service().RevokeTokenAsync("old-token");

        tokens.Verify(r => r.RevokeAsync(It.IsAny<RefreshToken>(), It.IsAny<DateTime>()), Times.Never);
    }
}
