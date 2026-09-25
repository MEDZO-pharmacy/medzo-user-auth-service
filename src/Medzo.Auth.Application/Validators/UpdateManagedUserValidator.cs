using FluentValidation;
using Medzo.Auth.Application.DTOs;
using Medzo.Auth.Application.Services;

namespace Medzo.Auth.Application.Validators;

public class UpdateManagedUserValidator : AbstractValidator<UpdateManagedUserRequest>
{
    private static readonly string[] ManagedRoles = ["Pharmacist", "InventoryManager"];

    public UpdateManagedUserValidator()
    {
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

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100).WithMessage("First name must not exceed 100 characters.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100).WithMessage("Last name must not exceed 100 characters.");

        RuleFor(x => x.Role)
            .Must(role => ManagedRoles.Contains(role))
            .WithMessage("Role must be Pharmacist or InventoryManager.");

        RuleFor(x => x.StaffId)
            .NotEmpty().WithMessage("Staff ID is required.")
            .Must(StaffRoleResolver.IsSignupEligible)
            .WithMessage("Staff ID must be 4-20 letters or numbers and start with P or I.")
            .Must((request, staffId) => StaffRoleResolver.MatchesRole(staffId, request.Role))
            .WithMessage("The selected role must match the Staff ID prefix.");
    }
}
