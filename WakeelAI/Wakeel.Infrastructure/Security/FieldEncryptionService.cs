using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Wakeel.Infrastructure.Security;

/// <inheritdoc cref="IFieldEncryptionService" />
public class FieldEncryptionService : IFieldEncryptionService
{
    private const string FormatVersion = "v1";
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;
    private const int KeySizeBytes = 32;

    private readonly byte[] _key;

    public FieldEncryptionService(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _key = ParseKey(configuration["Encryption:Key"]);
    }

    /// <summary>For callers that already have a validated key (e.g. design-time tooling).</summary>
    internal FieldEncryptionService(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length != KeySizeBytes)
            throw new ArgumentException($"Key must be exactly {KeySizeBytes} bytes.", nameof(key));

        _key = key;
    }

    /// <summary>
    /// Validates the configured key the same way at both service construction and
    /// application startup (see Program.EnsureRequiredConfiguration), so a missing or
    /// malformed key is caught before a single request is served, not on first use.
    /// </summary>
    public static byte[] ParseKey(string? keyBase64)
    {
        if (string.IsNullOrWhiteSpace(keyBase64))
            throw new InvalidOperationException("Encryption:Key is missing. It must be a base64-encoded 32-byte (256-bit) AES key.");

        byte[] key;
        try
        {
            key = Convert.FromBase64String(keyBase64);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Encryption:Key is not valid base64.", ex);
        }

        if (key.Length != KeySizeBytes)
            throw new InvalidOperationException($"Encryption:Key must decode to exactly {KeySizeBytes} bytes (256 bits); got {key.Length}.");

        return key;
    }

    public string Encrypt(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        // A fresh random nonce every call - the one AES-GCM invariant that must never be
        // violated. Reusing a nonce with the same key breaks GCM's confidentiality and
        // authenticity guarantees entirely.
        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSizeBytes];

        using (var aesGcm = new AesGcm(_key, TagSizeBytes))
        {
            aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);
        }

        return string.Join(':',
            FormatVersion,
            Convert.ToBase64String(nonce),
            Convert.ToBase64String(tag),
            Convert.ToBase64String(ciphertext));
    }

    public string Decrypt(string encryptedValue)
    {
        ArgumentNullException.ThrowIfNull(encryptedValue);

        var parts = encryptedValue.Split(':');
        if (parts.Length != 4 || parts[0] != FormatVersion)
        {
            throw new InvalidOperationException(
                $"Unrecognized encrypted value format - expected '{FormatVersion}:<nonce>:<tag>:<ciphertext>'.");
        }

        byte[] nonce, tag, ciphertext;
        try
        {
            nonce = Convert.FromBase64String(parts[1]);
            tag = Convert.FromBase64String(parts[2]);
            ciphertext = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Encrypted value contains an invalid base64 segment.", ex);
        }

        var plaintextBytes = new byte[ciphertext.Length];

        using (var aesGcm = new AesGcm(_key, TagSizeBytes))
        {
            // Throws CryptographicException if the tag doesn't verify (tampered or
            // corrupt data, or the wrong key) - it never returns a silently-wrong value.
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintextBytes);
        }

        return Encoding.UTF8.GetString(plaintextBytes);
    }
}
