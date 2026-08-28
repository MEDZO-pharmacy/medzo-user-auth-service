namespace Medzo.Auth.Application.DTOs;

public class CreateUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string StaffId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    // Must be explicitly set after the API warns about a matching staff name.
    public bool ConfirmPotentialDuplicate { get; set; }
}
