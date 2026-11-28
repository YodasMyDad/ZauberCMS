using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Data.Interfaces;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Installer;
using ZauberCMS.Core.Installer.Interfaces;
using ZauberCMS.Core.Installer.Models;
using ZauberCMS.Core.Installer.Parameters;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.SeedData;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Installer;

/// <summary>
/// ZA-004 / Roadmap D19–D21: the installer creates the administrator, "installed" is a flag in the database that
/// is set once, and a database that already has users counts as installed.
/// </summary>
public class InstallServiceTests : IAsyncLifetime
{
    private TestSite _site = null!;

    protected virtual string DatabaseProvider => TestSite.SqliteProvider;

    public async ValueTask InitializeAsync() =>
        _site = await TestSite.CreateAsync(databaseProvider: DatabaseProvider, createUser: false);

    public async ValueTask DisposeAsync() => await _site.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private InstallState State => _site.Services.GetRequiredService<InstallState>();

    private static InstallParameters Valid(string? starterKit = null) => new()
    {
        AdminName = "Ada Admin",
        AdminEmail = "ada@example.test",
        AdminPassword = "Passw0rd-For-Tests",
        SiteName = "Wand & Hat",
        Language = "en-GB",
        AllowRegistration = false,
        StarterKit = starterKit
    };

