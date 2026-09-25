using Medzo.Auth.Application.Interfaces;
using Medzo.Auth.Domain.Entities;
using Medzo.Auth.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Medzo.Auth.Infrastructure.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AuthDbContext _context;

    public RefreshTokenRepository(AuthDbContext context)
    {
        _context = context;
    }

    public Task<RefreshToken?> GetByHashAsync(string tokenHash)
    {
        return _context.RefreshTokens
            .Include(token => token.User)
            .ThenInclude(user => user.Roles)
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash);
    }

    public async Task AddAsync(RefreshToken refreshToken)
    {
        await _context.RefreshTokens.AddAsync(refreshToken);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> RotateAsync(
        RefreshToken currentToken,
        RefreshToken replacementToken)
    {
        await _context.RefreshTokens.AddAsync(replacementToken);

        try
        {
            await _context.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    public async Task<bool> RevokeAsync(RefreshToken refreshToken, DateTime revokedAt)
    {
        refreshToken.RevokedAt = revokedAt;

        try
        {
            await _context.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }
}
