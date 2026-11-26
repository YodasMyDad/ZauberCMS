using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Audit.Interfaces;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Data.Interfaces;
using ZauberCMS.Core.Email.Interfaces;
using ZauberCMS.Core.Languages.Interfaces;
using ZauberCMS.Core.Media.Interfaces;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Seo.Interfaces;
using ZauberCMS.Core.Tags.Interfaces;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Bootstrap;

/// <summary>The real bootstrap must produce a container that builds and validates for every database provider.</summary>
public class ServiceContainerTests
{
    // No connection is opened: building and validating the container only constructs registrations.
    [Theory]
    [InlineData("Sqlite", "DataSource=:memory:")]
    [InlineData("SqlServer", "Server=unused;Database=unused;User Id=unused;Password=unused;TrustServerCertificate=True")]
    [InlineData("PostgreSql", "Host=unused;Database=unused;Username=unused;Password=unused")]
    public async Task Container_builds_and_validates_for_each_database_provider(string provider, string connectionString)
    {
        var contentRoot = TestSite.CreateContentRoot();
        try
        {
            await using var app = TestSite.BuildApp(provider, connectionString, contentRoot, validateContainer: true);

            using var scope = app.Services.CreateScope();
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IZauberDbContext>());
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    // A site that starts without a database must still build: the installer chooses the provider later, so all
    // three DbContexts are registered. A site that has a database registers its own only, as it always has.
    [Fact]
    public async Task A_site_without_a_database_builds_a_valid_container_that_can_serve_any_provider()
    {
        var contentRoot = TestSite.CreateContentRoot();
        try
        {
            await using var app = TestSite.BuildApp("", "", contentRoot, validateContainer: true);
            var registered = app.Services.GetRequiredService<IServiceProviderIsService>();

            Assert.True(registered.IsService(typeof(SqliteZauberDbContext)));
            Assert.True(registered.IsService(typeof(ZauberDbContext)));
            Assert.True(registered.IsService(typeof(PostgreSqlZauberDbContext)));

            await using var configured = TestSite.BuildApp("Sqlite", "DataSource=:memory:", contentRoot, validateContainer: true);
            var registeredWhenConfigured = configured.Services.GetRequiredService<IServiceProviderIsService>();
            Assert.True(registeredWhenConfigured.IsService(typeof(SqliteZauberDbContext)));
            Assert.False(registeredWhenConfigured.IsService(typeof(ZauberDbContext)));
            Assert.False(registeredWhenConfigured.IsService(typeof(PostgreSqlZauberDbContext)));
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    [Fact]
    public void Unknown_database_provider_fails_at_startup()
    {
        var contentRoot = TestSite.CreateContentRoot();
        try
        {
            Assert.ThrowsAny<Exception>(() =>
                TestSite.BuildApp("Oracle", "unused", contentRoot, validateContainer: false));
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData(typeof(IContentService))]
    [InlineData(typeof(IContentVersioningService))]
    [InlineData(typeof(IMembershipService))]
    [InlineData(typeof(IMediaService))]
    [InlineData(typeof(ILanguageService))]
    [InlineData(typeof(ITagService))]
    [InlineData(typeof(IAuditService))]
    [InlineData(typeof(ISeoService))]
    [InlineData(typeof(IDataService))]
    [InlineData(typeof(IEmailService))]
    public async Task Every_core_service_resolves(Type serviceType)
    {
        await using var site = await TestSite.CreateAsync();
        using var scope = site.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService(serviceType));
    }

    [Fact(Skip = KnownBug.Za011)]
    public async Task One_DbContext_instance_is_shared_within_a_scope()
    {
        await using var site = await TestSite.CreateAsync();
        using var scope = site.CreateScope();

        var viaInterface = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var viaConcreteType = scope.ServiceProvider.GetRequiredService<SqliteZauberDbContext>();

        Assert.Same(viaConcreteType, viaInterface);
    }
}
