namespace Medzo.Auth.Domain.Entities;

public class Role
{
<<<<<<< Updated upstream
    public Guid Id { get; set; }
=======
    public string Id { get; set; } = string.Empty;
>>>>>>> Stashed changes
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Navigation properties
    public ICollection<User> Users { get; set; } = new List<User>();
}
