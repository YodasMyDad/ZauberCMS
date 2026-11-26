using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Plugins;
using ContentItem = ZauberCMS.Core.Content.Models.Content;
using ZauberCMS.Core.Data.Interfaces;

namespace ZauberCMS.Core.Tests.Infrastructure;

/// <summary>
/// A ZauberCMS site for service tests: the real DI graph from <see cref="ZauberSetup.AddZauberCms"/>, a private
/// database created by the real migrations (in-memory SQLite by default, or a fresh database in the shared
/// PostgreSQL container — see <see cref="TestContainers"/>), the seeders run, and one signed-in user.
/// The web server is never started, so no middleware, endpoints or hosted services run.
/// Create one per test (or per class) and dispose it.
/// </summary>
public sealed class TestSite : IAsyncDisposable
{
    public const string SqliteProvider = "Sqlite";
    public const string PostgreSqlProvider = "PostgreSql";
    public const string SqlServerProvider = "SqlServer";

    private readonly SqliteConnection? _keepAlive;
    private readonly string? _postgreSqlConnectionString;
    private readonly string? _sqlServerConnectionString;
    private readonly string _contentRoot;

    private TestSite(WebApplication app, SqliteConnection? keepAlive, string? postgreSqlConnectionString,
        string? sqlServerConnectionString, string contentRoot)
    {
        _postgreSqlConnectionString = postgreSqlConnectionString;
        _sqlServerConnectionString = sqlServerConnectionString;
        App = app;
        _keepAlive = keepAlive;
        _contentRoot = contentRoot;
    }

    public WebApplication App { get; }

    public IServiceProvider Services => App.Services;

    /// <summary>The user every service call runs as (services read it via AuthenticationStateProvider).</summary>
    public User CurrentUser { get; private set; } = null!;

    /// <param name="extraSettings">Configuration keys added to (or replacing) the test defaults.</param>
    /// <param name="databaseProvider"><see cref="SqliteProvider"/> (default), <see cref="PostgreSqlProvider"/> (skips
    /// without Docker) or <see cref="SqlServerProvider"/> (skips without <c>ZAUBER_TEST_SQLSERVER</c>).</param>
    /// <param name="createUser">False leaves the database without any user and nobody signed in: a site that
    /// has not been installed yet.</param>
    public static async Task<TestSite> CreateAsync(IDictionary<string, string?>? extraSettings = null,
        string databaseProvider = SqliteProvider, bool createUser = true)
    {
        string connectionString;
        SqliteConnection? keepAlive = null;
        if (databaseProvider == PostgreSqlProvider)
        {
            connectionString = await TestContainers.CreatePostgreSqlDatabaseAsync();
        }
        else if (databaseProvider == SqlServerProvider)
        {
            connectionString = await TestContainers.CreateSqlServerDatabaseAsync();
        }
        else
        {
            // A named shared-cache in-memory database lives for as long as one connection to it stays open.
            connectionString = $"DataSource=zauber-test-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
            keepAlive = new SqliteConnection(connectionString);
            await keepAlive.OpenAsync();
        }

        var contentRoot = CreateContentRoot();
        var app = BuildApp(databaseProvider, connectionString, contentRoot, validateContainer: false, extraSettings);
        var site = new TestSite(app, keepAlive,
            databaseProvider == PostgreSqlProvider ? connectionString : null,
            databaseProvider == SqlServerProvider ? connectionString : null, contentRoot);

        // Same order as ZauberSetup.AddZauberCms<T>(WebApplication): migrate, then run every ISeedData.
        using (var scope = site.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
            await dbContext.Database.MigrateAsync();
            var extensionManager = scope.ServiceProvider.GetRequiredService<ExtensionManager>();
            foreach (var seeder in extensionManager.GetInstances<ISeedData>())
            {
                seeder.Value.Initialise(dbContext);
            }
        }

        if (createUser)
        {
            site.CurrentUser = await site.CreateUserAsync("test-admin");
            site.SignIn(site.CurrentUser);
        }

        return site;
    }

