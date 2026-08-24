using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wakeel.Application.Interfaces.Services;

namespace Wakeel.API.BackgroundServices;

/// <summary>
/// Runs once at startup and then once every 24 hours. Named "Daily Maintenance" rather
/// than something leave-specific because it is meant to be the single home for every
/// low-stakes daily cleanup job this API needs (see FIX-25's retention purge) - one
/// timer loop rather than a new competing BackgroundService per job.
///
/// A BackgroundService is registered as a singleton, so it cannot inject scoped services
/// directly - each run resolves its own DI scope instead. The actual business logic for
/// each job lives in its own Application-layer service (see
/// IAbandonedDraftCleanupService) so it can be unit tested without this timer plumbing.
/// </summary>
public class DailyMaintenanceBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DailyMaintenanceBackgroundService> _logger;

    public DailyMaintenanceBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<DailyMaintenanceBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        do
        {
            await RunOnceAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        // Each job gets its own scope and its own try/catch so a failure in one never
        // skips the other - abandoned-draft cleanup and the retention purge are
        // unrelated concerns that happen to share this one timer loop.
        await RunAbandonedDraftCleanupAsync(stoppingToken);
        await RunDataRetentionPurgeAsync(stoppingToken);
    }

    private async Task RunAbandonedDraftCleanupAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var cleanupService = scope.ServiceProvider.GetRequiredService<IAbandonedDraftCleanupService>();

            var retentionDays = _configuration.GetValue<int?>("LeavePolicy:AbandonedDraftRetentionDays") ?? 7;
            var cancelledCount = await cleanupService.CancelAbandonedDraftsAsync(retentionDays, stoppingToken);

            // Count only - never per-row personal data.
            _logger.LogInformation("Abandoned draft cleanup: cancelled {Count} draft(s) older than {RetentionDays} day(s).", cancelledCount, retentionDays);
        }
        catch (Exception ex)
        {
            // A failed run must never crash the host - it just tries again next interval.
            _logger.LogError(ex, "Abandoned draft cleanup run failed.");
        }
    }

    /// <summary>FIX-25: purges audit logs and generated documents past their retention window.</summary>
    private async Task RunDataRetentionPurgeAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var retentionService = scope.ServiceProvider.GetRequiredService<IDataRetentionService>();

            var auditLogRetentionDays = _configuration.GetValue<int?>("DataRetention:AuditLogRetentionDays") ?? 365;
            var generatedDocumentRetentionDays = _configuration.GetValue<int?>("DataRetention:GeneratedDocumentRetentionDays") ?? 730;

            var result = await retentionService.PurgeExpiredRecordsAsync(
                auditLogRetentionDays, generatedDocumentRetentionDays, stoppingToken);

            // Count only - never per-row personal data.
            _logger.LogInformation(
                "Data retention purge: removed {AuditLogCount} audit log(s) older than {AuditLogRetentionDays} day(s) and {DocumentCount} generated document(s) older than {DocumentRetentionDays} day(s).",
                result.AuditLogsPurged, auditLogRetentionDays, result.GeneratedDocumentsPurged, generatedDocumentRetentionDays);
        }
        catch (Exception ex)
        {
            // A failed run must never crash the host - it just tries again next interval.
            _logger.LogError(ex, "Data retention purge run failed.");
        }
    }
}
