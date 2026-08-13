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
using Wakeel.Infrastructure.Persistence;
using Xunit;

namespace Wakeel.Tests.Integration.InternalContext;

public class InternalAiContextControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private const string ValidPsk = "test-internal-key";

    public InternalAiContextControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(Guid EmployeeId, Guid CompanyId)> SeedEmployeeAsync()
    {
        var client = _factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var ownerEmail = $"owner_{suffix}@test.local";
        
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register-company", new
        {
            company_name    = $"Test Company {suffix}",
            tax_id          = suffix,
            owner_full_name = "Test Owner",
            owner_email     = ownerEmail,
            password        = "TestPassword123!"
        });
        registerResponse.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var owner = await db.Users.Include(u => u.Company).FirstOrDefaultAsync(u => u.Email == ownerEmail);
        
        var employee = new Wakeel.Domain.Entities.User
        {
            Id = Guid.NewGuid(),
            CompanyId = owner!.CompanyId,
            FullName = "Test Employee",
            Email = $"emp_{suffix}@test.local",
            IsActive = true,
            Role = Wakeel.Domain.Enums.UserRole.Employee,
            PasswordHash = "hash"
        };
        db.Users.Add(employee);
        
        var dept = new Wakeel.Domain.Entities.Department
        {
            Id = Guid.NewGuid(),
            CompanyId = owner.CompanyId,
            Name = "Engineering"
        };
        db.Departments.Add(dept);
        
        var profile = new Wakeel.Domain.Entities.EmployeeProfile
        {
            UserId = employee.Id,
            DepartmentId = dept.Id,
            JobTitle = "Dev",
            HireDate = new DateOnly(2020, 1, 1),
            ContractType = "FullTime"
        };
        db.EmployeeProfiles.Add(profile);
        
        var balance = new Wakeel.Domain.Entities.LeaveBalance
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            LeaveType = "Annual",
            Year = DateTime.UtcNow.Year,
            TotalDays = 21,
            UsedDays = 5
        };
        db.LeaveBalances.Add(balance);
        await db.SaveChangesAsync();

        return (employee.Id, owner.CompanyId);
    }

    private HttpRequestMessage BuildInternalRequest(string url, string? psk, string? userId, string? companyId, string? role)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (psk != null) request.Headers.Add("X-Internal-API-Key", psk);
        if (userId != null) request.Headers.Add("X-User-Id", userId);
        if (companyId != null) request.Headers.Add("X-Company-Id", companyId);
        if (role != null) request.Headers.Add("X-Role", role);
        return request;
    }

    [Fact]
    public async Task GetEmployeeContext_MissingApiKey_Returns401()
    {
        var client = _factory.CreateClient();
        var request = BuildInternalRequest("/api/ai/employee-context", null, Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), "Employee");
        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetEmployeeContext_MissingIdentityHeaders_Returns400()
    {
        var client = _factory.CreateClient();
        var request = BuildInternalRequest("/api/ai/employee-context", ValidPsk, null, Guid.NewGuid().ToString(), "Employee");
        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("MISSING_IDENTITY_HEADERS");
    }

    [Fact]
    public async Task GetEmployeeContext_ValidHeaders_ReturnsCamelCaseJson()
    {
        var (employeeId, companyId) = await SeedEmployeeAsync();
        var client = _factory.CreateClient();
        var request = BuildInternalRequest("/api/ai/employee-context", ValidPsk, employeeId.ToString(), companyId.ToString(), "Employee");
        
        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        
        doc.RootElement.TryGetProperty("userId", out _).Should().BeTrue("Serialization must be camelCase");
        doc.RootElement.GetProperty("userId").GetString().Should().Be(employeeId.ToString());
        doc.RootElement.GetProperty("companyId").GetString().Should().Be(companyId.ToString());
        doc.RootElement.GetProperty("fullName").GetString().Should().Be("Test Employee");
        doc.RootElement.GetProperty("employmentStatus").GetString().Should().Be("Active");
        
        var balance = doc.RootElement.GetProperty("leaveBalance");
        balance.GetProperty("annual").GetInt32().Should().Be(21);
        balance.GetProperty("used").GetInt32().Should().Be(5);
        balance.GetProperty("remaining").GetInt32().Should().Be(16);
    }

    [Fact]
    public async Task GetEmployeeContext_CrossTenant_Returns404()
    {
        var (employeeId, _) = await SeedEmployeeAsync();
        var (_, otherCompanyId) = await SeedEmployeeAsync();
        
        var client = _factory.CreateClient();
        var request = BuildInternalRequest("/api/ai/employee-context", ValidPsk, employeeId.ToString(), otherCompanyId.ToString(), "Employee");
        
        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetCompanyContext_ValidHeaders_ReturnsCamelCaseJson()
    {
        var (_, companyId) = await SeedEmployeeAsync();
        var client = _factory.CreateClient();
        var request = BuildInternalRequest("/api/ai/company-context", ValidPsk, Guid.NewGuid().ToString(), companyId.ToString(), "Employee");
        
        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        
        doc.RootElement.TryGetProperty("companyId", out _).Should().BeTrue("Serialization must be camelCase");
        doc.RootElement.GetProperty("companyId").GetString().Should().Be(companyId.ToString());
        doc.RootElement.TryGetProperty("companyName", out _).Should().BeTrue();
    }
}
