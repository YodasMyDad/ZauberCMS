using SixLabors.ImageSharp.Web.DependencyInjection;
using ZauberCMS.Components;
using ZauberCMS.Core;

var builder = WebApplication.CreateBuilder(args);

builder.AddZauberCms();



var app = builder.Build();

app.UseImageSharp();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.AddZauberCms<App>();

app.Run();