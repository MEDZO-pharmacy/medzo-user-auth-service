namespace Medzo.Auth.Domain.Entities;

public class Review
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CustomerType { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

