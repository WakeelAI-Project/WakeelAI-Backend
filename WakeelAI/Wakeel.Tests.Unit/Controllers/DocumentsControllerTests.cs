using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Wakeel.API.Controllers;
using Wakeel.Application.DTOs.Documents;
using Wakeel.Application.Interfaces;
using Xunit;

namespace Wakeel.Tests.Unit.Controllers;

public class DocumentsControllerTests
{
    private readonly Mock<IDocumentService> _documentServiceMock;
    private readonly DocumentsController _controller;

    public DocumentsControllerTests()
    {
        _documentServiceMock = new Mock<IDocumentService>();
        _controller = new DocumentsController(_documentServiceMock.Object);

        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    /// <summary>
    /// Mirrors the real JWT's claim shape (see JwtTokenGenerator: "user_id", "role" —
    /// never the standard ClaimTypes URIs) so these tests actually exercise the same
    /// claim-reading path production requests go through.
    /// </summary>
    private void SetUser(string role, Guid userId)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim("user_id", userId.ToString()),
            new Claim(ClaimTypes.Role, role),
        }, "TestAuth");
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task GetDocuments_ReturnsOkResult_WithData()
    {
        var docs = new List<DocumentSummary>
        {
            new DocumentSummary { Id = Guid.NewGuid(), Title = "Test", DocumentType = "TEST", Status = "Draft", CreatedAt = DateTime.UtcNow }
        };
        _documentServiceMock.Setup(s => s.GetDocumentsAsync(1, 20, null, null, null, null, null)).ReturnsAsync((docs, 1));

        var result = await _controller.GetDocuments(1, 20, null, null, null, null, null);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetDocuments_GivenEmployeeRole_FiltersToOwnDocumentsOnly()
    {
        var employeeId = Guid.NewGuid();
        SetUser("Employee", employeeId);
        _documentServiceMock
            .Setup(s => s.GetDocumentsAsync(1, 20, null, null, employeeId, null, null))
            .ReturnsAsync((new List<DocumentSummary>(), 0));

        var result = await _controller.GetDocuments(1, 20, null, null, null, null, null);

        Assert.IsType<OkObjectResult>(result);
        _documentServiceMock.Verify(
            s => s.GetDocumentsAsync(1, 20, null, null, employeeId, null, null),
            Times.Once);
    }

    [Fact]
    public async Task GetDocument_GivenEmployeeRole_OwnDocument_ReturnsOk()
    {
        var employeeId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        SetUser("Employee", employeeId);
        _documentServiceMock
            .Setup(s => s.GetDocumentByIdAsync(docId))
            .ReturnsAsync(new DocumentDetail { Id = docId, EmployeeId = employeeId, Status = "Draft" });

        var result = await _controller.GetDocument(docId);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetDocument_GivenEmployeeRole_AnotherEmployeesDocument_ReturnsNotFound()
    {
        SetUser("Employee", Guid.NewGuid());
        var docId = Guid.NewGuid();
        _documentServiceMock
            .Setup(s => s.GetDocumentByIdAsync(docId))
            .ReturnsAsync(new DocumentDetail { Id = docId, EmployeeId = Guid.NewGuid(), Status = "Draft" });

        var result = await _controller.GetDocument(docId);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task FinalizeDocument_ReturnsNoContentResult()
    {
        var docId = Guid.NewGuid();

        _documentServiceMock.Setup(s => s.FinalizeDocumentAsync(docId)).Returns(Task.CompletedTask);

        var result = await _controller.FinalizeDocument(docId);

        var noContentResult = Assert.IsType<NoContentResult>(result);
    }
}
