using ZauberCMS.Components;
using ZauberCMS.Core;

var builder = WebApplication.CreateBuilder(args);

builder.AddZauberCms();

var app = builder.Build();

app.AddZauberCms<App>();

app.Run();