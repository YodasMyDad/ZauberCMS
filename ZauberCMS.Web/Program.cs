using ZauberCMS.Components;
using ZauberCMS.Core;

var builder = WebApplication.CreateBuilder(args);

builder.AddZauberCms(settings =>
{
    settings.DatabaseProvider = "postgresql";
    settings.ConnectionString = "Host=localhost;Port=51020;Username=postgres;Password=dCW7{-YUH9u8_8uv~W{M}x;Database=cms";
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.AddZauberCms<App>();

app.Run();