    /// <summary>
    /// Builds the application through the real bootstrap without touching the database.
    /// <paramref name="validateContainer"/> makes the container check every registration and scope rule on build.
    /// </summary>
    /// <param name="readSettingsFile">Also read <c>appsettings.json</c> in the content root (it wins over the
    /// test defaults), as a real site does. For tests of the installer, which writes that file.</param>
    public static WebApplication BuildApp(string databaseProvider, string connectionString, string contentRoot,
        bool validateContainer, IDictionary<string, string?>? extraSettings = null, bool readSettingsFile = false)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing",
            ContentRootPath = contentRoot,
            WebRootPath = Path.Combine(contentRoot, "wwwroot")
        });

        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Zauber:DatabaseProvider"] = databaseProvider,
            ["Zauber:ConnectionString"] = connectionString,
            ["Zauber:AdminDefaultLanguage"] = "en-US",
            ["Zauber:UploadFolderName"] = "media",
            ["Zauber:NewUserStartingRole"] = "Member",
            ["Zauber:Plugins:StorageProvider"] = "ZauberCMS.Core.Providers.DiskStorageProvider",
            ["Zauber:Plugins:EmailProvider"] = "ZauberCMS.Core.Providers.SmtpEmailProvider"
        });

        if (extraSettings != null)
        {
            builder.Configuration.AddInMemoryCollection(extraSettings);
        }

        if (readSettingsFile)
        {
            builder.Configuration.AddJsonFile(Path.Combine(contentRoot, "appsettings.json"), optional: true,
                reloadOnChange: false);
        }

        builder.AddZauberCms();

        // Services read the signed-in user from AuthenticationStateProvider; the real one needs a Blazor circuit.
        builder.Services.AddSingleton<TestSignIn>();
        builder.Services.AddScoped<AuthenticationStateProvider, TestAuthenticationStateProvider>();

        builder.Host.UseDefaultServiceProvider(options =>
        {
            options.ValidateOnBuild = validateContainer;
            options.ValidateScopes = validateContainer;
        });

        return builder.Build();
    }

    public static string CreateContentRoot()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "zauber-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(contentRoot, "wwwroot"));
        return contentRoot;
    }

    public IServiceScope CreateScope() => Services.CreateScope();

    /// <summary>Makes <paramref name="user"/> (or nobody) the signed-in user for later service calls.</summary>
    public void SignIn(User? user)
    {
        var signIn = Services.GetRequiredService<TestSignIn>();
        signIn.Principal = user == null
            ? new ClaimsPrincipal(new ClaimsIdentity())
            : new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.UserName ?? string.Empty)
            ], "Test"));
    }

    public async Task<User> CreateUserAsync(string userName)
    {
        using var scope = CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = new User { UserName = userName, Email = $"{userName}@example.test" };
        var result = await userManager.CreateAsync(user, "Passw0rd-For-Tests");
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));
        return user;
    }

    public async Task<ContentType> CreateContentTypeAsync(string alias, bool isElementType = false)
    {
        using var scope = CreateScope();
        var contentService = scope.ServiceProvider.GetRequiredService<IContentService>();
        var result = await contentService.SaveContentTypeAsync(new SaveContentTypeParameters
        {
            ContentType = new ContentType
            {
                Name = alias,
                Alias = alias,
                AllowAtRoot = true,
                IsElementType = isElementType
            }
        });
        Assert.True(result.Success, Describe(result.Messages));
        return result.Entity!;
    }

    /// <summary>Saves a published content item through the real service.</summary>
    public async Task<ContentItem> CreateContentAsync(ContentType contentType, string name,
        Guid? parentId = null)
    {
        using var scope = CreateScope();
        var contentService = scope.ServiceProvider.GetRequiredService<IContentService>();
        var result = await contentService.SaveContentAsync(new SaveContentParameters
        {
            Content = new ContentItem
            {
                Name = name,
                ContentTypeId = contentType.Id,
                ContentTypeAlias = contentType.Alias,
                ParentId = parentId,
                IsRootContent = parentId == null,
                Published = true
            }
        });
        Assert.True(result.Success, Describe(result.Messages));
        return result.Entity!;
    }

    /// <summary>Runs a query on a fresh DbContext, so the result is what is really stored.</summary>
    public async Task<T> QueryDatabaseAsync<T>(Func<IZauberDbContext, Task<T>> query)
    {
        using var scope = CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<IZauberDbContext>());
    }

    public static string Describe(IEnumerable<ZauberCMS.Core.Shared.Models.ResultMessage> messages) =>
        string.Join("; ", messages.Select(m => m.Message));

    public async ValueTask DisposeAsync()
    {
        await App.DisposeAsync();
        if (_keepAlive != null)
        {
            await _keepAlive.DisposeAsync();
        }

        if (_postgreSqlConnectionString != null)
        {
            await TestContainers.DropPostgreSqlDatabaseAsync(_postgreSqlConnectionString);
        }

        if (_sqlServerConnectionString != null)
        {
            await TestContainers.DropSqlServerDatabaseAsync(_sqlServerConnectionString);
        }
        try
        {
            Directory.Delete(_contentRoot, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp folder is harmless; never fail a test over it.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

/// <summary>Holds the signed-in principal for a <see cref="TestSite"/>.</summary>
public sealed class TestSignIn
{
    public ClaimsPrincipal Principal { get; set; } = new(new ClaimsIdentity());
}

public sealed class TestAuthenticationStateProvider(TestSignIn signIn)
    : AuthenticationStateProvider, IHostEnvironmentAuthenticationStateProvider
{
    public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
        Task.FromResult(new AuthenticationState(signIn.Principal));

    // What code outside a Blazor component uses to run as a user (the installer does, for its new administrator)
    public void SetAuthenticationState(Task<AuthenticationState> authenticationStateTask) =>
        signIn.Principal = authenticationStateTask.GetAwaiter().GetResult().User;
}
