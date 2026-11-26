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

public class DailyJob(IServiceProvider serviceProvider, ILogger<DailyJob> logger) : IHostedService, IDisposable
{
    private Timer? _timer;

    /// <summary>How long after start-up the first run waits.</summary>
    internal static readonly TimeSpan FirstRunDelay = TimeSpan.FromMinutes(5);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // First run a few minutes after start-up, not during it (ZA-083), then once a day
        _timer = new Timer(DoWork, null, FirstRunDelay, TimeSpan.FromHours(24));
        return Task.CompletedTask;
    }

    private async void DoWork(object? state)
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
            await auditService.CleanupOldAuditsAsync(new CleanupOldAuditsParameters());

            // Cleanup orphaned RelatedContentId references
            await contentService.CleanupOrphanedRelatedContentAsync(new CleanupOrphanedRelatedContentParameters());

            // Keep the newest versions of each page (Zauber:ContentVersionsToKeep, Roadmap D25)
            var versionsToKeep = scope.ServiceProvider.GetRequiredService<IOptions<ZauberSettings>>().Value
                .ContentVersionsToKeep;
            await scope.ServiceProvider.GetRequiredService<IContentVersioningService>()
                .PruneVersionsAsync(new PruneContentVersionsParameters { VersionsToKeep = versionsToKeep });
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while executing the daily cleanup job");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
        GC.SuppressFinalize(this);
    }
}