using Medzo.Auth.Application.Exceptions;
using Medzo.Auth.Application.Interfaces;
using Medzo.Auth.Domain.Entities;
using Medzo.Auth.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Medzo.Auth.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AuthDbContext _context;

    public UserRepository(AuthDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        return await _context.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        return await _context.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
    }

    public async Task<User?> GetByStaffIdAsync(string staffId)
    {
        return await _context.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.StaffId == staffId);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
    }

    public async Task<IEnumerable<User>> GetByNameAsync(string firstName, string lastName)
    {
        return await _context.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .Where(u =>
                u.FirstName.ToLower() == firstName.ToLower() &&
                u.LastName.ToLower() == lastName.ToLower())
            .ToListAsync();
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        return await _context.Users
            .Include(u => u.Roles)
            .ToListAsync();
    }

    public async Task<User> AddAsync(User user)
    {
        try
        {
            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();
            return user;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new UserConflictException(
                "A user with this username, email address, or Staff ID already exists.", exception);
        }
    }

    public async Task UpdateAsync(User user)
    {
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new UserConflictException(
                "A user with this username, email address, or Staff ID already exists.", exception);
        }
    }

    public async Task DeleteAsync(User user)
    {
        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
    }

    public async Task DeactivateAndReserveStaffIdAsync(User user, string roleName)
    {
        if (string.IsNullOrWhiteSpace(user.StaffId))
            throw new InvalidOperationException("The account does not have a Staff ID to reserve.");

        var now = DateTime.UtcNow;
        var invitation = await _context.StaffInvitations
            .FirstOrDefaultAsync(item => item.StaffId == user.StaffId);

        if (invitation is null)
        {
            _context.StaffInvitations.Add(new StaffInvitation
            {
                Id = Guid.NewGuid(),
                StaffId = user.StaffId,
                Role = roleName,
                IsClaimed = true,
                CreatedAt = now,
                ClaimedAt = now
            });
        }
        else
        {
            invitation.IsClaimed = true;
            invitation.ClaimedAt ??= now;
        }

        user.IsActive = false;
        user.UpdatedAt = now;
        await _context.SaveChangesAsync();
    }

}
