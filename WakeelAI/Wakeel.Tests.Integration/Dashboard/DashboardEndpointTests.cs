using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wakeel.Application.Interfaces;
using Wakeel.Infrastructure.Persistence;
using Xunit;

namespace Wakeel.Tests.Integration.Dashboard;

public class DashboardEndpointTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private const string KnownPassword = "IntegrationTest123!";

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly List<Guid> _companyIdsToCleanUp = new();

    public DashboardEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_companyIdsToCleanUp.Count == 0)
            return;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        foreach (var companyId in _companyIdsToCleanUp)
        {
            var userIds = await db.Users.Where(u => u.CompanyId == companyId).Select(u => u.Id).ToListAsync();

            db.LeaveBalances.RemoveRange(db.LeaveBalances.Where(lb => userIds.Contains(lb.EmployeeId)));
            db.EmployeeProfiles.RemoveRange(db.EmployeeProfiles.Where(ep => userIds.Contains(ep.UserId)));
            db.RefreshTokens.RemoveRange(db.RefreshTokens.Where(rt => userIds.Contains(rt.UserId)));
            db.Users.RemoveRange(db.Users.Where(u => u.CompanyId == companyId));
            db.Departments.RemoveRange(db.Departments.Where(d => d.CompanyId == companyId));
            db.Companies.RemoveRange(db.Companies.Where(c => c.Id == companyId));
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Summary_GivenNoEmployees_ShouldReturnZeroCounts()
    {
        var (hrToken, _, _) = await SeedCompanyWithHrAsync();

        var response = await SendAsync(HttpMethod.Get, "/api/dashboard/summary", hrToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("employee_count").GetInt32().Should().Be(0);
        body.GetProperty("active_employees").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Summary_GivenActiveAndInactiveEmployees_ShouldReturnAccurateCounts()
    {
        var (hrToken, _, departmentId) = await SeedCompanyWithHrAsync();

        await CreateEmployeeAsync(hrToken, departmentId);
        await CreateEmployeeAsync(hrToken, departmentId);
        var toDeactivate = await CreateEmployeeAsync(hrToken, departmentId);

        var deactivateResponse = await SendAsync(HttpMethod.Delete, $"/api/employees/{toDeactivate}", hrToken);
        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await SendAsync(HttpMethod.Get, "/api/dashboard/summary", hrToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("employee_count").GetInt32().Should().Be(3);
        body.GetProperty("active_employees").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Summary_ShouldReturnPlaceholderValuesForUnimplementedFeatures()
    {
        var (hrToken, _, _) = await SeedCompanyWithHrAsync();

        var response = await SendAsync(HttpMethod.Get, "/api/dashboard/summary", hrToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("pending_leave_requests").GetInt32().Should().Be(0);
        body.GetProperty("handbook_uploaded").GetBoolean().Should().BeFalse();
        body.GetProperty("generated_documents_count").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Summary_ShouldOnlyCountCallersCompanyEmployees()
    {
        var (hrTokenA, _, departmentIdA) = await SeedCompanyWithHrAsync();
        var (hrTokenB, _, departmentIdB) = await SeedCompanyWithHrAsync();

        await CreateEmployeeAsync(hrTokenA, departmentIdA);
        await CreateEmployeeAsync(hrTokenB, departmentIdB);
        await CreateEmployeeAsync(hrTokenB, departmentIdB);

        var response = await SendAsync(HttpMethod.Get, "/api/dashboard/summary", hrTokenA);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("employee_count").GetInt32().Should().Be(1);
        body.GetProperty("active_employees").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Summary_GivenNonHrCaller_ShouldReturn403()
    {
        var (_, _, ownerToken) = await SeedCompanyAsync();

        var response = await SendAsync(HttpMethod.Get, "/api/dashboard/summary", ownerToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------

    private async Task<(Guid CompanyId, string HrEmail, string OwnerToken)> SeedCompanyAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register-company", new
        {
            company_name = $"IT Co {suffix}",
            tax_id = suffix,
            owner_full_name = "Owner Test",
            owner_email = $"owner_{suffix}@integrationtest.local",
            password = "StrongPassword123!"
        });
        registerResponse.EnsureSuccessStatusCode();

        var registerBody = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var companyId = registerBody.GetProperty("company_id").GetGuid();
        var ownerToken = registerBody.GetProperty("access_token").GetString()!;

        _companyIdsToCleanUp.Add(companyId);

        return (companyId, $"hr_{suffix}@integrationtest.local", ownerToken);
    }

    private async Task<(string HrToken, Guid CompanyId, Guid DepartmentId)> SeedCompanyWithHrAsync()
    {
        var (companyId, hrEmail, ownerToken) = await SeedCompanyAsync();

        var inviteResponse = await SendAsync(HttpMethod.Post, "/api/users/invite", ownerToken, new
        {
            full_name = "HR Test",
            email = hrEmail,
            role = "HR_Manager"
        });
        inviteResponse.EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var hrUser = await db.Users.FirstAsync(u => u.Email == hrEmail);
            hrUser.PasswordHash = hasher.HashPassword(KnownPassword);
            await db.SaveChangesAsync();
        }

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new { email = hrEmail, password = KnownPassword });
        loginResponse.EnsureSuccessStatusCode();

        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var hrToken = loginBody.GetProperty("access_token").GetString()!;

        var departmentId = await SeedDepartmentAsync(ownerToken);

        return (hrToken, companyId, departmentId);
    }

    private async Task<Guid> SeedDepartmentAsync(string ownerToken)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/departments", ownerToken, new
        {
            name = $"Dept {Guid.NewGuid():N}"
        });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateEmployeeAsync(string hrToken, Guid departmentId)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/employees", hrToken, new
        {
            full_name = "Seeded Employee",
            email = $"emp_{Guid.NewGuid():N}@integrationtest.local",
            job_title = "Analyst",
            department_id = departmentId,
            hire_date = "2026-01-01",
            salary = 10000,
            contract_type = "Full-Time"
        });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("record_id").GetGuid();
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) }
        };
        if (body is not null)
            request.Content = JsonContent.Create(body);

        return await _client.SendAsync(request);
    }
}
