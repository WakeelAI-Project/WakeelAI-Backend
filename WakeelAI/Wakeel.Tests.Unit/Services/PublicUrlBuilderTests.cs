using System;
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Moq;
using Wakeel.Infrastructure.Services;
using Xunit;

namespace Wakeel.Tests.Unit.Services;

public class PublicUrlBuilderTests
{
    [Fact]
    public void ToAbsoluteUrl_WithConfiguredBaseUrl_ReturnsAbsoluteUrl()
    {
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["App:PublicBaseUrl"]).Returns("https://api.wakeelai.example.com");
        
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();

        var builder = new PublicUrlBuilder(configMock.Object, httpContextAccessorMock.Object);
        var result = builder.ToAbsoluteUrl("/uploads/x.pdf");

        result.Should().Be("https://api.wakeelai.example.com/uploads/x.pdf");
    }

    [Fact]
    public void ToAbsoluteUrl_AlreadyAbsoluteUrl_ReturnsAsIs()
    {
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["App:PublicBaseUrl"]).Returns("https://api.wakeelai.example.com");
        
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();

        var builder = new PublicUrlBuilder(configMock.Object, httpContextAccessorMock.Object);
        var result = builder.ToAbsoluteUrl("https://external.com/x.pdf");

        result.Should().Be("https://external.com/x.pdf");
    }

    [Fact]
    public void ToAbsoluteUrl_NoConfigActiveRequest_UsesRequestHost()
    {
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["App:PublicBaseUrl"]).Returns(string.Empty);
        
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost", 5000);

        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        httpContextAccessorMock.Setup(x => x.HttpContext).Returns(context);

        var builder = new PublicUrlBuilder(configMock.Object, httpContextAccessorMock.Object);
        var result = builder.ToAbsoluteUrl("/uploads/x.pdf");

        result.Should().Be("http://localhost:5000/uploads/x.pdf");
    }
}

public class QuestPdfGeneratorServiceTests
{
    [Theory]
    [InlineData("مرحبا", true)]
    [InlineData("Hello", false)]
    [InlineData("Hello مرحبا", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ContainsArabic_ReturnsExpectedResult(string text, bool expected)
    {
        var method = typeof(QuestPdfGeneratorService).GetMethod("ContainsArabic", BindingFlags.NonPublic | BindingFlags.Static);
        var result = (bool)method!.Invoke(null, new object[] { text });

        result.Should().Be(expected);
    }
}
