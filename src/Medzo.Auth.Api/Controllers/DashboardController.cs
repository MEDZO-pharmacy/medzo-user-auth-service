using System.Security.Claims;
using Medzo.Auth.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Medzo.Auth.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IUserService _users;

    public DashboardController(IUserService users) => _users = users;

    [HttpGet("pharmacist")]
    [Authorize(Roles = "Pharmacist")]
    public IActionResult Pharmacist() => Ok(Build(
        "Pharmacist", "Prescription review", "Medicine dispensing", "Patient medication guidance"));

    [HttpGet("inventory")]
    [Authorize(Roles = "InventoryManager")]
    public IActionResult InventoryManager() => Ok(Build(
        "Inventory Manager", "Stock monitoring", "Purchase planning", "Expiry and shortage tracking"));

    [HttpGet("admin")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Admin()
    {
        var users = (await _users.GetAllAsync()).ToArray();
        return Ok(new
        {
            role = "Admin",
            staffId = User.FindFirstValue("staff_id"),
            displayName = User.Identity?.Name,
            modules = new[] { "Staff accounts", "Role oversight", "System access management" },
            totalUsers = users.Length,
            users
        });
    }

    private object Build(string role, params string[] modules) => new
    {
        role,
        staffId = User.FindFirstValue("staff_id"),
        displayName = User.Identity?.Name,
        modules
    };
}

