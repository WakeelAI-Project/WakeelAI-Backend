namespace Wakeel.Infrastructure.Security;

/// <summary>
/// Application-level field encryption for sensitive columns (FIX-26). Randomized
/// AES-256-GCM - encrypting the same plaintext twice never produces the same
/// ciphertext, so this can never be used for equality lookups.
/// </summary>
public interface IFieldEncryptionService
{
    /// <summary>Encrypts <paramref name="plaintext"/>, returning a self-describing versioned string.</summary>
    string Encrypt(string plaintext);

    /// <summary>
    /// Decrypts a value produced by <see cref="Encrypt"/>. Throws if the value's format
    /// is unrecognized or its authentication tag does not verify (tampered/corrupt data) -
    /// it never silently returns garbage.
    /// </summary>
    string Decrypt(string encryptedValue);
}
