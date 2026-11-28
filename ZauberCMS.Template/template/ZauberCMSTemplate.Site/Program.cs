using ZauberCMS.Components;
using ZauberCMS.Core;

var builder = WebApplication.CreateBuilder(args);

// Add configuration sources of your own (Key Vault, another JSON file) above this line: AddZauberCms reads some
// Zauber settings while it registers the services. Settings changed in code (Configure<ZauberSettings>) work too.
builder.AddZauberCms();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    // Errors: AddZauberCms sends them to Zauber:ErrorPagePath (default /error) outside Development
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.AddZauberCms<App>();

app.Run();