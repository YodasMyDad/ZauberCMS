using MediatR;
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
using ZauberCMS.Core.Shared.Middleware;
using ZauberCMS.Core.Shared.Services;
using ZauberCMS.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration.ReadFrom.Configuration(context.Configuration));

// Add services to the container.
#if DEBUG
builder.Services
    .AddRazorComponents(c => c.DetailedErrors = true)
    .AddInteractiveServerComponents(c => c.DetailedErrors = true);
#else
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
#endif

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddRadzenComponents();
builder.Services.AddScoped<AuthenticationStateProvider, RevalidatingIdentityAuthenticationStateProvider<User>>();

builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddImageSharp();
builder.Services.AddAuthentication();

builder.Services.AddScoped<ExtensionManager>();
builder.Services.AddScoped<ProviderService>();
builder.Services.AddScoped<ICacheService, MemoryCacheService>();
builder.Services.AddScoped<SignInManager<User>, ZauberSignInManager>();

builder.Services.Configure<ZauberSettings>(builder.Configuration.GetSection(Constants.SettingsConfigName));
builder.Services.AddMvc();
builder.Services.EnableZauberPlugins(builder.Configuration);

var app = builder.Build();

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
app.UseRouting();
app.UseAuthorization();
app.UseAntiforgery();

app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();