using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Wakeel.API.Controllers;
using Wakeel.Application.Interfaces;
using Wakeel.Infrastructure.Persistence;
using Wakeel.Domain.Entities;
using Xunit;

namespace Wakeel.Tests.Unit.Controllers;

public class LeaveAttachmentControllerTests
{
    private readonly Mock<IFileService> _fileServiceMock;
    private readonly Mock<ApplicationDbContext> _dbContextMock; // not used for DB ops in unit tests
    private readonly LeaveAttachmentController _controller;

    public LeaveAttachmentControllerTests()
    {
        _fileServiceMock = new Mock<IFileService>();
        _dbContextMock = new Mock<ApplicationDbContext>();
        _controller = new LeaveAttachmentController(_fileServiceMock.Object, null!, Mock.Of<Microsoft.Extensions.Logging.ILogger<LeaveAttachmentController>>());

        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    [Fact]
    public async Task UploadAttachment_ReturnsBadRequest_WhenNoFile()
    {
        var result = await _controller.UploadAttachment(null, CancellationToken.None);
        var bad = Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UploadAttachment_ReturnsCreated_WhenValidFileAndHeaders()
    {
        var fileMock = new Mock<IFormFile>();
        var content = "Hello World from a fake file";
        var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        fileMock.Setup(f => f.OpenReadStream()).Returns(ms);
        fileMock.Setup(f => f.Length).Returns(ms.Length);
        fileMock.Setup(f => f.FileName).Returns("report.pdf");

        _controller.Request.Headers["X-User-Id"] = Guid.NewGuid().ToString();
        _controller.Request.Headers["X-Company-Id"] = Guid.NewGuid().ToString();

        _fileServiceMock.Setup(s => s.SaveFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/files/report.pdf");

        var result = await _controller.UploadAttachment(fileMock.Object, CancellationToken.None);

        var created = Assert.IsType<CreatedResult>(result);
    }
}
