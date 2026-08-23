using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Wakeel.Tests.Integration.Cors;

/// <summary>
/// FIX-S3: the wildcard AllowAnyOrigin() policy was replaced with an explicit allow-list read
/// from Cors:AllowedOrigins. CustomWebApplicationFactory configures exactly one allowed test
/// origin (https://allowed.integrationtest.local) - these tests confirm only that origin is
/// ever echoed back.
/// </summary>
public class CorsPolicyTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CorsPolicyTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RequestFromAllowedOrigin_EchoesThatOrigin()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", "https://allowed.integrationtest.local");

        var response = await _client.SendAsync(request);

        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values).Should().BeTrue();
        values.Should().ContainSingle("https://allowed.integrationtest.local");
    }

    [Fact]
    public async Task RequestFromUnlistedOrigin_GetsNoAllowOriginHeader()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", "https://not-allowed.example.com");

        var response = await _client.SendAsync(request);

        response.Headers.TryGetValues("Access-Control-Allow-Origin", out _).Should().BeFalse();
    }
}
