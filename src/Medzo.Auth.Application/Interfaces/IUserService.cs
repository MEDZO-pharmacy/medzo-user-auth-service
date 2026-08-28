using Medzo.Auth.Application.DTOs;

namespace Medzo.Auth.Application.Interfaces;

public interface IUserService
{
    Task<UserResponse> CreateAsync(CreateUserRequest request);
    Task<UserResponse?> GetByIdAsync(Guid id);
    Task<IEnumerable<UserResponse>> GetAllAsync();
    Task<UserResponse> UpdateAsync(Guid id, RegisterUserRequest request);
    Task<bool> DeleteAsync(Guid id);
    Task<UserResponse> SetActiveAsync(Guid id, bool isActive);
    Task<StaffInvitationResponse> ApproveStaffIdAsync(StaffInvitationRequest request);
    Task<IReadOnlyList<StaffInvitationResponse>> GetStaffInvitationsAsync();
}
