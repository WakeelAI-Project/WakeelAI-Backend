using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Wakeel.Application.Interfaces;
using Wakeel.Application.Interfaces.Repositories;
using Wakeel.Application.Services;
using Wakeel.Domain.Entities;
using Xunit;

namespace Wakeel.Tests.Unit.Services;

/// <summary>FIX-07: covers the abandoned-draft cleanup job's business logic.</summary>
public class AbandonedDraftCleanupServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ILeaveRequestRepository> _leaveRequestRepositoryMock = new();
    private readonly Mock<IAuditLogService> _auditLogServiceMock = new();
    private readonly Mock<ILogger<AbandonedDraftCleanupService>> _loggerMock = new();

    private readonly AbandonedDraftCleanupService _sut;

    public AbandonedDraftCleanupServiceTests()
    {
        _unitOfWorkMock.Setup(u => u.LeaveRequests).Returns(_leaveRequestRepositoryMock.Object);
        _sut = new AbandonedDraftCleanupService(_unitOfWorkMock.Object, _auditLogServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task CancelAbandonedDraftsAsync_GivenNoStaleDrafts_CancelsNothing()
    {
        _leaveRequestRepositoryMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<LeaveRequest, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LeaveRequest>());

        var count = await _sut.CancelAbandonedDraftsAsync(7, CancellationToken.None);

        count.Should().Be(0);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelAbandonedDraftsAsync_GivenStaleDrafts_CancelsThemAndSetsCancelledAt()
    {
        var staleDrafts = new List<LeaveRequest>
        {
            new() { Id = Guid.NewGuid(), Status = "Draft", CreatedAt = DateTime.UtcNow.AddDays(-10) },
            new() { Id = Guid.NewGuid(), Status = "Draft", CreatedAt = DateTime.UtcNow.AddDays(-30) }
        };
        _leaveRequestRepositoryMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<LeaveRequest, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(staleDrafts);

        var count = await _sut.CancelAbandonedDraftsAsync(7, CancellationToken.None);

        count.Should().Be(2);
        staleDrafts.Should().OnlyContain(lr => lr.Status == "Cancelled" && lr.CancelledAt != null);
        _leaveRequestRepositoryMock.Verify(r => r.Update(It.IsAny<LeaveRequest>()), Times.Exactly(2));
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _auditLogServiceMock.Verify(a => a.LogActionAsync(null, "LEAVE_DRAFT_AUTO_CANCELLED", It.IsAny<string>()), Times.Exactly(2));
    }

    [Fact]
    public async Task CancelAbandonedDraftsAsync_QueriesOnlyDraftsOlderThanTheRetentionWindow()
    {
        Expression<Func<LeaveRequest, bool>>? capturedPredicate = null;
        _leaveRequestRepositoryMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<LeaveRequest, bool>>>(), It.IsAny<CancellationToken>()))
            .Callback<Expression<Func<LeaveRequest, bool>>, CancellationToken>((predicate, _) => capturedPredicate = predicate)
            .ReturnsAsync(new List<LeaveRequest>());

        await _sut.CancelAbandonedDraftsAsync(7, CancellationToken.None);

        capturedPredicate.Should().NotBeNull();
        var compiled = capturedPredicate!.Compile();

        compiled(new LeaveRequest { Status = "Draft", CreatedAt = DateTime.UtcNow.AddDays(-8) }).Should().BeTrue();
        compiled(new LeaveRequest { Status = "Draft", CreatedAt = DateTime.UtcNow.AddDays(-1) }).Should().BeFalse();
        compiled(new LeaveRequest { Status = "Pending", CreatedAt = DateTime.UtcNow.AddDays(-30) }).Should().BeFalse();
    }

    [Fact]
    public async Task CancelAbandonedDraftsAsync_AuditWriteFailure_StillCancelsAndDoesNotThrow()
    {
        var staleDraft = new LeaveRequest { Id = Guid.NewGuid(), Status = "Draft", CreatedAt = DateTime.UtcNow.AddDays(-10) };
        _leaveRequestRepositoryMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<LeaveRequest, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LeaveRequest> { staleDraft });
        _auditLogServiceMock
            .Setup(a => a.LogActionAsync(It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("db_unreachable"));

        var count = await _sut.CancelAbandonedDraftsAsync(7, CancellationToken.None);

        count.Should().Be(1);
        staleDraft.Status.Should().Be("Cancelled");
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
