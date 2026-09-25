using FluentValidation;
using Medzo.Auth.Application.DTOs;

namespace Medzo.Auth.Application.Validators;

public class ContactMessageRequestValidator : AbstractValidator<ContactMessageRequest>
{
    public ContactMessageRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Message).NotEmpty().MinimumLength(10).MaximumLength(2000);
    }
}

