using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Wakeel.API;
using Xunit;

namespace Wakeel.Tests.Unit.Configuration;

/// <summary>
/// Covers the startup fail-fast guard that refuses to boot when a required secret is absent
/// (FIX-S1). Secrets are never committed, so a misconfigured deployment must fail loudly
/// at startup with the missing key names - and never with the values.
/// </summary>
public class RequiredConfigurationTests
{
    private static IConfiguration BuildConfiguration(params (string Key, string? Value)[] entries)
    {
        var dict = new Dictionary<string, string?>();
        foreach (var (key, value) in entries)
        {
            dict[key] = value;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    private static (string Key, string? Value)[] FullyPopulated() =>
    [
        ("ConnectionStrings:DefaultConnection", "Server=.;Database=x;Trusted_Connection=True;"),
        ("Jwt:SecretKey", "a-secret-key-that-is-at-least-32-characters"),
        ("AiNode:InternalApiKey", "internal-key")
    ];

    [Fact]
    public void EnsureRequiredConfiguration_GivenAllRequiredKeys_DoesNotThrow()
    {
        var configuration = BuildConfiguration(FullyPopulated());

        var act = () => Program.EnsureRequiredConfiguration(configuration);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("ConnectionStrings:DefaultConnection")]
    [InlineData("Jwt:SecretKey")]
    [InlineData("AiNode:InternalApiKey")]
    public void EnsureRequiredConfiguration_GivenMissingKey_ThrowsNamingTheKey(string missingKey)
    {
        var entries = new List<(string Key, string? Value)>(FullyPopulated());
        entries.RemoveAll(e => e.Key == missingKey);

        var act = () => Program.EnsureRequiredConfiguration(BuildConfiguration(entries.ToArray()));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{missingKey}*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EnsureRequiredConfiguration_GivenBlankValue_Throws(string blank)
    {
        var entries = new List<(string Key, string? Value)>(FullyPopulated());
        entries.RemoveAll(e => e.Key == "Jwt:SecretKey");
        entries.Add(("Jwt:SecretKey", blank));

        var act = () => Program.EnsureRequiredConfiguration(BuildConfiguration(entries.ToArray()));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Jwt:SecretKey*");
    }

    [Fact]
    public void EnsureRequiredConfiguration_WhenThrowing_DoesNotLeakSecretValues()
    {
        var entries = new List<(string Key, string? Value)>(FullyPopulated());
        entries.RemoveAll(e => e.Key == "AiNode:InternalApiKey");

        var act = () => Program.EnsureRequiredConfiguration(BuildConfiguration(entries.ToArray()));

        var message = act.Should().Throw<InvalidOperationException>().Which.Message;
        message.Should().NotContain("a-secret-key-that-is-at-least-32-characters");
        message.Should().NotContain("Trusted_Connection");
    }

    [Fact]
    public void EnsureRequiredConfiguration_GivenNullConfiguration_Throws()
    {
        var act = () => Program.EnsureRequiredConfiguration(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
