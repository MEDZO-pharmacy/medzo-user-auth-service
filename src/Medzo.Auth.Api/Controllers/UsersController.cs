using System.Security.Claims;
using FluentValidation;
using Medzo.Auth.Application.DTOs;
using Medzo.Auth.Application.Exceptions;
using Medzo.Auth.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Medzo.Auth.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IValidator<CreateUserRequest> _createUserValidator;
    private readonly IValidator<RegisterUserRequest> _updateUserValidator;
    private readonly IValidator<UpdateManagedUserRequest> _managedUserValidator;
    private readonly IValidator<StaffInvitationRequest> _staffInvitationValidator;

    public UsersController(
        IUserService userService,
        IValidator<CreateUserRequest> createUserValidator,
        IValidator<RegisterUserRequest> updateUserValidator,
        IValidator<UpdateManagedUserRequest> managedUserValidator,
        IValidator<StaffInvitationRequest> staffInvitationValidator)
    {
        _userService = userService;
        _createUserValidator = createUserValidator;
        _updateUserValidator = updateUserValidator;
        _managedUserValidator = managedUserValidator;
        _staffInvitationValidator = staffInvitationValidator;
    }

    [HttpPut("{id:guid}/managed")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UserResponse>> UpdateManaged(
        Guid id, [FromBody] UpdateManagedUserRequest request)
    {
        var validation = await _managedUserValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            return ValidationProblem(ModelState);
        }

        try
        {
            return Ok(await _userService.UpdateManagedAsync(id, request));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "User not found." });
        }
        catch (UserConflictException exception)
        {
            return Conflict(new { code = "duplicate_user", message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CreateUserResponse>> Create([FromBody] CreateUserRequest request)
    {
        var validation = await _createUserValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);

            return ValidationProblem(ModelState);
        }

        try
        {
            var user = await _userService.CreateAsync(request);
            return CreatedAtAction(
                nameof(GetById),
                new { id = user.Id },
                new CreateUserResponse { User = user });
        }
        catch (PotentialDuplicateUserException exception)
        {
            return Conflict(new PotentialDuplicateResponse { Duplicates = exception.Duplicates });
        }
        catch (UserConflictException exception)
        {
            return Conflict(new { code = "duplicate_user", message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<UserResponse>>> GetAll()
    {
        var users = await _userService.GetAllAsync();
        return Ok(users);
    }

    [HttpGet("staff-invitations")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IReadOnlyList<StaffInvitationResponse>>> GetStaffInvitations() =>
        Ok(await _userService.GetStaffInvitationsAsync());

    [HttpPost("staff-invitations")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<StaffInvitationResponse>> ApproveStaffId(StaffInvitationRequest request)
    {
        var validation = await _staffInvitationValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            return ValidationProblem(ModelState);
        }

        try
        {
            var invitation = await _userService.ApproveStaffIdAsync(request);
            return CreatedAtAction(nameof(GetStaffInvitations), invitation);
        }
        catch (UserConflictException exception)
        {
            return Conflict(new { code = "duplicate_staff_id", message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserResponse>> GetById(Guid id)
    {
        if (!CanAccessUser(id))
            return Forbid();

        var user = await _userService.GetByIdAsync(id);
        if (user is null)
            return NotFound(new { message = "User not found." });

        return Ok(user);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserResponse>> Update(Guid id, [FromBody] RegisterUserRequest request)
    {
        if (!CanAccessUser(id))
            return Forbid();

        var validation = await _updateUserValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            return ValidationProblem(ModelState);
        }

        try
        {
            var user = await _userService.UpdateAsync(id, request);
            return Ok(user);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "User not found." });
        }
        catch (UserConflictException exception)
        {
            return Conflict(new { code = "duplicate_user", message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _userService.DeleteAsync(id);
        if (!result)
            return NotFound(new { message = "User not found." });

        return NoContent();
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UserResponse>> SetStatus(Guid id, SetUserStatusRequest request)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!request.IsActive && Guid.TryParse(currentUserId, out var parsedUserId) && parsedUserId == id)
            return BadRequest(new { message = "You cannot deactivate your own Admin account." });

        try
        {
            return Ok(await _userService.SetActiveAsync(id, request.IsActive));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "User not found." });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    private bool CanAccessUser(Guid requestedUserId)
    {
        if (User.IsInRole("Admin"))
            return true;

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(currentUserId, out var parsedUserId) &&
               parsedUserId == requestedUserId;
    }
}
