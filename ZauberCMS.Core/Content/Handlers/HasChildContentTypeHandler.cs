using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Content.Handlers;

public class HasChildContentTypeHandler(IServiceProvider serviceProvider, ICacheService cacheService) 
    : IRequestHandler<HasChildContentTypeCommand, bool>
{
    public async Task<bool> Handle(HasChildContentTypeCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var cacheKey = GenerateCacheKey(request);

        if (request.Cached)
        {
            return await cacheService.GetSetCachedItemAsync(cacheKey, async () => await CheckHasChildContentTypeAsync(request, dbContext, cancellationToken));
        }

        return await CheckHasChildContentTypeAsync(request, dbContext, cancellationToken);
    }

    private static string GenerateCacheKey(HasChildContentTypeCommand request)
    {
        var key = $"HasContentTypeChild-{request.ParentId}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return typeof(ContentType).ToCacheKey(Convert.ToBase64String(hash));
    }

    private static async Task<bool> CheckHasChildContentTypeAsync(HasChildContentTypeCommand request, IZauberDbContext dbContext, CancellationToken cancellationToken)
    {
        return await dbContext.ContentTypes.AsNoTracking().AnyAsync(c => c.ParentId == request.ParentId, cancellationToken: cancellationToken);
    }
}