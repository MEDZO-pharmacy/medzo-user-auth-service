using Medzo.Auth.Application.DTOs;
using Medzo.Auth.Application.Exceptions;
using Medzo.Auth.Application.Interfaces;
using Medzo.Auth.Domain.Entities;

namespace Medzo.Auth.Application.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IStaffInvitationRepository _staffInvitations;

    public UserService(
        IUserRepository users,
        IRoleRepository roles,
        IPasswordHasher passwordHasher,
        IStaffInvitationRepository staffInvitations)
    {
        _users = users;
        _roles = roles;
        _passwordHasher = passwordHasher;
        _staffInvitations = staffInvitations;
    }

    public async Task<UserResponse> CreateAsync(CreateUserRequest request)
    {
        var username = request.Username.Trim();
        var staffId = StaffRoleResolver.Normalize(request.StaffId);
        if (request.Role == "Admin" || !StaffRoleResolver.IsSignupEligible(staffId))
            throw new InvalidOperationException("Admin accounts must be provisioned manually in the database.");
        var email = request.Email.Trim().ToLowerInvariant();
        var firstName = request.FirstName.Trim();
        var lastName = request.LastName.Trim();

        if (await _users.GetByUsernameAsync(username) is not null)
            throw new UserConflictException("A user with this username already exists.");

        if (await _users.GetByStaffIdAsync(staffId) is not null)
            throw new UserConflictException("A user with this Staff ID already exists.");

        if (await _users.GetByEmailAsync(email) is not null)
            throw new UserConflictException("A user with this email address already exists.");

        var possibleDuplicates = (await _users.GetByNameAsync(firstName, lastName)).ToArray();
        if (possibleDuplicates.Length > 0 && !request.ConfirmPotentialDuplicate)
            throw new PotentialDuplicateUserException(possibleDuplicates.Select(Map));

        if (!StaffRoleResolver.MatchesRole(staffId, request.Role))
            throw new InvalidOperationException("The selected role must match the Staff ID prefix.");

        var canonicalRoleName = request.Role;
        var role = await _roles.GetByNameAsync(canonicalRoleName);
        if (role is null)
            throw new InvalidOperationException($"The role '{canonicalRoleName}' is not configured.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            StaffId = staffId,
            Email = email,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            FirstName = firstName,
            LastName = lastName,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Roles = new List<Role> { role }
        };

        return Map(await _users.AddAsync(user));
    }

    public async Task<UserResponse?> GetByIdAsync(Guid id)
    {
        var user = await _users.GetByIdAsync(id);
        return user is null ? null : Map(user);
    }

    public async Task<IEnumerable<UserResponse>> GetAllAsync()
    {
        return (await _users.GetAllAsync())
            .Select(Map)
            .ToArray();
    }

    public async Task<UserResponse> UpdateAsync(Guid id, RegisterUserRequest request)
    {
        var user = await _users.GetByIdAsync(id) ?? throw new KeyNotFoundException();
        var staffId = StaffRoleResolver.Normalize(request.StaffId);
        if (!string.Equals(user.StaffId, staffId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Staff ID and assigned role cannot be changed here.");
        var usernameOwner = await _users.GetByUsernameAsync(request.Username.Trim());
        if (usernameOwner is not null && usernameOwner.Id != id)
            throw new UserConflictException("A user with this username already exists.");

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var emailOwner = await _users.GetByEmailAsync(normalizedEmail);
        if (emailOwner is not null && emailOwner.Id != id)
            throw new UserConflictException("A user with this email address already exists.");

        user.Username = request.Username.Trim();
        user.Email = normalizedEmail;
        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.PasswordHash = _passwordHasher.HashPassword(request.Password);
        user.UpdatedAt = DateTime.UtcNow;
        await _users.UpdateAsync(user);
        return Map(user);
    }

    public async Task<UserResponse> UpdateManagedAsync(Guid id, UpdateManagedUserRequest request)
    {
        var user = await _users.GetByIdAsync(id) ?? throw new KeyNotFoundException();
        if (user.Roles.Any(role => role.Name == "Admin"))
            throw new InvalidOperationException("Admin accounts cannot be modified from the dashboard.");

        var username = request.Username.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        var staffId = StaffRoleResolver.Normalize(request.StaffId);

        var usernameOwner = await _users.GetByUsernameAsync(username);
        if (usernameOwner is not null && usernameOwner.Id != id)
            throw new UserConflictException("A user with this username already exists.");

        var emailOwner = await _users.GetByEmailAsync(email);
        if (emailOwner is not null && emailOwner.Id != id)
            throw new UserConflictException("A user with this email address already exists.");

        var staffIdOwner = await _users.GetByStaffIdAsync(staffId);
        if (staffIdOwner is not null && staffIdOwner.Id != id)
            throw new UserConflictException("A user with this Staff ID already exists.");

        if (!StaffRoleResolver.MatchesRole(staffId, request.Role))
            throw new InvalidOperationException("The selected role must match the Staff ID prefix.");

        var role = await _roles.GetByNameAsync(request.Role)
            ?? throw new InvalidOperationException($"The role '{request.Role}' is not configured.");

        user.Username = username;
        user.StaffId = staffId;
        user.Email = email;
        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        var currentRole = user.Roles.SingleOrDefault();
        if (currentRole?.Id != role.Id)
        {
            user.Roles.Clear();
            user.Roles.Add(role);
        }
        user.UpdatedAt = DateTime.UtcNow;

        await _users.UpdateAsync(user);
        return Map(user);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var user = await _users.GetByIdAsync(id);
        if (user is null)
            return false;

        await _users.DeleteAsync(user);
        return true;
    }

    public async Task<UserResponse> SetActiveAsync(Guid id, bool isActive)
    {
        var user = await _users.GetByIdAsync(id) ?? throw new KeyNotFoundException();
        if (user.Roles.Any(role => role.Name == "Admin"))
            throw new InvalidOperationException("Admin accounts cannot be deactivated from the dashboard.");
        if (isActive)
            throw new InvalidOperationException("A deactivated Staff ID cannot be activated or reused.");

        var roleName = user.Roles.Select(role => role.Name).SingleOrDefault()
            ?? throw new InvalidOperationException("The account does not have a configured staff role.");

        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        await _users.DeactivateAndReserveStaffIdAsync(user, roleName);
        return Map(user);
    }

    public async Task<StaffInvitationResponse> ApproveStaffIdAsync(StaffInvitationRequest request)
    {
        var staffId = StaffRoleResolver.Normalize(request.StaffId);
        if (request.Role == "Admin" || !StaffRoleResolver.IsSignupEligible(staffId))
            throw new InvalidOperationException("Admin accounts must be provisioned manually in the database.");
        if (!StaffRoleResolver.MatchesRole(staffId, request.Role))
            throw new InvalidOperationException("The selected role must match the Staff ID prefix.");
        if (await _users.GetByStaffIdAsync(staffId) is not null)
            throw new UserConflictException("A user already exists with this Staff ID.");
        if (await _staffInvitations.GetByStaffIdAsync(staffId) is not null)
            throw new UserConflictException("This Staff ID has already been approved.");

        return Map(await _staffInvitations.AddAsync(new StaffInvitation
        {
            Id = Guid.NewGuid(),
            StaffId = staffId,
            Role = request.Role,
            CreatedAt = DateTime.UtcNow
        }));
    }

    public async Task<IReadOnlyList<StaffInvitationResponse>> GetStaffInvitationsAsync() =>
        (await _staffInvitations.GetAllAsync()).Select(Map).ToArray();

    private static UserResponse Map(User user) => new()
    {
        Id = user.Id,
        UserNumber = user.UserNumber,
        Username = user.Username,
        StaffId = user.StaffId,
        Email = user.Email,
        FirstName = user.FirstName,
        LastName = user.LastName,
        IsActive = user.IsActive,
        Roles = user.Roles.Select(role => role.Name).ToArray(),
        CreatedAt = user.CreatedAt
    };

    private static StaffInvitationResponse Map(StaffInvitation invitation) => new()
    {
        Id = invitation.Id,
        StaffId = invitation.StaffId,
        Role = invitation.Role,
        IsClaimed = invitation.IsClaimed,
        CreatedAt = invitation.CreatedAt
    };
}
