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
            _logger.LogError(ex, "Daily maintenance run failed.");
        }
    }
}
