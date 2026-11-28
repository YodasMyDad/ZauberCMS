using System.Reflection;
using ImageResize.Core.Configuration;
using ImageResize.Core.Extensions;
using ImageResize.Core.Interfaces;
using ImageResize.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using ZauberCMS.Core.Membership.Middleware;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Data.Interfaces;
using ZauberCMS.Core.Email;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Installer;
using ZauberCMS.Core.Installer.Interfaces;
using ZauberCMS.Core.Installer.Middleware;
using ZauberCMS.Core.Installer.Services;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Services;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Membership.Services;
using ZauberCMS.Core.Media;
using ZauberCMS.Core.Media.Interfaces;
using ZauberCMS.Core.Media.Services;
using ZauberCMS.Core.Languages.Interfaces;
using ZauberCMS.Core.Languages.Services;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Tags.Interfaces;
using ZauberCMS.Core.Tags.Services;
using ZauberCMS.Core.Audit.Interfaces;
using ZauberCMS.Core.Audit.Services;
using ZauberCMS.Core.Security.Interfaces;
using ZauberCMS.Core.Security.Services;
using ZauberCMS.Core.Seo;
using ZauberCMS.Core.Seo.Interfaces;
using ZauberCMS.Core.Seo.Services;
using ZauberCMS.Core.Data.Services;
using ZauberCMS.Core.Email.Interfaces;
using ZauberCMS.Core.Email.Services;
using ZauberCMS.Core.Jobs;
using ZauberCMS.Core.Languages.Parameters;
using ZauberCMS.Core.Media.Middleware;
using ZauberCMS.Core.Membership;
using ZauberCMS.Core.Middleware;
using ZauberCMS.Core.Membership.Claims;
using Microsoft.AspNetCore.Authorization;
using ZauberCMS.Core.Sections.Authorization;
using ZauberCMS.Core.Sections.Interfaces;
using ZauberCMS.Core.Sections.Services;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Plugins.Interfaces;
using ZauberCMS.Core.Providers;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Shared;
using ZauberCMS.Core.Shared.Services;
using ZauberCMS.RTE.Services;

namespace ZauberCMS.Core;

public static class ZauberSetup
{
    /// <summary>The rate limit policy of the registration page: 5 posts per address in 10 minutes.</summary>
    public const string RegisterRateLimitPolicy = "register";

    public static void AddZauberCms(this WebApplicationBuilder builder, params Assembly[] additionalAssemblies)
    {
        builder.Host.UseSerilog((context, configuration) =>
            configuration.ReadFrom.Configuration(context.Configuration));

        // Enable static web assets from NuGet RCL packages for Kestrel
        builder.WebHost.UseStaticWebAssets();

        // What the bootstrap finds before logging exists; AddZauberCms(app) writes it to the log (ZA-120)
        var startupMessages = new StartupMessages();
        builder.Services.AddSingleton(startupMessages);

        // The Zauber section, read the same way here and for IOptions<ZauberSettings>: a list the configuration sets
        // replaces its default instead of being added to it (ZA-167). Settings that would fail at the first request
        // that needs them fail at start-up instead (ZA-120, D48).
        var zauberSection = builder.Configuration.GetSection(Constants.SettingsConfigName);
        var zauberSettings = ZauberSettingsBinding.Read(zauberSection);
        builder.Services.AddOptions<ZauberSettings>()
            .Bind(zauberSection)
            .Configure(settings => ZauberSettingsBinding.ReplaceConfiguredLists(settings, zauberSection))
            .ValidateOnStart();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<ZauberSettings>, ZauberSettingsValidator>());

        // The copy read above decides only what is registered (the database, Redis on or off), so those come from
        // configuration. Everything else is read from IOptions<ZauberSettings> where it is used, also the options
        // of the libraries ZauberCMS sets up (OptionsFromZauberSettings), so settings changed in code count; a code
        // change to one of the first kind is a start-up warning (ZA-192, Roadmap D81).
        builder.Services.AddSingleton(new ComposedZauberSettings(zauberSettings));

