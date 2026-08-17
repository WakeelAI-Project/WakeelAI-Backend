using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Wakeel.Application.DTOs.LeaveRequests;
using Wakeel.Application.Interfaces;
using Wakeel.Application.Interfaces.Repositories;
using Wakeel.Application.Interfaces.Services;
using Wakeel.Application.Services;
using Wakeel.Domain.Entities;
using Xunit;

namespace Wakeel.Tests.Unit.Services;

public class LeaveRequestServiceTests
{
    [Fact]
    public async Task CreateDraftAsync_OverlappingRequest_ThrowsInvalidOperationException()
    {
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var fileServiceMock = new Mock<IFileService>();
        var emailSenderMock = new Mock<IEmailSender>();
        var loggerMock = new Mock<ILogger<LeaveRequestService>>();

        var employeeId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var existingRequest = new LeaveRequest
        {
            Id = Guid.NewGuid(),
            EmployeeId = employeeId,
            CompanyId = companyId,
            Status = "Approved",
            StartDate = today.AddDays(1),
            EndDate = today.AddDays(5)
        };

        unitOfWorkMock.Setup(u => u.LeaveRequests.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<LeaveRequest, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRequest);

        var service = new LeaveRequestService(unitOfWorkMock.Object, fileServiceMock.Object, emailSenderMock.Object, loggerMock.Object);

        var dto = new CreateLeaveRequestDto
        {
            LeaveType = "Annual",
            StartDate = today.AddDays(3).ToString("yyyy-MM-dd"), // Overlaps
            EndDate = today.AddDays(7).ToString("yyyy-MM-dd"),
            Reason = "Vacation"
        };

        var act = async () => await service.CreateDraftAsync(employeeId, companyId, dto, null);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("overlapping_leave_request");
    }

    [Fact]
    public async Task CreateDraftAsync_ExceedsReservedDays_ThrowsInvalidOperationException()
    {
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var fileServiceMock = new Mock<IFileService>();
        var emailSenderMock = new Mock<IEmailSender>();
        var loggerMock = new Mock<ILogger<LeaveRequestService>>();

        var employeeId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        unitOfWorkMock.Setup(u => u.LeaveRequests.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<LeaveRequest, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveRequest?)null);

        var balance = new LeaveBalance
        {
            EmployeeId = employeeId,
            LeaveType = "Annual",
            Year = today.Year,
            TotalDays = 10,
            UsedDays = 2 // 8 days remaining natively
        };

        unitOfWorkMock.Setup(u => u.LeaveBalances.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<LeaveBalance, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(balance);

        var activeRequests = new List<LeaveRequest>
        {
            new LeaveRequest { DaysRequested = 5 } // 5 days reserved, 3 remaining
        };

        unitOfWorkMock.Setup(u => u.LeaveRequests.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<LeaveRequest, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeRequests);

        var service = new LeaveRequestService(unitOfWorkMock.Object, fileServiceMock.Object, emailSenderMock.Object, loggerMock.Object);

        var dto = new CreateLeaveRequestDto
        {
            LeaveType = "Annual",
            StartDate = today.AddDays(10).ToString("yyyy-MM-dd"),
            EndDate = today.AddDays(13).ToString("yyyy-MM-dd"), // 4 days requested, but only 3 remaining!
            Reason = "Vacation"
        };

        var act = async () => await service.CreateDraftAsync(employeeId, companyId, dto, null);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("insufficient_leave_balance");
    }
}
