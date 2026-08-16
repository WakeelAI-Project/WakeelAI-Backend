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

            db.GeneratedDocuments.RemoveRange(db.GeneratedDocuments.Where(gd => gd.CompanyId == companyId));
            db.DocumentTemplates.RemoveRange(db.DocumentTemplates.Where(dt => dt.CompanyId == companyId));
            db.LeaveRequests.RemoveRange(db.LeaveRequests.Where(lr => lr.CompanyId == companyId));
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
    public async Task Summary_GivenNoLeaveRequests_ShouldReturnZeroPendingAndDocuments()
    {
        var (hrToken, _, _) = await SeedCompanyWithHrAsync();

        var response = await SendAsync(HttpMethod.Get, "/api/dashboard/summary", hrToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("pending_leave_requests").GetInt32().Should().Be(0);
        body.GetProperty("employees_on_leave_today").GetInt32().Should().Be(0);
        body.GetProperty("generated_documents_count").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Summary_ShouldCountOnlyPendingLeaveRequestsForCallersCompany()
    {
        var (hrTokenA, companyIdA, departmentIdA) = await SeedCompanyWithHrAsync();
        var employeeIdA = await CreateEmployeeAsync(hrTokenA, departmentIdA);
        await SeedLeaveRequestAsync(companyIdA, employeeIdA, "Pending");
        await SeedLeaveRequestAsync(companyIdA, employeeIdA, "Pending");
        await SeedLeaveRequestAsync(companyIdA, employeeIdA, "Approved");
        await SeedLeaveRequestAsync(companyIdA, employeeIdA, "Rejected");

        var (hrTokenB, companyIdB, departmentIdB) = await SeedCompanyWithHrAsync();
        var employeeIdB = await CreateEmployeeAsync(hrTokenB, departmentIdB);
        await SeedLeaveRequestAsync(companyIdB, employeeIdB, "Pending");

        var response = await SendAsync(HttpMethod.Get, "/api/dashboard/summary", hrTokenA);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("pending_leave_requests").GetInt32().Should().Be(2);
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

    [Fact]
    public async Task Summary_ShouldCountEmployeesOnLeaveTodayCorrectly()
    {
        var (hrToken, companyId, departmentId) = await SeedCompanyWithHrAsync();

        var employee1 = await CreateEmployeeAsync(hrToken, departmentId);
        var employee2 = await CreateEmployeeAsync(hrToken, departmentId);

        // Employee 1 on leave today
        var today = DateTime.UtcNow;
        await SeedLeaveRequestForDatesAsync(companyId, employee1, "Approved", today.AddDays(-1), today.AddDays(1));
        
        // Employee 1 has an overlapping approved leave (should not be counted twice)
        await SeedLeaveRequestForDatesAsync(companyId, employee1, "Approved", today, today.AddDays(2));
        
        // Employee 2 on leave but in the future
        await SeedLeaveRequestForDatesAsync(companyId, employee2, "Approved", today.AddDays(5), today.AddDays(10));
        
        // Employee 1 has another pending leave (should not count as on leave today)
        await SeedLeaveRequestForDatesAsync(companyId, employee1, "Pending", today.AddDays(-1), today.AddDays(1));

        var response = await SendAsync(HttpMethod.Get, "/api/dashboard/summary", hrToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("employees_on_leave_today").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Summary_ShouldCountGeneratedDocumentsForCallersCompanyOnly()
    {
        var (hrTokenA, companyIdA, _) = await SeedCompanyWithHrAsync();
        var (hrTokenB, companyIdB, _) = await SeedCompanyWithHrAsync();

        await SeedGeneratedDocumentAsync(companyIdA);
        await SeedGeneratedDocumentAsync(companyIdA);
        await SeedGeneratedDocumentAsync(companyIdA);

        await SeedGeneratedDocumentAsync(companyIdB);

        var response = await SendAsync(HttpMethod.Get, "/api/dashboard/summary", hrTokenA);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("generated_documents_count").GetInt32().Should().Be(3);
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

    private async Task<Guid> CreateEmployeeAsync(string hrToken, Guid departmentId, string? nationalId = null)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/employees", hrToken, new
        {
            full_name = "Seeded Employee",
            email = $"emp_{Guid.NewGuid():N}@integrationtest.local",
            job_title = "Analyst",
            department_id = departmentId,
            hire_date = "2026-01-01",
            salary = 10000,
            contract_type = "Full-Time",
            national_id = nationalId
        });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("record_id").GetGuid();
    }

    private async Task SeedLeaveRequestAsync(Guid companyId, Guid employeeId, string status)
    {
        await SeedLeaveRequestForDatesAsync(companyId, employeeId, status, new DateTime(2026, 1, 1), new DateTime(2026, 1, 2));
    }

    private async Task SeedLeaveRequestForDatesAsync(Guid companyId, Guid employeeId, string status, DateTime start, DateTime end)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        db.LeaveRequests.Add(new Wakeel.Domain.Entities.LeaveRequest
        {
            Id = Guid.NewGuid(),
            EmployeeId = employeeId,
            CompanyId = companyId,
            LeaveType = "Annual",
            StartDate = DateOnly.FromDateTime(start),
            EndDate = DateOnly.FromDateTime(end),
            DaysRequested = (end - start).Days + 1,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            SubmittedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }

    private async Task SeedGeneratedDocumentAsync(Guid companyId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var template = new Wakeel.Domain.Entities.DocumentTemplate
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Name = "Template",
            DocumentType = "Contract",
            ContentTemplate = "Test content",
            IsActive = true
        };
        db.DocumentTemplates.Add(template);

        db.GeneratedDocuments.Add(new Wakeel.Domain.Entities.GeneratedDocument
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            TemplateId = template.Id,
            EmployeeId = null,
            PdfUrl = "http://example.com/doc",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            DocumentType = "Contract",
            Title = "Contract doc",
            Content = "Hello",
            Status = "Draft"
        });

        await db.SaveChangesAsync();
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
