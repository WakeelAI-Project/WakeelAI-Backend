using Microsoft.AspNetCore.Mvc;
using Wakeel.Application.DTOs.Auth;
using Wakeel.Application.Enums;
using Wakeel.Application.Interfaces;

namespace Wakeel.API.Controllers;

/// <summary>
/// Handles authentication-related endpoints (registration, login, token refresh).
/// Data validation is handled automatically by Data Annotations on the DTOs.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService authService) : ControllerBase
{
    /// <summary>
    /// Registers a new company and creates its admin user account.
    /// Request validation is performed automatically based on Data Annotations on RegisterCompanyRequest.
    /// </summary>
    /// <param name="request">The registration request with company name and admin credentials.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// 201 Created: Registration successful with company and user IDs.
    /// 400 Bad Request: Validation failed (empty fields, invalid email format, weak password).
    /// 409 Conflict: Email already registered.
    /// 500 Internal Server Error: Unexpected server error.
    /// </returns>
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterCompanyResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RegisterCompanyResponse>> Register(
        [FromBody] RegisterCompanyRequest request,
        CancellationToken cancellationToken
    )
    {
        // ModelState validation happens automatically based on Data Annotations
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return BadRequest(new ErrorResponse
            {
                Message = "Validation failed. Please check the errors below.",
                Errors = errors
            });
        }

        // Call the service to handle registration logic
        var (isSuccess, data, errorMessage, status) = await authService.RegisterCompanyAsync(
            request,
            cancellationToken
        );

        if (!isSuccess)
        {
            // Map AuthResultStatus enum to appropriate HTTP response
            return status switch
            {
                AuthResultStatus.EmailAlreadyExists => Conflict(new ErrorResponse
                {
                    Message = errorMessage ?? "The provided email is already registered. Please use a different email address."
                }),

                AuthResultStatus.ValidationError => BadRequest(new ErrorResponse
                {
                    Message = errorMessage ?? "Registration validation failed. Please check your input data."
                }),

                AuthResultStatus.Failure => BadRequest(new ErrorResponse
                {
                    Message = errorMessage ?? "Registration failed. Please try again later."
                }),

                _ => StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
                {
                    Message = errorMessage ?? "An unexpected error occurred. Please contact support."
                })
            };
        }

        // Return 201 Created with the response data
        return Created(string.Empty, data);
    }
}

/// <summary>
/// Standard error response structure for API errors.
/// Provides consistent error information across all endpoints.
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// A descriptive error message explaining what went wrong.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Optional list of validation errors (used when multiple validations fail).
    /// </summary>
    public List<string>? Errors { get; set; }
}
