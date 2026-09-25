using Medzo.Auth.Application.Exceptions;
using Medzo.Auth.Application.Interfaces;
using Medzo.Auth.Domain.Entities;
using Medzo.Auth.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Medzo.Auth.Infrastructure.Repositories;

public class StaffInvitationRepository : IStaffInvitationRepository
{
    private readonly AuthDbContext _context;

    public StaffInvitationRepository(AuthDbContext context) => _context = context;

    public Task<StaffInvitation?> GetByStaffIdAsync(string staffId) =>
        _context.StaffInvitations.FirstOrDefaultAsync(x => x.StaffId == staffId);

    public async Task<IReadOnlyList<StaffInvitation>> GetAllAsync() =>
        await _context.StaffInvitations.AsNoTracking().OrderByDescending(x => x.CreatedAt).ToListAsync();

    public async Task<StaffInvitation> AddAsync(StaffInvitation invitation)
    {
        try
        {
            _context.StaffInvitations.Add(invitation);
            await _context.SaveChangesAsync();
            return invitation;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new UserConflictException("This Staff ID has already been approved.", exception);
        }
    }

    public async Task MarkClaimedAsync(StaffInvitation invitation)
    {
        invitation.IsClaimed = true;
        invitation.ClaimedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }
}

