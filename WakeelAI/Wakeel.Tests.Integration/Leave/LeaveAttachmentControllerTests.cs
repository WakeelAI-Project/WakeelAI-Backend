using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Wakeel.Application.Interfaces;
using Wakeel.Infrastructure.Persistence;
using Xunit;

namespace Wakeel.Tests.Integration.Leave;

/// <summary>
/// FIX-S2: POST /api/leave-requests/attachments used to carry [AllowAnonymous] and trust
/// raw X-User-Id/X-Company-Id headers with no internal-key check, and wrote the file to
/// disk before any identity check ran. These tests cover the corrected behaviour: only an
/// authenticated Employee can upload, and a forged/anonymous call is rejected before
/// anything is persisted.
/// </summary>
public class LeaveAttachmentControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string KnownPassword = "IntegrationTest123!";

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public LeaveAttachmentControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UploadAttachment_AuthenticatedEmployee_Returns201AndCreatesRecord()
    {
        var (employeeToken, employeeId, companyId) = await SeedAuthenticatedEmployeeAsync();

        var res = await SendUploadAsync(employeeToken, "report.pdf", "application/pdf");
        var responseBody = await res.Content.ReadAsStringAsync();

        res.StatusCode.Should().Be(HttpStatusCode.Created, $"Response body: {responseBody}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var attachment = await db.LeaveAttachments.FirstOrDefaultAsync(a => a.EmployeeId == employeeId);
        attachment.Should().NotBeNull();
        attachment!.CompanyId.Should().Be(companyId);
        attachment.Url.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task UploadAttachment_AnonymousWithForgedHeaders_Returns401AndWritesNoFile()
    {
        var (_, employeeId, companyId) = await SeedAuthenticatedEmployeeAsync();

        using var content = BuildFileContent("report.pdf", "application/pdf");
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/leave-requests/attachments")
        {
            Content = content
        };
        // Forged identity headers - InternalApiKeyMiddleware only guards /api/ai/*, this
        // route is /api/leave-requests/attachments, so these must never be trusted.
        req.Headers.Add("X-User-Id", employeeId.ToString());
        req.Headers.Add("X-Company-Id", companyId.ToString());

        var res = await _client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var attachmentCount = await db.LeaveAttachments.CountAsync(a => a.EmployeeId == employeeId);
        attachmentCount.Should().Be(0, "no file should ever be saved for an unauthenticated caller");
    }

    [Fact]
    public async Task UploadAttachment_NonEmployeeCaller_Returns403()
    {
        var (_, hrEmail, ownerToken) = await SeedCompanyAsync();
        var hrToken = await InviteAndLoginHrAsync(ownerToken, hrEmail);

        var res = await SendUploadAsync(hrToken, "report.pdf", "application/pdf");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UploadAttachment_DisallowedExtension_Returns400()
    {
        var (employeeToken, _, _) = await SeedAuthenticatedEmployeeAsync();

        var res = await SendUploadAsync(employeeToken, "malware.exe", "application/octet-stream");

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UploadAttachment_MismatchedContentType_Returns400()
    {
        var (employeeToken, _, _) = await SeedAuthenticatedEmployeeAsync();

        // Extension says PDF, declared content-type says something else entirely -
        // relying on the extension alone is exactly what this fix closes off.
        var res = await SendUploadAsync(employeeToken, "report.pdf", "text/html");

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------

    private static MultipartFormDataContent BuildFileContent(string fileName, string contentType)
    {
        var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(new MemoryStream(System.Text.Encoding.UTF8.GetBytes("dummy")));
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(streamContent, "file", fileName);
        return content;
    }

    private async Task<HttpResponseMessage> SendUploadAsync(string token, string fileName, string contentType)
    {
        using var content = BuildFileContent(fileName, contentType);
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/leave-requests/attachments")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
            Content = content
        };
        return await _client.SendAsync(req);
    }

    private async Task<(Guid CompanyId, string HrEmail, string OwnerToken)> SeedCompanyAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register-company", new
        {
            company_name = $"UploadCo {suffix}",
            tax_id = suffix,
            owner_full_name = "Upload Owner",
            owner_email = $"owner_{suffix}@integrationtest.local",
            password = KnownPassword
        });
        registerResponse.EnsureSuccessStatusCode();

        var body = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var companyId = body.GetProperty("company_id").GetGuid();
        var ownerToken = body.GetProperty("access_token").GetString()!;

        return (companyId, $"hr_{suffix}@integrationtest.local", ownerToken);
    }

    private async Task<string> InviteAndLoginHrAsync(string ownerToken, string hrEmail)
    {
        var inviteResponse = await SendAsync(HttpMethod.Post, "/api/users/invite", ownerToken, new
        {
            full_name = "Upload HR",
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
        return loginBody.GetProperty("access_token").GetString()!;
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

    private async Task<(string EmployeeToken, Guid EmployeeId, Guid CompanyId)> SeedAuthenticatedEmployeeAsync()
    {
        var (companyId, hrEmail, ownerToken) = await SeedCompanyAsync();
        var hrToken = await InviteAndLoginHrAsync(ownerToken, hrEmail);
        var departmentId = await SeedDepartmentAsync(ownerToken);

        var createResponse = await SendAsync(HttpMethod.Post, "/api/employees", hrToken, new
        {
            full_name = "Upload Employee",
            email = $"emp_{Guid.NewGuid():N}@integrationtest.local",
            job_title = "Analyst",
            department_id = departmentId,
            hire_date = "2026-01-01",
            salary = 10000,
            contract_type = "Full-Time"
        });
        createResponse.EnsureSuccessStatusCode();
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var employeeId = createBody.GetProperty("record_id").GetGuid();

        string employeeEmail;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var employeeUser = await db.Users.FirstAsync(u => u.Id == employeeId);
            employeeEmail = employeeUser.Email;
            employeeUser.PasswordHash = hasher.HashPassword(KnownPassword);
            employeeUser.MustChangePassword = false;
            await db.SaveChangesAsync();
        }

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new { email = employeeEmail, password = KnownPassword });
        loginResponse.EnsureSuccessStatusCode();
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var employeeToken = loginBody.GetProperty("access_token").GetString()!;

        return (employeeToken, employeeId, companyId);
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
