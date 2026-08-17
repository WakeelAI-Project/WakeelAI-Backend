using System.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Wakeel.Application.DTOs.Auth;
using Wakeel.Application.Enums;
using Wakeel.Application.Interfaces;

namespace Wakeel.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService authService, IMemoryCache cache) : ControllerBase
{
    private const string RefreshTokenCookieName = "refresh_token";
    private static readonly TimeSpan ForgotPasswordWindow = TimeSpan.FromMinutes(15);
    private const int ForgotPasswordMaxAttemptsPerEmail = 3;
    private const int ForgotPasswordMaxAttemptsPerIp = 10;

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

    [HttpPost("forgot-password")]
    [ProducesResponseType(typeof(ForgotPasswordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ForgotPasswordResponse>> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!ModelState.IsValid)
            return BadRequest(BuildValidationErrorResponse());

        if (IsForgotPasswordRateLimited(request.Email))
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new ApiErrorResponse
            {
                Error = "too_many_requests",
                Message = "Too many password reset requests. Please try again later.",
                Status = StatusCodes.Status429TooManyRequests
            });
        }

        // Always succeeds from the caller's perspective — AuthService never reveals
        // whether the email is registered, so this response never varies by outcome.
        // The mobile client also calls this endpoint again to resend a code.
        await authService.ForgotPasswordAsync(request, cancellationToken);

        return Ok(new ForgotPasswordResponse
        {
            Message = "If an account exists for this email, a verification code has been sent."
        });
    }

    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!ModelState.IsValid)
            return BadRequest(BuildValidationErrorResponse());

        var (isSuccess, errorMessage, status) = await authService.ResetPasswordAsync(request, cancellationToken);

        if (!isSuccess)
            return MapOtpErrorResponse(status, errorMessage);

        return Ok();
    }

    [HttpPost("verify-otp")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> VerifyOtp(
        [FromBody] VerifyOtpRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!ModelState.IsValid)
            return BadRequest(BuildValidationErrorResponse());

        var (isSuccess, errorMessage, status) = await authService.VerifyOtpAsync(request, cancellationToken);

        if (!isSuccess)
            return MapOtpErrorResponse(status, errorMessage);

        return Ok();
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

        SetRefreshTokenCookie(data!.RefreshToken);
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

    /// <summary>
    /// Maps a failed OTP-verification outcome (shared by reset-password and verify-otp)
    /// to its HTTP response, so both endpoints stay consistent.
    /// </summary>
    private IActionResult MapOtpErrorResponse(AuthResultStatus status, string? errorMessage) => status switch
    {
        AuthResultStatus.OtpExpired => BadRequest(new ApiErrorResponse
        {
            Error = "otp_expired",
            Message = "This verification code has expired. Please request a new one.",
            Status = StatusCodes.Status400BadRequest
        }),
        AuthResultStatus.InvalidOtp => BadRequest(new ApiErrorResponse
        {
            Error = "invalid_otp",
            Message = "Invalid verification code.",
            Status = StatusCodes.Status400BadRequest
        }),
        AuthResultStatus.TooManyOtpAttempts => StatusCode(StatusCodes.Status429TooManyRequests, new ApiErrorResponse
        {
            Error = "too_many_attempts",
            Message = "Too many incorrect attempts. Please request a new verification code.",
            Status = StatusCodes.Status429TooManyRequests
        }),
        _ => StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
        {
            Error = "internal_error",
            Message = errorMessage ?? "An unexpected error occurred.",
            Status = StatusCodes.Status500InternalServerError
        })
    };

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

    /// <summary>
    /// Enforces a per-email and per-IP request cap on top of the global rate-limiting
    /// middleware, so repeated forgot-password requests can't be used to spam a
    /// specific inbox or brute-force account enumeration from a single source.
    /// </summary>
    private bool IsForgotPasswordRateLimited(string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        var emailAttempts = IncrementRateLimitCounter($"forgot_password:email:{normalizedEmail}");
        var ipAttempts = IncrementRateLimitCounter($"forgot_password:ip:{ip}");

        return emailAttempts > ForgotPasswordMaxAttemptsPerEmail || ipAttempts > ForgotPasswordMaxAttemptsPerIp;
    }

    private int IncrementRateLimitCounter(string cacheKey)
    {
        var counter = cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = ForgotPasswordWindow;
            return new RateLimitCounter();
        })!;

        return Interlocked.Increment(ref counter.Count);
    }

    private sealed class RateLimitCounter
    {
        public int Count;
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