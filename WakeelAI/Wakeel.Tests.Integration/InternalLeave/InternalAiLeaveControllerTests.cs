using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wakeel.Application.Interfaces;
using Wakeel.Infrastructure.Persistence;
using Xunit;

namespace Wakeel.Tests.Integration.InternalLeave;

/// <summary>
/// Integration tests for InternalAiLeaveController (/api/ai/leave-requests).
/// 
/// Security contract being tested:
///   - Missing X-Internal-API-Key                 → 401 Unauthorized
///   - Valid PSK + missing identity header         → 400 missing_identity_headers
///   - Valid PSK + full headers + valid payload    → 201 Created
///   - Submit non-existent request                 → 404 Not Found
///   - Cancel non-existent request                 → 404 Not Found
///   - Sick leave without attachment_url           → 422 attachment_required
/// 
/// Requires: local SQL Server running (used by CustomWebApplicationFactory).
/// </summary>
public class InternalAiLeaveControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private const string ValidPsk      = "test-internal-key";
    private const string KnownPassword = "TestPassword123!";

    public InternalAiLeaveControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // -------- Helpers --------

    /// <summary>
    /// Full seeding flow:
    /// 1. Register company → get owner token.
    /// 2. Invite HR_Manager → patch password directly in DB (invitation flow has no password).
    /// 3. Login as HR_Manager → get hr token.
    /// 4. Owner creates a department (department create is Company_Owner/HR_Manager).
    /// 5. HR_Manager creates an employee (POST /api/employees requires HR_Manager).
    /// Returns (employeeId, companyId) for use in M2M headers.
    /// </summary>
    private async Task<(Guid EmployeeId, Guid CompanyId)> SeedEmployeeAsync()
    {
        var client    = _factory.CreateClient();
        var suffix    = Guid.NewGuid().ToString("N")[..12];
        var ownerEmail = $"owner_{suffix}@test.local";
        var hrEmail    = $"hr_{suffix}@test.local";
        var hrPassword = "Test1234!";

        // 1. Register company
        var registerRes = await client.PostAsJsonAsync("/api/auth/register-company", new
        {
            company_name    = $"LeaveTest Corp {suffix}",
            tax_id          = suffix,
            owner_full_name = "Test Owner",
            owner_email     = ownerEmail,
            password        = KnownPassword
        });
        registerRes.EnsureSuccessStatusCode();
        var regJson    = await registerRes.Content.ReadAsStringAsync();
        var regDoc     = JsonDocument.Parse(regJson);
        var ownerToken = regDoc.RootElement.GetProperty("access_token").GetString()!;
        var companyId  = regDoc.RootElement.GetProperty("company_id").GetGuid();

        // 2. Set the HttpClient Authorization header using the Owner token.
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ownerToken);

        // 3. Create department (owner creates it; owner can create departments)
        var deptRes = await client.PostAsJsonAsync("/api/departments", new { name = $"Dept {suffix}", description = "Test dept" });
        deptRes.EnsureSuccessStatusCode();
        var deptId = JsonDocument.Parse(await deptRes.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();

        // 4. Invite HR_Manager (owner only)
        var inviteRes = await client.PostAsJsonAsync("/api/users/invite", new { full_name = "Test HR", email = hrEmail, role = "HR_Manager" });
        inviteRes.EnsureSuccessStatusCode();

        // 5. Patch the invited HR user's password hash and IsActive directly via DI scope
        using (var scope = _factory.Services.CreateScope())
        {
            var db     = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hrUser = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.Email == hrEmail);
            hrUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(hrPassword);
            hrUser.IsActive = true;
            await db.SaveChangesAsync();
        }

        // 6. Login as HR_Manager
        var hrLoginRes  = await client.PostAsJsonAsync("/api/auth/login", new { email = hrEmail, password = hrPassword });
        hrLoginRes.EnsureSuccessStatusCode();
        var hrDoc   = JsonDocument.Parse(await hrLoginRes.Content.ReadAsStringAsync());
        var hrToken = hrDoc.RootElement.GetProperty("access_token").GetString()!;

        // 7. Update the HttpClient Authorization header to use the HR token.
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", hrToken);

        // 8. Create employee as HR_Manager
        var empRes = await client.PostAsJsonAsync("/api/employees", new
        {
            full_name     = "Test Employee",
            email         = $"emp_{suffix}@test.local",
            job_title     = "Developer",
            salary        = 5000,
            hire_date     = "2026-01-01",
            contract_type = "Full-Time",
            department_id = deptId
        });
        empRes.EnsureSuccessStatusCode();
        var empId = JsonDocument.Parse(await empRes.Content.ReadAsStringAsync()).RootElement.GetProperty("user_id").GetGuid();

        return (empId, companyId);
    }

    private static HttpRequestMessage BuildInternalRequest(
        HttpMethod method,
        string url,
        string? psk,
        string? userId,
        string? companyId,
        string? role,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, url);

        if (psk      != null) request.Headers.Add("X-Internal-API-Key", psk);
        if (userId   != null) request.Headers.Add("X-User-Id",          userId);
        if (companyId != null) request.Headers.Add("X-Company-Id",      companyId);
        if (role     != null) request.Headers.Add("X-Role",             role);

        if (body != null)
            request.Content = JsonContent.Create(body);

        return request;
    }

    // -------- Tests --------

    [Fact]
    public async Task CreateDraft_MissingPsk_Returns401()
    {
        // Arrange
        var client = _factory.CreateClient();
        using var request = BuildInternalRequest(
            HttpMethod.Post, "/api/ai/leave-requests",
            psk: null, userId: Guid.NewGuid().ToString(), companyId: Guid.NewGuid().ToString(), role: "Employee",
            body: new { leave_type = "Annual", start_date = "2027-01-10", end_date = "2027-01-15" });

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var json = await response.Content.ReadAsStringAsync();
        var doc  = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("error").GetString().Should().Be("unauthorized");
    }

    [Fact]
    public async Task CreateDraft_ValidPsk_MissingUserId_Returns400WithMissingIdentityHeadersCode()
    {
        // Arrange
        var client = _factory.CreateClient();
        using var request = BuildInternalRequest(
            HttpMethod.Post, "/api/ai/leave-requests",
            psk: ValidPsk, userId: null, companyId: Guid.NewGuid().ToString(), role: "Employee",
            body: new { leave_type = "Annual", start_date = "2027-01-10", end_date = "2027-01-15" });

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        var doc  = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("error").GetString().Should().Be("missing_identity_headers");
    }

    [Fact]
    public async Task CreateDraft_ValidPsk_MissingCompanyId_Returns400WithMissingIdentityHeadersCode()
    {
        // Arrange
        var client = _factory.CreateClient();
        using var request = BuildInternalRequest(
            HttpMethod.Post, "/api/ai/leave-requests",
            psk: ValidPsk, userId: Guid.NewGuid().ToString(), companyId: null, role: "Employee",
            body: new { leave_type = "Annual", start_date = "2027-01-10", end_date = "2027-01-15" });

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        var doc  = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("error").GetString().Should().Be("missing_identity_headers");
    }

    [Fact]
    public async Task CreateDraft_ValidPsk_ValidPayload_AnnualLeave_Returns201()
    {
        // Arrange — seed a real employee so the service can find their leave balance
        var (employeeId, companyId) = await SeedEmployeeAsync();
        var client = _factory.CreateClient();

        using var request = BuildInternalRequest(
            HttpMethod.Post, "/api/ai/leave-requests",
            psk: ValidPsk,
            userId:    employeeId.ToString(),
            companyId: companyId.ToString(),
            role:      "Employee",
            body: new { leave_type = "Annual", start_date = "2026-10-01", end_date = "2026-10-03" });

        // Act
        var response = await client.SendAsync(request);

        // Assert — 201 Created with the expected shape
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var json = await response.Content.ReadAsStringAsync();
        var doc  = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("request_id").GetString().Should().NotBeNullOrEmpty();
        doc.RootElement.GetProperty("status").GetString().Should().Be("Draft");
        doc.RootElement.GetProperty("days_requested").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task CreateDraft_SickLeave_MissingAttachmentUrl_Returns422AttachmentRequired()
    {
        // Arrange
        var (employeeId, companyId) = await SeedEmployeeAsync();
        var client = _factory.CreateClient();

        using var request = BuildInternalRequest(
            HttpMethod.Post, "/api/ai/leave-requests",
            psk: ValidPsk,
            userId:    employeeId.ToString(),
            companyId: companyId.ToString(),
            role:      "Employee",
            // No attachment_url — service should check balance first (Sick=10 days in 2026), then throw attachment_required
            body: new { leave_type = "Sick", start_date = "2026-09-01", end_date = "2026-09-02" });

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var json = await response.Content.ReadAsStringAsync();
        var doc  = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("error").GetString().Should().Be("attachment_required");
    }

    [Fact]
    public async Task CreateDraft_SickLeaveWithInvalidAttachmentUrl_Returns422()
    {
        // Arrange
        var (employeeId, companyId) = await SeedEmployeeAsync();
        var client = _factory.CreateClient();

        using var request = BuildInternalRequest(
            HttpMethod.Post, "/api/ai/leave-requests",
            psk: ValidPsk,
            userId:    employeeId.ToString(),
            companyId: companyId.ToString(),
            role:      "Employee",
            body: new { leave_type = "Sick", start_date = "2026-09-01", end_date = "2026-09-02", attachment_url = "https://invalid-url.com/file.pdf" });

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var json = await response.Content.ReadAsStringAsync();
        var doc  = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("error").GetString().Should().Be("invalid_attachment");
    }

    [Fact]
    public async Task SubmitDraft_NonExistentRequest_Returns404()
    {
        // Arrange
        var client = _factory.CreateClient();
        var fakeId    = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var companyId  = Guid.NewGuid();

        using var request = BuildInternalRequest(
            HttpMethod.Patch, $"/api/ai/leave-requests/{fakeId}/submit",
            psk: ValidPsk,
            userId:    employeeId.ToString(),
            companyId: companyId.ToString(),
            role:      "Employee");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var json = await response.Content.ReadAsStringAsync();
        var doc  = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("error").GetString().Should().Be("leave_request_not_found");
    }

    [Fact]
    public async Task CancelDraft_NonExistentRequest_Returns404()
    {
        // Arrange
        var client = _factory.CreateClient();
        var fakeId    = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var companyId  = Guid.NewGuid();

        using var request = BuildInternalRequest(
            HttpMethod.Delete, $"/api/ai/leave-requests/{fakeId}",
            psk: ValidPsk,
            userId:    employeeId.ToString(),
            companyId: companyId.ToString(),
            role:      "Employee");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var json = await response.Content.ReadAsStringAsync();
        var doc  = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("error").GetString().Should().Be("leave_request_not_found");
    }

    [Fact]
    public async Task SubmitDraft_ThenSubmitAgain_Returns409NotADraft()
    {
        // Arrange — create and submit a real draft, then try to submit again
        var (employeeId, companyId) = await SeedEmployeeAsync();
        var client = _factory.CreateClient();

        // Create draft
        using var createReq = BuildInternalRequest(
            HttpMethod.Post, "/api/ai/leave-requests",
            psk: ValidPsk, userId: employeeId.ToString(), companyId: companyId.ToString(), role: "Employee",
            body: new { leave_type = "Annual", start_date = "2026-11-01", end_date = "2026-11-02" });

        var createRes = await client.SendAsync(createReq);
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var createJson = await createRes.Content.ReadAsStringAsync();
        var requestId  = JsonDocument.Parse(createJson).RootElement.GetProperty("request_id").GetString()!;

        // Submit once
        using var submitReq1 = BuildInternalRequest(
            HttpMethod.Patch, $"/api/ai/leave-requests/{requestId}/submit",
            psk: ValidPsk, userId: employeeId.ToString(), companyId: companyId.ToString(), role: "Employee");
        var submitRes1 = await client.SendAsync(submitReq1);
        submitRes1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Submit again → 409
        using var submitReq2 = BuildInternalRequest(
            HttpMethod.Patch, $"/api/ai/leave-requests/{requestId}/submit",
            psk: ValidPsk, userId: employeeId.ToString(), companyId: companyId.ToString(), role: "Employee");
        var submitRes2 = await client.SendAsync(submitReq2);

        // Assert
        submitRes2.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var json = await submitRes2.Content.ReadAsStringAsync();
        var doc  = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("error").GetString().Should().Be("not_a_draft");
    }
}
