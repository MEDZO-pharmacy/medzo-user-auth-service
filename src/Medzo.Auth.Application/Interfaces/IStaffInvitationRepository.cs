using Medzo.Auth.Domain.Entities;

namespace Medzo.Auth.Application.Interfaces;

public interface IStaffInvitationRepository
{
    Task<StaffInvitation?> GetByStaffIdAsync(string staffId);
    Task<IReadOnlyList<StaffInvitation>> GetAllAsync();
    Task<StaffInvitation> AddAsync(StaffInvitation invitation);
    Task MarkClaimedAsync(StaffInvitation invitation);
}

