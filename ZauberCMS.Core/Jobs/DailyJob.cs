using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZauberCMS.Core.Audit.Interfaces;
using ZauberCMS.Core.Audit.Parameters;

namespace ZauberCMS.Core.Jobs;

public class DailyJob(IServiceProvider serviceProvider, ILogger<DailyJob> logger) : IHostedService, IDisposable
{
    private Timer? _timer;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromHours(24));
        return Task.CompletedTask;
    }

    private async void DoWork(object? state)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
            await auditService.CleanupOldAuditsAsync(new CleanupOldAuditsParameters());
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