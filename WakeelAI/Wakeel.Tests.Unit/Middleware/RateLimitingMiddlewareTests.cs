using System;
using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;
using Wakeel.API.Middleware;

namespace Wakeel.Tests.Unit.Middleware;

public class RateLimitingMiddlewareTests
{
    [Fact]
    public void GetIdentity_WithXUserIdHeaderOnly_ReturnsNullBecauseFallbackRemoved()
    {
        var method = typeof(RateLimitingMiddleware).GetMethod("GetIdentity", BindingFlags.NonPublic | BindingFlags.Static);

        var context = new DefaultHttpContext();
        context.Request.Headers["X-User-Id"] = "some-user-id";

        var result = (string?)method!.Invoke(null, new object[] { context });

        result.Should().BeNull();
    }

    [Fact]
    public void GetIdentity_WithAuthenticatedUser_ReturnsUserId()
    {
        var method = typeof(RateLimitingMiddleware).GetMethod("GetIdentity", BindingFlags.NonPublic | BindingFlags.Static);

        var context = new DefaultHttpContext();
        var userId = Guid.NewGuid().ToString();
        var claims = new[] { new Claim("user_id", userId) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        context.User = new ClaimsPrincipal(identity);

        var result = (string?)method!.Invoke(null, new object[] { context });

        result.Should().Be(userId);
    }
}
