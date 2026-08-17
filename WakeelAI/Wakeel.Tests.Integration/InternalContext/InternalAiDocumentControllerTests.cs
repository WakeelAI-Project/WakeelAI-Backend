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
using Wakeel.Application.DTOs.AiIntegrations;
using Wakeel.Infrastructure.Persistence;
using Xunit;

namespace Wakeel.Tests.Integration.InternalContext;

public class InternalAiDocumentControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private const string ValidPsk = "test-internal-key";
    private const string TestInternalKey = "test-internal-key";

    public InternalAiDocumentControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, Guid companyId, object? body = null)
    {
        var request = new HttpRequestMessage(method, url)
        {
            Headers =
            {
                { "X-Internal-API-Key", TestInternalKey },
                { "X-Company-Id", companyId.ToString() },
                { "X-Role", "HR_Manager" }
            }
        };
        if (body != null)
            request.Content = JsonContent.Create(body);
        return request;
    }

    private HttpRequestMessage BuildInternalRequest(string url, string? psk, string? userId, string? companyId, string? role, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        if (psk != null) request.Headers.Add("X-Internal-API-Key", psk);
        if (userId != null) request.Headers.Add("X-User-Id", userId);
        if (companyId != null) request.Headers.Add("X-Company-Id", companyId);
        if (role != null) request.Headers.Add("X-Role", role);
        request.Content = JsonContent.Create(body);
        return request;
    }

    [Fact]
    public async Task SaveGeneratedDocument_ValidPayload_ShouldDeserializeSnakeCasePropertiesAndSave()
    {
        // 1. Seed employee and company
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
        await db.SaveChangesAsync();

        // 2. Build request with snake_case
        var payload = new
        {
            document_type = "Certificate",
            title = "Test Cert",
            content_html = "<html>...</html>",
            employee_id = employee.Id.ToString(),
            metadata = new { reason = "Test" }
        };

        var request = BuildInternalRequest("/api/ai/documents/save", ValidPsk, Guid.NewGuid().ToString(), owner.CompanyId.ToString(), "HR_Manager", payload);
        
        // 3. Send request
        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, content);

        // 4. Verify in DB
        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var doc = await verifyDb.GeneratedDocuments.FirstOrDefaultAsync(d => d.Title == "Test Cert");
        
        doc.Should().NotBeNull();
        doc!.DocumentType.Should().Be("Certificate");
        doc.Content.Should().Be("<html>...</html>");
        doc.EmployeeId.Should().Be(employee.Id);
        doc.CompanyId.Should().Be(owner.CompanyId);
    }

    [Fact]
    public async Task SaveGeneratedDocument_OmittedEmployeeId_ShouldSaveAsUnassignedDraft()
    {
        // 1. Seed company
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

        // 2. Build request with omitted employee_id
        var payload = new
        {
            document_type = "Contract",
            title = "New Employee Draft",
            content_html = "<html>Draft Content</html>",
            metadata = new { reason = "New Hire" }
        };

        var request = BuildInternalRequest("/api/ai/documents/save", ValidPsk, Guid.NewGuid().ToString(), owner!.CompanyId.ToString(), "HR_Manager", payload);
        
        // 3. Send request
        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, content);

        // 4. Verify in DB
        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var doc = await verifyDb.GeneratedDocuments.FirstOrDefaultAsync(d => d.Title == "New Employee Draft");
        
        doc.Should().NotBeNull();
        doc!.DocumentType.Should().Be("Contract");
        doc.Content.Should().Be("<html>Draft Content</html>");
        doc.EmployeeId.Should().BeNull();
        doc.CompanyId.Should().Be(owner.CompanyId);
    }
}
