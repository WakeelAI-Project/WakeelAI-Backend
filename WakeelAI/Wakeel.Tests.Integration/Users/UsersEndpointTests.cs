using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wakeel.Application.Interfaces;
using Wakeel.Infrastructure.Persistence;
using Xunit;

namespace Wakeel.Tests.Integration.Users;

/// <summary>FIX-10: GET /api/users/me used to be HR_Manager-only, so a Company_Owner got
/// 403 from the endpoint that resolves the current user - even though the method already
/// derives the user from the caller's own claim and returns only that user's own record.</summary>
public class UsersEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string KnownPassword = "IntegrationTest123!";

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public UsersEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(Guid UserId, string Token)> SeedOwnerAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var response = await _client.PostAsJsonAsync("/api/auth/register-company", new
        {
            company_name = $"UsersCo {suffix}",
            tax_id = suffix,
            owner_full_name = "Owner Test",
            owner_email = $"owner_{suffix}@integrationtest.local",
            password = KnownPassword
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("access_token").GetString()!;
        var companyId = body.GetProperty("company_id").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var owner = await db.Users.FirstAsync(u => u.CompanyId == companyId);
        return (owner.Id, token);
    }

    private async Task<(Guid UserId, string Token)> SeedHrAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var ownerResponse = await _client.PostAsJsonAsync("/api/auth/register-company", new
        {
            company_name = $"UsersCo {suffix}",
            tax_id = suffix,
            owner_full_name = "Owner Test",
            owner_email = $"owner_{suffix}@integrationtest.local",
            password = KnownPassword
        });
        ownerResponse.EnsureSuccessStatusCode();
        var ownerBody = await ownerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var ownerToken = ownerBody.GetProperty("access_token").GetString()!;

        var hrEmail = $"hr_{suffix}@integrationtest.local";
        using (var inviteReq = new HttpRequestMessage(HttpMethod.Post, "/api/users/invite"))
        {
            inviteReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
            inviteReq.Content = JsonContent.Create(new { full_name = "HR Test", email = hrEmail, role = "HR_Manager" });
            (await _client.SendAsync(inviteReq)).EnsureSuccessStatusCode();
        }

        Guid hrUserId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var hrUser = await db.Users.FirstAsync(u => u.Email == hrEmail);
            hrUser.PasswordHash = hasher.HashPassword(KnownPassword);
            hrUserId = hrUser.Id;
            await db.SaveChangesAsync();
        }

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new { email = hrEmail, password = KnownPassword });
        loginResponse.EnsureSuccessStatusCode();
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        return (hrUserId, loginBody.GetProperty("access_token").GetString()!);
    }

    private async Task<HttpResponseMessage> GetMeAsync(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/users/me")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) }
        };
        return await _client.SendAsync(request);
    }

    [Fact]
    public async Task GetMe_GivenOwnerToken_Returns200WithOwnersOwnRecord()
    {
        var (ownerId, token) = await SeedOwnerAsync();

        var response = await GetMeAsync(token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("user_id").GetGuid().Should().Be(ownerId);
        body.GetProperty("role").GetString().Should().Be("Company_Owner");
    }

    [Fact]
    public async Task GetMe_GivenHrToken_Returns200WithHrsOwnRecord()
    {
        var (hrUserId, token) = await SeedHrAsync();

        var response = await GetMeAsync(token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("user_id").GetGuid().Should().Be(hrUserId);
        body.GetProperty("role").GetString().Should().Be("HR_Manager");
    }

    [Fact]
    public async Task GetMe_GivenNoToken_Returns401()
    {
        var response = await _client.GetAsync("/api/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
