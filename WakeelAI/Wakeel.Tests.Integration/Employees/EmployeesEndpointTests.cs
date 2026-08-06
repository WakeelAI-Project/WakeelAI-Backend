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

namespace Wakeel.Tests.Integration.Employees;

public class EmployeesEndpointTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private const string KnownPassword = "IntegrationTest123!";

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly List<Guid> _companyIdsToCleanUp = new();

    public EmployeesEndpointTests(CustomWebApplicationFactory factory)
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

    // ------------------------------------------------------------
    // Create
    // ------------------------------------------------------------

    [Fact]
    public async Task Create_GivenValidRequest_ShouldReturn201AndPersistEmployeeAndLeaveBalances()
    {
        var (hrToken, _, departmentId) = await SeedCompanyWithHrAsync();

        var response = await SendAsync(HttpMethod.Post, "/api/employees", hrToken, new
        {
            full_name = "Integration Employee",
            email = $"emp_{Guid.NewGuid():N}@integrationtest.local",
            job_title = "Analyst",
            department_id = departmentId,
            hire_date = "2026-01-01",
            salary = 12000,
            contract_type = "Full-Time"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var recordId = body.GetProperty("record_id").GetGuid();
        body.GetProperty("department_id").GetGuid().Should().Be(departmentId);
        body.GetProperty("employment_status").GetString().Should().Be("Active");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var profile = await db.EmployeeProfiles.FirstOrDefaultAsync(ep => ep.UserId == recordId);
        profile.Should().NotBeNull();
        profile!.JobTitle.Should().Be("Analyst");
        profile.DepartmentId.Should().Be(departmentId);

        var leaveBalances = await db.LeaveBalances.Where(lb => lb.EmployeeId == recordId).ToListAsync();
        leaveBalances.Should().HaveCount(3);
        leaveBalances.Should().ContainSingle(lb => lb.LeaveType == "Annual" && lb.TotalDays == 15 && lb.UsedDays == 0);
        leaveBalances.Should().ContainSingle(lb => lb.LeaveType == "Sick" && lb.TotalDays == 10 && lb.UsedDays == 0);
        leaveBalances.Should().ContainSingle(lb => lb.LeaveType == "Unpaid" && lb.TotalDays == null && lb.UsedDays == 0);
    }

    [Fact]
    public async Task Create_GivenDuplicateEmail_ShouldReturn409()
    {
        var (hrToken, _, departmentId) = await SeedCompanyWithHrAsync();
        var email = $"emp_{Guid.NewGuid():N}@integrationtest.local";
        var payload = new
        {
            full_name = "Duplicate Employee",
            email,
            job_title = "Analyst",
            department_id = departmentId,
            hire_date = "2026-01-01",
            salary = 12000,
            contract_type = "Full-Time"
        };

        var first = await SendAsync(HttpMethod.Post, "/api/employees", hrToken, payload);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await SendAsync(HttpMethod.Post, "/api/employees", hrToken, payload);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_GivenNonHrCaller_ShouldReturn403()
    {
        var (_, _, ownerToken) = await SeedCompanyAsync();

        var response = await SendAsync(HttpMethod.Post, "/api/employees", ownerToken, new
        {
            full_name = "Should Not Be Created",
            email = $"emp_{Guid.NewGuid():N}@integrationtest.local",
            job_title = "Analyst",
            department_id = Guid.NewGuid(),
            hire_date = "2026-01-01",
            salary = 12000,
            contract_type = "Full-Time"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_GivenUnknownDepartment_ShouldReturn404()
    {
        var (hrToken, _, _) = await SeedCompanyWithHrAsync();

        var response = await SendAsync(HttpMethod.Post, "/api/employees", hrToken, new
        {
            full_name = "Should Not Be Created",
            email = $"emp_{Guid.NewGuid():N}@integrationtest.local",
            job_title = "Analyst",
            department_id = Guid.NewGuid(),
            hire_date = "2026-01-01",
            salary = 12000,
            contract_type = "Full-Time"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString().Should().Be("department_not_found");
    }

    [Fact]
    public async Task Create_GivenDepartmentFromAnotherCompany_ShouldReturn404()
    {
        var (hrTokenA, _, _) = await SeedCompanyWithHrAsync();
        var (_, _, departmentIdB) = await SeedCompanyWithHrAsync();

        var response = await SendAsync(HttpMethod.Post, "/api/employees", hrTokenA, new
        {
            full_name = "Should Not Be Created",
            email = $"emp_{Guid.NewGuid():N}@integrationtest.local",
            job_title = "Analyst",
            department_id = departmentIdB,
            hire_date = "2026-01-01",
            salary = 12000,
            contract_type = "Full-Time"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString().Should().Be("department_not_found");
    }

    // ------------------------------------------------------------
    // Update
    // ------------------------------------------------------------

    [Fact]
    public async Task Update_GivenValidPartialRequest_ShouldReturn200AndPersistChanges()
    {
        var (hrToken, _, departmentId) = await SeedCompanyWithHrAsync();
        var recordId = await CreateEmployeeAsync(hrToken, departmentId);

        var response = await SendAsync(HttpMethod.Patch, $"/api/employees/{recordId}", hrToken, new
        {
            job_title = "Senior Analyst",
            salary = 15000
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("job_title").GetString().Should().Be("Senior Analyst");
        body.GetProperty("salary").GetDecimal().Should().Be(15000);
    }

    [Fact]
    public async Task Update_GivenUnknownRecordId_ShouldReturn404()
    {
        var (hrToken, _, _) = await SeedCompanyWithHrAsync();

        var response = await SendAsync(HttpMethod.Patch, $"/api/employees/{Guid.NewGuid()}", hrToken, new
        {
            job_title = "Senior Analyst"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_GivenInvalidNationalId_ShouldReturn400()
    {
        var (hrToken, _, departmentId) = await SeedCompanyWithHrAsync();
        var recordId = await CreateEmployeeAsync(hrToken, departmentId);

        var response = await SendAsync(HttpMethod.Patch, $"/api/employees/{recordId}", hrToken, new
        {
            national_id = "123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_GivenValidDepartmentId_ShouldReassignAndReturnDepartmentName()
    {
        var (hrToken, companyId, departmentId) = await SeedCompanyWithHrAsync();
        var recordId = await CreateEmployeeAsync(hrToken, departmentId);
        var newDepartmentId = await SeedDepartmentAsync(_ownerTokensByCompanyId[companyId]);

        var response = await SendAsync(HttpMethod.Patch, $"/api/employees/{recordId}", hrToken, new
        {
            department_id = newDepartmentId
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("department_id").GetGuid().Should().Be(newDepartmentId);
        body.GetProperty("department").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Update_GivenUnknownDepartment_ShouldReturn404()
    {
        var (hrToken, _, departmentId) = await SeedCompanyWithHrAsync();
        var recordId = await CreateEmployeeAsync(hrToken, departmentId);

        var response = await SendAsync(HttpMethod.Patch, $"/api/employees/{recordId}", hrToken, new
        {
            department_id = Guid.NewGuid()
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString().Should().Be("department_not_found");
    }

    [Fact]
    public async Task Update_GivenDepartmentFromAnotherCompany_ShouldReturn404()
    {
        var (hrTokenA, _, departmentIdA) = await SeedCompanyWithHrAsync();
        var recordId = await CreateEmployeeAsync(hrTokenA, departmentIdA);
        var (_, _, departmentIdB) = await SeedCompanyWithHrAsync();

        var response = await SendAsync(HttpMethod.Patch, $"/api/employees/{recordId}", hrTokenA, new
        {
            department_id = departmentIdB
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString().Should().Be("department_not_found");
    }

    // ------------------------------------------------------------
    // Delete (deactivate)
    // ------------------------------------------------------------

    [Fact]
    public async Task Delete_GivenValidRecord_ShouldReturn204AndDeactivate()
    {
        var (hrToken, _, departmentId) = await SeedCompanyWithHrAsync();
        var recordId = await CreateEmployeeAsync(hrToken, departmentId);

        var response = await SendAsync(HttpMethod.Delete, $"/api/employees/{recordId}", hrToken);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await SendAsync(HttpMethod.Get, $"/api/employees/{recordId}", hrToken);
        var body = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("employment_status").GetString().Should().Be("Inactive");
    }

    [Fact]
    public async Task Delete_GivenUnknownRecordId_ShouldReturn404()
    {
        var (hrToken, _, _) = await SeedCompanyWithHrAsync();

        var response = await SendAsync(HttpMethod.Delete, $"/api/employees/{Guid.NewGuid()}", hrToken);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_GivenNonHrCaller_ShouldReturn403()
    {
        var (hrToken, companyId, departmentId) = await SeedCompanyWithHrAsync();
        var recordId = await CreateEmployeeAsync(hrToken, departmentId);
        var ownerToken = _ownerTokensByCompanyId[companyId];

        var response = await SendAsync(HttpMethod.Delete, $"/api/employees/{recordId}", ownerToken);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ------------------------------------------------------------
    // Get
    // ------------------------------------------------------------

    [Fact]
    public async Task Get_ShouldIncludeDepartmentIdAndName()
    {
        var (hrToken, _, departmentId) = await SeedCompanyWithHrAsync();
        var recordId = await CreateEmployeeAsync(hrToken, departmentId);

        var response = await SendAsync(HttpMethod.Get, $"/api/employees/{recordId}", hrToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("department_id").GetGuid().Should().Be(departmentId);
        body.GetProperty("department").GetString().Should().NotBeNullOrEmpty();
    }

    // ------------------------------------------------------------
    // List
    // ------------------------------------------------------------

    [Fact]
    public async Task List_ShouldOnlyReturnCallersCompanyEmployees()
    {
        var (hrTokenA, _, departmentIdA) = await SeedCompanyWithHrAsync();
        var (hrTokenB, _, departmentIdB) = await SeedCompanyWithHrAsync();

        var recordIdA = await CreateEmployeeAsync(hrTokenA, departmentIdA);
        await CreateEmployeeAsync(hrTokenB, departmentIdB);

        var response = await SendAsync(HttpMethod.Get, "/api/employees", hrTokenA);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("data").EnumerateArray().ToList();
        var ids = items.Select(item => item.GetProperty("record_id").GetGuid()).ToList();

        ids.Should().Contain(recordIdA);
        ids.Should().HaveCount(1);
        items.Single().GetProperty("department").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task List_GivenStatusFilter_ShouldReturnMatchingEmployeesOnly()
    {
        var (hrToken, _, departmentId) = await SeedCompanyWithHrAsync();
        var activeId = await CreateEmployeeAsync(hrToken, departmentId);
        var inactiveId = await CreateEmployeeAsync(hrToken, departmentId);

        var deactivateResponse = await SendAsync(HttpMethod.Delete, $"/api/employees/{inactiveId}", hrToken);
        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var activeResponse = await SendAsync(HttpMethod.Get, "/api/employees?status=Active", hrToken);
        var activeBody = await activeResponse.Content.ReadFromJsonAsync<JsonElement>();
        var activeIds = activeBody.GetProperty("data").EnumerateArray().Select(i => i.GetProperty("record_id").GetGuid()).ToList();
        activeIds.Should().Contain(activeId);
        activeIds.Should().NotContain(inactiveId);

        var inactiveResponse = await SendAsync(HttpMethod.Get, "/api/employees?status=Inactive", hrToken);
        var inactiveBody = await inactiveResponse.Content.ReadFromJsonAsync<JsonElement>();
        var inactiveIds = inactiveBody.GetProperty("data").EnumerateArray().Select(i => i.GetProperty("record_id").GetGuid()).ToList();
        inactiveIds.Should().Contain(inactiveId);
        inactiveIds.Should().NotContain(activeId);
    }

    // ------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------

    private readonly Dictionary<Guid, string> _ownerTokensByCompanyId = new();

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
        _ownerTokensByCompanyId[companyId] = ownerToken;

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
