using FluentValidation;
using Medzo.Auth.Application.DTOs;
using Medzo.Auth.Application.Services;

namespace Medzo.Auth.Application.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Identifier)
            .NotEmpty().WithMessage("Staff ID, username, or email is required.")
            .MaximumLength(256).WithMessage("Login identifier is too long.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(6).WithMessage("Password must be at least 6 characters.");
    }
}
