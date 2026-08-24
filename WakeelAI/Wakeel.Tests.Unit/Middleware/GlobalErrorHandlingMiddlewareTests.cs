using System;
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;
using Wakeel.API.Middleware;
using Wakeel.Application.Exceptions;

namespace Wakeel.Tests.Unit.Middleware;

public class GlobalErrorHandlingMiddlewareTests
{
    private static (int Status, string Error, string Message) MapException(Exception ex)
    {
        var method = typeof(GlobalErrorHandlingMiddleware).GetMethod("MapException", BindingFlags.NonPublic | BindingFlags.Static);
        return (ValueTuple<int, string, string>)method!.Invoke(null, new object[] { ex })!;
    }

    [Fact]
    public void MapException_UnmappedInvalidOperationException_ReturnsInternalErrorWithGenericMessage()
    {
        var ex = new InvalidOperationException("some_unmapped_error_code");

        var result = MapException(ex);

        result.Status.Should().Be(StatusCodes.Status500InternalServerError);
        result.Error.Should().Be("internal_error");
        result.Message.Should().Be("An unexpected error occurred.");
    }

    [Fact]
    public void MapException_OverlappingLeaveRequestException_NamesTheConflictingTypeAndDates()
    {
        // FIX-07: the message must name the conflicting request's type and dates instead
        // of a generic "you already have an overlapping request" - while the error code
        // (which both clients pattern-match on) stays exactly "overlapping_leave_request".
        var ex = new OverlappingLeaveRequestException("Annual", new DateOnly(2026, 3, 3), new DateOnly(2026, 3, 7), "Approved");

        var result = MapException(ex);

        result.Status.Should().Be(StatusCodes.Status409Conflict);
        result.Error.Should().Be("overlapping_leave_request");
        result.Message.Should().Contain("Annual");
        result.Message.Should().Contain("2026-03-03");
        result.Message.Should().Contain("2026-03-07");
    }

    [Fact]
    public void MapException_OverlappingLeaveRequestException_DistinguishesApprovedFromPending()
    {
        var approved = MapException(new OverlappingLeaveRequestException("Annual", new DateOnly(2026, 3, 3), new DateOnly(2026, 3, 7), "Approved"));
        var pending = MapException(new OverlappingLeaveRequestException("Sick", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 2), "Pending"));

        approved.Message.Should().Contain("approved");
        pending.Message.Should().Contain("pending");
    }
}
