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
using Wakeel.Domain.Entities;
using Wakeel.Infrastructure.Persistence;
using Xunit;

namespace Wakeel.Tests.Integration.Leave;

/// <summary>FIX-14: Company_Owner can list and view (but never review) leave requests for
/// their own company - the same read-only, non-Draft/Cancelled scope HR gets.</summary>
public class LeaveRequestsOwnerAccessTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string KnownPassword = "IntegrationTest123!";

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public LeaveRequestsOwnerAccessTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(string OwnerToken, Guid CompanyId, Guid EmployeeId, Guid PendingRequestId)> SeedCompanyWithPendingRequestAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register-company", new
        {
            company_name = $"OwnerLeaveCo {suffix}",
            tax_id = suffix,
            owner_full_name = "Owner Test",
            owner_email = $"owner_{suffix}@integrationtest.local",
            password = KnownPassword
        });
        registerResponse.EnsureSuccessStatusCode();
        var registerBody = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var ownerToken = registerBody.GetProperty("access_token").GetString()!;
        var companyId = registerBody.GetProperty("company_id").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var department = new Department { Id = Guid.NewGuid(), CompanyId = companyId, Name = "Engineering", IsDeleted = false, CreatedAt = DateTime.UtcNow };
        db.Departments.Add(department);

        var employee = new Wakeel.Domain.Entities.User
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Email = $"emp_{suffix}@integrationtest.local",
            FullName = "Owner-Visible Employee",
            Role = Wakeel.Domain.Enums.UserRole.Employee,
            IsActive = true,
            PasswordHash = hasher.HashPassword(KnownPassword),
            IsEmailConfirmed = true,
            MustChangePassword = false,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(employee);

        var profile = new EmployeeProfile
        {
            UserId = employee.Id,
            DepartmentId = department.Id,
            JobTitle = "Engineer",
            Salary = 10000m,
            HireDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1)),
            ContractType = "Full-Time"
        };
        db.EmployeeProfiles.Add(profile);

        var pendingRequest = new LeaveRequest
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            CompanyId = companyId,
            LeaveType = "Annual",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(6)),
            DaysRequested = 2,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            SubmittedAt = DateTime.UtcNow
        };
        db.LeaveRequests.Add(pendingRequest);

        await db.SaveChangesAsync();

        return (ownerToken, companyId, employee.Id, pendingRequest.Id);
    }

    [Fact]
    public async Task List_GivenOwnerToken_Returns200WithCompanysRequests()
    {
        var (ownerToken, _, _, pendingRequestId) = await SeedCompanyWithPendingRequestAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/leave-requests")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", ownerToken) }
        };
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var ids = body.GetProperty("data").EnumerateArray();
        ids.Should().Contain(e => e.GetProperty("request_id").GetGuid() == pendingRequestId);
    }

    [Fact]
    public async Task GetById_GivenOwnerToken_Returns200()
    {
        var (ownerToken, _, _, pendingRequestId) = await SeedCompanyWithPendingRequestAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/leave-requests/{pendingRequestId}")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", ownerToken) }
        };
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReviewRequest_GivenOwnerToken_Returns403()
    {
        // FIX-14: read-only for Owner - no Approve/Reject access, even by calling the API directly.
        var (ownerToken, _, _, pendingRequestId) = await SeedCompanyWithPendingRequestAsync();

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/leave-requests/{pendingRequestId}")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", ownerToken) },
            Content = JsonContent.Create(new { status = "Approved" })
        };
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
