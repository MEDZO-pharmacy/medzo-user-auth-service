namespace Medzo.Auth.Domain.Entities;

public class Role
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Navigation properties
    public ICollection<User> Users { get; set; } = new List<User>();
}