    private async Task<T> WithServiceAsync<T>(Func<IInstallService, Task<T>> action)
    {
        using var scope = _site.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<IInstallService>());
    }

    [Fact]
    public async Task A_new_database_is_not_installed()
    {
        Assert.False(await WithServiceAsync(s => s.CheckInstalledAsync(Ct)));
        Assert.False(State.IsInstalled);
        Assert.True(State.IsDatabaseConfigured);
    }

    [Fact]
    public async Task Installing_creates_a_confirmed_administrator_and_marks_the_site_installed()
    {
        var result = await WithServiceAsync(s => s.InstallAsync(Valid(), Ct));

        Assert.True(result.Success, string.Join("; ", result.Errors));
        Assert.Empty(result.Warnings);
        Assert.True(State.IsInstalled);

        using var scope = _site.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var admin = await userManager.FindByEmailAsync("ada@example.test");
        Assert.NotNull(admin);
        Assert.Equal("Ada Admin", admin.UserName);
        Assert.True(admin.EmailConfirmed);
        Assert.Equal(["Admin"], await userManager.GetRolesAsync(admin));
        Assert.True(await userManager.CheckPasswordAsync(admin, "Passw0rd-For-Tests"));

        var flag = await _site.QueryDatabaseAsync(db =>
            db.GlobalDatas.AsNoTracking().SingleAsync(x => x.Alias == InstallFlag.Alias, Ct));
        Assert.Contains(InstallFlag.Installer, flag.Data);
    }

    [Fact]
    public async Task Installing_saves_the_site_name_the_language_and_the_registration_choice()
    {
        var parameters = Valid();
        parameters.AllowRegistration = true;

        var result = await WithServiceAsync(s => s.InstallAsync(parameters, Ct));

        Assert.True(result.Success, string.Join("; ", result.Errors));
        using var scope = _site.CreateScope();
        var settings = await scope.ServiceProvider.GetRequiredService<IDataService>().GetGlobalSettings();
        Assert.Equal("Wand & Hat", settings.SiteName);
        Assert.Equal("en-GB", settings.DefaultLanguage);
        Assert.True(settings.AllowRegistration);
        Assert.Equal(["en-GB"], await _site.QueryDatabaseAsync(db =>
            db.Languages.AsNoTracking().Select(x => x.LanguageIsoCode!).ToListAsync(Ct)));
    }

    [Fact]
    public async Task The_installer_turns_registration_off_unless_asked()
    {
        await WithServiceAsync(s => s.InstallAsync(Valid(), Ct));

        using var scope = _site.CreateScope();
        Assert.False((await scope.ServiceProvider.GetRequiredService<IDataService>().GetGlobalSettings()).AllowRegistration);
    }

    [Theory]
    [InlineData("weak", "admin")]
    [InlineData("", "admin")]
    public async Task A_password_the_site_would_refuse_stops_the_install_before_anything_is_saved(string password,
        string expectedPart)
    {
        var parameters = Valid();
        parameters.AdminPassword = password;

        var result = await WithServiceAsync(s => s.InstallAsync(parameters, Ct));

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
        Assert.Equal(expectedPart, result.ErrorPart);
        Assert.False(State.IsInstalled);
        Assert.False(await _site.QueryDatabaseAsync(db => db.Users.AnyAsync(Ct)));
        // The seed data ran before the installer (the Editor role's marker row); the installer itself saved nothing
        Assert.False(await _site.QueryDatabaseAsync(db => db.GlobalDatas.AnyAsync(x => x.Alias != SeedEditorRole.MarkerAlias, Ct)));
        Assert.False(await _site.QueryDatabaseAsync(db => db.Languages.AnyAsync(Ct)));
    }

    [Fact]
    public async Task Missing_site_details_are_reported_for_their_part_of_the_form()
    {
        var parameters = Valid();
        parameters.SiteName = " ";
        parameters.Language = "not-a-culture";

        var result = await WithServiceAsync(s => s.InstallAsync(parameters, Ct));

        Assert.False(result.Success);
        Assert.Equal("site", result.ErrorPart);
        Assert.Equal(2, result.Errors.Count);
        Assert.False(await _site.QueryDatabaseAsync(db => db.Users.AnyAsync(Ct)));
    }

    [Fact]
    public async Task The_installer_runs_once()
    {
        Assert.True((await WithServiceAsync(s => s.InstallAsync(Valid(), Ct))).Success);

        var second = Valid();
        second.AdminName = "Mallory";
        second.AdminEmail = "mallory@example.test";
        var again = await WithServiceAsync(s => s.InstallAsync(second, Ct));

        Assert.False(again.Success);
        Assert.True(again.AlreadyInstalled);
        Assert.Equal(["Ada Admin"], await _site.QueryDatabaseAsync(db =>
            db.Users.AsNoTracking().Select(x => x.UserName!).ToListAsync(Ct)));
    }

    // D21: a site from before the installer has users and no flag
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_database_that_already_has_users_is_marked_installed(bool onlyDeletedUsers)
    {
        var user = await _site.CreateUserAsync("from-before");
        if (onlyDeletedUsers)
        {
            // Every account soft-deleted: the installer must still stay shut (ZA-004 "re-opens")
            await _site.QueryDatabaseAsync(async db =>
            {
                (await db.Users.SingleAsync(x => x.Id == user.Id, Ct)).Deleted = true;
                return await db.SaveChangesAsync(Ct);
            });
        }

        // Looking alone decides nothing from the users: a request that looks while another one is in the
        // middle of creating the administrator must not call the site installed
        Assert.False(await WithServiceAsync(s => s.CheckInstalledAsync(Ct)));
        Assert.False(State.IsInstalled);

        // Start-up does
        await WithServiceAsync(async s =>
        {
            await s.InitialiseDatabaseAsync(Ct);
            return true;
        });

        Assert.True(State.IsInstalled);
        var flag = await _site.QueryDatabaseAsync(db =>
            db.GlobalDatas.AsNoTracking().SingleAsync(x => x.Alias == InstallFlag.Alias, Ct));
        Assert.Contains(InstallFlag.Upgrade, flag.Data);

        var refused = await WithServiceAsync(s => s.InstallAsync(Valid(), Ct));
        Assert.True(refused.AlreadyInstalled);
        Assert.Null(await _site.QueryDatabaseAsync(db =>
            db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Email == "ada@example.test", Ct)));
    }

    [Fact]
    public async Task An_install_attempt_on_a_database_that_has_users_is_refused_and_marks_it_installed()
    {
        await _site.CreateUserAsync("from-before");

        var refused = await WithServiceAsync(s => s.InstallAsync(Valid(), Ct));

        Assert.True(refused.AlreadyInstalled);
        Assert.True(State.IsInstalled);
        Assert.Equal(["from-before"], await _site.QueryDatabaseAsync(db =>
            db.Users.AsNoTracking().Select(x => x.UserName!).ToListAsync(Ct)));
    }

    // The registration choice must be in place before the account exists: from that moment the site counts
    // as installed, and without a settings row registration is on
    [Fact]
    public async Task The_settings_are_saved_even_when_the_account_is_then_refused()
    {
        var parameters = Valid();
        // Passes the checks made before anything is written, refused by Identity when the account is created
        parameters.AdminName = "not/allowed";

        var result = await WithServiceAsync(s => s.InstallAsync(parameters, Ct));

        Assert.False(result.Success);
        Assert.Equal("admin", result.ErrorPart);
        Assert.False(State.IsInstalled);
        Assert.False(await _site.QueryDatabaseAsync(db => db.Users.AnyAsync(Ct)));
        using var scope = _site.CreateScope();
        Assert.False((await scope.ServiceProvider.GetRequiredService<IDataService>().GetGlobalSettings()).AllowRegistration);
    }

    [Fact]
    public async Task The_flag_another_server_wrote_is_found()
    {
        await _site.QueryDatabaseAsync(async db =>
        {
            db.GlobalDatas.Add(new ZauberCMS.Core.Data.Models.GlobalData { Alias = InstallFlag.Alias, Data = "{}" });
            return await db.SaveChangesAsync(Ct);
        });

        Assert.False(State.IsInstalled);
        Assert.True(await WithServiceAsync(s => s.CheckInstalledAsync(Ct)));
        Assert.True(State.IsInstalled);
    }

    [Fact]
    public async Task The_starter_kit_runs_as_the_new_administrator()
    {
        var result = await WithServiceAsync(s => s.InstallAsync(Valid(typeof(RecordingStarterKit).FullName), Ct));

        Assert.True(result.Success, string.Join("; ", result.Errors));
        Assert.Empty(result.Warnings);
        var type = await _site.QueryDatabaseAsync(db =>
            db.ContentTypes.AsNoTracking().SingleAsync(x => x.Alias == RecordingStarterKit.ContentTypeAlias, Ct));
        Assert.Equal(result.Administrator!.Id, type.LastUpdatedById);
        // It was handed the site name and the saved language
        Assert.Equal("Wand & Hat / en-GB", type.Description);
    }

    [Fact]
    public async Task A_failing_starter_kit_is_reported_and_does_not_undo_the_install()
    {
        var result = await WithServiceAsync(s => s.InstallAsync(Valid(typeof(FailingStarterKit).FullName), Ct));

        Assert.True(result.Success);
        Assert.True(State.IsInstalled);
        var warning = Assert.Single(result.Warnings);
        Assert.Contains("kit exploded", warning);
        Assert.True(await _site.QueryDatabaseAsync(db => db.Users.AnyAsync(x => x.Email == "ada@example.test", Ct)));
    }

    [Fact]
    public async Task A_starter_kit_the_site_does_not_have_stops_the_install()
    {
        var result = await WithServiceAsync(s => s.InstallAsync(Valid("Some.Missing.Kit"), Ct));

        Assert.False(result.Success);
        Assert.Equal("starter", result.ErrorPart);
        Assert.False(await _site.QueryDatabaseAsync(db => db.Users.AnyAsync(Ct)));
    }

    [Fact]
    public async Task The_kits_in_the_scanned_assemblies_are_offered()
    {
        var kits = await WithServiceAsync(s => Task.FromResult(s.GetStarterKits()));

        Assert.Contains(typeof(RecordingStarterKit).FullName!, kits.Keys);
        Assert.Contains(typeof(FailingStarterKit).FullName!, kits.Keys);
        // A kit that cannot even be created is left out; it does not take the installer page down
        Assert.DoesNotContain(typeof(UnconstructableStarterKit).FullName!, kits.Keys);
    }

    [Fact]
    public async Task Choosing_a_kit_that_cannot_be_created_stops_the_install()
    {
        var result = await WithServiceAsync(s => s.InstallAsync(Valid(typeof(UnconstructableStarterKit).FullName), Ct));

        Assert.False(result.Success);
        Assert.Equal("starter", result.ErrorPart);
        Assert.False(await _site.QueryDatabaseAsync(db => db.Users.AnyAsync(Ct)));
    }
}