        // ImageResize, set from Zauber:ImageResize
        builder.Services.AddImageResize();
        builder.Services.ConfigureOptions<ImageResizeOptionsFromZauberSettings>();
        // The resizer keeps each image's copies in a folder of their own, so a permanent delete removes them (D63).
        // Only the resizer's cache is wrapped: the registered IImageCache stays ImageResize's (or the site's), so
        // its start-up prune still runs.
        builder.Services.Replace(ServiceDescriptor.Singleton<IImageResizerService>(sp =>
            ActivatorUtilities.CreateInstance<ImageResizerService>(sp,
                new ResizedImageCache(sp.GetRequiredService<IImageCache>(),
                    sp.GetRequiredService<IOptions<ImageResizeOptions>>()))));

        // IHttpClientFactory for sites and plugins; an injected HttpClient is the factory's default client. ZauberCMS's
        // own scoped HttpClient is gone (ZA-121): its base address was the request's (from the Host header the sender
        // controls) and it failed outside a request.
        builder.Services.AddHttpClient();
        builder.Services.AddSeoHttpClient();

        builder.Services.AddCascadingAuthenticationState();
#pragma warning disable CS0618 // obsolete, replaced by ICurrentUserAccessor; registered until it is removed (ZA-121)
        builder.Services.AddScoped<IdentityUserAccessor>();
#pragma warning restore CS0618
        builder.Services.AddScoped<IdentityRedirectManager>();
        builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
        // Who a service call runs for, in a circuit, a static page, a controller or a job (ZA-093); replaceable
        builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();

        // Register Service Interfaces and Implementations
        builder.Services.AddScoped<IContentService, ContentService>();
        builder.Services.AddScoped<IContentVersioningService, ContentVersioningService>();
        builder.Services.AddScoped<IMembershipService, MembershipService>();
        builder.Services.AddScoped<IMediaService, MediaService>();
        builder.Services.AddScoped<ILanguageService, LanguageService>();
        builder.Services.AddScoped<ITagService, TagService>();
        builder.Services.AddScoped<IAuditService, AuditService>();
        builder.Services.AddScoped<ISeoService, SeoService>();
        builder.Services.AddScoped<IDataService, DataService>();
        builder.Services.AddScoped<IEmailService, EmailService>();
        // API keys by name, from configuration and Global Settings (Roadmap D73); replaceable
        builder.Services.AddScoped<ZauberCMS.Core.Settings.Interfaces.IApiKeyService, ZauberCMS.Core.Settings.Services.ApiKeyService>();

        builder.Services.AddHostedService<DailyJob>();

        // Redis on or off is decided here, from configuration; its address is read from the options (D81)
        if (!zauberSettings.RedisConnectionString.IsNullOrWhiteSpace())
        {
            builder.Services.AddStackExchangeRedisCache(_ => { });
            builder.Services.ConfigureOptions<RedisCacheOptionsFromZauberSettings>();
        }

        // A provider that is named but unknown is a configuration mistake. A blank one (or a blank connection
        // string) means the site has no database yet: it starts, and the installer asks for one.
        if (!zauberSettings.DatabaseProvider.IsNullOrWhiteSpace() &&
            DatabaseProviders.Normalise(zauberSettings.DatabaseProvider) == null)
        {
            throw new InvalidOperationException(
                $"Zauber:DatabaseProvider is '{zauberSettings.DatabaseProvider}', which is not a database provider " +
                $"ZauberCMS knows. Use one of: {string.Join(", ", DatabaseProviders.All)}; or leave it and " +
                "Zauber:ConnectionString empty to set the database up in the installer.");
        }

        var installState = new InstallState(builder.Configuration);
        if (installState.IsDatabaseConfigured)
        {
            // Only this provider's services are registered below, so it is the provider until the next start
            installState.FixProvider();
        }

        builder.Services.AddSingleton(installState);
        builder.Services.AddScoped<IInstallService, InstallService>();
        builder.Services.AddSingleton<IFormProtectionService, FormProtectionService>();

