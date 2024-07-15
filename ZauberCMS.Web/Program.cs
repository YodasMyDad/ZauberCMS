using ZauberCMS.Core.Membership;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.AddZauberCms(builder.Configuration);

var app = builder.Build();

app.AddZauberCms();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
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