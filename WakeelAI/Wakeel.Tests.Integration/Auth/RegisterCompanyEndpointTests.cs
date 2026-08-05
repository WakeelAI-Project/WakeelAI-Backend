using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Wakeel.Tests.Integration.Auth;

public class RegisterCompanyEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public RegisterCompanyEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RegisterCompany_GivenPasswordUnder8Chars_ShouldReturn400BadRequest()
    {
        var request = new
        {
            company_name = "Test Corp",
            tax_id = "123456789",
            owner_full_name = "Sara Ahmed",
            owner_email = $"test_{Guid.NewGuid()}@test.com",
            password = "Sh0rt!" // 6 chars — below the 8-char minimum
        };

        var response = await _client.PostAsJsonAsync("/api/auth/register-company", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
