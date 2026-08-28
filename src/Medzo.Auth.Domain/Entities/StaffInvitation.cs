namespace Medzo.Auth.Domain.Entities;

public class StaffInvitation
{
    public Guid Id { get; set; }
    public string StaffId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsClaimed { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ClaimedAt { get; set; }
}

