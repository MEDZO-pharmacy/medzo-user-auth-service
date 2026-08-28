namespace Medzo.Auth.Application.DTOs;

public class StaffInvitationRequest
{
    public string StaffId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class StaffInvitationResponse
{
    public Guid Id { get; set; }
    public string StaffId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsClaimed { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SetUserStatusRequest
{
    public bool IsActive { get; set; }
}

