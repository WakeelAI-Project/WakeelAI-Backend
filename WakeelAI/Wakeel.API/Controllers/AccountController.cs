using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wakeel.Application.DTOs.Auth;
using Wakeel.Application.Interfaces;

namespace Wakeel.API.Controllers;

[ApiController]
[Route("api/account")]
public class AccountController(IAuthService authService) : ControllerBase
{
    [Authorize(Roles = "HR_Manager,Employee")]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ApiErrorResponse { Error = "validation_error", Message = "Invalid payload.", Status = 400 });

        var userIdClaim = User.FindFirst("user_id")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Forbid();

        var (isSuccess, error) = await authService.ChangePasswordAsync(userId, request, cancellationToken);
        if (!isSuccess)
        {
            return error switch
            {
                "user_not_found" => NotFound(new ApiErrorResponse { Error = "user_not_found", Message = "User not found.", Status = 404 }),
                "invalid_current_password" => BadRequest(new ApiErrorResponse { Error = "invalid_current_password", Message = "Current password is incorrect.", Status = 400 }),
                _ => StatusCode(500, new ApiErrorResponse { Error = "internal_error", Message = "An unexpected error occurred.", Status = 500 })
            };
        }

        return NoContent();
    }
}
