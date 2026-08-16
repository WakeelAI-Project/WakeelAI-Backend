using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Wakeel.API.Controllers;
using Wakeel.Application.DTOs.Templates;
using Wakeel.Application.Interfaces;
using Xunit;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Wakeel.Tests.Unit.Controllers;

public class TemplatesControllerTests
{
    private readonly Mock<ITemplateService> _templateServiceMock;
    private readonly TemplatesController _controller;

    public TemplatesControllerTests()
    {
        _templateServiceMock = new Mock<ITemplateService>();

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AiNode:InternalApiKey"] = "test-internal-key"
            })
            .Build();

        _controller = new TemplatesController(
            _templateServiceMock.Object,
            httpClientFactoryMock.Object,
            configuration,
            NullLogger<TemplatesController>.Instance);

        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }


    [Fact]
    public async Task GetTemplates_ReturnsOkResult_WithData()
    {
        var templates = new List<TemplateDto>
        {
            new TemplateDto { Id = Guid.NewGuid(), Name = "Test", DocumentType = "TEST", IsActive = true }
        };
        _templateServiceMock.Setup(s => s.GetTemplatesAsync(1, 20, null)).ReturnsAsync((templates, 1));

        var result = await _controller.GetTemplates(null, 1, 20);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task CreateTemplate_ReturnsCreatedResult_WithTemplate()
    {
        var dto = new CreateTemplateRequest { Name = "Test", DocumentType = "TEST", ContentTemplate = "test" };
        var created = new TemplateDto { Id = Guid.NewGuid(), Name = "Test", DocumentType = "TEST", IsActive = true };

        _templateServiceMock.Setup(s => s.CreateTemplateAsync(dto)).ReturnsAsync(created);

        var result = await _controller.CreateTemplate(dto);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(created.Id, ((TemplateDto)createdResult.Value!).Id);
    }
}
