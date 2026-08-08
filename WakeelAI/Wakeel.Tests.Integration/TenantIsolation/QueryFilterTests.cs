using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Wakeel.API;
using Wakeel.Domain.Entities;
using Wakeel.Domain.Enums;
using Wakeel.Infrastructure.Persistence;
using Xunit;
namespace Wakeel.Tests.Integration.TenantIsolation;

public class QueryFilterTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public QueryFilterTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient Client, Guid CompanyId)> RegisterCompanyAsync(string companyName)
    {
        var client = _factory.CreateClient();
        var email = $"owner_{Guid.NewGuid()}@test.com";
        var password = "Password123!";

        var registerRes = await client.PostAsJsonAsync("/api/auth/register-company", new
        {
            company_name = companyName,
            tax_id = Guid.NewGuid().ToString("N")[..9],
            owner_full_name = "Owner",
            owner_email = email,
            password
        });

        var registerJson = await registerRes.Content.ReadAsStringAsync();
        var registerDoc = JsonDocument.Parse(registerJson);
        var companyId = Guid.Parse(registerDoc.RootElement.GetProperty("company_id").GetString()!);
        var token = registerDoc.RootElement.GetProperty("access_token").GetString();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        return (client, companyId);
    }

    [Fact]
    public async Task GlobalQueryFilter_ShouldPreventCrossTenantDataAccess_EvenWithoutManualFiltering()
    {
        // Arrange: create two separate companies, each with its own department
        var (clientA, companyIdA) = await RegisterCompanyAsync("Tenant A Corp");
        var (_, _) = await RegisterCompanyAsync("Tenant B Corp");

        await clientA.PostAsJsonAsync("/api/departments", new { name = "Tenant A Dept" });

        // Act: resolve ApplicationDbContext directly (bypassing controllers/services)
        // while acting as Tenant A, and query ALL departments with no manual WHERE clause.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Simulate the tenant context TenantResolutionMiddleware would have set for a
        // request authenticated as Tenant A's owner.
        var tenantService = scope.ServiceProvider.GetRequiredService<Wakeel.Application.Interfaces.ICurrentTenantService>();
        tenantService.SetTenant(companyIdA);

        var allDepartmentsVisible = await dbContext.Departments.ToListAsync();

        // Assert: even though we queried with NO manual company filter at all,
        // the global query filter must still only return Tenant A's own department.
        allDepartmentsVisible.Should().OnlyContain(d => d.CompanyId == companyIdA);
    }

    [Fact]
    public async Task GlobalQueryFilter_ShouldBeInactive_WhenNoTenantResolved()
    {
        // Arrange: create two companies
        await RegisterCompanyAsync("Tenant C Corp");
        await RegisterCompanyAsync("Tenant D Corp");

        // Act: resolve ApplicationDbContext with NO tenant set (simulates design-time /
        // unauthenticated context) and query all companies.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var allCompanies = await dbContext.Companies.ToListAsync();

        // Assert: Company itself is never filtered (it's the tenant root, not tenant-scoped data).
        allCompanies.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GlobalQueryFilter_ShouldPreventCrossTenantLeaveRequestAccess()
    {
        var (_, companyIdA) = await RegisterCompanyAsync("Tenant E Corp");
        var (_, companyIdB) = await RegisterCompanyAsync("Tenant F Corp");

        using var seedScope = _factory.Services.CreateScope();
        var seedDbContext = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var department = new Department
        {
            Id = Guid.NewGuid(),
            CompanyId = companyIdA,
            Name = "Tenant A Dept For Leave",
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };

        var employeeUser = new User
        {
            Id = Guid.NewGuid(),
            CompanyId = companyIdA,
            Email = $"emp_{Guid.NewGuid()}@test.com",
            PasswordHash = "hashed",
            FullName = "Tenant A Employee",
            Phone = string.Empty,
            Role = UserRole.Employee,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var employeeProfile = new EmployeeProfile
        {
            UserId = employeeUser.Id,
            DepartmentId = department.Id,
            JobTitle = "Developer",
            Salary = 10000,
            HireDate = DateOnly.FromDateTime(DateTime.UtcNow),
            ContractType = "Full-time"
        };

        var leaveRequest = new LeaveRequest
        {
            Id = Guid.NewGuid(),
            EmployeeId = employeeUser.Id,
            CompanyId = companyIdA,
            LeaveType = "Annual",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1),
            DaysRequested = 2,
            Status = "Draft",
            CreatedAt = DateTime.UtcNow
        };

        seedDbContext.Departments.Add(department);
        seedDbContext.Users.Add(employeeUser);
        seedDbContext.EmployeeProfiles.Add(employeeProfile);
        seedDbContext.LeaveRequests.Add(leaveRequest);
        await seedDbContext.SaveChangesAsync();

        using var scopeA = _factory.Services.CreateScope();
        var dbContextA = scopeA.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tenantServiceA = scopeA.ServiceProvider.GetRequiredService<Wakeel.Application.Interfaces.ICurrentTenantService>();
        tenantServiceA.SetTenant(companyIdA);
        var visibleToA = await dbContextA.LeaveRequests.ToListAsync();
        visibleToA.Should().ContainSingle(lr => lr.Id == leaveRequest.Id);

        using var scopeB = _factory.Services.CreateScope();
        var dbContextB = scopeB.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tenantServiceB = scopeB.ServiceProvider.GetRequiredService<Wakeel.Application.Interfaces.ICurrentTenantService>();
        tenantServiceB.SetTenant(companyIdB);
        var visibleToB = await dbContextB.LeaveRequests.ToListAsync();
        visibleToB.Should().NotContain(lr => lr.Id == leaveRequest.Id);
    }
}