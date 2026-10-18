using FluentAssertions;
using Medzo.Auth.Application.Services;
using Xunit;

namespace Medzo.Auth.UnitTests;

public class StaffRoleResolverTests
{
    [Fact]
    public void Normalize_TrimsAndUppercasesStaffId()
    {
        StaffRoleResolver.Normalize(" p10a ").Should().Be("P10A");
    }

    [Theory]
    [InlineData("P123", true)]
    [InlineData("I123", true)]
    [InlineData("A123", true)]
    [InlineData("P1234567890123456789", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("P12", false)]
    [InlineData("P12345678901234567890", false)]
    [InlineData("X123", false)]
    [InlineData("P12-", false)]
    public void IsValid_EnforcesLengthPrefixAndAlphanumericSuffix(string? staffId, bool expected)
    {
        StaffRoleResolver.IsValid(staffId).Should().Be(expected);
    }

    [Theory]
    [InlineData(" p100 ", "Pharmacist")]
    [InlineData("i100", "InventoryManager")]
    [InlineData("A100", "Admin")]
    public void GetRoleName_ReturnsRoleForEverySupportedPrefix(string staffId, string expectedRole)
    {
        StaffRoleResolver.GetRoleName(staffId).Should().Be(expectedRole);
    }

    [Fact]
    public void GetRoleName_WithUnsupportedPrefix_ThrowsClearError()
    {
        var action = () => StaffRoleResolver.GetRoleName("X100");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Staff ID must start with P, I, or A.");
    }

    [Theory]
    [InlineData("P100", "pharmacist", true)]
    [InlineData("I100", "Pharmacist", false)]
    [InlineData("P1", "Pharmacist", false)]
    public void MatchesRole_RequiresBothValidStaffIdAndMatchingRole(
        string staffId, string role, bool expected)
    {
        StaffRoleResolver.MatchesRole(staffId, role).Should().Be(expected);
    }

    [Theory]
    [InlineData("P100", true)]
    [InlineData("I100", true)]
    [InlineData("A100", false)]
    [InlineData("P1", false)]
    public void IsSignupEligible_AllowsOnlyValidPharmacistAndInventoryIds(
        string staffId, bool expected)
    {
        StaffRoleResolver.IsSignupEligible(staffId).Should().Be(expected);
    }
}
