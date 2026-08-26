using FluentValidation;
using Medzo.Auth.Application.DTOs;
using Medzo.Auth.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Medzo.Auth.Api.Controllers;

[ApiController]
[Route("api/reviews")]
public class ReviewsController : ControllerBase
{
    private readonly IFeedbackService _feedback;
    private readonly IValidator<ReviewRequest> _validator;

    public ReviewsController(IFeedbackService feedback, IValidator<ReviewRequest> validator)
    {
        _feedback = feedback;
        _validator = validator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReviewResponse>>> Get() =>
        Ok(await _feedback.GetReviewsAsync());

    [HttpPost]
    public async Task<ActionResult<ReviewResponse>> Create(ReviewRequest request)
    {
        var validation = await _validator.ValidateAsync(request);
        if (!validation.IsValid)
            return BadRequest(ToValidationProblem(validation));

        var review = await _feedback.AddReviewAsync(request);
        return CreatedAtAction(nameof(Get), review);
    }

    private static ValidationProblemDetails ToValidationProblem(
        FluentValidation.Results.ValidationResult result) => new(
        result.Errors.GroupBy(x => char.ToLowerInvariant(x.PropertyName[0]) + x.PropertyName[1..])
            .ToDictionary(x => x.Key, x => x.Select(e => e.ErrorMessage).Distinct().ToArray()))
    { Status = StatusCodes.Status400BadRequest, Title = "One or more validation errors occurred." };
}

