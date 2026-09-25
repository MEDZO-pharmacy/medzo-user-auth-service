namespace Medzo.Auth.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public int UserNumber { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? StaffId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<Role> Roles { get; set; } = new List<Role>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
