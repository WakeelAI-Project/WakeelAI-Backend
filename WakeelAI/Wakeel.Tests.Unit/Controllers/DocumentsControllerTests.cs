using System;
using System.Collections.Generic;
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
    public async Task FinalizeDocument_ReturnsNoContentResult()
    {
        var docId = Guid.NewGuid();

        _documentServiceMock.Setup(s => s.FinalizeDocumentAsync(docId)).Returns(Task.CompletedTask);

        var result = await _controller.FinalizeDocument(docId);

        var noContentResult = Assert.IsType<NoContentResult>(result);
    }
}
