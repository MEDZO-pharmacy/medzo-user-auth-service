namespace Medzo.Auth.Application.DTOs;

public class UpdateManagedUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string StaffId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
