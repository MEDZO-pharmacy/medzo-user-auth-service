namespace Medzo.Auth.Application.DTOs;

public class UserResponse
{
    public Guid Id { get; set; }
    public int UserNumber { get; set; }
    public string UserCode => UserNumber.ToString("D3");
    public string Username { get; set; } = string.Empty;
    public string? StaffId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public IEnumerable<string> Roles { get; set; } = Enumerable.Empty<string>();
    public DateTime CreatedAt { get; set; }
}
