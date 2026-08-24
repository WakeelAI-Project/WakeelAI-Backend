using System;
using System.Threading;
using System.Threading.Tasks;
using Wakeel.Application.Interfaces.Repositories;
using Wakeel.Application.Interfaces.Services;

namespace Wakeel.Application.Services;

/// <inheritdoc cref="IDataRetentionService" />
public class DataRetentionService : IDataRetentionService
{
    private readonly IUnitOfWork _unitOfWork;

    public DataRetentionService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    /// <inheritdoc />
    public async Task<DataRetentionPurgeResult> PurgeExpiredRecordsAsync(
        int auditLogRetentionDays,
        int generatedDocumentRetentionDays,
        CancellationToken cancellationToken = default)
    {
        var auditLogsPurged = 0;
        var generatedDocumentsPurged = 0;
        var now = DateTime.UtcNow;

        if (auditLogRetentionDays > 0)
        {
            var auditLogCutoff = now.AddDays(-auditLogRetentionDays);
            var staleAuditLogs = await _unitOfWork.AuditLogs.FindAsync(
                a => a.CreatedAt < auditLogCutoff, cancellationToken);

            foreach (var log in staleAuditLogs)
            {
                _unitOfWork.AuditLogs.Remove(log);
                auditLogsPurged++;
            }
        }

        if (generatedDocumentRetentionDays > 0)
        {
            var documentCutoff = now.AddDays(-generatedDocumentRetentionDays);
            var staleDocuments = await _unitOfWork.GeneratedDocuments.FindAsync(
                d => d.CreatedAt < documentCutoff, cancellationToken);

            foreach (var document in staleDocuments)
            {
                _unitOfWork.GeneratedDocuments.Remove(document);
                generatedDocumentsPurged++;
            }
        }

        if (auditLogsPurged > 0 || generatedDocumentsPurged > 0)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new DataRetentionPurgeResult(auditLogsPurged, generatedDocumentsPurged);
    }
}
