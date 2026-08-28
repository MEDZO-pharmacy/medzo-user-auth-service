using FluentValidation;
using Medzo.Auth.Application.DTOs;
using Medzo.Auth.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Medzo.Auth.Api.Controllers;

[ApiController]
[Route("api/contact")]
public class ContactController : ControllerBase
{
    private readonly IFeedbackService _feedback;
    private readonly IValidator<ContactMessageRequest> _validator;

    public ContactController(
        IFeedbackService feedback,
        IValidator<ContactMessageRequest> validator)
    {
        _feedback = feedback;
        _validator = validator;
    }

    [HttpPost]
    public async Task<IActionResult> Create(ContactMessageRequest request)
    {
        var validation = await _validator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            var errors = validation.Errors
                .GroupBy(x => char.ToLowerInvariant(x.PropertyName[0]) + x.PropertyName[1..])
                .ToDictionary(x => x.Key, x => x.Select(e => e.ErrorMessage).Distinct().ToArray());
            return BadRequest(new ValidationProblemDetails(errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred."
            });
        }

        var id = await _feedback.AddContactMessageAsync(request);
        return Accepted(new { id, message = "Thank you. Your message has been received." });
    }
}
