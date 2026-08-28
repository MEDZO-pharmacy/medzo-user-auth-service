using Medzo.Auth.Domain.Entities;

namespace Medzo.Auth.Application.Interfaces;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByHashAsync(string tokenHash);
    Task AddAsync(RefreshToken refreshToken);
    Task<bool> RotateAsync(RefreshToken currentToken, RefreshToken replacementToken);
    Task<bool> RevokeAsync(RefreshToken refreshToken, DateTime revokedAt);
}
