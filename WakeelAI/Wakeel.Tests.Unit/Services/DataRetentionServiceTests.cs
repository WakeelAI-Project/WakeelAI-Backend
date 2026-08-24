using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Wakeel.Application.Interfaces.Repositories;
using Wakeel.Application.Services;
using Wakeel.Domain.Entities;
using Xunit;

namespace Wakeel.Tests.Unit.Services;

/// <summary>FIX-25: covers the data-retention purge job's business logic.</summary>
public class DataRetentionServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IAuditLogRepository> _auditLogRepositoryMock = new();
    private readonly Mock<IGeneratedDocumentRepository> _generatedDocumentRepositoryMock = new();

    private readonly DataRetentionService _sut;

    public DataRetentionServiceTests()
    {
        _unitOfWorkMock.Setup(u => u.AuditLogs).Returns(_auditLogRepositoryMock.Object);
        _unitOfWorkMock.Setup(u => u.GeneratedDocuments).Returns(_generatedDocumentRepositoryMock.Object);
        _auditLogRepositoryMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<AuditLog, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AuditLog>());
        _generatedDocumentRepositoryMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<GeneratedDocument, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GeneratedDocument>());

        _sut = new DataRetentionService(_unitOfWorkMock.Object);
    }

    [Fact]
    public async Task PurgeExpiredRecordsAsync_GivenNothingStale_PurgesNothing()
    {
        var result = await _sut.PurgeExpiredRecordsAsync(365, 730, CancellationToken.None);

        result.AuditLogsPurged.Should().Be(0);
        result.GeneratedDocumentsPurged.Should().Be(0);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PurgeExpiredRecordsAsync_RemovesStaleAuditLogsAndDocuments()
    {
        var staleLogs = new List<AuditLog>
        {
            new() { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow.AddDays(-400) },
            new() { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow.AddDays(-500) }
        };
        var staleDocuments = new List<GeneratedDocument>
        {
            new() { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow.AddDays(-800) }
        };
        _auditLogRepositoryMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<AuditLog, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(staleLogs);
        _generatedDocumentRepositoryMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<GeneratedDocument, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(staleDocuments);

        var result = await _sut.PurgeExpiredRecordsAsync(365, 730, CancellationToken.None);

        result.AuditLogsPurged.Should().Be(2);
        result.GeneratedDocumentsPurged.Should().Be(1);
        _auditLogRepositoryMock.Verify(r => r.Remove(It.IsAny<AuditLog>()), Times.Exactly(2));
        _generatedDocumentRepositoryMock.Verify(r => r.Remove(It.IsAny<GeneratedDocument>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PurgeExpiredRecordsAsync_QueriesOnlyRecordsOlderThanTheirRespectiveRetentionWindow()
    {
        Expression<Func<AuditLog, bool>>? auditPredicate = null;
        Expression<Func<GeneratedDocument, bool>>? documentPredicate = null;
        _auditLogRepositoryMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<AuditLog, bool>>>(), It.IsAny<CancellationToken>()))
            .Callback<Expression<Func<AuditLog, bool>>, CancellationToken>((p, _) => auditPredicate = p)
            .ReturnsAsync(new List<AuditLog>());
        _generatedDocumentRepositoryMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<GeneratedDocument, bool>>>(), It.IsAny<CancellationToken>()))
            .Callback<Expression<Func<GeneratedDocument, bool>>, CancellationToken>((p, _) => documentPredicate = p)
            .ReturnsAsync(new List<GeneratedDocument>());

        await _sut.PurgeExpiredRecordsAsync(365, 730, CancellationToken.None);

        auditPredicate.Should().NotBeNull();
        var compiledAudit = auditPredicate!.Compile();
        compiledAudit(new AuditLog { CreatedAt = DateTime.UtcNow.AddDays(-366) }).Should().BeTrue();
        compiledAudit(new AuditLog { CreatedAt = DateTime.UtcNow.AddDays(-1) }).Should().BeFalse();

        documentPredicate.Should().NotBeNull();
        var compiledDocument = documentPredicate!.Compile();
        compiledDocument(new GeneratedDocument { CreatedAt = DateTime.UtcNow.AddDays(-731) }).Should().BeTrue();
        compiledDocument(new GeneratedDocument { CreatedAt = DateTime.UtcNow.AddDays(-1) }).Should().BeFalse();
    }

    [Fact]
    public async Task PurgeExpiredRecordsAsync_ZeroOrNegativeRetentionDisablesThatPurge()
    {
        var result = await _sut.PurgeExpiredRecordsAsync(0, -1, CancellationToken.None);

        result.AuditLogsPurged.Should().Be(0);
        result.GeneratedDocumentsPurged.Should().Be(0);
        _auditLogRepositoryMock.Verify(r => r.FindAsync(It.IsAny<Expression<Func<AuditLog, bool>>>(), It.IsAny<CancellationToken>()), Times.Never);
        _generatedDocumentRepositoryMock.Verify(r => r.FindAsync(It.IsAny<Expression<Func<GeneratedDocument, bool>>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
