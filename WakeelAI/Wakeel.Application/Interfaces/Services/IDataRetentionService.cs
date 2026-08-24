using System.Threading;
using System.Threading.Tasks;

namespace Wakeel.Application.Interfaces.Services;

/// <summary>
/// Purges audit logs and generated documents once they are past their configured
/// retention window (FIX-25). These are hard deletes - unlike leave-draft cleanup,
/// there is no "soft" state for a record that has simply aged out of the retention
/// policy.
/// </summary>
public interface IDataRetentionService
{
    /// <summary>
    /// Deletes audit logs and generated documents older than their respective retention
    /// windows. A retention period of 0 or less disables purging for that record type.
    /// Returns counts only - the caller must never log the purged records themselves.
    /// </summary>
    Task<DataRetentionPurgeResult> PurgeExpiredRecordsAsync(
        int auditLogRetentionDays,
        int generatedDocumentRetentionDays,
        CancellationToken cancellationToken = default);
}

public record DataRetentionPurgeResult(int AuditLogsPurged, int GeneratedDocumentsPurged);
