using Wakeel.Application.Interfaces;
using BC = BCrypt.Net.BCrypt;

namespace Wakeel.Infrastructure.Security;

/// <summary>
/// Implementation of IPasswordHasher using BCrypt.Net-Next for secure password hashing.
/// BCrypt provides automatic salt generation and adaptive work factors for security.
/// </summary>
public class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    /// <summary>
    /// Hashes a plain-text password using BCrypt with a cost factor of 12.
    /// The cost factor makes brute-force attacks computationally expensive.
    /// </summary>
    /// <param name="password">The plain-text password to hash.</param>
    /// <returns>The BCrypt-hashed password (includes salt and cost factor).</returns>
    /// <exception cref="ArgumentException">Thrown if password is null or empty.</exception>
    public string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("Password cannot be null or empty.", nameof(password));
        }

        return BC.HashPassword(password, workFactor: WorkFactor);
    }

    /// <summary>
    /// Verifies a plain-text password against a BCrypt-hashed password.
    /// </summary>
    /// <param name="password">The plain-text password to verify.</param>
    /// <param name="hashedPassword">The BCrypt-hashed password to verify against.</param>
    /// <returns>True if the password matches the hash; otherwise, false.</returns>
    /// <exception cref="ArgumentException">Thrown if either parameter is null or empty.</exception>
    public bool VerifyPassword(string password, string hashedPassword)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("Password cannot be null or empty.", nameof(password));
        }

        if (string.IsNullOrEmpty(hashedPassword))
        {
            throw new ArgumentException("Hashed password cannot be null or empty.", nameof(hashedPassword));
        }

        return BC.Verify(password, hashedPassword);
    }
}
