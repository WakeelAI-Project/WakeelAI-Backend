// NEW FILE: Wakeel.Tests.Integration/Templates/GenerateClausesSecurityTests.cs
//
// Complements the 4 tests already in TemplatesIntegrationTests.cs
// (400 validation, cross-tenant 404, AI-unavailable 502, valid 200).
// This file adds the security/robustness cases:
//   1. Anonymous            -> 401
//   2. Employee token       -> 403
//   3. Rogue companyId in body is IGNORED (JWT value forwarded to Node)
//   4. language = "ar"      -> forwarded to Node payload
//   5. Node returns 500     -> ai_error envelope, upstream body NOT leaked
//   6. Node times out       -> 504 ai_timeout
//   7. Non-mutation         -> template content unchanged after a successful call

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
using Wakeel.API;
using Wakeel.Application.DTOs.Templates;
using Wakeel.Infrastructure.Persistence;
using Xunit;

namespace Wakeel.Tests.Integration.Templates;

public class GenerateClausesSecurityTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private const string KnownPassword = "TestPassword123!";

    public GenerateClausesSecurityTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ---------------------------------------------------------------
    // Setup helpers (same conventions as TemplatesIntegrationTests)
    // ---------------------------------------------------------------

    private WebApplicationFactory<Program> BuildFactory(HttpMessageHandler? mockAiNodeHandler = null)
    {
        if (mockAiNodeHandler == null) return _factory;
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddHttpClient("AiNodeClient")
                        .ConfigurePrimaryHttpMessageHandler(() => mockAiNodeHandler);
            });
        });
    }

    private async Task<(HttpClient Client, string HrToken, string EmployeeToken, Guid CompanyId, Guid TemplateId)>
        SetupSeededEnvironmentAsync(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var ownerEmail = $"owner_{suffix}@test.com";
        var hrEmail = $"hr_{suffix}@test.com";
        var empEmail = $"emp_{suffix}@test.com";

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

        // 3. Activate HR password + seed an Employee user directly in the DB
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hrUser = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.Email == hrEmail);
            hrUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(KnownPassword);
            hrUser.IsActive = true;

            db.Users.Add(new Wakeel.Domain.Entities.User
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                FullName = "Test Employee",
                Email = empEmail,
                IsActive = true,
                Role = Wakeel.Domain.Enums.UserRole.Employee,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(KnownPassword)
            });
            await db.SaveChangesAsync();
        }

        // 4. Login HR + Employee
        var hrLoginRes = await client.PostAsJsonAsync("/api/auth/login", new { email = hrEmail, password = KnownPassword });
        hrLoginRes.EnsureSuccessStatusCode();
        var hrToken = JsonDocument.Parse(await hrLoginRes.Content.ReadAsStringAsync()).RootElement.GetProperty("access_token").GetString()!;

        var empLoginRes = await client.PostAsJsonAsync("/api/auth/login", new { email = empEmail, password = KnownPassword });
        empLoginRes.EnsureSuccessStatusCode();
        var employeeToken = JsonDocument.Parse(await empLoginRes.Content.ReadAsStringAsync()).RootElement.GetProperty("access_token").GetString()!;

        // 5. Create Template as HR
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", hrToken);
        var createTplRes = await client.PostAsJsonAsync("/api/templates", new
        {
            name = "Test Template",
            document_type = "EMPLOYMENT_CONTRACT",
            content_template = "Template content"
        });
        createTplRes.EnsureSuccessStatusCode();
        var templateId = JsonDocument.Parse(await createTplRes.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();

        return (client, hrToken, employeeToken, companyId, templateId);
    }

    private static void UseToken(HttpClient client, string? token)
    {
        client.DefaultRequestHeaders.Authorization = token == null
            ? null
            : new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    // ---------------------------------------------------------------
    // Tests
    // ---------------------------------------------------------------

    [Fact]
    public async Task GenerateClauses_Anonymous_Returns401()
    {
        var (client, _, _, _, templateId) = await SetupSeededEnvironmentAsync(BuildFactory());
        UseToken(client, null);

        var response = await client.PostAsJsonAsync($"/api/templates/{templateId}/generate-clauses", new GenerateClausesRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GenerateClauses_EmployeeRole_Returns403()
    {
        var (client, _, employeeToken, _, templateId) = await SetupSeededEnvironmentAsync(BuildFactory());
        UseToken(client, employeeToken);

        var response = await client.PostAsJsonAsync($"/api/templates/{templateId}/generate-clauses", new GenerateClausesRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GenerateClauses_RogueCompanyIdInBody_IsIgnored_JwtValueForwarded()
    {
        var mockHandler = new CapturingMockAiNodeHandler();
        var factory = BuildFactory(mockHandler);
        var (client, hrToken, _, companyId, templateId) = await SetupSeededEnvironmentAsync(factory);
        UseToken(client, hrToken);

        // Malicious body tries to spoof another tenant.
        var rogueCompanyId = Guid.NewGuid().ToString();
        var response = await client.PostAsJsonAsync($"/api/templates/{templateId}/generate-clauses", new
        {
            language = "en",
            include_labor_law = true,
            include_company_policy = true,
            companyId = rogueCompanyId,   // must be ignored by model binding
            company_id = rogueCompanyId   // snake_case variant must be ignored too
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        mockHandler.LastRequestBody.Should().NotBeNull();
        var forwarded = JsonDocument.Parse(mockHandler.LastRequestBody!).RootElement;
        forwarded.GetProperty("companyId").GetString().Should().Be(companyId.ToString());
        forwarded.GetProperty("companyId").GetString().Should().NotBe(rogueCompanyId);
        mockHandler.LastCompanyIdHeader.Should().Be(companyId.ToString());
    }

    [Fact]
    public async Task GenerateClauses_ArabicLanguage_IsForwardedToNode()
    {
        var mockHandler = new CapturingMockAiNodeHandler();
        var factory = BuildFactory(mockHandler);
        var (client, hrToken, _, _, templateId) = await SetupSeededEnvironmentAsync(factory);
        UseToken(client, hrToken);

        var response = await client.PostAsJsonAsync($"/api/templates/{templateId}/generate-clauses",
            new GenerateClausesRequest { Language = "ar" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var forwarded = JsonDocument.Parse(mockHandler.LastRequestBody!).RootElement;
        forwarded.GetProperty("language").GetString().Should().Be("ar");
    }

    [Fact]
    public async Task GenerateClauses_NodeReturns500_ReturnsAiErrorWithoutLeakingBody()
    {
        var secretInternalError = "SECRET_INTERNAL_STACK_TRACE_DO_NOT_LEAK";
        var mockHandler = new StaticResponseHandler(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent(secretInternalError)
        });
        var factory = BuildFactory(mockHandler);
        var (client, hrToken, _, _, templateId) = await SetupSeededEnvironmentAsync(factory);
        UseToken(client, hrToken);

        var response = await client.PostAsJsonAsync($"/api/templates/{templateId}/generate-clauses", new GenerateClausesRequest());
        var json = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        json.Should().Contain("ai_error");
        json.Should().NotContain(secretInternalError); // upstream body must not leak
    }

    [Fact]
    public async Task GenerateClauses_NodeTimesOut_Returns504AiTimeout()
    {
        var mockHandler = new ThrowingHandler(new TaskCanceledException("simulated timeout"));
        var factory = BuildFactory(mockHandler);
        var (client, hrToken, _, _, templateId) = await SetupSeededEnvironmentAsync(factory);
        UseToken(client, hrToken);

        var response = await client.PostAsJsonAsync($"/api/templates/{templateId}/generate-clauses", new GenerateClausesRequest());
        var json = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.GatewayTimeout);
        json.Should().Contain("ai_timeout");
    }

    [Fact]
    public async Task GenerateClauses_SuccessfulCall_DoesNotMutateTemplate()
    {
        var mockHandler = new CapturingMockAiNodeHandler();
        var factory = BuildFactory(mockHandler);
        var (client, hrToken, _, _, templateId) = await SetupSeededEnvironmentAsync(factory);
        UseToken(client, hrToken);

        // Snapshot the template before
        var before = await client.GetStringAsync($"/api/templates/{templateId}");

        var response = await client.PostAsJsonAsync($"/api/templates/{templateId}/generate-clauses", new GenerateClausesRequest());
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Snapshot after — must be byte-identical (endpoint is non-mutating)
        var after = await client.GetStringAsync($"/api/templates/{templateId}");
        after.Should().Be(before);
    }

    // ---------------------------------------------------------------
    // Mock handlers
    // ---------------------------------------------------------------

    private class CapturingMockAiNodeHandler : HttpMessageHandler
    {
        public string? LastRequestBody { get; private set; }
        public string? LastCompanyIdHeader { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content != null)
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            if (request.Headers.TryGetValues("X-Company-Id", out var values))
                LastCompanyIdHeader = values.FirstOrDefault();

            var responseBody = new NodeTemplateClausesResponse
            {
                Success = true,
                Clauses = new System.Collections.Generic.List<GeneratedClauseDto>
                {
                    new GeneratedClauseDto { Title = "Mock Clause", Content = "Mock Content" }
                }
            };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(responseBody) };
        }
    }

    private class StaticResponseHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;
        public StaticResponseHandler(HttpResponseMessage response) => _response = response;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_response);
    }

    private class ThrowingHandler : HttpMessageHandler
    {
        private readonly Exception _exception;
        public ThrowingHandler(Exception exception) => _exception = exception;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromException<HttpResponseMessage>(_exception);
    }
}
