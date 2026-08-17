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

    /// <summary>
    /// Issues a one-time password (OTP) for self-service password reset, emailed to the
    /// given address. Always completes successfully from the caller's perspective — it
    /// never reveals whether the email is registered, so the controller can return an
    /// identical response regardless of outcome. Generating a new OTP invalidates any
    /// previously issued, unexpired OTP for the same user.
    /// </summary>
    /// <param name="request">The request containing the email to send a reset code to.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes a self-service password reset by verifying the OTP previously issued via
    /// <see cref="ForgotPasswordAsync"/> and, if valid, replacing the user's password.
    /// Unlike <see cref="ChangePasswordAsync"/>, this does not set MustChangePassword —
    /// the user has already chosen their own password. Revokes all of the user's existing
    /// refresh tokens on success.
    /// </summary>
    /// <param name="request">The email, submitted OTP, and new password.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A tuple containing:
    /// - IsSuccess: Whether the password was reset.
    /// - ErrorMessage: A machine-readable error code if unsuccessful (null if successful).
    /// - Status: Success, InvalidOtp, OtpExpired, TooManyOtpAttempts, or Failure.
    /// </returns>
    Task<(bool IsSuccess, string? ErrorMessage, AuthResultStatus Status)> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Checks whether an OTP previously issued via <see cref="ForgotPasswordAsync"/> is
    /// currently valid for the given email, without consuming it or touching the user's
    /// password. Shares its lookup/expiry/attempt-lockout logic with
    /// <see cref="ResetPasswordAsync"/>, so the two can never drift out of sync — a code
    /// that passes verification here is guaranteed to still work when submitted to
    /// ResetPasswordAsync afterward (unless it expires or is consumed by a request in
    /// between).
    /// </summary>
    /// <param name="request">The email and submitted OTP to check.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A tuple containing:
    /// - IsSuccess: Whether the code is currently valid.
    /// - ErrorMessage: A machine-readable error code if unsuccessful (null if successful).
    /// - Status: Success, InvalidOtp, OtpExpired, TooManyOtpAttempts, or Failure.
    /// </returns>
    Task<(bool IsSuccess, string? ErrorMessage, AuthResultStatus Status)> VerifyOtpAsync(
        VerifyOtpRequest request,
        CancellationToken cancellationToken = default
    );
}
