using System;
using System.Globalization;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Wakeel.Infrastructure.Security;
using Xunit;

namespace Wakeel.Tests.Unit.Security;

/// <summary>FIX-26: covers FieldEncryptionService's AES-256-GCM round-trip and tamper detection.</summary>
public class FieldEncryptionServiceTests
{
    private static FieldEncryptionService CreateService(string? keyBase64 = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new System.Collections.Generic.KeyValuePair<string, string?>(
                    "Encryption:Key", keyBase64 ?? Convert.ToBase64String(RandomKeyBytes()))
            })
            .Build();

        return new FieldEncryptionService(configuration);
    }

    private static byte[] RandomKeyBytes() => System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);

    [Theory]
    [InlineData("12345.67")]
    [InlineData("999999999999.99")]
    [InlineData("0")]
    public void Encrypt_ThenDecrypt_RoundTripsADecimalStringExactly(string decimalAsString)
    {
        var sut = CreateService();
        var original = decimal.Parse(decimalAsString, CultureInfo.InvariantCulture);
        var plaintext = original.ToString("G", CultureInfo.InvariantCulture);

        var encrypted = sut.Encrypt(plaintext);
        var decrypted = sut.Decrypt(encrypted);

        decimal.Parse(decrypted, CultureInfo.InvariantCulture).Should().Be(original);
    }

    [Fact]
    public void Encrypt_ThenDecrypt_RoundTripsANonNullNationalId()
    {
        var sut = CreateService();
        const string nationalId = "29912345678901";

        var encrypted = sut.Encrypt(nationalId);
        var decrypted = sut.Decrypt(encrypted);

        decrypted.Should().Be(nationalId);
    }

    [Fact]
    public void Encrypt_ProducesAVersionedFormatDistinctFromThePlaintext()
    {
        var sut = CreateService();

        var encrypted = sut.Encrypt("29912345678901");

        encrypted.Should().StartWith("v1:");
        encrypted.Should().NotContain("29912345678901");
        encrypted.Split(':').Should().HaveCount(4);
    }

    [Fact]
    public void Encrypt_TwoCallsWithTheSamePlaintext_ProduceDifferentCiphertext()
    {
        // Proves the nonce is never reused - randomized encryption, not deterministic.
        var sut = CreateService();

        var first = sut.Encrypt("29912345678901");
        var second = sut.Encrypt("29912345678901");

        first.Should().NotBe(second);
        // Same plaintext decrypted from both must still match.
        sut.Decrypt(first).Should().Be(sut.Decrypt(second));
    }

    [Fact]
    public void Decrypt_TamperedCiphertext_ThrowsInsteadOfReturningAWrongValue()
    {
        var sut = CreateService();
        var encrypted = sut.Encrypt("29912345678901");
        var parts = encrypted.Split(':');

        // Flip the last character of the ciphertext segment - this must fail the GCM
        // authentication tag check, not silently decrypt into garbage.
        var tamperedCiphertext = parts[3][..^1] + (parts[3][^1] == 'A' ? 'B' : 'A');
        var tampered = string.Join(':', parts[0], parts[1], parts[2], tamperedCiphertext);

        var act = () => sut.Decrypt(tampered);

        act.Should().Throw<System.Security.Cryptography.CryptographicException>();
    }

    [Fact]
    public void Decrypt_UnrecognizedFormat_ThrowsAClearException()
    {
        var sut = CreateService();

        var act = () => sut.Decrypt("not-an-encrypted-value");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Decrypt_WrongKey_ThrowsInsteadOfReturningAWrongValue()
    {
        var encryptingService = CreateService();
        var decryptingServiceWithDifferentKey = CreateService();

        var encrypted = encryptingService.Encrypt("29912345678901");

        var act = () => decryptingServiceWithDifferentKey.Decrypt(encrypted);

        act.Should().Throw<System.Security.Cryptography.CryptographicException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("dG9vc2hvcnQ=")] // valid base64, but decodes to far fewer than 32 bytes
    public void Constructor_EmptyOrMalformedKey_ThrowsInvalidOperationException(string badKey)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new[] { new System.Collections.Generic.KeyValuePair<string, string?>("Encryption:Key", badKey) })
            .Build();

        var act = () => new FieldEncryptionService(configuration);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Constructor_KeyNotConfiguredAtAll_ThrowsInvalidOperationException()
    {
        var configuration = new ConfigurationBuilder().Build();

        var act = () => new FieldEncryptionService(configuration);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Constructor_KeyThatIsNotValidBase64_ThrowsInvalidOperationException()
    {
        var act = () => CreateService("not valid base64!!!");

        act.Should().Throw<InvalidOperationException>();
    }
}
