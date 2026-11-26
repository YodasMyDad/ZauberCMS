using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Data.Interfaces;
using ZauberCMS.Core.Data.Models;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Installer.Interfaces;
using ZauberCMS.Core.Installer.Models;
using ZauberCMS.Core.Installer.Parameters;
using ZauberCMS.Core.Languages;
using ZauberCMS.Core.Languages.Interfaces;
using ZauberCMS.Core.Languages.Models;
using ZauberCMS.Core.Languages.Parameters;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Settings;

namespace ZauberCMS.Core.Installer.Services;

/// <inheritdoc />
/// <remarks>
/// The methods are virtual so a site can change one step (register a subclass as <see cref="IInstallService"/>
/// after <c>AddZauberCms</c>) without rewriting the others.
/// </remarks>
public class InstallService(
    InstallState installState,
    IConfiguration configuration,
    IHostEnvironment environment,
    IServiceScopeFactory serviceScopeFactory,
    IServiceProvider serviceProvider,
    ExtensionManager extensionManager,
    IOptions<ZauberSettings> settings,
    ILogger<InstallService> logger) : IInstallService
{
    private string SettingsFile => Path.Combine(environment.ContentRootPath, "appsettings.json");

    public virtual async Task InitialiseDatabaseAsync(CancellationToken cancellationToken = default)
    {
        // Start-up, the installer's database step and the first request after a database was configured by hand
        // can all arrive here; the migrations and the seeders must run once, not side by side.
        await installState.InitialiseGate.WaitAsync(cancellationToken);
        try
        {
            if (!installState.IsDatabaseInitialised)
            {
                using var scope = serviceScopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();

                // Migrations must succeed: the exception goes to the caller, so a host fails fast at start-up
                // instead of running half-migrated.
                if ((await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
                {
                    await dbContext.Database.MigrateAsync(cancellationToken);
                }

                // Seed data comes from plugins too: one misbehaving seeder must not bring the host down
                var seeders = scope.ServiceProvider.GetRequiredService<ExtensionManager>().GetInstances<ISeedData>();
                foreach (var seeder in seeders)
                {
                    try
                    {
                        seeder.Value.Initialise(dbContext);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Seed data plugin {Plugin} failed during startup", seeder.Value.GetType().FullName);
                    }
                }

                installState.MarkDatabaseInitialised();
            }
        }
        finally
        {
            installState.InitialiseGate.Release();
        }

        // A database that already has users was set up before the installer existed (or by a seeder): installed
        if (!await CheckInstalledAsync(usersCountAsInstalled: true, cancellationToken))
        {
            logger.LogWarning(
                "This site is not installed yet. Until it is, every request is sent to the installer, and the " +
                "first visitor to finish it becomes the administrator: install the site before making it public");
        }

        await RecoverAdminAsync(cancellationToken);
    }

    public virtual Task<bool> CheckInstalledAsync(CancellationToken cancellationToken = default) =>
        CheckInstalledAsync(usersCountAsInstalled: false, cancellationToken);

    // usersCountAsInstalled: also treat a database with any user row as installed, and write the flag for it.
    // Only for callers that cannot overlap an install in progress (start-up, and the install itself under its
    // lock): a request that merely looks while another one is creating the administrator must not conclude
    // "installed" from the half-finished account.
    private async Task<bool> CheckInstalledAsync(bool usersCountAsInstalled, CancellationToken cancellationToken)
    {
        if (installState.IsInstalled)
        {
            return true;
        }

        if (!installState.IsDatabaseConfigured)
        {
            return false;
        }

        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();

        if (await dbContext.GlobalDatas.AsNoTracking().AnyAsync(x => x.Alias == InstallFlag.Alias, cancellationToken))
        {
            installState.MarkInstalled();
            return true;
        }

        // Deleted users count too: the installer must not reopen because every account was removed (that is
        // what RecoverAdminUser is for).
        if (usersCountAsInstalled && await dbContext.Users.AsNoTracking().AnyAsync(cancellationToken))
        {
            await WriteFlagAsync(dbContext, InstallFlag.Upgrade, cancellationToken);
            installState.MarkInstalled();
            logger.LogInformation("This site already has users: marked as installed");
            return true;
        }

        return false;
    }

    public virtual async Task<DatabaseTestResult> TestDatabaseAsync(DatabaseSetupParameters parameters,
        CancellationToken cancellationToken = default)
    {
        string connectionString;
        try
        {
            connectionString = parameters.BuildConnectionString();
        }
        catch (ArgumentException ex)
        {
            return new DatabaseTestResult(false, false, ex.Message);
        }

        return await InstallerDatabase.TestAsync(parameters.Provider, connectionString, cancellationToken);
    }

    public virtual async Task<DatabaseSetupResult> SetUpDatabaseAsync(DatabaseSetupParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var result = new DatabaseSetupResult();

        await installState.InstallGate.WaitAsync(cancellationToken);
        try
        {
            if (installState.IsDatabaseConfigured)
            {
                result.Errors.Add("This site already has a database. Change it in the site's configuration.");
                return result;
            }

            var test = await TestDatabaseAsync(parameters, cancellationToken);
            if (!test.Success)
            {
                result.Errors.Add(test.Message);
                return result;
            }

            var provider = DatabaseProviders.Normalise(parameters.Provider)!;
            var connectionString = parameters.BuildConnectionString();
            result.DatabaseProvider = provider;
            result.ConnectionString = connectionString;

            // Create the tables first, on a context of our own. Configuration is only changed once the database
            // is known to work, so a failure here leaves the site exactly as it was.
            try
            {
                await using var dbContext = InstallerDatabase.CreateContext(provider, connectionString);
                await dbContext.Database.MigrateAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "The installer could not create the {Provider} database", provider);
                result.Errors.Add($"The database could not be set up: {Describe(ex)}");
                return result;
            }

            var written = AppSettingsWriter.Write(SettingsFile, provider, connectionString);
            if (!written.Success)
            {
                logger.LogWarning("The installer could not write {File}: {Reason}", SettingsFile, written.Error);
                result.ManualConfigurationRequired = true;
                result.ManualConfigurationReason =
                    $"The site cannot write to {SettingsFile}: {written.Error?.TrimEnd('.')}";
                return result;
            }

            result.SettingsFileWritten = true;

            // The file watcher would pick the change up a moment later; the next step needs it now
            try
            {
                (configuration as IConfigurationRoot)?.Reload();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Configuration could not be reloaded after the installer wrote {File}", SettingsFile);
                result.ManualConfigurationRequired = true;
                result.ManualConfigurationReason =
                    $"{SettingsFile} was updated, but the site's configuration could not be read again: {Describe(ex).TrimEnd('.')}";
                return result;
            }

            if (!installState.IsDatabaseConfigured ||
                installState.DatabaseProvider != provider ||
                installState.ConnectionString != connectionString)
            {
                result.ManualConfigurationRequired = true;
                result.ManualConfigurationReason =
                    $"{SettingsFile} was updated, but another configuration source (an environment variable, " +
                    $"appsettings.{environment.EnvironmentName}.json, a command-line argument) sets the same keys " +
                    "and wins";
                return result;
            }

            logger.LogInformation("The installer configured a {Provider} database", provider);
            await InitialiseDatabaseAsync(cancellationToken);
            result.Success = true;
            return result;
        }
        finally
        {
            installState.InstallGate.Release();
        }
    }

    public virtual IReadOnlyDictionary<string, IStarterKit> GetStarterKits()
    {
        var kits = new Dictionary<string, IStarterKit>();
        foreach (var (name, type) in extensionManager.GetImplementations<IStarterKit>())
        {
            if (CreateStarterKit(serviceProvider, type) is { } kit)
            {
                kits[name] = kit;
            }
        }

        return kits.OrderBy(x => x.Value.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.Value);
    }

    public virtual async Task<InstallResult> InstallAsync(InstallParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var result = new InstallResult();

        await installState.InstallGate.WaitAsync(cancellationToken);
        try
        {
            if (!installState.IsDatabaseConfigured)
            {
                result.Errors.Add("Choose a database first.");
                return result;
            }

            if (await CheckInstalledAsync(usersCountAsInstalled: true, cancellationToken))
            {
                result.AlreadyInstalled = true;
                result.Errors.Add("This site is already installed.");
                return result;
            }

            var adminName = parameters.AdminName?.Trim();
            var adminEmail = parameters.AdminEmail?.Trim();
            var siteName = parameters.SiteName?.Trim();

            void Refuse(string part, string message)
            {
                result.ErrorPart ??= part;
                result.Errors.Add(message);
            }

            if (string.IsNullOrEmpty(adminName))
            {
                Refuse("admin", "Enter a user name for the administrator.");
            }

            if (string.IsNullOrEmpty(adminEmail) || !new EmailAddressAttribute().IsValid(adminEmail))
            {
                Refuse("admin", "Enter the administrator's email address.");
            }

            if (string.IsNullOrEmpty(parameters.AdminPassword))
            {
                Refuse("admin", "Enter a password for the administrator.");
            }

            if (string.IsNullOrEmpty(siteName))
            {
                Refuse("site", "Enter a name for the site.");
            }
            else if (siteName.Length > 200)
            {
                Refuse("site", "The site name can be at most 200 characters.");
            }

            // No language is acceptable (a server without culture data offers none); an unknown one is not
            CultureInfo? culture = null;
            if (!string.IsNullOrWhiteSpace(parameters.Language))
            {
                culture = CultureLookup.FindSpecific(parameters.Language);
                if (culture == null)
                {
                    Refuse("site", "Choose the language of the site.");
                }
            }

            using var scope = serviceScopeFactory.CreateScope();
            var services = scope.ServiceProvider;

            // The kit is created in this scope, so the services it asks for run as the new administrator (below)
            IStarterKit? starterKit = null;
            if (!string.IsNullOrWhiteSpace(parameters.StarterKit))
            {
                if (extensionManager.GetImplementations<IStarterKit>().TryGetValue(parameters.StarterKit, out var kitType))
                {
                    starterKit = CreateStarterKit(services, kitType);
                }

                if (starterKit == null)
                {
                    Refuse("starter", "The starter content you chose is not available in this site.");
                }
            }

            if (result.Errors.Count > 0)
            {
                return result;
            }

            var userManager = services.GetRequiredService<UserManager<User>>();
            var roleManager = services.GetRequiredService<RoleManager<Role>>();

            // Nothing has been changed yet: every reason to refuse the password is known before the first write
            var admin = new User
            {
                Id = Guid.NewGuid().NewSequentialGuid(),
                UserName = adminName,
                Email = adminEmail,
                // The person at the keyboard typed it; there may be no working email to confirm it with yet
                EmailConfirmed = true
            };

            foreach (var validator in userManager.PasswordValidators)
            {
                var valid = await validator.ValidateAsync(userManager, admin, parameters.AdminPassword);
                foreach (var error in valid.Errors)
                {
                    Refuse("admin", error.Description);
                }
            }

            if (result.Errors.Count > 0)
            {
                return result;
            }

            var startingRole = settings.Value.NewUserStartingRole ?? Constants.Roles.StandardRoleName;
            foreach (var roleName in new[] { Constants.Roles.AdminRoleName, startingRole }.Distinct())
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    var roleResult = await roleManager.CreateAsync(new Role { Name = roleName });
                    if (!roleResult.Succeeded)
                    {
                        result.Errors.AddRange(roleResult.Errors.Select(e => e.Description));
                        return result;
                    }
                }
            }

            // The settings go in before the account exists. The moment there is a user the site counts as
            // installed, and registration must not be open for even one request when the installer was told to
            // keep it shut (without a settings row it is on, for the sake of sites from before the setting).
            var settingsError = await SaveSettingsAsync(services, siteName!, culture, parameters.AllowRegistration);
            if (settingsError != null)
            {
                Refuse("site", settingsError);
                return result;
            }

            // The user validators (unique name and address, allowed characters) run inside CreateAsync
            var created = await userManager.CreateAsync(admin, parameters.AdminPassword!);
            if (!created.Succeeded)
            {
                foreach (var error in created.Errors)
                {
                    Refuse("admin", error.Description);
                }

                return result;
            }

            // From the first write of the account to the flag nothing may stop half-way: a visitor who closes
            // the tab must not leave a site with a user and no administrator. So no request cancellation from
            // here on, and an account that could not be made an administrator is removed again.
            try
            {
                var addedToRole = await userManager.AddToRoleAsync(admin, Constants.Roles.AdminRoleName);
                if (!addedToRole.Succeeded)
                {
                    await userManager.DeleteAsync(admin);
                    result.Errors.AddRange(addedToRole.Errors.Select(e => e.Description));
                    return result;
                }

                await WriteFlagAsync(services.GetRequiredService<IZauberDbContext>(), InstallFlag.Installer, CancellationToken.None);
            }
            catch
            {
                try
                {
                    await userManager.DeleteAsync(admin);
                }
                catch (Exception cleanup)
                {
                    logger.LogError(cleanup, "The installer could not remove the half-created account {UserName}",
                        admin.UserName);
                }

                throw;
            }

            installState.MarkInstalled();
            result.Success = true;
            result.Administrator = admin;
            logger.LogInformation("ZauberCMS installed; administrator {UserName} created", admin.UserName);

            // The site is installed from here on. What follows is set-up the administrator can redo in the
            // admin, so a failure is reported and logged, not rolled back.
            Language? language = null;
            try
            {
                await RunAsAsync(services, admin);
                language = culture == null ? null : await SaveLanguageAsync(services, culture, result, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "The installer could not finish setting up after the install");
                result.Warnings.Add($"The language could not be saved: {Describe(ex)}");
            }

            if (starterKit != null)
            {
                try
                {
                    await starterKit.InstallAsync(new StarterKitContext
                    {
                        SiteName = siteName!,
                        Language = language,
                        Administrator = admin
                    }, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Starter kit {Kit} failed", starterKit.GetType().FullName);
                    result.Warnings.Add($"The starter content \"{starterKit.Name}\" could not be installed: {Describe(ex)}");
                }
            }

            return result;
        }
        finally
        {
            installState.InstallGate.Release();
        }
    }

    // A kit from a package the site does not control must not take the installer down with it
    private IStarterKit? CreateStarterKit(IServiceProvider services, Type type)
    {
        try
        {
            return (IStarterKit)ActivatorUtilities.CreateInstance(services, type);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Starter kit {Kit} could not be created and is left out", type.FullName);
            return null;
        }
    }

    // Services read the signed-in user from AuthenticationStateProvider. Outside a Blazor component nobody has
    // set it, and the built-in provider throws when asked; give this scope the new administrator.
    private static async Task RunAsAsync(IServiceProvider services, User user)
    {
        if (services.GetService<AuthenticationStateProvider>() is IHostEnvironmentAuthenticationStateProvider provider)
        {
            var principal = await services.GetRequiredService<SignInManager<User>>().CreateUserPrincipalAsync(user);
            provider.SetAuthenticationState(Task.FromResult(new AuthenticationState(principal)));
        }
    }

    private async Task<Language?> SaveLanguageAsync(IServiceProvider services, CultureInfo culture,
        InstallResult result, CancellationToken cancellationToken)
    {
        var languageService = services.GetRequiredService<ILanguageService>();
        var existing = await languageService.QueryLanguageAsync(new QueryLanguageParameters
        {
            LanguageIsoCodes = [culture.Name],
            AmountPerPage = 1
        }, cancellationToken);
        if (existing.Items.FirstOrDefault() is { } found)
        {
            return found;
        }

        var saved = await languageService.SaveLanguageAsync(
            new SaveLanguageParameters { CultureInfo = culture }, cancellationToken);
        if (saved.Success)
        {
            return saved.Entity;
        }

        result.Warnings.Add($"The language {culture.EnglishName} could not be saved: " +
                            string.Join(" ", saved.Messages.Select(m => m.Message)));
        return null;
    }

    // Returns why the settings could not be saved, or null when they were
    private async Task<string?> SaveSettingsAsync(IServiceProvider services, string siteName, CultureInfo? culture,
        bool allowRegistration)
    {
        try
        {
            var dataService = services.GetRequiredService<IDataService>();
            var globalSettings = await dataService.GetGlobalSettings();
            globalSettings.SiteName = siteName;
            globalSettings.DefaultLanguage = culture?.Name;
            globalSettings.AllowRegistration = allowRegistration;
            return await dataService.SaveGlobalSettings(globalSettings)
                ? null
                : "The site's settings could not be saved. The details are in the site's log.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "The installer could not save the global settings");
            return $"The site's settings could not be saved: {Describe(ex)}";
        }
    }

    // Written directly, not through SaveChangesAndLog: nothing caches or hooks this row, and it must not depend
    // on a signed-in user or on a plugin's save hook being able to run this early.
    private static async Task WriteFlagAsync(IZauberDbContext dbContext, string source,
        CancellationToken cancellationToken)
    {
        dbContext.GlobalDatas.Add(new GlobalData
        {
            Alias = InstallFlag.Alias,
            Data = JsonSerializer.Serialize(new InstallFlag
            {
                Source = source,
                Version = typeof(InstallService).Assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
            })
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // D21: the installer never reopens, so a site that has lost its last administrator names one in configuration
    private async Task RecoverAdminAsync(CancellationToken cancellationToken)
    {
        var recover = settings.Value.Identity.RecoverAdminUser?.Trim();
        if (string.IsNullOrEmpty(recover))
        {
            return;
        }

        const string key = "Zauber:Identity:RecoverAdminUser";
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();

            // A value with an @ is an email address and is matched as one only: somebody who registers with
            // that text as their user name must not be the one who is promoted
            var user = recover.Contains('@')
                ? await userManager.FindByEmailAsync(recover)
                : await userManager.FindByNameAsync(recover);
            if (user == null)
            {
                logger.LogError("{Key} is set, but no user has that email address or user name. Nobody was made " +
                                "an administrator. Remove the key or correct it: while it is set, an account " +
                                "created with that name later becomes an administrator on the next start", key);
                return;
            }

            if (!await roleManager.RoleExistsAsync(Constants.Roles.AdminRoleName))
            {
                await roleManager.CreateAsync(new Role { Name = Constants.Roles.AdminRoleName });
            }

            // The owner of the site named this account: whatever stands between it and signing in goes
            // (deleted, locked out, an address that was never confirmed on a site that requires it)
            var changed = false;
            if (user.Deleted)
            {
                user.Deleted = false;
                changed = true;
            }

            if (user.LockoutEnd != null || user.AccessFailedCount != 0)
            {
                user.LockoutEnd = null;
                user.AccessFailedCount = 0;
                changed = true;
            }

            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                changed = true;
            }

            if (changed)
            {
                var updated = await userManager.UpdateAsync(user);
                if (!updated.Succeeded)
                {
                    logger.LogError("{Key}: could not restore {UserName}: {Errors}", key, user.UserName,
                        string.Join("; ", updated.Errors.Select(e => e.Description)));
                    return;
                }
            }

            if (!await userManager.IsInRoleAsync(user, Constants.Roles.AdminRoleName))
            {
                var added = await userManager.AddToRoleAsync(user, Constants.Roles.AdminRoleName);
                if (!added.Succeeded)
                {
                    logger.LogError("{Key}: could not make {UserName} an administrator: {Errors}", key,
                        user.UserName, string.Join("; ", added.Errors.Select(e => e.Description)));
                    return;
                }

                changed = true;
            }

            if (changed)
            {
                scope.ServiceProvider.GetRequiredService<Shared.Services.ICacheService>()
                    .ClearCachedItemsWithPrefix(nameof(User));
            }

            logger.LogWarning("{Key} is set: {UserName} is an administrator. Remove the key from the site's " +
                              "configuration now; while it is there, this user is restored on every start",
                key, user.UserName);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "{Key} could not be applied", key);
        }
    }

    // The innermost message is the one that says what is wrong ("password authentication failed", …)
    private static string Describe(Exception exception)
    {
        var inner = exception;
        while (inner.InnerException != null)
        {
            inner = inner.InnerException;
        }

        return inner.Message;
    }
}