        var identityBuilder = builder.Services.AddIdentityCore<User>(options =>
        {
            // Lockout settings.
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            options.Lockout.MaxFailedAccessAttempts = 6;
            options.Lockout.AllowedForNewUsers = true;

            // User settings.
            options.User.RequireUniqueEmail = true;
            options.User.AllowedUserNameCharacters += " ";
        });
        // The password rules and confirmed-account sign-in, from Zauber:Identity (D81)
        builder.Services.ConfigureOptions<IdentityOptionsFromZauberSettings>();

        identityBuilder.AddRoles<Role>();

        // A site with a database registers that provider's DbContext only, as it always has. A site without one
        // registers all three, because the installer's database step decides which is used.
        var providersToRegister = installState.IsDatabaseConfigured
            ? [installState.DatabaseProvider!]
            : DatabaseProviders.All;
        foreach (var provider in providersToRegister)
        {
            switch (provider)
            {
                case DatabaseProviders.Sqlite:
                    builder.Services.AddDbContext<SqliteZauberDbContext>();
                    break;
                case DatabaseProviders.PostgreSql:
                    builder.Services.AddDbContext<PostgreSqlZauberDbContext>();
                    break;
                default:
                    builder.Services.AddDbContext<ZauberDbContext>();
                    break;
            }
        }

        // The provider is read when a scope first asks, so the database the installer has just written into
        // appsettings.json is used without a restart. The services and the Identity stores share the scope's one
        // context (the AddDbContext instance), so one SaveChanges or transaction covers both (ZA-011).
        builder.Services.AddScoped(sp => (IZauberDbContext)sp.GetRequiredService(ActiveContextType(sp)));
        builder.Services.AddScoped(sp => (IUserStore<User>)ActivatorUtilities.CreateInstance(sp,
            typeof(UserStore<,,,,,,,,>).MakeGenericType(typeof(User), typeof(Role), ActiveContextType(sp), typeof(Guid),
                typeof(UserClaim), typeof(UserRole), typeof(UserLogin), typeof(UserToken), typeof(RoleClaim))));
        builder.Services.AddScoped(sp => (IRoleStore<Role>)ActivatorUtilities.CreateInstance(sp,
            typeof(RoleStore<,,,,>).MakeGenericType(typeof(Role), ActiveContextType(sp), typeof(Guid),
                typeof(UserRole), typeof(RoleClaim))));

        identityBuilder
            .AddClaimsPrincipalFactory<ZauberUserClaimsPrincipalFactory>()
            // SignInManager<User> is ZauberSignInManager (also resolvable as itself); registered once (ZA-121)
            .AddSignInManager<ZauberSignInManager>()
            .AddDefaultTokenProviders();

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddAntiforgery();
        
        // Rate limiting for the sign-in and registration pages: the "login" and "register" policies, from
        // Zauber:Identity:LoginRateLimit / RegisterRateLimit (ZA-149, D81)
        builder.Services.AddRateLimiter(_ => { });
        builder.Services.ConfigureOptions<RateLimiterOptionsFromZauberSettings>();

        builder.Services.AddScoped<ExtensionManager>();
        builder.Services.AddScoped<ProviderService>();
        // Cache (Roadmap D13): values stay in each server's memory; with Redis configured, only clears travel.
        // IMemoryCache (ServerCache, confirmation re-sends, form tokens) and a memory IDistributedCache (for a site's
        // own session state) came with the MVC view services until ZA-090 removed those; they are registered here.
        // With Redis configured, AddStackExchangeRedisCache above has already registered IDistributedCache.
        builder.Services.AddMemoryCache();
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddSingleton<ServerCache>();
        if (zauberSettings.RedisConnectionString.IsNullOrWhiteSpace())
        {
            builder.Services.AddSingleton<ICacheInvalidationBus, NoCacheInvalidationBus>();
        }
        else
        {
            builder.Services.AddSingleton<ICacheInvalidationBus, RedisCacheInvalidationBus>();
            builder.Services.AddHostedService<CacheInvalidationStartup>();
        }
        builder.Services.AddScoped<ICacheService, DefaultCacheService>();
        builder.Services.AddScoped<IHtmlSanitizerService, DefaultHtmlSanitizerService>();
        builder.Services.AddSingleton<IRichTextEmbedService, DefaultRichTextEmbedService>();
        builder.Services.AddScoped<Microsoft.AspNetCore.Components.Server.Circuits.CircuitHandler, Languages.AdminCultureCircuitHandler>();
        builder.Services.AddScoped<Microsoft.AspNetCore.Components.Server.Circuits.CircuitHandler, Languages.ContentCultureCircuitHandler>();
        builder.Services.AddSingleton<RefreshSignInTicketService>();
        // How long a cookie is trusted before its security stamp is compared with the database again. The
        // framework default is 30 minutes; a removed user or a reset password should end a session sooner.
        builder.Services.Configure<SecurityStampValidatorOptions>(options =>
            options.ValidationInterval = TimeSpan.FromMinutes(5));
        builder.Services.AddScoped<IEmailSender<User>, IdentityEmailSender>();
        builder.Services.AddScoped<TreeState>();

