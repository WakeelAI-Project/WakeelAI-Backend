using Wakeel.Domain.Enums;

namespace Wakeel.Application.Interfaces;

/// <summary>
/// Abstraction for JWT token generation.
/// Handles creation of access tokens and refresh tokens for authenticated users.
/// </summary>
public interface IJwtTokenGenerator
{
    /// <summary>
    /// Generates a JWT access token for a user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="email">The email address of the user.</param>
    /// <param name="role">The role of the user within the company.</param>
    /// <param name="companyId">The company ID associated with the user.</param>
    /// <returns>A JWT access token string.</returns>
    string GenerateAccessToken(Guid userId, string email, UserRole role, Guid companyId);

    /// <summary>
    /// Generates a JWT refresh token for a user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <returns>A JWT refresh token string.</returns>
    string GenerateRefreshToken(Guid userId);

    /// <summary>
    /// Gets the expiration time in seconds for access tokens.
    /// </summary>
    int AccessTokenExpirationSeconds { get; }

    /// <summary>
    /// Gets the expiration time in days for refresh tokens.
    /// </summary>
    int RefreshTokenExpirationDays { get; }
}