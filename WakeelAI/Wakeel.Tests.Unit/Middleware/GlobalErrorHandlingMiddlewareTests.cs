using System;
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;
using Wakeel.API.Middleware;

namespace Wakeel.Tests.Unit.Middleware;

public class GlobalErrorHandlingMiddlewareTests
{
    [Fact]
    public void MapException_UnmappedInvalidOperationException_ReturnsInternalErrorWithGenericMessage()
    {
        var method = typeof(GlobalErrorHandlingMiddleware).GetMethod("MapException", BindingFlags.NonPublic | BindingFlags.Static);

        var ex = new InvalidOperationException("some_unmapped_error_code");

        var result = (ValueTuple<int, string, string>)method!.Invoke(null, new object[] { ex })!;

        result.Item1.Should().Be(StatusCodes.Status500InternalServerError);
        result.Item2.Should().Be("internal_error");
        result.Item3.Should().Be("An unexpected error occurred.");
    }
}
