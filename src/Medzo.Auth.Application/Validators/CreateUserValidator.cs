using FluentValidation;
using Medzo.Auth.Application.DTOs;
using Medzo.Auth.Application.Services;

namespace Medzo.Auth.Application.Validators;

public class CreateUserValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.StaffId)
            .NotEmpty().WithMessage("Staff ID is required.")
            .Must(StaffRoleResolver.IsSignupEligible)
            .WithMessage("Staff ID must be 4-20 letters or numbers and start with P or I.");

        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required.")
            .MinimumLength(3).WithMessage("Username must be at least 3 characters.")
            .MaximumLength(50).WithMessage("Username must not exceed 50 characters.")
            .Matches("^[a-zA-Z0-9._-]+$")
            .WithMessage("Username may only contain letters, numbers, dots, underscores, and hyphens.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character.");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("Password confirmation is required.")
            .Equal(x => x.Password).WithMessage("Passwords do not match.");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100).WithMessage("First name must not exceed 100 characters.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100).WithMessage("Last name must not exceed 100 characters.");

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Role is required.")
            .Must(role => role is "Pharmacist" or "InventoryManager")
            .WithMessage("Role must be Pharmacist or InventoryManager. Admin accounts are provisioned manually.");

        RuleFor(x => x)
            .Must(x => string.IsNullOrWhiteSpace(x.StaffId) || string.IsNullOrWhiteSpace(x.Role) ||
                       StaffRoleResolver.MatchesRole(x.StaffId, x.Role))
            .WithMessage("The selected role must match the Staff ID prefix.")
            .WithName(nameof(CreateUserRequest.Role));

    }
}
