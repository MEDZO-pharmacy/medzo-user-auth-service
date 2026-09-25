using FluentAssertions;
using Medzo.Auth.Application.DTOs;
using Medzo.Auth.Application.Validators;
using Xunit;

namespace Medzo.Auth.UnitTests;

public class CreateUserValidatorTests
{
    private readonly CreateUserValidator _validator = new();

    [Fact]
    public async Task Validate_WithMissingAndInvalidFields_ReturnsClearErrors()
    {
        var result = await _validator.ValidateAsync(new CreateUserRequest
        {
            Email = "not-an-email",
            Password = "weak",
            ConfirmPassword = "different"
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.PropertyName).Should().Contain(new[]
        {
            "StaffId", "Username", "Email", "Password", "ConfirmPassword", "FirstName", "LastName"
        });
    }

    [Fact]
    public async Task Validate_WithValidStaffId_IsValid()
    {
        var result = await _validator.ValidateAsync(new CreateUserRequest
        {
            Username = "valid.user",
            StaffId = "P1001",
            Email = "valid@example.com",
            Password = "Strong1!",
            ConfirmPassword = "Strong1!",
            FirstName = "Valid",
            LastName = "User",
            Role = "Pharmacist"
        });

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("valid.user", true)]
    [InlineData("valid-user_2", true)]
    [InlineData("invalid user", false)]
    [InlineData("invalid@user", false)]
    public async Task SignupAndAdminCreation_UseTheSameUsernameRules(
        string username, bool expectedValid)
    {
        var signup = new RegisterUserRequest
        {
            Username = username,
            StaffId = "P1001",
            Email = "valid@example.com",
            Password = "Strong1!",
            ConfirmPassword = "Strong1!",
            FirstName = "Valid",
            LastName = "User"
        };
        var admin = new CreateUserRequest
        {
            Username = username,
            StaffId = signup.StaffId,
            Email = signup.Email,
            Password = signup.Password,
            ConfirmPassword = signup.ConfirmPassword,
            FirstName = signup.FirstName,
            LastName = signup.LastName,
            Role = "Pharmacist"
        };

        var signupResult = await new RegisterUserValidator().ValidateAsync(signup);
        var adminResult = await _validator.ValidateAsync(admin);

        signupResult.IsValid.Should().Be(expectedValid);
        adminResult.IsValid.Should().Be(expectedValid);
    }
}
