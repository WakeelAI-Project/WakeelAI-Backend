using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wakeel.Application.Interfaces;
using Wakeel.Application.Interfaces.Repositories;
using Wakeel.Application.Interfaces.Services;

namespace Wakeel.Application.Services;

/// <inheritdoc cref="IAbandonedDraftCleanupService" />
public class AbandonedDraftCleanupService : IAbandonedDraftCleanupService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<AbandonedDraftCleanupService> _logger;

    public AbandonedDraftCleanupService(IUnitOfWork unitOfWork, IAuditLogService auditLogService, ILogger<AbandonedDraftCleanupService> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<int> CancelAbandonedDraftsAsync(int retentionDays, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

        var staleDrafts = await _unitOfWork.LeaveRequests.FindAsync(
            lr => lr.Status == "Draft" && lr.CreatedAt < cutoff, cancellationToken);

        var cancelledCount = 0;
        foreach (var draft in staleDrafts)
        {
            draft.Status = "Cancelled";
            draft.CancelledAt = DateTime.UtcNow;
            _unitOfWork.LeaveRequests.Update(draft);
            cancelledCount++;

            try
            {
                await _auditLogService.LogActionAsync(null, "LEAVE_DRAFT_AUTO_CANCELLED", $"Auto-cancelled abandoned leave draft {draft.Id}.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write LEAVE_DRAFT_AUTO_CANCELLED audit entry for draft {DraftId}.", draft.Id);
            }
        }

        if (cancelledCount > 0)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        return cancelledCount;
    }
}
