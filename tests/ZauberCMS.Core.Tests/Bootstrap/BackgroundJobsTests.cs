using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Bootstrap;

/// <summary>
/// ZA-198 (Roadmap D61): each background job has an on/off switch under <c>Zauber:Jobs</c>, on by default, so a
/// server of a web farm can leave a job to another one.
/// </summary>
public class BackgroundJobsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string DailyJob = "ZauberCMS.Core.Jobs.DailyJob";
    private const string SitemapGenerator = "ZauberCMS.Components.Seo.Startup.SitemapGeneratorService";

    [Fact]
    public void ZA198_every_job_is_on_by_default()
    {
        var jobs = new ZauberSettings().Jobs;
        Assert.True(jobs.DailyCleanup);
        Assert.True(jobs.SitemapGenerator);
    }

    // The jobs a site runs are the hosted services ZauberCMS registers, apart from the Redis cache-clear listener
    // (only with Redis, and it must run on every server). A new job needs a switch and a row here.
    [Fact]
    public async Task ZA198_the_jobs_with_a_switch_are_every_hosted_service_ZauberCMS_registers()
    {
        var contentRoot = TestSite.CreateContentRoot();
        try
        {
            await using var app = TestSite.BuildApp("", "", contentRoot, validateContainer: false);
            var ours = app.Services.GetServices<IHostedService>()
                .Select(s => s.GetType().FullName!)
                .Where(name => name.StartsWith("ZauberCMS.", StringComparison.Ordinal))
                .Order(StringComparer.Ordinal)
                .ToList();
            Assert.Equal([SitemapGenerator, DailyJob], ours);
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData(DailyJob, "DailyCleanup")]
    [InlineData(SitemapGenerator, "SitemapGenerator")]
    public async Task ZA198_a_job_turned_off_does_not_start(string jobType, string key)
    {
        await RunJobAsync(jobType, new Dictionary<string, string?> { [$"Zauber:Jobs:{key}"] = "false" }, async job =>
        {
            // ExecuteAsync returned at once: nothing scheduled, nothing run
            await job.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10), Ct);
            Assert.True(job.ExecuteTask.IsCompletedSuccessfully);
        });
    }

    [Theory]
    [InlineData(DailyJob, null)]
    [InlineData(DailyJob, "true")]
    [InlineData(SitemapGenerator, null)]
    [InlineData(SitemapGenerator, "true")]
    public async Task ZA198_a_job_that_is_on_keeps_running_until_the_site_stops(string jobType, string? value)
    {
        var settings = new Dictionary<string, string?>();
        if (value != null)
        {
            settings[jobType == DailyJob ? "Zauber:Jobs:DailyCleanup" : "Zauber:Jobs:SitemapGenerator"] = value;
        }

        await RunJobAsync(jobType, settings, async job =>
        {
            // Waiting for its first or next run (the daily job a few minutes after start-up, the sitemap every six hours)
            await Assert.ThrowsAsync<TimeoutException>(() => job.ExecuteTask!.WaitAsync(TimeSpan.FromMilliseconds(500), Ct));
        });
    }

    private static async Task RunJobAsync(string jobType, Dictionary<string, string?> settings, Func<BackgroundService, Task> check)
    {
        var contentRoot = TestSite.CreateContentRoot();
        try
        {
            // No database: the installer state, so a run that did start would do no work
            await using var app = TestSite.BuildApp("", "", contentRoot, validateContainer: false, settings);
            var job = Assert.IsAssignableFrom<BackgroundService>(
                app.Services.GetServices<IHostedService>().Single(s => s.GetType().FullName == jobType));

            await job.StartAsync(Ct);
            try
            {
                await check(job);
            }
            finally
            {
                await job.StopAsync(Ct);
            }

            Assert.True(job.ExecuteTask!.IsCompleted);
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }
}
