using Medzo.Auth.Domain.Entities;

namespace Medzo.Auth.Application.Interfaces;

public interface IRoleRepository
{
    Task<Role?> GetByNameAsync(string name);
}
