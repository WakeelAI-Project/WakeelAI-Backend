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
}
