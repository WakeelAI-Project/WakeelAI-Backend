namespace Wakeel.Application.Interfaces;

/// <summary>
/// Produces a deterministic hash of a raw refresh token for storage/lookup.
/// Unlike password hashing, this must be deterministic (same input -> same output)
/// since refresh tokens are looked up by hash, not verified against a stored value.
/// </summary>
public interface IRefreshTokenHasher
{
    string Hash(string rawToken);
}