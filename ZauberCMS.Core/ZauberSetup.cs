using System.Reflection;
using Blazored.Modal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Radzen;
using Serilog;
using SixLabors.ImageSharp.Web.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Data.Interfaces;
using ZauberCMS.Core.Email;
using ZauberCMS.Core.Membership;
using ZauberCMS.Core.Membership.Claims;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Membership.Stores;
using ZauberCMS.Core.Plugins.Interfaces;
using ZauberCMS.Core.Providers;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Shared;
using ZauberCMS.Core.Shared.Services;

// ReSharper disable once CheckNamespace
namespace ZauberCMS.Core.Plugins;

public static class ZauberSetup
{
    public static void AddZauberCms<T>(this WebApplication app)
    {
        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
            var extensionManager = scope.ServiceProvider.GetRequiredService<ExtensionManager>();
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
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during startup trying to do Db migrations");
            }
        }
        
        app.UseImageSharp();
        app.UseSerilogRequestLogging();
        var supportedCultures = new[] { "en" };
        var localizationOptions = new RequestLocalizationOptions()
            .SetDefaultCulture(supportedCultures[0])
            .AddSupportedCultures(supportedCultures)
            .AddSupportedUICultures(supportedCultures);
        app.UseRequestLocalization(localizationOptions);
        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAntiforgery();
        app.MapControllers();

        // Add authentication and authorization middleware
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapRazorComponents<T>()
            .AddInteractiveServerRenderMode()
            .AddAdditionalAssemblies(ExtensionManager.GetFilteredAssemblies(null).ToArray()!);

        // Add additional endpoints required by the Identity /Account Razor components.
        app.MapAdditionalIdentityEndpoints();
    }

    public static void AddZauberCms(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, configuration) =>
            configuration.ReadFrom.Configuration(context.Configuration));

        builder.Services.AddControllers();

// Add services to the container.
#if DEBUG
        builder.Services
            .AddRazorComponents(c => c.DetailedErrors = true)
            .AddInteractiveServerComponents(c => c.DetailedErrors = true);
        builder.Services.AddDatabaseDeveloperPageExceptionFilter();
#else
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
#endif

        builder.Services.AddHttpClient();

        builder.Services.AddScoped(sp =>
        {
            var navigationManager = sp.GetRequiredService<NavigationManager>();
            return new HttpClient { BaseAddress = new Uri(navigationManager.BaseUri) };
        });

        builder.Services.AddCascadingAuthenticationState();
        
        // Bind configuration to ZauberSettings instance
        var zauberSettings = new ZauberSettings();
        builder.Configuration.GetSection(Constants.SettingsConfigName).Bind(zauberSettings);
        
        builder.Services.Configure<ZauberSettings>(builder.Configuration.GetSection(Constants.SettingsConfigName));
        builder.Services.AddScoped<IdentityUserAccessor>();
        builder.Services.AddScoped<IdentityRedirectManager>();
        builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

        builder.Services.AddRadzenComponents();

        var databaseProvider = zauberSettings.DatabaseProvider;
        if (databaseProvider != null)
        {
            switch (databaseProvider)
            {
                case "Sqlite":
                    builder.Services.AddDbContext<ZauberDbContext, SqliteZauberDbContext>();
                    break;
                case "SqlServer":
                    builder.Services.AddDbContext<ZauberDbContext>();
                    break;
            }

#if DEBUG
            builder.Services.AddDatabaseDeveloperPageExceptionFilter();
#endif
            
            builder.Services.AddIdentityCore<User>(options =>
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
                })
                .AddRoles<Role>()
                .AddEntityFrameworkStores<ZauberDbContext>()
                .AddUserStore<UserStore<User, Role, ZauberDbContext, Guid, UserClaim, UserRole, UserLogin, UserToken,
                    RoleClaim>>()
                .AddRoleStore<RoleStore<Role, ZauberDbContext, Guid, UserRole, RoleClaim>>()
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
        builder.Services.AddImageSharp();
        builder.Services.AddAntiforgery();
        builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        builder.Services.AddBlazoredModal();

        builder.Services.AddScoped<ExtensionManager>();
        builder.Services.AddScoped<ProviderService>();
        builder.Services.AddScoped(typeof(ValidateService<>));
        builder.Services.AddScoped<ICacheService, MemoryCacheService>();
        builder.Services.AddScoped<SignInManager<User>, ZauberSignInManager>();
        builder.Services.AddScoped<IEmailSender<User>, IdentityEmailSender>();
        builder.Services.AddScoped<TreeState>();

        builder.Services.AddSingleton<LayoutResolverService>();
        builder.Services.AddSingleton<AppState>();
        
        builder.Services.AddMvc();

        // Add Authentication
        var authBuilder = builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
        });
        authBuilder.AddIdentityCookies();

        /*services.AddAuthorizationBuilder()
                    .AddPolicy("AdminOnly", policy => policy.RequireRole(Constants.Roles.AdminRoleName));*/

        // Build the service provider and get the extension manager
        var serviceProvider = builder.Services.BuildServiceProvider();
        var extensionManager = serviceProvider.GetRequiredService<ExtensionManager>();

        // Plugins
        var assemblyProvider = new DefaultAssemblyProvider(serviceProvider);
        var assemblies = assemblyProvider.GetAssemblies();
        Assembly[] discoverAssemblies = (assemblies as Assembly[] ?? assemblies.ToArray())!;
        AssemblyManager.SetAssemblies(discoverAssemblies);

        // Mediatr
        builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(discoverAssemblies));

        // Automapper
        builder.Services.AddAutoMapper(discoverAssemblies);

        // Start up items
        var startUpItems = extensionManager?.GetInstances<IStartupPlugin>();
        if (startUpItems != null)
        {
            foreach (var startUpItem in startUpItems)
            {
                startUpItem.Value.Register(builder.Services, builder.Configuration);
            }
        }

        // Add external authentication providers
        foreach (var provider in extensionManager?.GetInstances<IExternalAuthenticationProvider>()!)
        {
            provider.Value.Add(builder.Services, authBuilder, builder.Configuration);
        }

        // Add localization services
        builder.Services.AddLocalization(opts => { opts.ResourcesPath = "Resources"; });
    }
}