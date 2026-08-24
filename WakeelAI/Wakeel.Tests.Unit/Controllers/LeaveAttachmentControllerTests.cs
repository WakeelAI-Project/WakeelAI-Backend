using System;
using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Wakeel.API.Controllers;
using Wakeel.Application.Interfaces;
using Wakeel.Domain.Entities;
using Wakeel.Domain.Enums;
using Wakeel.Infrastructure.Persistence;
using Wakeel.Infrastructure.Security;
using Xunit;

namespace Wakeel.Tests.Unit.Controllers;

/// <summary>
/// FIX-S2: identity for this endpoint now comes exclusively from verified JWT claims
/// (via [Authorize(Roles = "Employee")]), never from client-supplied X-User-Id/X-Company-Id
/// headers. These tests build the ClaimsPrincipal the way the real JWT middleware would.
/// </summary>
public class LeaveAttachmentControllerTests
{
    private readonly Mock<IFileService> _fileServiceMock;
    private readonly Mock<ICurrentTenantService> _tenantServiceMock;
    private readonly ApplicationDbContext _dbContext;
    private readonly LeaveAttachmentController _controller;

    public LeaveAttachmentControllerTests()
    {
        _fileServiceMock = new Mock<IFileService>();

        // The controller uses ApplicationDbContext to verify that the employee exists.
        _tenantServiceMock = new Mock<ICurrentTenantService>();

        // Disable tenant filtering for these unit tests.
        _tenantServiceMock
            .SetupGet(x => x.HasTenant)
            .Returns(false);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var encryptionConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new[] { new KeyValuePair<string, string?>("Encryption:Key", Convert.ToBase64String(new byte[32])) })
            .Build();

        _dbContext = new ApplicationDbContext(
            options,
            _tenantServiceMock.Object,
            new FieldEncryptionService(encryptionConfig));

        _controller = new LeaveAttachmentController(
            _fileServiceMock.Object,
            _dbContext,
            Mock.Of<ILogger<LeaveAttachmentController>>());

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    private void AuthenticateAs(Guid userId, Guid companyId)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim("user_id", userId.ToString()),
            new Claim("company_id", companyId.ToString()),
            new Claim("role", "Employee")
        }, "TestAuth");

        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(identity);
    }

    private async Task<Guid> SeedEmployeeAsync()
    {
        var userId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        var user = new User
        {
            Id = userId,
            CompanyId = companyId,
            Email = "employee@test.com",
            PasswordHash = "test-password-hash",
            FullName = "Test Employee",
            Phone = "01000000000",
            Role = UserRole.Employee,
            IsActive = true,
            IsEmailConfirmed = true,
            MustChangePassword = false,
            ActivationToken = string.Empty,
            ActivationTokenExpiry = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        AuthenticateAs(userId, companyId);
        return userId;
    }

    private static Mock<IFormFile> BuildFileMock(string fileName, string contentType, string content = "Hello World from a fake file")
    {
        var fileMock = new Mock<IFormFile>();
        var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));

        fileMock.Setup(f => f.OpenReadStream()).Returns(ms);
        fileMock.Setup(f => f.Length).Returns(ms.Length);
        fileMock.Setup(f => f.FileName).Returns(fileName);
        fileMock.Setup(f => f.ContentType).Returns(contentType);

        return fileMock;
    }

    [Fact]
    public async Task UploadAttachment_ReturnsBadRequest_WhenCallerHasNoClaims()
    {
        // No AuthenticateAs() call - simulates a request that somehow reached the action
        // without a resolvable identity (defense in depth behind [Authorize]).
        var result = await _controller.UploadAttachment(null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UploadAttachment_ReturnsBadRequest_WhenNoFile()
    {
        await SeedEmployeeAsync();

        var result = await _controller.UploadAttachment(null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UploadAttachment_ReturnsBadRequest_WhenContentTypeDoesNotMatchExtension()
    {
        await SeedEmployeeAsync();
        var fileMock = BuildFileMock("report.pdf", "text/html");

        var result = await _controller.UploadAttachment(fileMock.Object, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        _fileServiceMock.Verify(
            s => s.SaveFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "nothing should be written to disk when validation fails");
    }

    [Fact]
    public async Task UploadAttachment_ReturnsCreated_WhenValidFileAndAuthenticatedEmployee()
    {
        // Arrange
        await SeedEmployeeAsync();
        var fileMock = BuildFileMock("report.pdf", "application/pdf");

        _fileServiceMock
            .Setup(s => s.SaveFileAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("/files/report.pdf");

        // Act
        var result = await _controller.UploadAttachment(
            fileMock.Object,
            CancellationToken.None);

        // Assert
        var created = Assert.IsType<CreatedResult>(result);

        Assert.NotNull(created.Value);
    }
}
