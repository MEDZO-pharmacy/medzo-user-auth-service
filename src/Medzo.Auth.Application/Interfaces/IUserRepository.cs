using Medzo.Auth.Domain.Entities;

namespace Medzo.Auth.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByStaffIdAsync(string staffId);
    Task<User?> GetByEmailAsync(string email);
    Task<IEnumerable<User>> GetByNameAsync(string firstName, string lastName);
    Task<IEnumerable<User>> GetAllAsync();
    Task<User> AddAsync(User user);
    Task UpdateAsync(User user);
    Task DeleteAsync(User user);
    Task DeactivateAndReserveStaffIdAsync(User user, string roleName);
}
