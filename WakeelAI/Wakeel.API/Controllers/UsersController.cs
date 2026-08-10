using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wakeel.Application.DTOs.Users;
using Wakeel.Application.Interfaces;

namespace Wakeel.API.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController(IUserService userService) : ControllerBase
{
    [Authorize(Roles = "Company_Owner")]
    [HttpPost("invite")]
    public async Task<IActionResult> Invite([FromBody] InviteUserRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ApiErrorResponse { Error = "validation_error", Message = "Invalid payload.", Status = 400 });

        var companyIdClaim = User.FindFirst("company_id")?.Value;
        var userIdClaim = User.FindFirst("user_id")?.Value;
        if (!Guid.TryParse(companyIdClaim, out var companyId) || !Guid.TryParse(userIdClaim, out var userId))
            return Forbid();

        // Only allow owner to invite HR managers. Employees must be created by HR.
        if (!string.Equals(request.Role, "HR_Manager", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ApiErrorResponse { Error = "invalid_role", Message = "Owner can only invite HR_Manager.", Status = 400 });
        }

        try
        {
            var result = await userService.InviteUserAsync(userId, companyId, request, cancellationToken);
            return Created(string.Empty, result);
        }
        catch (InvalidOperationException ex) when (ex.Message == "email_already_exists")
        {
            return Conflict(new ApiErrorResponse { Error = "email_already_exists", Message = "Email is already registered.", Status = 409 });
        }
    }

    [Authorize(Roles = "HR_Manager")]
    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst("user_id")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Forbid();

        var profile = await userService.GetMyProfileAsync(userId, cancellationToken);
        if (profile is null)
            return NotFound(new ApiErrorResponse { Error = "user_not_found", Message = "User not found.", Status = 404 });

        return Ok(profile);
    }

    [Authorize(Roles = "Company_Owner")]
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? role, [FromQuery] int page = 1, [FromQuery] int limit = 20, CancellationToken cancellationToken = default)
    {
        var companyIdClaim = User.FindFirst("company_id")?.Value;
        if (!Guid.TryParse(companyIdClaim, out var companyId))
            return Forbid();

        var list = await userService.ListUsersAsync(companyId, role, page, limit, cancellationToken);
        return Ok(list);
    }

    [Authorize(Roles = "Company_Owner")]
    [HttpPatch("{userId:guid}/status")]
    public async Task<IActionResult> UpdateStatus([FromRoute] Guid userId, [FromBody] UpdateUserStatusRequest request, CancellationToken cancellationToken)
    {
        var companyIdClaim = User.FindFirst("company_id")?.Value;
        if (!Guid.TryParse(companyIdClaim, out var companyId))
            return Forbid();

        var updated = await userService.UpdateUserStatusAsync(companyId, userId, request.IsActive, cancellationToken);
        if (updated is null)
            return NotFound(new ApiErrorResponse { Error = "user_not_found", Message = "User not found.", Status = 404 });

        return Ok(updated);
    }
}
