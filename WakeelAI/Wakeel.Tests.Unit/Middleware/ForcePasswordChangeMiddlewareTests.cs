using System;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Moq;
using Wakeel.API.Middleware;
using Wakeel.Application.Interfaces.Repositories;
using Wakeel.Domain.Entities;
using Xunit;

namespace Wakeel.Tests.Unit.Middleware;

public class ForcePasswordChangeMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_MustChangePasswordTrue_OnNormalEndpoint_Returns403()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/some/normal/endpoint";
        
        var userId = Guid.NewGuid();
        var claims = new[] { new Claim("user_id", userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        context.User = new ClaimsPrincipal(identity);

        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var user = new User { Id = userId, MustChangePassword = true };
        unitOfWorkMock.Setup(u => u.Users.GetByIdAsync(userId, default)).ReturnsAsync(user);

        var envMock = new Mock<IHostEnvironment>();
        envMock.SetupGet(e => e.EnvironmentName).Returns("Production");

        bool nextCalled = false;
        var middleware = new ForcePasswordChangeMiddleware(innerContext =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, envMock.Object);

        await middleware.InvokeAsync(context, unitOfWorkMock.Object);

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_MustChangePasswordTrue_OnAllowedEndpoint_CallsNext()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/account/change-password";
        
        var userId = Guid.NewGuid();
        var claims = new[] { new Claim("user_id", userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        context.User = new ClaimsPrincipal(identity);

        var unitOfWorkMock = new Mock<IUnitOfWork>();
        // Even if MustChangePassword is true, allowed paths bypass the DB check

        var envMock = new Mock<IHostEnvironment>();
        envMock.SetupGet(e => e.EnvironmentName).Returns("Production");
        
        bool nextCalled = false;
        var middleware = new ForcePasswordChangeMiddleware(innerContext =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, envMock.Object);

        await middleware.InvokeAsync(context, unitOfWorkMock.Object);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_MustChangePasswordTrue_TestingEnvironment_NoHeader_CallsNext()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/some/normal/endpoint";
        
        var userId = Guid.NewGuid();
        var claims = new[] { new Claim("user_id", userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        context.User = new ClaimsPrincipal(identity);

        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var user = new User { Id = userId, MustChangePassword = true };
        unitOfWorkMock.Setup(u => u.Users.GetByIdAsync(userId, default)).ReturnsAsync(user);

        var envMock = new Mock<IHostEnvironment>();
        envMock.SetupGet(e => e.EnvironmentName).Returns("Testing");

        bool nextCalled = false;
        var middleware = new ForcePasswordChangeMiddleware(innerContext =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, envMock.Object);

        await middleware.InvokeAsync(context, unitOfWorkMock.Object);

        nextCalled.Should().BeTrue();
    }
}
