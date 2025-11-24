using System.Reflection;
using System.Threading.RateLimiting;
using Blazored.Modal;
using ImageResize.Core.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Radzen;
using Serilog;
using ZauberCMS.Core.Content.ContentFinders;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Data.Interfaces;
using ZauberCMS.Core.Email;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Services;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Membership.Services;
using ZauberCMS.Core.Media.Interfaces;
using ZauberCMS.Core.Media.Services;
using ZauberCMS.Core.Languages.Interfaces;
using ZauberCMS.Core.Languages.Services;
using ZauberCMS.Core.Tags.Interfaces;
using ZauberCMS.Core.Tags.Services;
using ZauberCMS.Core.Audit.Interfaces;
using ZauberCMS.Core.Audit.Services;
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
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Membership.Stores;
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
    public static void AddZauberCms(this WebApplicationBuilder builder, params Assembly[] additionalAssemblies)
    {
        builder.Host.UseSerilog((context, configuration) =>
            configuration.ReadFrom.Configuration(context.Configuration));

        // Enable static web assets from NuGet RCL packages for Kestrel
        builder.WebHost.UseStaticWebAssets();

        // Bind configuration to ZauberSettings instance
        var zauberSettings = new ZauberSettings();
        builder.Configuration.GetSection(Constants.SettingsConfigName).Bind(zauberSettings);
        builder.Services.Configure<ZauberSettings>(builder.Configuration.GetSection(Constants.SettingsConfigName));

        // Configure ImageResize with settings from ZauberSettings
        builder.Services.AddImageResize(o =>
        {
            o.EnableMiddleware = zauberSettings.ImageResize.EnableMiddleware;
            o.ContentRoots = zauberSettings.ImageResize.ContentRoots;
            o.WebRoot = zauberSettings.ImageResize.WebRoot ?? builder.Environment.WebRootPath;
            o.CacheRoot = zauberSettings.ImageResize.CacheRoot ?? Path.Combine(builder.Environment.WebRootPath, "_mediacache");
            o.AllowUpscale = zauberSettings.ImageResize.AllowUpscale;
            o.DefaultQuality = zauberSettings.ImageResize.DefaultQuality;
            o.PngCompressionLevel = zauberSettings.ImageResize.PngCompressionLevel;
            o.HashOriginalContent = zauberSettings.ImageResize.HashOriginalContent;
            o.Cache.FolderSharding = zauberSettings.ImageResize.Cache.FolderSharding;
            o.Cache.PruneOnStartup = zauberSettings.ImageResize.Cache.PruneOnStartup;
            o.Cache.MaxCacheBytes = zauberSettings.ImageResize.Cache.MaxCacheBytes;
            o.ResponseCache.ClientCacheSeconds = zauberSettings.ImageResize.ResponseCache.ClientCacheSeconds;
            o.ResponseCache.SendETag = zauberSettings.ImageResize.ResponseCache.SendETag;
            o.ResponseCache.SendLastModified = zauberSettings.ImageResize.ResponseCache.SendLastModified;
        });

        builder.Services.AddHttpClient();

        builder.Services.AddScoped(sp =>
        {
            var navigationManager = sp.GetRequiredService<NavigationManager>();
            return new HttpClient { BaseAddress = new Uri(navigationManager.BaseUri) };
        });

        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddScoped<IdentityUserAccessor>();
        builder.Services.AddScoped<IdentityRedirectManager>();
        builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
        
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
        
        builder.Services.AddScoped<ZauberRouteValueTransformer>();
        builder.Services.AddScoped<MediaValidationService>();

        builder.Services.AddHostedService<DailyJob>();
        
        builder.Services.AddRadzenComponents();

        if (!zauberSettings.RedisConnectionString.IsNullOrWhiteSpace())
        {
            builder.Services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = zauberSettings.RedisConnectionString;
            });
        }

        var databaseProvider = zauberSettings.DatabaseProvider;
        if (databaseProvider != null)
        {
            var identityBuilder = builder.Services.AddIdentityCore<User>(options =>
            {
                {
                    // Password settings.
                    options.Password.RequireDigit = zauberSettings.Identity.PasswordRequireDigit;
                    options.Password.RequireLowercase = zauberSettings.Identity.PasswordRequireLowercase;
                    options.Password.RequireNonAlphanumeric = zauberSettings.Identity.PasswordRequireNonAlphanumeric;
                    options.Password.RequireUppercase = zauberSettings.Identity.PasswordRequireUppercase;
                    options.Password.RequiredLength = zauberSettings.Identity.PasswordRequiredLength;
                    options.Password.RequiredUniqueChars = zauberSettings.Identity.PasswordRequiredUniqueChars;

                    // Lockout settings.
                    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                    options.Lockout.MaxFailedAccessAttempts = 6;
                    options.Lockout.AllowedForNewUsers = true;

                    // User settings.
                    options.User.RequireUniqueEmail = true;
                    options.User.AllowedUserNameCharacters += " ";

                    // Email
                    options.SignIn.RequireConfirmedAccount = zauberSettings.Identity.SignInRequireConfirmedAccount;
                }
            });
            
            identityBuilder.AddRoles<Role>();
            
            switch (databaseProvider.ToLower())
            {
                case "sqlite":
                    builder.Services.AddDbContext<SqliteZauberDbContext>();
                    builder.Services.AddScoped<IZauberDbContext, SqliteZauberDbContext>();
                    identityBuilder
                        .AddEntityFrameworkStores<SqliteZauberDbContext>()
                        .AddUserStore<UserStore<User, Role, SqliteZauberDbContext, Guid, UserClaim, UserRole, UserLogin, UserToken, RoleClaim>>()
                        .AddRoleStore<RoleStore<Role, SqliteZauberDbContext, Guid, UserRole, RoleClaim>>();
                    break;
                case "postgresql":
                    builder.Services.AddDbContext<PostgreSqlZauberDbContext>();
                    builder.Services.AddScoped<IZauberDbContext, PostgreSqlZauberDbContext>();
                    identityBuilder
                        .AddEntityFrameworkStores<PostgreSqlZauberDbContext>()
                        .AddUserStore<UserStore<User, Role, PostgreSqlZauberDbContext, Guid, UserClaim, UserRole, UserLogin, UserToken, RoleClaim>>()
                        .AddRoleStore<RoleStore<Role, PostgreSqlZauberDbContext, Guid, UserRole, RoleClaim>>();
                    break;
                case "sqlserver":
                    builder.Services.AddDbContext<ZauberDbContext>();
                    builder.Services.AddScoped<IZauberDbContext, ZauberDbContext>();
                    identityBuilder
                        .AddEntityFrameworkStores<ZauberDbContext>()
                        .AddUserStore<UserStore<User, Role, ZauberDbContext, Guid, UserClaim, UserRole, UserLogin, UserToken, RoleClaim>>()
                        .AddRoleStore<RoleStore<Role, ZauberDbContext, Guid, UserRole, RoleClaim>>();
                    break;
                default:
                    throw new Exception("Unable to find database provider in appSettings");
            }
            
            identityBuilder
                .AddClaimsPrincipalFactory<ZauberUserClaimsPrincipalFactory>()
                .AddSignInManager()
                .AddDefaultTokenProviders();

            builder.Services.AddScoped<IUserEmailStore<User>, UserEmailStore>();
        }
        else
        {
            throw new Exception("Unable to find database provider in appSettings");
        }

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddAntiforgery();
        builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        builder.Services.AddBlazoredModal();
        
        // Add rate limiting for authentication endpoints
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            
            // Sliding window rate limiter for login attempts
            options.AddPolicy("login", httpContext =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 10, // 10 attempts
                        Window = TimeSpan.FromMinutes(5), // per 5 minutes
                        SegmentsPerWindow = 5,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0 // No queueing
                    }));
        });

        builder.Services.AddScoped<ExtensionManager>();
        builder.Services.AddScoped<ProviderService>();
        builder.Services.AddScoped(typeof(ValidateService<>));
        builder.Services.AddScoped<ICacheService, DefaultCacheService>();
        builder.Services.AddScoped<IHtmlSanitizerService, DefaultHtmlSanitizerService>();
        builder.Services.AddScoped<SignInManager<User>, ZauberSignInManager>();
        builder.Services.AddScoped<IEmailSender<User>, IdentityEmailSender>();
        builder.Services.AddScoped<TreeState>();
        builder.Services.AddScoped<ContentFinderPipeline>();

        builder.Services.AddSingleton<LayoutResolverService>();
        builder.Services.AddSingleton<AppState>();

        // Add Authentication
        var authBuilder = builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
        });
        authBuilder.AddIdentityCookies();

        /*services.AddAuthorizationBuilder()
                    .AddPolicy("AdminOnly", policy => policy.RequireRole(Constants.Roles.AdminRoleName));*/
        
        
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
        AssemblyManager.SetAssemblies(discoverAssemblies);

        // Add Zauber RTE services
        builder.Services.AddZauberRte(discoverAssemblies);
        
        // Build the service provider and get the extension manager
        var serviceProvider = builder.Services.BuildServiceProvider();
        var extensionManager = serviceProvider.GetRequiredService<ExtensionManager>();
        
        // Detailed errors have been enabled
        if (zauberSettings.ShowDetailedErrors)
        {
            builder.Services
                .AddRazorComponents(c => c.DetailedErrors = true)
                .AddInteractiveServerComponents(c => c.DetailedErrors = true);
            builder.Services.AddDatabaseDeveloperPageExceptionFilter();
        }
        else
        {
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();
        }

        builder.Services.AddControllersWithViews()
            .AddRazorOptions(options =>
            {
                // This adds another search path that looks for views in the root Views folder.
                options.ViewLocationFormats.Add("/Views/{0}.cshtml");
            });
        


        // Start up items
        var startUpItems = extensionManager.GetInstances<IStartupPlugin>();
        foreach (var startUpItem in startUpItems)
        {
            startUpItem.Value.Register(builder.Services, builder.Configuration);
        }


        // Add external authentication providers
        foreach (var provider in extensionManager.GetInstances<IExternalAuthenticationProvider>())
        {
            provider.Value.Add(builder.Services, authBuilder, builder.Configuration);
        }

        // Add localization services
        builder.Services.AddLocalization(opts => { opts.ResourcesPath = "Resources"; });
    }

    public static void AddZauberCms<T>(this WebApplication app)
    {
        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
            var extensionManager = scope.ServiceProvider.GetRequiredService<ExtensionManager>();
            var languageService = scope.ServiceProvider.GetRequiredService<ILanguageService>();
            var settings = scope.ServiceProvider.GetRequiredService<IOptions<ZauberSettings>>();

            try
            {
                if (dbContext.Database.GetPendingMigrations().Any())
                {
                    dbContext.Database.Migrate();
                }

                // Get any seed data
                var seedData = extensionManager.GetInstances<ISeedData>();
                foreach (var data in seedData)
                {
                    data.Value.Initialise(dbContext);
                }

                // Is this ok to use the awaiter and result here?
                var langs = languageService.QueryLanguageAsync(new QueryLanguageParameters { AmountPerPage = 200 }).GetAwaiter().GetResult();

                // en-US must be the default culture as that's what the backoffice resource is
                var supportedCultures = new List<string> { settings.Value.AdminDefaultLanguage };

                foreach (var langsItem in langs.Items)
                {
                    if (langsItem.LanguageIsoCode != null) supportedCultures.Add(langsItem.LanguageIsoCode);
                }

                var supportedCulturesArray = supportedCultures.Distinct().ToArray();
                var localizationOptions = new RequestLocalizationOptions()
                    .SetDefaultCulture(settings.Value.AdminDefaultLanguage)
                    .AddSupportedCultures(supportedCulturesArray)
                    .AddSupportedUICultures(supportedCulturesArray);
                app.UseRequestLocalization(localizationOptions);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during startup trying to do Db migrations");
            }
        }

        app.UseSerilogRequestLogging();
        app.UseHttpsRedirection();

        app.UseAuthentication();

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
        
        //app.MapBlazorHub();
        
        //app.MapDynamicControllerRoute<ZauberRouteValueTransformer>("{**slug}");

        /*app.MapControllerRoute(
                name: "default",
                pattern: "{controller=ZauberRender}/{action=Index}/{id?}")
            .WithMetadata(new RouteOptions { LowercaseUrls = true }) // Lowercase URLs for better SEO
            .WithStaticAssets(); // Ensures static files load before hitting controllers; */

        //app.MapFallbackToController("Index", "ZauberRender");
    }
}