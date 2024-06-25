using Blazored.Modal;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Radzen;
using Serilog;
using SixLabors.ImageSharp.Web.DependencyInjection;
using ZauberCMS.Core;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Providers;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Shared;
using ZauberCMS.Core.Shared.Middleware;
using ZauberCMS.Core.Shared.Services;
using ZauberCMS.Web.Components;
using ZauberCMS.Core.Email;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration.ReadFrom.Configuration(context.Configuration));

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
builder.Services.AddScoped<IdentityUserAccessor>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddRadzenComponents();
//builder.Services.AddScoped<AuthenticationStateProvider, RevalidatingIdentityAuthenticationStateProvider<User>>();
builder.Services.AddDatabase(builder.Configuration);
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

builder.Services.AddSingleton<LayoutResolverService>();
builder.Services.AddSingleton<AppState>();

builder.Services.Configure<ZauberSettings>(builder.Configuration.GetSection(Constants.SettingsConfigName));
builder.Services.AddMvc();
builder.Services.EnableZauberPlugins(builder.Configuration);

// Add localization services
builder.Services.AddLocalization(opts => { opts.ResourcesPath = "Resources"; });

var app = builder.Build();

// Look into this
//app.UseMigrationsEndPoint();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
    var mediatr = scope.ServiceProvider.GetRequiredService<IMediator>();
    try
    {
        if (dbContext.Database.GetPendingMigrations().Any())
        {
            dbContext.Database.Migrate();
        }
        
        await dbContext.SeedData(mediatr);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error during startup trying to do Db migrations");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseMiddleware<MissingImageMiddleware>();
app.UseImageSharp();
app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseStaticFiles();

var supportedCultures = new[] { "en" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture(supportedCultures[0])
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);
app.UseRequestLocalization(localizationOptions);
app.UseRouting();
app.UseAntiforgery();
app.MapControllers();

// Add authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(ExtensionManager.GetFilteredAssemblies(null).ToArray()!);

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.Run();