        builder.Services.AddSingleton<LayoutResolverService>();
        builder.Services.AddSingleton<AppState>();

        // Add Authentication
        var authBuilder = builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
        });
        authBuilder.AddIdentityCookies();

        // Back-office access per section and role (Roadmap D38, D66-D68): the section policies are built by name
        // (SectionPolicies, [AuthorizeSection]), so a plugin's section needs no registration; the decision is
        // IBackOfficeAccessService's, which a site can replace.
        builder.Services.AddAuthorization();
        builder.Services.Replace(ServiceDescriptor.Singleton<IAuthorizationPolicyProvider, SectionAuthorizationPolicyProvider>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IAuthorizationHandler, SectionAccessHandler>());
        builder.Services.AddScoped<IBackOfficeAccessService, BackOfficeAccessService>();
        
        
        // Build explicit assembly list - only scan assemblies we explicitly register
        var assembliesToScan = new List<Assembly>
        {
            // Core Zauber assemblies
            typeof(ZauberSetup).Assembly, // ZauberCMS.Core (using typeof since it's this assembly)
            Assembly.Load(new AssemblyName("ZauberCMS.Components")), // Required
        };

        // Add entry assembly (the host application)
        var entryAssembly = Assembly.GetEntryAssembly();
        if (entryAssembly != null && !assembliesToScan.Contains(entryAssembly))
        {
            assembliesToScan.Add(entryAssembly);
        }

        // Add any additional assemblies passed by the user
        if (additionalAssemblies.Length > 0)
        {
            assembliesToScan.AddRange(additionalAssemblies.Where(a => !assembliesToScan.Contains(a)));
        }

        // Add Routing last (critical for proper component discovery order - required for Blazor routing)
        var zauberRouting = Assembly.Load(new AssemblyName("ZauberCMS.Routing"));
        if (!assembliesToScan.Contains(zauberRouting))
        {
            assembliesToScan.Add(zauberRouting);
        }

        var discoverAssemblies = assembliesToScan.ToArray();
        var zauberAssemblies = new[] { typeof(ZauberSetup).Assembly, assembliesToScan[1], zauberRouting };

        // A site or plugin assembly whose public types cannot load (built against another ZauberCMS version) would
        // make discovery and Blazor's router throw at every use: stop here, naming it (ZA-176, D48)
        foreach (var assembly in discoverAssemblies.Except(zauberAssemblies))
        {
            ExtensionManager.EnsurePublicTypesLoad(assembly);
        }

        AssemblyManager.SetAssemblies(discoverAssemblies);

        // An extension type in the site or a plugin that is not public is never discovered: say so (ZA-120)
        ExtensionManager.ReportHiddenExtensionTypes(discoverAssemblies.Except(zauberAssemblies), startupMessages);

        // Add Zauber RTE services
        // RTE 3.x no longer scans assemblies by itself: register every IToolbarItem found in the
        // discovery assemblies so the CMS items and plugin items keep appearing.
        builder.Services.AddZauberRte(options =>
        {
            foreach (var assembly in discoverAssemblies)
            {
                options.AddToolbarItemsFromAssembly(assembly);
            }
        });

        // Detailed errors (Zauber:ShowDetailedErrors) are clamped to non-Production environments regardless of the
        // setting — leaking exception messages / stack traces / SQL parameter values to an end-user browser is
        // exactly the misconfiguration that turns a low-severity bug into an information-disclosure CVE. Operators
        // who genuinely need them in Production must set ASPNETCORE_ENVIRONMENT=Development. Read from the options
        // (D81). The database error page is the developer exception page's, which only Development shows.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();
        builder.Services.ConfigureOptions<DetailedErrorsFromZauberSettings>();
        if (builder.Environment.IsDevelopment())
        {
            builder.Services.AddDatabaseDeveloperPageExceptionFilter();
        }

        // Controllers (the admin's AuthController, and a site's or plugin's own). ZauberCMS renders no MVC views
        // (ZA-090), but MVC's view features are registered (no Razor view engine): the antiforgery attributes
        // ([ValidateAntiForgeryToken], [AutoValidateAntiforgeryToken], [IgnoreAntiforgeryToken]) are filters that
        // come with them, and without them such an action answers 500 (ZA-185, Roadmap D60). A site with its own
        // Razor views calls AddControllersWithViews itself.
        builder.Services.AddControllers();
        builder.Services.AddMvcCore().AddViews();
        


        // Start up items - bootstrap interfaces are discovered without DI so we don't have
        // to BuildServiceProvider() before the host container is built (avoids ASP0000 and
        // a parallel singleton container).
        foreach (var startUpItem in ExtensionManager.GetBootstrapInstances<IStartupPlugin>(startupMessages))
        {
            startUpItem.Register(builder.Services, builder.Configuration);
        }

        // Add external authentication providers
        foreach (var provider in ExtensionManager.GetBootstrapInstances<IExternalAuthenticationProvider>(startupMessages))
        {
            provider.Add(builder.Services, authBuilder, builder.Configuration);
        }

        // Localisation, and ZauberCMS's own text from the scanned assemblies' ZauberText resources (D37, D90)
        builder.Services.AddZauberText(discoverAssemblies, zauberAssemblies);
    }

    // The DbContext for the database this site uses now. Fails with a readable message while there is none.
    private static Type ActiveContextType(IServiceProvider services)
    {
        var installState = services.GetRequiredService<InstallState>();
        if (!installState.IsDatabaseConfigured)
        {
            throw new InvalidOperationException(
                "This site has no database yet. Open " + InstallerMiddleware.Path + " in a browser to set one up, " +
                "or set Zauber:DatabaseProvider and Zauber:ConnectionString.");
        }

        return DatabaseProviders.ContextType(installState.DatabaseProvider);
    }

    /// <summary>Builds the forwarded-headers options from <c>Zauber:ForwardedHeaders</c>.</summary>
    /// <exception cref="InvalidOperationException">An entry is not an IP address or a network in CIDR form.</exception>
    public static ForwardedHeadersOptions BuildForwardedHeadersOptions(ForwardedHeadersSettings settings)
    {
        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        };

        foreach (var proxy in settings.KnownProxies ?? [])
        {
            if (!IPAddress.TryParse(proxy, out var address))
            {
                throw new InvalidOperationException(
                    $"Zauber:ForwardedHeaders:KnownProxies contains '{proxy}', which is not an IP address.");
            }

            options.KnownProxies.Add(address);
        }

        foreach (var network in settings.KnownNetworks ?? [])
        {
            if (!System.Net.IPNetwork.TryParse(network, out var parsed))
            {
                throw new InvalidOperationException(
                    $"Zauber:ForwardedHeaders:KnownNetworks contains '{network}', which is not a network such as 10.0.0.0/8.");
            }

            options.KnownIPNetworks.Add(parsed);
        }

        return options;
    }

    /// <summary>
    /// Logs, as warnings of category <c>ZauberCMS.Startup</c>, what the bootstrap skipped before logging existed and
    /// the settings that do nothing as written (ZA-120).
    /// </summary>
    internal static void WriteStartupWarnings(IServiceProvider services, ZauberSettings zauberSettings)
    {
        var startupMessages = services.GetRequiredService<StartupMessages>();
        using (var scope = services.CreateScope())
        {
            var extensionManager = scope.ServiceProvider.GetRequiredService<ExtensionManager>();
            foreach (var warning in ZauberSettingsValidator.FindWarnings(zauberSettings, extensionManager))
            {
                startupMessages.Warning(warning);
            }
        }

        // A setting that decides what is registered, changed where it no longer applies (D81, ZA-231)
        if (services.GetService<ComposedZauberSettings>() is { } composed)
        {
            foreach (var warning in ZauberSettingsValidator.FindChangesThatDoNotApply(composed.Settings,
                         ConfiguredSettings(services), zauberSettings))
            {
                startupMessages.Warning(warning);
            }
        }

        startupMessages.WriteTo(services.GetRequiredService<ILoggerFactory>().CreateLogger("ZauberCMS.Startup"));
    }

    /// <summary>The <c>Zauber</c> section as configuration has it now, every source included (no code changes).</summary>
    private static ZauberSettings ConfiguredSettings(IServiceProvider services) =>
        ZauberSettingsBinding.Read(services.GetRequiredService<IConfiguration>().GetSection(Constants.SettingsConfigName));

    /// <summary>
    /// Writes what the bootstrap collected, then reads (and so validates) the settings and writes their warnings. A
    /// setting that cannot work is logged as critical (category <c>ZauberCMS.Startup</c>) and rethrown, so the
    /// reason reaches the site's log and not only the console (ZA-176).
    /// </summary>
    internal static ZauberSettings ReadSettingsAndWriteStartupWarnings(IServiceProvider services)
    {
        // First: what the bootstrap skipped often explains a failing setting (a provider that is not public)
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("ZauberCMS.Startup");
        services.GetRequiredService<StartupMessages>().WriteTo(logger);

        ZauberSettings zauberSettings;
        try
        {
            zauberSettings = services.GetRequiredService<IOptions<ZauberSettings>>().Value;
            if (services.GetService<ComposedZauberSettings>() is { } composed &&
                ZauberSettingsValidator.ChangeThatCannotWork(composed.Settings, ConfiguredSettings(services),
                    zauberSettings) is { } failure)
            {
                throw new InvalidOperationException(failure);
            }
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "ZauberCMS cannot start: {Reason}", ex.Message);
            throw;
        }

        WriteStartupWarnings(services, zauberSettings);
        return zauberSettings;
    }

    public static void AddZauberCms<T>(this WebApplication app)
    {
        // Validated here (ZauberSettingsValidator): a setting that cannot work stops the site before it serves anything
        var zauberSettings = ReadSettingsAndWriteStartupWarnings(app.Services);

        // First in the pipeline: everything after it (HTTPS redirect, rate limiter, admin IP allow-list, links)
        // must see the visitor's address and scheme, not the proxy's. Off unless Zauber:ForwardedHeaders:Enabled.
        if (zauberSettings.ForwardedHeaders.Enabled)
        {
            app.UseForwardedHeaders(BuildForwardedHeadersOptions(zauberSettings.ForwardedHeaders));
        }

        // The error page (ZA-098, D33), in front of everything else so it catches what they throw. Development
        // keeps the developer exception page. The built-in page answers when the site's own page cannot.
        if (!app.Environment.IsDevelopment() && !string.IsNullOrWhiteSpace(zauberSettings.ErrorPagePath))
        {
            app.UseMiddleware<ErrorPageMiddleware>();
            app.UseExceptionHandler(new ExceptionHandlerOptions
            {
                ExceptionHandlingPath = "/" + zauberSettings.ErrorPagePath.Trim().TrimStart('/'),
                CreateScopeForErrors = true
            });

            // The error page is shown as a GET: a form post that failed must not be handed to the error page as a
            // form (Blazor would look for the form there and answer 400)
            app.Use((context, next) =>
            {
                if (context.Features.Get<IExceptionHandlerFeature>() != null &&
                    !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
                {
                    context.Request.Method = HttpMethods.Get;
                }

                return next(context);
            });
        }

        if (!app.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(zauberSettings.SiteUrl) &&
            app.Configuration["AllowedHosts"] is null or "" or "*")
        {
            Log.Warning("Zauber:SiteUrl is not set and AllowedHosts accepts any host. Links in emails (password " +
                        "reset, email confirmation) are built from the request's Host header, which the sender " +
                        "controls. Set Zauber:SiteUrl to the public address of the site");
        }

        // en-US must be the default culture as that's what the backoffice resource is
        var supportedCultures = new List<string> { zauberSettings.AdminDefaultLanguage };

        var installState = app.Services.GetRequiredService<InstallState>();
        if (installState.IsDatabaseConfigured)
        {
            using var scope = app.Services.CreateScope();

            // Migrations (must succeed; the exception stops the host so it never runs half-migrated), seed
            // data, whether the site is installed, and the admin recovery key.
            scope.ServiceProvider.GetRequiredService<IInstallService>()
                .InitialiseDatabaseAsync().GetAwaiter().GetResult();

            // Localization — must succeed.
            var languageService = scope.ServiceProvider.GetRequiredService<ILanguageService>();
            var langs = languageService.QueryLanguageAsync(new QueryLanguageParameters { AmountPerPage = 200 }).GetAwaiter().GetResult();
            foreach (var langsItem in langs.Items)
            {
                if (langsItem.LanguageIsoCode != null) supportedCultures.Add(langsItem.LanguageIsoCode);
            }
        }
        else
        {
            // Error, not Warning: for a new site this is the expected first start, but for a site that was
            // working it means its database settings are gone (a deployment that overwrote appsettings.json,
            // a missing environment variable) and the public now sees the installer.
            Log.Error("This site has no database (Zauber:DatabaseProvider / Zauber:ConnectionString are empty). " +
                      "Every request is sent to the installer at {Path}, and the first visitor to finish it " +
                      "becomes the administrator. A new site: install it before making it public. A site that " +
                      "was installed before: put its database settings back and restart",
                InstallerMiddleware.Path);
        }

        var supportedCulturesArray = supportedCultures.Distinct().ToArray();
        var localizationOptions = new RequestLocalizationOptions()
            .SetDefaultCulture(zauberSettings.AdminDefaultLanguage)
            .AddSupportedCultures(supportedCulturesArray)
            .AddSupportedUICultures(supportedCulturesArray);
        app.UseRequestLocalization(localizationOptions);

        app.UseSerilogRequestLogging();
        app.UseHttpsRedirection();

        // The installer. Until the site is installed it answers every request itself, so nothing below runs
        // without a database or an administrator; afterwards it passes everything through. In front of
        // authentication because a site with no database cannot validate a sign-in cookie.
        app.UseMiddleware<InstallerMiddleware>();

        app.UseAuthentication();

        // Adds nosniff (and a sandboxing CSP for SVG) to responses from the upload folder. It only registers a
        // callback, so it sits in front of the three middleware that can serve those files.
        app.UseMiddleware<MediaSecurityHeadersMiddleware>();

        // Our middleware must run before ImageResize to check restricted media
        app.UseMiddleware<RestrictedMediaMiddleware>();

        // ImageResize must run before UseStaticFiles to intercept image requests from media folders
        app.UseImageResize();

        // Serve static files before routing to prevent catch-all route from intercepting non-image media files
        app.UseStaticFiles();

        app.UseRouting();
        
        // Redirect middleware must run after routing to process SEO redirects with proper HTTP status codes
        app.UseMiddleware<RedirectMiddleware>();
        
        // Culture middleware must run after routing so we have access to route values
        app.UseMiddleware<CultureMiddleware>();
        
        // Admin IP allow-list and fail-closed authorisation for /admin/**. Needs the signed-in user
        // (UseAuthentication, above) and the matched endpoint (UseRouting), and must refuse before authorisation
        // lets a page without [Authorize] through.
        app.UseMiddleware<AdminAccessMiddleware>();

        app.UseRateLimiter();

        app.UseAuthorization();

        app.UseAntiforgery();
        app.MapControllers();

        app.MapStaticAssets();
        
        app.MapRazorComponents<T>()
            .AddInteractiveServerRenderMode(o => o.ContentSecurityFrameAncestorsPolicy = "'none'")
            .AddAdditionalAssemblies(ExtensionManager.GetFilteredAssemblies(null).ToArray()!);

        // Add additional endpoints required by the Identity /Account Razor components.
        app.MapAdditionalIdentityEndpoints();
        
    }
}