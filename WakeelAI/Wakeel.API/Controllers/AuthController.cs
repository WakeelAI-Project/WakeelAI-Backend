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
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
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
                AuthResultStatus.EmailAlreadyExists => Conflict(new ApiErrorResponse
                {
                    Error = "email_already_exists",
                    Message = errorMessage!,
                    Status = StatusCodes.Status409Conflict
                }),
                AuthResultStatus.ValidationError => BadRequest(new ApiErrorResponse
                {
                    Error = "validation_error",
                    Message = errorMessage!,
                    Status = StatusCodes.Status400BadRequest
                }),
                _ => StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    Error = "internal_error",
                    Message = errorMessage!,
                    Status = StatusCodes.Status500InternalServerError
                })
            };
        }

        SetRefreshTokenCookie(data!.RefreshToken);
        return Created(string.Empty, data);
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
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
                AuthResultStatus.InvalidCredentials => Unauthorized(new ApiErrorResponse
                {
                    Error = "invalid_credentials",
                    Message = "Wrong email or password.",
                    Status = StatusCodes.Status401Unauthorized
                }),
                AuthResultStatus.AccountInactive => StatusCode(StatusCodes.Status403Forbidden, new ApiErrorResponse
                {
                    Error = "account_inactive",
                    Message = "Account is deactivated.",
                    Status = StatusCodes.Status403Forbidden
                }),
                _ => StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    Error = "internal_error",
                    Message = errorMessage ?? "An unexpected error occurred.",
                    Status = StatusCodes.Status500InternalServerError
                })
            };
        }

        SetRefreshTokenCookie(data!.RefreshToken);
        return Ok(data);
    }

    [HttpPost("refresh")]
    [ProducesResponseType(typeof(RefreshTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RefreshTokenResponse>> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!ModelState.IsValid)
            return BadRequest(BuildValidationErrorResponse());

        var (isSuccess, data, errorMessage, _) = await authService.RefreshTokenAsync(request, cancellationToken);

        if (!isSuccess)
            return BadRequest(new ApiErrorResponse
            {
                Error = "invalid_refresh_token",
                Message = errorMessage ?? "Invalid refresh token.",
                Status = StatusCodes.Status400BadRequest
            });

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

    private ApiErrorResponse BuildValidationErrorResponse()
    {
        var errors = ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .ToList();

        return new ApiErrorResponse
        {
            Error = "validation_error",
            Message = string.Join(" ", errors),
            Status = StatusCodes.Status400BadRequest
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

public class ApiErrorResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("status")]
    public int Status { get; set; }
}