using System.Security.Claims;

namespace Medzo.Auth.Application.Interfaces;

public interface IJwtService
{
    string GenerateAccessToken(IEnumerable<Claim> claims);
    string GenerateRefreshToken();
    DateTime GetAccessTokenExpiration();
    DateTime GetRefreshTokenExpiration();
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}
