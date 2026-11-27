using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Audit.Interfaces;
using ZauberCMS.Core.Audit.Parameters;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Installer;
using ZauberCMS.Core.Settings;

namespace ZauberCMS.Core.Jobs;

/// <summary>
/// The daily clean-up: old audit rows, orphaned related-content links and old page versions. The first run is a few
/// minutes after start-up, then once a day. A shutdown stops a run that is under way (ZA-111). It does not start on
/// a server where <c>Zauber:Jobs:DailyCleanup</c> is false (Roadmap D61).
/// </summary>
internal class DailyJob(IServiceProvider serviceProvider, IOptions<ZauberSettings> settings, ILogger<DailyJob> logger)
    : BackgroundService
{
    /// <summary>How long after start-up the first run waits.</summary>
    internal static readonly TimeSpan FirstRunDelay = TimeSpan.FromMinutes(5);

    internal static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.Value.Jobs.DailyCleanup)
        {
            logger.LogInformation("The daily clean-up job is off on this server (Zauber:Jobs:DailyCleanup)");
            return;
        }

        try
        {
            // First run a few minutes after start-up, not during it (ZA-083), then once a day
            await Task.Delay(FirstRunDelay, stoppingToken);
            using var timer = new PeriodicTimer(Interval);
            do
            {
                await RunAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The site is stopping
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!serviceProvider.GetRequiredService<InstallState>().IsDatabaseConfigured)
            {
                // The installer has not been given a database yet; the next run is tomorrow
                return;
            }

            using var scope = serviceProvider.CreateScope();
            var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
            var contentService = scope.ServiceProvider.GetRequiredService<IContentService>();

            // Cleanup old audit records
            await auditService.CleanupOldAuditsAsync(new CleanupOldAuditsParameters(), cancellationToken);

            // Cleanup orphaned RelatedContentId references
            await contentService.CleanupOrphanedRelatedContentAsync(new CleanupOrphanedRelatedContentParameters(),
                cancellationToken);

            // Keep the newest versions of each page (Zauber:ContentVersionsToKeep, Roadmap D25)
            var versionsToKeep = settings.Value.ContentVersionsToKeep;
            await scope.ServiceProvider.GetRequiredService<IContentVersioningService>()
                .PruneVersionsAsync(new PruneContentVersionsParameters { VersionsToKeep = versionsToKeep },
                    cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while executing the daily cleanup job");
        }
    }
}
