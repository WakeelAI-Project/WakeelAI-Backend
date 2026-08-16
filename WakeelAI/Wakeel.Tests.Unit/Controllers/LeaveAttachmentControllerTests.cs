using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Wakeel.API.Controllers;
using Wakeel.Application.Interfaces;
using Wakeel.Domain.Entities;
using Wakeel.Domain.Enums;
using Wakeel.Infrastructure.Persistence;
using Xunit;

namespace Wakeel.Tests.Unit.Controllers;

public class LeaveAttachmentControllerTests
{
    private readonly Mock<IFileService> _fileServiceMock;
    private readonly Mock<ICurrentTenantService> _tenantServiceMock;
    private readonly ApplicationDbContext _dbContext;
    private readonly LeaveAttachmentController _controller;

    public LeaveAttachmentControllerTests()
    {
        _fileServiceMock = new Mock<IFileService>();

        // The controller now uses ApplicationDbContext to verify
        // that the employee exists. Therefore, this test needs
        // a real test DbContext instead of null.
        _tenantServiceMock = new Mock<ICurrentTenantService>();

        // Disable tenant filtering for these unit tests.
        _tenantServiceMock
            .SetupGet(x => x.HasTenant)
            .Returns(false);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(
            options,
            _tenantServiceMock.Object);

        _controller = new LeaveAttachmentController(
            _fileServiceMock.Object,
            _dbContext,
            Mock.Of<ILogger<LeaveAttachmentController>>());

        var httpContext = new DefaultHttpContext();

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task UploadAttachment_ReturnsBadRequest_WhenNoFile()
    {
        // Act
        var result = await _controller.UploadAttachment(
            null,
            CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UploadAttachment_ReturnsCreated_WhenValidFileAndHeaders()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        // The controller checks that the employee exists
        // and belongs to the supplied company.
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

        var fileMock = new Mock<IFormFile>();

        var content = "Hello World from a fake file";

        var ms = new MemoryStream(
            System.Text.Encoding.UTF8.GetBytes(content));

        fileMock
            .Setup(f => f.OpenReadStream())
            .Returns(ms);

        fileMock
            .Setup(f => f.Length)
            .Returns(ms.Length);

        fileMock
            .Setup(f => f.FileName)
            .Returns("report.pdf");

        _controller.Request.Headers["X-User-Id"] =
            userId.ToString();

        _controller.Request.Headers["X-Company-Id"] =
            companyId.ToString();

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