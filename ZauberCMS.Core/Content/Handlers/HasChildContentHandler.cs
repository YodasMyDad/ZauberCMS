using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Content.Handlers;

public class HasChildContentHandler(IServiceProvider serviceProvider, ICacheService cacheService) 
    : IRequestHandler<HasChildContentCommand, bool>
{
    public async Task<bool> Handle(HasChildContentCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var cacheKey = GenerateCacheKey(request);

        if (request.Cached)
        {
            return await cacheService.GetSetCachedItemAsync(cacheKey, async () => await CheckHasChildContentAsync(request, dbContext, cancellationToken));
        }

        return await CheckHasChildContentAsync(request, dbContext, cancellationToken);
    }

    private static string GenerateCacheKey(HasChildContentCommand request)
    {
        var key = $"HasChild-{request.ParentId}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return typeof(Models.Content).ToCacheKey(Convert.ToBase64String(hash));
    }

    private async Task<bool> CheckHasChildContentAsync(HasChildContentCommand request, ZauberDbContext dbContext, CancellationToken cancellationToken)
    {
        return await dbContext.Contents.AsNoTracking().AnyAsync(c => c.ParentId == request.ParentId, cancellationToken: cancellationToken);
    }
}