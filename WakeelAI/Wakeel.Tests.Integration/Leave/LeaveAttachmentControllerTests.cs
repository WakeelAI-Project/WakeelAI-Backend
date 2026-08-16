using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Wakeel.Infrastructure.Persistence;
using Xunit;

namespace Wakeel.Tests.Integration.Leave;

public class LeaveAttachmentControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public LeaveAttachmentControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UploadAttachment_PublicMode_Returns201AndCreatesRecord()
    {
        var client = _factory.CreateClient();

        // Create company and employee directly
        Guid companyId;
        Guid employeeId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var company = new Wakeel.Domain.Entities.Company { Id = Guid.NewGuid(), Name = "UploadCo", TaxId = "upload", RegisteredAt = DateTime.UtcNow, IsActive = true };
            db.Companies.Add(company);
            var user = new Wakeel.Domain.Entities.User { Id = Guid.NewGuid(), CompanyId = company.Id, Email = $"emp_{Guid.NewGuid():N}@test.local", FullName = "Upload Emp", Role = Wakeel.Domain.Enums.UserRole.Employee, IsActive = true, PasswordHash = "hashed" };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            companyId = company.Id;
            employeeId = user.Id;
        }

        using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("dummy"));
        var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(ms);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        content.Add(streamContent, "file", "report.pdf");

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/leave-requests/attachments");
        req.Content = content;
        req.Headers.Add("X-User-Id", employeeId.ToString());
        req.Headers.Add("X-Company-Id", companyId.ToString());

        var res = await client.SendAsync(req);
        res.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);

        // verify DB record
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var attach = await db.LeaveAttachments.FirstOrDefaultAsync(a => a.EmployeeId == employeeId);
            attach.Should().NotBeNull();
            attach!.CompanyId.Should().Be(companyId);
        }
    }
}
