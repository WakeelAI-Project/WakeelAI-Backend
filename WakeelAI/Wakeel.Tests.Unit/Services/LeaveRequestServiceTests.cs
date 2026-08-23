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
        var auditLogServiceMock = new Mock<IAuditLogService>();

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

        var leaveBalanceProvisioningService = new LeaveBalanceProvisioningService(unitOfWorkMock.Object);
        var service = new LeaveRequestService(unitOfWorkMock.Object, fileServiceMock.Object, emailSenderMock.Object, loggerMock.Object, auditLogServiceMock.Object, leaveBalanceProvisioningService);

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
    public async Task CreateDraftAsync_OverlapCheck_NeverConsidersDraftRequestsBlocking()
    {
        // FIX-07: an abandoned Draft used to block the same dates forever. Only Pending
        // and Approved requests may block an overlap now.
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var fileServiceMock = new Mock<IFileService>();
        var emailSenderMock = new Mock<IEmailSender>();
        var loggerMock = new Mock<ILogger<LeaveRequestService>>();
        var auditLogServiceMock = new Mock<IAuditLogService>();

        System.Linq.Expressions.Expression<Func<LeaveRequest, bool>>? capturedPredicate = null;
        unitOfWorkMock
            .Setup(u => u.LeaveRequests.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<LeaveRequest, bool>>>(), It.IsAny<CancellationToken>()))
            .Callback<System.Linq.Expressions.Expression<Func<LeaveRequest, bool>>, CancellationToken>((predicate, _) => capturedPredicate = predicate)
            .ReturnsAsync((LeaveRequest?)null);
        unitOfWorkMock
            .Setup(u => u.LeaveBalances.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<LeaveBalance, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveBalance?)null);
        unitOfWorkMock
            .Setup(u => u.LeaveEntitlements.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<LeaveEntitlement, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeaveEntitlement { LeaveType = "Unpaid", DefaultDays = null });

        var employeeId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        unitOfWorkMock.Setup(u => u.EmployeeProfiles.GetByUserIdAsync(employeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmployeeProfile { UserId = employeeId, HireDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-2)) });

        var leaveBalanceProvisioningService = new LeaveBalanceProvisioningService(unitOfWorkMock.Object);
        var service = new LeaveRequestService(unitOfWorkMock.Object, fileServiceMock.Object, emailSenderMock.Object, loggerMock.Object, auditLogServiceMock.Object, leaveBalanceProvisioningService);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dto = new CreateLeaveRequestDto
        {
            LeaveType = "Unpaid",
            StartDate = today.AddDays(3).ToString("yyyy-MM-dd"),
            EndDate = today.AddDays(4).ToString("yyyy-MM-dd"),
            Reason = "Personal"
        };

        // Should succeed - no Pending/Approved requests exist, only the mocked "any
        // predicate returns null" stands in for "no blocking request found".
        await service.CreateDraftAsync(employeeId, companyId, dto, null);

        capturedPredicate.Should().NotBeNull();
        var compiled = capturedPredicate!.Compile();
        var draftOnDates = new LeaveRequest { EmployeeId = employeeId, Status = "Draft", StartDate = today.AddDays(3), EndDate = today.AddDays(4) };
        compiled(draftOnDates).Should().BeFalse("a Draft must never be treated as blocking an overlap");
    }

    [Fact]
    public async Task CreateDraftAsync_ExceedsReservedDays_ThrowsInvalidOperationException()
    {
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var fileServiceMock = new Mock<IFileService>();
        var emailSenderMock = new Mock<IEmailSender>();
        var loggerMock = new Mock<ILogger<LeaveRequestService>>();
        var auditLogServiceMock = new Mock<IAuditLogService>();

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

        var leaveBalanceProvisioningService = new LeaveBalanceProvisioningService(unitOfWorkMock.Object);
        var service = new LeaveRequestService(unitOfWorkMock.Object, fileServiceMock.Object, emailSenderMock.Object, loggerMock.Object, auditLogServiceMock.Object, leaveBalanceProvisioningService);

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

    [Fact]
    public async Task CreateDraftAsync_UnpaidLeaveWithNoExistingBalanceRow_SucceedsBecauseUnpaidIsUncapped()
    {
        // FIX-01: a missing balance row used to throw insufficient_leave_balance
        // unconditionally. It must now provision one instead - and since Unpaid is
        // uncapped (TotalDays = null), any length of Unpaid request should succeed.
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var fileServiceMock = new Mock<IFileService>();
        var emailSenderMock = new Mock<IEmailSender>();
        var loggerMock = new Mock<ILogger<LeaveRequestService>>();
        var auditLogServiceMock = new Mock<IAuditLogService>();

        var employeeId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        unitOfWorkMock.Setup(u => u.LeaveRequests.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<LeaveRequest, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveRequest?)null);
        unitOfWorkMock.Setup(u => u.LeaveBalances.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<LeaveBalance, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveBalance?)null);
        unitOfWorkMock.Setup(u => u.LeaveEntitlements.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<LeaveEntitlement, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeaveEntitlement { LeaveType = "Unpaid", DefaultDays = null });
        unitOfWorkMock.Setup(u => u.EmployeeProfiles.GetByUserIdAsync(employeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmployeeProfile { UserId = employeeId, HireDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-2)) });

        var leaveBalanceProvisioningService = new LeaveBalanceProvisioningService(unitOfWorkMock.Object);
        var service = new LeaveRequestService(unitOfWorkMock.Object, fileServiceMock.Object, emailSenderMock.Object, loggerMock.Object, auditLogServiceMock.Object, leaveBalanceProvisioningService);

        var dto = new CreateLeaveRequestDto
        {
            LeaveType = "Unpaid",
            StartDate = today.AddDays(10).ToString("yyyy-MM-dd"),
            EndDate = today.AddDays(40).ToString("yyyy-MM-dd"), // 31 days - would fail any finite cap
            Reason = "Personal matters"
        };

        var result = await service.CreateDraftAsync(employeeId, companyId, dto, null);

        result.LeaveType.Should().Be("Unpaid");
        result.Status.Should().Be("Draft");
        unitOfWorkMock.Verify(u => u.LeaveBalances.AddAsync(It.Is<LeaveBalance>(lb => lb.LeaveType == "Unpaid" && lb.TotalDays == null), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReviewLeaveRequestAsync_UpdatesAuditLogs()
    {
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var fileServiceMock = new Mock<IFileService>();
        var emailSenderMock = new Mock<IEmailSender>();
        var loggerMock = new Mock<ILogger<LeaveRequestService>>();
        var auditLogServiceMock = new Mock<IAuditLogService>();

        var employeeId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var hrUserId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var request = new LeaveRequest
        {
            Id = requestId,
            CompanyId = companyId,
            EmployeeId = employeeId,
            Status = "Pending",
            LeaveType = "Annual",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            DaysRequested = 2
        };

        unitOfWorkMock.Setup(u => u.LeaveRequests.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<LeaveRequest, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var employeeUser = new User { Id = employeeId, FullName = "Employee Name" };
        var hrUser = new User { Id = hrUserId, FullName = "HR Name" };

        unitOfWorkMock.Setup(u => u.Users.GetByIdAsync(employeeId, It.IsAny<CancellationToken>())).ReturnsAsync(employeeUser);
        unitOfWorkMock.Setup(u => u.Users.GetByIdAsync(hrUserId, It.IsAny<CancellationToken>())).ReturnsAsync(hrUser);

        var leaveBalanceProvisioningService = new LeaveBalanceProvisioningService(unitOfWorkMock.Object);
        var service = new LeaveRequestService(unitOfWorkMock.Object, fileServiceMock.Object, emailSenderMock.Object, loggerMock.Object, auditLogServiceMock.Object, leaveBalanceProvisioningService);

        var dto = new ReviewLeaveRequestDto { Status = "Rejected", HrNote = "Try again later" };

        var result = await service.ReviewLeaveRequestAsync(requestId, companyId, hrUserId, dto);

        result.Status.Should().Be("Rejected");
        result.ReviewedByUserId.Should().Be(hrUserId);
        result.ReviewedByName.Should().Be("HR Name");
        result.ReviewedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        unitOfWorkMock.Verify(u => u.LeaveRequests.Update(It.Is<LeaveRequest>(r => r.ReviewedByUserId == hrUserId && r.Status == "Rejected")), Times.Once);
        unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReviewLeaveRequestAsync_ApprovingUncappedLeaveType_StillIncrementsUsedDays()
    {
        // FIX-02: the usage increment used to sit inside the "has a cap" branch, so
        // approving an uncapped type (Sick, Unpaid) never recorded any usage at all.
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var fileServiceMock = new Mock<IFileService>();
        var emailSenderMock = new Mock<IEmailSender>();
        var loggerMock = new Mock<ILogger<LeaveRequestService>>();
        var auditLogServiceMock = new Mock<IAuditLogService>();

        var employeeId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var hrUserId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var request = new LeaveRequest
        {
            Id = requestId,
            CompanyId = companyId,
            EmployeeId = employeeId,
            Status = "Pending",
            LeaveType = "Unpaid",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            DaysRequested = 5
        };

        unitOfWorkMock.Setup(u => u.LeaveRequests.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<LeaveRequest, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        unitOfWorkMock.Setup(u => u.Users.GetByIdAsync(employeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = employeeId, FullName = "Employee Name" });
        unitOfWorkMock.Setup(u => u.Users.GetByIdAsync(hrUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = hrUserId, FullName = "HR Name" });

        var balance = new LeaveBalance { Id = Guid.NewGuid(), EmployeeId = employeeId, LeaveType = "Unpaid", Year = request.StartDate.Year, TotalDays = null, UsedDays = 0 };
        unitOfWorkMock.Setup(u => u.LeaveBalances.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<LeaveBalance, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(balance);

        var leaveBalanceProvisioningService = new LeaveBalanceProvisioningService(unitOfWorkMock.Object);
        var service = new LeaveRequestService(unitOfWorkMock.Object, fileServiceMock.Object, emailSenderMock.Object, loggerMock.Object, auditLogServiceMock.Object, leaveBalanceProvisioningService);

        var dto = new ReviewLeaveRequestDto { Status = "Approved" };

        var result = await service.ReviewLeaveRequestAsync(requestId, companyId, hrUserId, dto);

        result.Status.Should().Be("Approved");
        balance.UsedDays.Should().Be(5);
        balance.TotalDays.Should().BeNull("Unpaid stays uncapped");
        unitOfWorkMock.Verify(u => u.LeaveBalances.Update(It.Is<LeaveBalance>(b => b.UsedDays == 5)), Times.Once);
    }

    [Fact]
    public async Task ReviewLeaveRequestAsync_ApprovingWithANote_PersistsItAndIncludesItInTheEmail()
    {
        // FIX-11: HrNote was only ever assigned inside the Rejected branch, so approving
        // with a note silently dropped it - and the email (which already renders HrNote
        // when present) went out without it.
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var fileServiceMock = new Mock<IFileService>();
        var emailSenderMock = new Mock<IEmailSender>();
        var loggerMock = new Mock<ILogger<LeaveRequestService>>();
        var auditLogServiceMock = new Mock<IAuditLogService>();

        var employeeId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var hrUserId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var request = new LeaveRequest
        {
            Id = requestId,
            CompanyId = companyId,
            EmployeeId = employeeId,
            Status = "Pending",
            LeaveType = "Annual",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            DaysRequested = 2
        };

        unitOfWorkMock.Setup(u => u.LeaveRequests.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<LeaveRequest, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        unitOfWorkMock.Setup(u => u.Users.GetByIdAsync(employeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = employeeId, FullName = "Employee Name", Email = "employee@test.com" });
        unitOfWorkMock.Setup(u => u.Users.GetByIdAsync(hrUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = hrUserId, FullName = "HR Name" });

        var balance = new LeaveBalance { Id = Guid.NewGuid(), EmployeeId = employeeId, LeaveType = "Annual", Year = request.StartDate.Year, TotalDays = 21, UsedDays = 0 };
        unitOfWorkMock.Setup(u => u.LeaveBalances.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<LeaveBalance, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(balance);

        var leaveBalanceProvisioningService = new LeaveBalanceProvisioningService(unitOfWorkMock.Object);
        var service = new LeaveRequestService(unitOfWorkMock.Object, fileServiceMock.Object, emailSenderMock.Object, loggerMock.Object, auditLogServiceMock.Object, leaveBalanceProvisioningService);

        var dto = new ReviewLeaveRequestDto { Status = "Approved", HrNote = "Enjoy your trip!" };

        var result = await service.ReviewLeaveRequestAsync(requestId, companyId, hrUserId, dto);

        result.HrNote.Should().Be("Enjoy your trip!");
        request.HrNote.Should().Be("Enjoy your trip!");
        emailSenderMock.Verify(e => e.SendEmailAsync(
            "employee@test.com",
            It.IsAny<string>(),
            It.Is<string>(body => body.Contains("Enjoy your trip!")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReviewLeaveRequestAsync_ApprovingWithNoNote_DoesNotWipeAnExistingOne()
    {
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var fileServiceMock = new Mock<IFileService>();
        var emailSenderMock = new Mock<IEmailSender>();
        var loggerMock = new Mock<ILogger<LeaveRequestService>>();
        var auditLogServiceMock = new Mock<IAuditLogService>();

        var employeeId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var hrUserId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var request = new LeaveRequest
        {
            Id = requestId,
            CompanyId = companyId,
            EmployeeId = employeeId,
            Status = "Pending",
            LeaveType = "Annual",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            DaysRequested = 2,
            HrNote = "Earlier note from a prior touch"
        };

        unitOfWorkMock.Setup(u => u.LeaveRequests.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<LeaveRequest, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        unitOfWorkMock.Setup(u => u.Users.GetByIdAsync(employeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = employeeId, FullName = "Employee Name" });
        unitOfWorkMock.Setup(u => u.Users.GetByIdAsync(hrUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = hrUserId, FullName = "HR Name" });

        var balance = new LeaveBalance { Id = Guid.NewGuid(), EmployeeId = employeeId, LeaveType = "Annual", Year = request.StartDate.Year, TotalDays = 21, UsedDays = 0 };
        unitOfWorkMock.Setup(u => u.LeaveBalances.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<LeaveBalance, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(balance);

        var leaveBalanceProvisioningService = new LeaveBalanceProvisioningService(unitOfWorkMock.Object);
        var service = new LeaveRequestService(unitOfWorkMock.Object, fileServiceMock.Object, emailSenderMock.Object, loggerMock.Object, auditLogServiceMock.Object, leaveBalanceProvisioningService);

        var dto = new ReviewLeaveRequestDto { Status = "Approved" };

        var result = await service.ReviewLeaveRequestAsync(requestId, companyId, hrUserId, dto);

        result.HrNote.Should().Be("Earlier note from a prior touch");
    }
}
