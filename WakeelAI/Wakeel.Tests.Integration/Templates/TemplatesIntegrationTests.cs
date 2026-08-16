using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wakeel.Application.DTOs.Templates;
using Wakeel.Infrastructure.Persistence;
using Xunit;

namespace Wakeel.Tests.Integration.Templates;

public class TemplatesIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private const string KnownPassword = "TestPassword123!";

    public TemplatesIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient Client, string HrToken, Guid CompanyId, Guid TemplateId)> SetupSeededEnvironmentAsync(
        HttpMessageHandler? mockAiNodeHandler = null)
    {
        var factory = mockAiNodeHandler != null
            ? _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    var httpClientFactoryDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IHttpClientFactory));
                    if (httpClientFactoryDescriptor != null)
                        services.Remove(httpClientFactoryDescriptor);

                    // Simplistic mock for the named client
                    services.AddHttpClient("AiNodeClient")
                            .ConfigurePrimaryHttpMessageHandler(() => mockAiNodeHandler);
                });
            })
            : _factory;

        var client = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var ownerEmail = $"owner_{suffix}@test.com";
        var hrEmail = $"hr_{suffix}@test.com";

        // 1. Register company
        var regRes = await client.PostAsJsonAsync("/api/auth/register-company", new
        {
            company_name = $"Test Co {suffix}",
            tax_id = suffix,
            owner_full_name = "Owner",
            owner_email = ownerEmail,
            password = KnownPassword
        });
        regRes.EnsureSuccessStatusCode();
        var regDoc = JsonDocument.Parse(await regRes.Content.ReadAsStringAsync());
        var ownerToken = regDoc.RootElement.GetProperty("access_token").GetString()!;
        var companyId = regDoc.RootElement.GetProperty("company_id").GetGuid();

        // 2. Invite HR
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ownerToken);
        var inviteRes = await client.PostAsJsonAsync("/api/users/invite", new { full_name = "HR", email = hrEmail, role = "HR_Manager" });
        inviteRes.EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hrUser = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.Email == hrEmail);
            hrUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(KnownPassword);
            hrUser.IsActive = true;
            await db.SaveChangesAsync();
        }

        // 3. Login HR
        var hrLoginRes = await client.PostAsJsonAsync("/api/auth/login", new { email = hrEmail, password = KnownPassword });
        hrLoginRes.EnsureSuccessStatusCode();
        var hrToken = JsonDocument.Parse(await hrLoginRes.Content.ReadAsStringAsync()).RootElement.GetProperty("access_token").GetString()!;

        // 4. Create Template as HR
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", hrToken);
        var createTplRes = await client.PostAsJsonAsync("/api/templates", new
        {
            name = "Test Template",
            document_type = "EMPLOYMENT_CONTRACT",
            content = "Template content"
        });
        createTplRes.EnsureSuccessStatusCode();
        var templateId = JsonDocument.Parse(await createTplRes.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();

        return (client, hrToken, companyId, templateId);
    }

    [Fact]
    public async Task GenerateClauses_InvalidPayload_Returns400()
    {
        var (client, hrToken, _, templateId) = await SetupSeededEnvironmentAsync();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", hrToken);

        var response = await client.PostAsJsonAsync($"/api/templates/{templateId}/generate-clauses", new
        {
            language = "fr", // Invalid
            include_labor_law = false,
            include_company_policy = false // Both false is invalid
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("validation_error");
    }

    [Fact]
    public async Task GenerateClauses_TemplateFromAnotherCompany_Returns404()
    {
        var (client1, _, _, templateId1) = await SetupSeededEnvironmentAsync();
        var (client2, hrToken2, _, _) = await SetupSeededEnvironmentAsync();

        // client2 tries to access client1's template
        client2.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", hrToken2);
        
        var response = await client2.PostAsJsonAsync($"/api/templates/{templateId1}/generate-clauses", new GenerateClausesRequest());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("template_not_found");
    }

    [Fact]
    public async Task GenerateClauses_AiUnavailable_Returns502()
    {
        // No mock handler means it tries to hit localhost:3001 and fails
        var (client, hrToken, _, templateId) = await SetupSeededEnvironmentAsync();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", hrToken);

        var response = await client.PostAsJsonAsync($"/api/templates/{templateId}/generate-clauses", new GenerateClausesRequest());

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("ai_unavailable");
    }

    [Fact]
    public async Task GenerateClauses_ValidRequest_Returns200()
    {
        var mockHandler = new MockAiNodeHandler();
        var (client, hrToken, _, templateId) = await SetupSeededEnvironmentAsync(mockHandler);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", hrToken);

        var requestBody = new GenerateClausesRequest { Instruction = "Test instruction" };
        var response = await client.PostAsJsonAsync($"/api/templates/{templateId}/generate-clauses", requestBody);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<NodeTemplateClausesResponse>();
        
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Clauses.Should().ContainSingle();
        result.Clauses[0].Title.Should().Be("Mock Clause");
        
        // Assert the mock handler received the injected companyId and userId
        mockHandler.LastRequestCompanyId.Should().NotBeNullOrEmpty();
    }

    private class MockAiNodeHandler : HttpMessageHandler
    {
        public string? LastRequestCompanyId { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Headers.TryGetValues("X-Company-Id", out var values))
            {
                LastRequestCompanyId = values.FirstOrDefault();
            }

            var responseBody = new NodeTemplateClausesResponse
            {
                Success = true,
                Clauses = new System.Collections.Generic.List<GeneratedClauseDto>
                {
                    new GeneratedClauseDto { Title = "Mock Clause", Content = "Mock Content" }
                }
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(responseBody)
            };
        }
    }
}
