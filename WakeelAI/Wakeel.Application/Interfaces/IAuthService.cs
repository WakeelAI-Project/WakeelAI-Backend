using Wakeel.Application.DTOs.Auth;
using Wakeel.Application.Enums;

namespace Wakeel.Application.Interfaces;

/// <summary>
/// Defines authentication and authorization operations for the application.
/// Handles user registration, authentication, and token generation.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Registers a new company and its admin user asynchronously.
    /// Creates a Company entity with an associated Owner-role User and stores hashed credentials.
    /// </summary>
    /// <param name="request">The registration request containing company name and admin credentials.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A tuple containing:
    /// - IsSuccess: Whether the registration was successful.
    /// - Data: The response containing CompanyId, UserId, and a success message (null if failed).
    /// - ErrorMessage: A descriptive error message if registration failed (null if successful).
    /// - Status: An AuthResultStatus enum indicating the result of the operation (Success, EmailAlreadyExists, ValidationError, or Failure).
    /// </returns>
    Task<(bool IsSuccess, RegisterCompanyResponse? Data, string? ErrorMessage, AuthResultStatus Status)> RegisterCompanyAsync(
        RegisterCompanyRequest request,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Authenticates a user asynchronously using their email and password.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<(bool IsSuccess, LoginResponse? Data, string? ErrorMessage, AuthResultStatus Status)> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Refreshes an access token asynchronously using a valid refresh token.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>

    Task<(bool IsSuccess, RefreshTokenResponse? Data, string? ErrorMessage, AuthResultStatus Status)> RefreshTokenAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Logs out a user asynchronously by invalidating their refresh token.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<(bool IsSuccess, string? ErrorMessage, AuthResultStatus Status)> LogoutAsync(
        LogoutRequest request,
        CancellationToken cancellationToken = default
    );
    Task<(bool IsSuccess, string? ErrorMessage)> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);
}
