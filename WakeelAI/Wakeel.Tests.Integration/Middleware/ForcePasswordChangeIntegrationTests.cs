using System;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wakeel.Infrastructure.Persistence;
using Xunit;

namespace Wakeel.Tests.Integration.Middleware;

public class ForcePasswordChangeIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ForcePasswordChangeIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task MustChangePassword_WhenEnforcedViaHeader_Returns403_OtherwisePasses()
    {
        var client = _factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var ownerEmail = $"owner_{suffix}@test.local";

        // 1. Register company
        var regRes = await client.PostAsJsonAsync("/api/auth/register-company", new
        {
            company_name = $"Test Co {suffix}",
            tax_id = suffix,
            owner_full_name = "Owner",
            owner_email = ownerEmail,
            password = "TestPassword123!"
        });
        regRes.EnsureSuccessStatusCode();
        var regDoc = JsonDocument.Parse(await regRes.Content.ReadAsStringAsync());
        var ownerToken = regDoc.RootElement.GetProperty("access_token").GetString()!;

        // 2. Modify the owner in DB to set MustChangePassword = true
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var owner = await db.Users.FirstAsync(u => u.Email == ownerEmail);
            owner.MustChangePassword = true;
            await db.SaveChangesAsync();
        }

        // 3. Make a request to a normal endpoint WITH X-Test-ForcePassword: true
        var requestWithHeader = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, "/api/company/profile");
        requestWithHeader.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ownerToken);
        requestWithHeader.Headers.Add("X-Test-ForcePassword", "true");

        var responseWithHeader = await client.SendAsync(requestWithHeader);
        
        responseWithHeader.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await responseWithHeader.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString().Should().Be("password_change_required");
        body.GetProperty("status").GetInt32().Should().Be(403);

        // 4. Make a request WITHOUT the header -> bypasses enforcement due to Testing env
        var requestWithoutHeader = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, "/api/company/profile");
        requestWithoutHeader.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ownerToken);

        var responseWithoutHeader = await client.SendAsync(requestWithoutHeader);
        var bodyStr = await responseWithoutHeader.Content.ReadAsStringAsync();
        
        // Should pass the middleware and hit the controller (returns 200 OK with empty list or normal response)
        responseWithoutHeader.StatusCode.Should().Be(HttpStatusCode.OK, "Because: " + bodyStr);
    }
}
