using FluentValidation;
using Medzo.Auth.Application.DTOs;

namespace Medzo.Auth.Application.Validators;

public class ReviewRequestValidator : AbstractValidator<ReviewRequest>
{
    public ReviewRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CustomerType)
            .Must(value => value is "New Customer" or "Regular Customer")
            .WithMessage("Customer type must be New Customer or Regular Customer.");
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.Comment).NotEmpty().MinimumLength(10).MaximumLength(1000);
    }
}

