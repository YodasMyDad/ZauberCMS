using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace ZauberCMS.Core.Shared.Middleware;

public class MissingImageMiddleware(RequestDelegate next, IWebHostEnvironment env)
{
    public async Task Invoke(HttpContext context)
    {
        var path = context.Request.Path;

        if (path.StartsWithSegments("/uploads"))
        {
            var imagePath = env.WebRootPath + path.Value;

            if (!File.Exists(imagePath))
            {
                context.Request.Path = "/img/missing.jpg";
            }
        }

        await next(context);
    }
}