public class InstallServiceTestsOnPostgreSql : InstallServiceTests
{
    protected override string DatabaseProvider => TestSite.PostgreSqlProvider;
}

public class InstallServiceTestsOnSqlServer : InstallServiceTests
{
    protected override string DatabaseProvider => TestSite.SqlServerProvider;
}

/// <summary>Found by discovery, like a kit in a site's project. Saves a content type through the real service.</summary>
public class RecordingStarterKit(IContentService contentService) : IStarterKit
{
    public const string ContentTypeAlias = "starterKitPage";

    public string Name => "Recording kit";

    public string Description => "Records what the installer handed it.";

    public async Task InstallAsync(StarterKitContext context, CancellationToken cancellationToken = default)
    {
        var saved = await contentService.SaveContentTypeAsync(new SaveContentTypeParameters
        {
            ContentType = new ContentType
            {
                Name = "Starter kit page",
                Alias = ContentTypeAlias,
                Description = $"{context.SiteName} / {context.Language?.LanguageIsoCode}",
                AllowAtRoot = true
            }
        }, cancellationToken);

        if (!saved.Success)
        {
            throw new InvalidOperationException(TestSite.Describe(saved.Messages));
        }
    }
}

public class UnconstructableStarterKit : IStarterKit
{
    public UnconstructableStarterKit() => throw new InvalidOperationException("a dependency is missing");

    public string Name => "Broken kit";

    public string Description => "Cannot be created.";

    public Task InstallAsync(StarterKitContext context, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

public class FailingStarterKit : IStarterKit
{
    public string Name => "Failing kit";

    public string Description => "Always fails.";

    public Task InstallAsync(StarterKitContext context, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("kit exploded");
}
