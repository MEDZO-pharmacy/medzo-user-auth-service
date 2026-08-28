using FluentValidation;
using Medzo.Auth.Application.DTOs;
using Medzo.Auth.Application.Services;

namespace Medzo.Auth.Application.Validators;

public class StaffInvitationRequestValidator : AbstractValidator<StaffInvitationRequest>
{
    public StaffInvitationRequestValidator()
    {
        RuleFor(x => x.StaffId)
            .NotEmpty().WithMessage("Staff ID is required.")
            .Must(StaffRoleResolver.IsSignupEligible)
            .WithMessage("Staff ID must be 4-20 letters or numbers and start with P or I.");
        RuleFor(x => x.Role)
            .Must(role => role is "Pharmacist" or "InventoryManager")
            .WithMessage("Role must be Pharmacist or InventoryManager. Admin accounts are provisioned manually.");
        RuleFor(x => x)
            .Must(x => string.IsNullOrWhiteSpace(x.StaffId) || string.IsNullOrWhiteSpace(x.Role) ||
                       StaffRoleResolver.MatchesRole(x.StaffId, x.Role))
            .WithMessage("The selected role must match the Staff ID prefix.")
            .WithName(nameof(StaffInvitationRequest.Role));
    }
}
