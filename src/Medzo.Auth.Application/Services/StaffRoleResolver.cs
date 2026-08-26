using Medzo.Auth.Domain.Enums;

namespace Medzo.Auth.Application.Services;

public static class StaffRoleResolver
{
    public static string Normalize(string staffId) => staffId.Trim().ToUpperInvariant();

    public static bool IsValid(string? staffId)
    {
        if (string.IsNullOrWhiteSpace(staffId))
            return false;

        var normalized = Normalize(staffId);
        return normalized.Length is >= 4 and <= 20 &&
               normalized[0] is 'P' or 'I' or 'A' &&
               normalized[1..].All(char.IsLetterOrDigit);
    }

    public static string GetRoleName(string staffId) => Normalize(staffId)[0] switch
    {
        'P' => UserRole.Pharmacist.ToString(),
        'I' => UserRole.InventoryManager.ToString(),
        'A' => UserRole.Admin.ToString(),
        _ => throw new InvalidOperationException("Staff ID must start with P, I, or A.")
    };

    public static bool MatchesRole(string staffId, string role) =>
        IsValid(staffId) && string.Equals(GetRoleName(staffId), role, StringComparison.OrdinalIgnoreCase);

    public static bool IsSignupEligible(string? staffId) =>
        IsValid(staffId) && Normalize(staffId!)[0] is 'P' or 'I';
}
