using System.Threading;
using System.Threading.Tasks;

namespace Wakeel.Application.Interfaces.Services;

/// <summary>
/// Cancels leave requests that have sat in Draft for longer than the configured retention
/// window - a draft nobody ever submitted is not a commitment, so it is a soft state
/// change (Cancelled), never a hard delete.
/// </summary>
public interface IAbandonedDraftCleanupService
{
    /// <summary>
    /// Cancels every Draft leave request created more than <paramref name="retentionDays"/>
    /// days ago. Returns the number cancelled. Writes a LEAVE_DRAFT_AUTO_CANCELLED audit
    /// entry per cancelled draft - an audit failure never fails the cancellation itself.
    /// </summary>
    Task<int> CancelAbandonedDraftsAsync(int retentionDays, CancellationToken cancellationToken = default);
}
