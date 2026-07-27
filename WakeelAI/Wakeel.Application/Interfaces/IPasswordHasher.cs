namespace Wakeel.Application.Interfaces;

/// <summary>
/// Abstraction for password hashing and verification operations.
/// Enables secure password storage and validation without exposing the hashing implementation.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Hashes a plain-text password using a cryptographically secure algorithm (BCrypt).
    /// </summary>
    /// <param name="password">The plain-text password to hash.</param>
    /// <returns>The hashed password.</returns>
    string HashPassword(string password);

    /// <summary>
    /// Verifies a plain-text password against a previously hashed password.
    /// </summary>
    /// <param name="password">The plain-text password to verify.</param>
    /// <param name="hashedPassword">The previously hashed password to verify against.</param>
    /// <returns>True if the password matches; otherwise, false.</returns>
    bool VerifyPassword(string password, string hashedPassword);
}
