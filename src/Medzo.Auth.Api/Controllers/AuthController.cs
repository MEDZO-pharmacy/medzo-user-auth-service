using FluentValidation;
using Medzo.Auth.Application.DTOs;
using Medzo.Auth.Application.Exceptions;
using Medzo.Auth.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Medzo.Auth.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private const string RefreshCookieName = "medzo.refresh";
    private readonly IAuthService _authService;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<RegisterUserRequest> _registerValidator;

    public AuthController(
        IAuthService authService,
        IValidator<LoginRequest> loginValidator,
        IValidator<RegisterUserRequest> registerValidator)
    {
        _authService = authService;
        _loginValidator = loginValidator;
        _registerValidator = registerValidator;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var validation = await _loginValidator.ValidateAsync(request);
        if (!validation.IsValid)
            return BadRequest(CreateValidationProblem(validation));

        try
        {
            var response = await _authService.LoginAsync(request);
            SetRefreshCookie(response.RefreshToken);
            return Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { message = "Invalid credentials." });
        }
    }

    [HttpPost("register")]
    public async Task<ActionResult<LoginResponse>> Register([FromBody] RegisterUserRequest request)
    {
        var validation = await _registerValidator.ValidateAsync(request);
        if (!validation.IsValid)
            return BadRequest(CreateValidationProblem(validation));

        try
        {
            var response = await _authService.RegisterAsync(request);
            SetRefreshCookie(response.RefreshToken);
            return CreatedAtAction(nameof(Register), response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UserConflictException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<LoginResponse>> RefreshToken()
    {
        var refreshToken = ReadRefreshToken();
        if (string.IsNullOrWhiteSpace(refreshToken))
            return Unauthorized(new { message = "Authentication session is unavailable." });

        try
        {
            var response = await _authService.RefreshTokenAsync(refreshToken);
            SetRefreshCookie(response.RefreshToken);
            return Ok(response);
        }
        catch (InvalidRefreshTokenException)
        {
            return Unauthorized(new { message = "Invalid refresh token." });
        }
    }

    [HttpPost("revoke")]
    public async Task<IActionResult> RevokeToken()
    {
        var refreshToken = ReadRefreshToken();
        if (!string.IsNullOrWhiteSpace(refreshToken))
            await _authService.RevokeTokenAsync(refreshToken);

        Response.Cookies.Delete(RefreshCookieName, RefreshCookieOptions());
        return NoContent();
    }

    [HttpGet("session")]
    [Authorize]
    public IActionResult EvaluateSession() => NoContent();

    private string? ReadRefreshToken()
    {
        return Request.Cookies.TryGetValue(RefreshCookieName, out var cookieToken)
            ? cookieToken
            : null;
    }

    private void SetRefreshCookie(string refreshToken)
    {
        Response.Cookies.Append(RefreshCookieName, refreshToken, RefreshCookieOptions());
    }

    private CookieOptions RefreshCookieOptions() => new()
    {
        HttpOnly = true,
        Secure = Request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Path = "/api/auth",
        MaxAge = TimeSpan.FromDays(7),
        IsEssential = true
    };

    private static ValidationProblemDetails CreateValidationProblem(
        FluentValidation.Results.ValidationResult validation)
    {
        var errors = validation.Errors
            .GroupBy(error => char.ToLowerInvariant(error.PropertyName[0]) + error.PropertyName[1..])
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).Distinct().ToArray());

        return new ValidationProblemDetails(errors)
        {
            Title = "One or more validation errors occurred.",
            Status = StatusCodes.Status400BadRequest
        };
    }
}
