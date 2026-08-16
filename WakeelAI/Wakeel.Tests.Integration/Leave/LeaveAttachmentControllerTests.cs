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

        Guid companyId;
        Guid employeeId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // 1. Create company
            var company = new Wakeel.Domain.Entities.Company
            {
                Id = Guid.NewGuid(),
                Name = "UploadCo",
                TaxId = $"upload_{Guid.NewGuid():N}",
                RegisteredAt = DateTime.UtcNow,
                IsActive = true
            };

            db.Companies.Add(company);

            // 2. Create department
            var department = new Wakeel.Domain.Entities.Department
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                Name = "Test Department",
                Description = "Integration test department",
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            };

            db.Departments.Add(department);

            // 3. Create employee user
            var user = new Wakeel.Domain.Entities.User
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                Email = $"emp_{Guid.NewGuid():N}@test.local",
                FullName = "Upload Emp",
                Role = Wakeel.Domain.Enums.UserRole.Employee,
                IsActive = true,
                PasswordHash = "hashed",
                IsEmailConfirmed = true,
                MustChangePassword = false,
                CreatedAt = DateTime.UtcNow
            };

            db.Users.Add(user);

            // 4. Create EmployeeProfile
            // LeaveAttachment.EmployeeId references EmployeeProfile.UserId.
            var employeeProfile = new Wakeel.Domain.Entities.EmployeeProfile
            {
                UserId = user.Id,
                DepartmentId = department.Id,
                JobTitle = "Test Employee",
                Salary = 10000m,
                HireDate = DateOnly.FromDateTime(DateTime.UtcNow),
                ContractType = "FullTime"
            };

            db.EmployeeProfiles.Add(employeeProfile);

            await db.SaveChangesAsync();

            companyId = company.Id;
            employeeId = user.Id;
        }

        // 5. Create fake PDF upload
        using var ms = new MemoryStream(
            System.Text.Encoding.UTF8.GetBytes("dummy"));

        using var content = new MultipartFormDataContent();

        var streamContent = new StreamContent(ms);

        streamContent.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");

        content.Add(
            streamContent,
            "file",
            "report.pdf");

        // 6. Send request
        using var req = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/leave-requests/attachments");

        req.Content = content;

        req.Headers.Add(
            "X-User-Id",
            employeeId.ToString());

        req.Headers.Add(
            "X-Company-Id",
            companyId.ToString());

        var res = await client.SendAsync(req);

        var responseBody = await res.Content.ReadAsStringAsync();

        // 7. Verify HTTP response
        res.StatusCode.Should().Be(
            System.Net.HttpStatusCode.Created,
            $"Response body: {responseBody}");

        // 8. Verify database record
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var attachment = await db.LeaveAttachments
                .FirstOrDefaultAsync(a => a.EmployeeId == employeeId);

            attachment.Should().NotBeNull();

            attachment!.CompanyId.Should().Be(companyId);

            attachment.Url.Should().NotBeNullOrEmpty();
        }
    }
}
