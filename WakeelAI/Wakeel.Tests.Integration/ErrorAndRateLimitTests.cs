using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Moq;
using Moq.Protected;
using Wakeel.Infrastructure.Persistence;
using Xunit;

namespace Wakeel.Tests.Integration;

public class ErrorAndRateLimitTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ErrorAndRateLimitTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CrossTenantEmployeeLookup_ShouldReturn404Envelope()
    {
        // Arrange: create Company B and an HR user + employee directly in the DB, generate an access token for HR B
        var clientB = _factory.CreateClient();
        Guid recordId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tokenGenerator = scope.ServiceProvider.GetRequiredService<Wakeel.Application.Interfaces.IJwtTokenGenerator>();

            var companyB = new Domain.Entities.Company { Id = Guid.NewGuid(), Name = "Tenant B", TaxId = Guid.NewGuid().ToString("N")[..9], RegisteredAt = DateTime.UtcNow, IsActive = true };
            db.Companies.Add(companyB);

            var hrUserB = new Domain.Entities.User { Id = Guid.NewGuid(), CompanyId = companyB.Id, Email = $"hr_b_{Guid.NewGuid():N}@test.local", FullName = "HR B", Role = Domain.Enums.UserRole.HR_Manager, IsActive = true, PasswordHash = "hashed" };
            db.Users.Add(hrUserB);

            var user = new Domain.Entities.User { Id = Guid.NewGuid(), CompanyId = companyB.Id, Email = $"emp_b_{Guid.NewGuid():N}@test.local", PasswordHash = "hashed", FullName = "Employee B", Phone = string.Empty, Role = Domain.Enums.UserRole.Employee, IsActive = true, CreatedAt = DateTime.UtcNow };
            db.Users.Add(user);

            var dept = new Domain.Entities.Department { Id = Guid.NewGuid(), CompanyId = companyB.Id, Name = "Dept B", IsDeleted = false, CreatedAt = DateTime.UtcNow };
            db.Departments.Add(dept);

            var profile = new Domain.Entities.EmployeeProfile { UserId = user.Id, DepartmentId = dept.Id, JobTitle = "Dev", Salary = 10000, HireDate = DateOnly.FromDateTime(DateTime.UtcNow), ContractType = "Full-Time" };
            db.EmployeeProfiles.Add(profile);

            await db.SaveChangesAsync();

            // generate HR token and attach to clientB
            var hrTokenB = tokenGenerator.GenerateAccessToken(hrUserB.Id, hrUserB.Email, hrUserB.Role, companyB.Id);
            clientB.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", hrTokenB);

            recordId = profile.UserId;
        }

        // Arrange: create Company A and get HR token
        // Arrange: create Company A and HR user directly and mint token
        var clientA = _factory.CreateClient();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tokenGenerator = scope.ServiceProvider.GetRequiredService<Wakeel.Application.Interfaces.IJwtTokenGenerator>();

            var companyA = new Domain.Entities.Company { Id = Guid.NewGuid(), Name = "Tenant A", TaxId = Guid.NewGuid().ToString("N")[..9], RegisteredAt = DateTime.UtcNow, IsActive = true };
            db.Companies.Add(companyA);

            var hrUserA = new Domain.Entities.User { Id = Guid.NewGuid(), CompanyId = companyA.Id, Email = $"hr_a_{Guid.NewGuid():N}@test.local", FullName = "HR A", Role = Domain.Enums.UserRole.HR_Manager, IsActive = true, PasswordHash = "hashed" };
            db.Users.Add(hrUserA);
            await db.SaveChangesAsync();

            var hrTokenA = tokenGenerator.GenerateAccessToken(hrUserA.Id, hrUserA.Email, hrUserA.Role, companyA.Id);
            clientA.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", hrTokenA);
        }

        // Act: attempt to get employee B as HR from A
        var getResponse = await clientA.GetAsync($"/api/employees/{recordId}");

        // Assert: 404 with standardized envelope
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString().Should().Be("employee_not_found");
        body.GetProperty("status").GetInt32().Should().Be(404);
    }

    [Fact]
    public async Task MalformedRequest_ShouldReturn400Envelope()
    {
        var client = _factory.CreateClient();

        // Create company and an HR user directly, then mint an access token for HR
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tokenGenerator = scope.ServiceProvider.GetRequiredService<Wakeel.Application.Interfaces.IJwtTokenGenerator>();

            var company = new Domain.Entities.Company { Id = Guid.NewGuid(), Name = "MalformedCo", TaxId = Guid.NewGuid().ToString("N")[..9], RegisteredAt = DateTime.UtcNow, IsActive = true };
            db.Companies.Add(company);

            var hr = new Domain.Entities.User { Id = Guid.NewGuid(), CompanyId = company.Id, Email = $"hr_{Guid.NewGuid():N}@test.local", FullName = "HR Test", Role = Domain.Enums.UserRole.HR_Manager, IsActive = true, PasswordHash = "hashed" };
            db.Users.Add(hr);

            await db.SaveChangesAsync();

            var hrToken = tokenGenerator.GenerateAccessToken(hr.Id, hr.Email, hr.Role, company.Id);
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", hrToken);
        }

        // Send malformed employee create (missing required fields)
        var response = await client.PostAsJsonAsync("/api/employees", new { email = "no_name@x.com" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var bodyStr = await response.Content.ReadAsStringAsync();
        var bodyJson = JsonDocument.Parse(bodyStr).RootElement;
        if (bodyJson.TryGetProperty("error", out var errProp))
            errProp.GetString().Should().Be("validation_error");
        bodyJson.GetProperty("status").GetInt32().Should().Be(400);
    }

    [Fact]
    public async Task RateLimit_ChatAsk_ShouldReturn429AndRetryAfter()
    {
        // Prepare a custom factory with mocked AiNodeClient to avoid external calls
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<System.Threading.CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{ \"message\": \"ok\" }") });

        var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddHttpClient("AiNodeClient").ConfigurePrimaryHttpMessageHandler(() => handlerMock.Object);
            });
        });

        var client = customFactory.CreateClient();

        // Register company and create an employee into DB, then login as employee
        var ownerEmail = $"owner_rl_{Guid.NewGuid():N}@test.local";
        var register = await client.PostAsJsonAsync("/api/auth/register-company", new
        {
            company_name = "RateLimitCo",
            tax_id = Guid.NewGuid().ToString("N")[..9],
            owner_full_name = "Owner RL",
            owner_email = ownerEmail,
            password = "TestPassword123!"
        });
        register.EnsureSuccessStatusCode();
        var regBody = await register.Content.ReadFromJsonAsync<JsonElement>();
        // ownerEmail is known from the request variable above

        string empEmail = $"emp_rl_{Guid.NewGuid():N}@test.local";
        using (var scope = customFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var owner = await db.Users.FirstAsync(u => u.Email == ownerEmail);
            db.Users.Add(new Domain.Entities.User
            {
                Id = Guid.NewGuid(),
                CompanyId = owner.CompanyId,
                FullName = "RateLimit Emp",
                Email = empEmail,
                IsActive = true,
                Role = Domain.Enums.UserRole.Employee,
                PasswordHash = owner.PasswordHash
            });
            await db.SaveChangesAsync();
        }

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = empEmail, password = "TestPassword123!" });
        login.EnsureSuccessStatusCode();
        var loginBody = await login.Content.ReadFromJsonAsync<JsonElement>();
        var token = loginBody.GetProperty("access_token").GetString();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Send 21 requests quickly to exceed the 20 req/min limit
        HttpResponseMessage lastResponse = null!;
        for (int i = 0; i < 21; i++)
        {
            var resp = await client.PostAsJsonAsync("/api/ai/chat", new { message = "hi", language = "en" });
            lastResponse = resp;
            if (resp.StatusCode == HttpStatusCode.TooManyRequests) break;
        }

        lastResponse.Should().NotBeNull();
        lastResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        lastResponse.Headers.Contains("Retry-After").Should().BeTrue();
        var contentStr = await lastResponse.Content.ReadAsStringAsync();
        JsonElement bodyElem;
        try
        {
            bodyElem = JsonDocument.Parse(contentStr).RootElement;
        }
        catch (JsonException)
        {
            throw new Xunit.Sdk.XunitException("Expected JSON body but got: " + contentStr);
        }

        if (bodyElem.ValueKind != JsonValueKind.Object)
            throw new Xunit.Sdk.XunitException("Expected JSON object body but got: " + contentStr);

        if (bodyElem.TryGetProperty("error", out var errProp))
            errProp.GetString().Should().Be("rate_limited");
        else
            throw new Xunit.Sdk.XunitException("Missing 'error' property in rate limit response. Body: " + contentStr);

        if (bodyElem.TryGetProperty("status", out var statusProp))
            statusProp.GetInt32().Should().Be(429);
        else if (bodyElem.TryGetProperty("Status", out var statusProp2))
            statusProp2.GetInt32().Should().Be(429);
        else
            throw new Xunit.Sdk.XunitException("Missing 'status' property in rate limit response. Body: " + contentStr);
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string url, string token, object? body = null)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        if (body != null) req.Content = JsonContent.Create(body);
        return client.SendAsync(req);
    }
}
