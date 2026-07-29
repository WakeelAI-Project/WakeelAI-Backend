using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wakeel.Application.DTOs.Auth;
using Wakeel.Application.Enums;
using Wakeel.Application.Interfaces;

namespace Wakeel.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService authService) : ControllerBase
{
    private const string RefreshTokenCookieName = "refresh_token";

    [HttpPost("register-company")]
    [ProducesResponseType(typeof(RegisterCompanyResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RegisterCompanyResponse>> RegisterCompany(
        [FromBody] RegisterCompanyRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!ModelState.IsValid)
            return BadRequest(BuildValidationErrorResponse());

        var (isSuccess, data, errorMessage, status) = await authService.RegisterCompanyAsync(request, cancellationToken);

        if (!isSuccess)
        {
            return status switch
            {
                AuthResultStatus.EmailAlreadyExists => Conflict(new ErrorResponse { Message = errorMessage! }),
                AuthResultStatus.ValidationError => BadRequest(new ErrorResponse { Message = errorMessage! }),
                _ => StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse { Message = errorMessage! })
            };
        }

        SetRefreshTokenCookie(data!.RefreshToken);
        return Created(string.Empty, data);
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AuthErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(AuthErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!ModelState.IsValid)
            return BadRequest(BuildValidationErrorResponse());

        var (isSuccess, data, errorMessage, status) = await authService.LoginAsync(request, cancellationToken);

        if (!isSuccess)
        {
            return status switch
            {
                AuthResultStatus.InvalidCredentials => Unauthorized(new AuthErrorResponse { Error = "invalid_credentials" }),
                AuthResultStatus.AccountInactive => StatusCode(StatusCodes.Status403Forbidden, new AuthErrorResponse { Error = "account_inactive" }),
                _ => StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse { Message = errorMessage ?? "An unexpected error occurred." })
            };
        }

        SetRefreshTokenCookie(data!.RefreshToken);
        return Ok(data);
    }

    [HttpPost("refresh")]
    [ProducesResponseType(typeof(RefreshTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RefreshTokenResponse>> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!ModelState.IsValid)
            return BadRequest(BuildValidationErrorResponse());

        var (isSuccess, data, errorMessage, _) = await authService.RefreshTokenAsync(request, cancellationToken);

        if (!isSuccess)
            return BadRequest(new ErrorResponse { Message = errorMessage ?? "Invalid refresh token." });

        return Ok(data);
    }

    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!ModelState.IsValid)
            return BadRequest(BuildValidationErrorResponse());

        await authService.LogoutAsync(request, cancellationToken);
        Response.Cookies.Delete(RefreshTokenCookieName);

        return NoContent();
    }

    private ErrorResponse BuildValidationErrorResponse()
    {
        var errors = ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .ToList();

        return new ErrorResponse
        {
            Message = "Validation failed. Please check the errors below.",
            Errors = errors
        };
    }

    private void SetRefreshTokenCookie(string refreshToken)
    {
        Response.Cookies.Append(RefreshTokenCookieName, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });
    }
}

public class ErrorResponse
{
    public string Message { get; set; } = string.Empty;
    public List<string>? Errors { get; set; }
}

public class AuthErrorResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;
}