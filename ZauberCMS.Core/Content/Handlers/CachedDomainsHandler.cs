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

public class CachedDomainsHandler(IServiceProvider serviceProvider, ICacheService cacheService)
    : IRequestHandler<CachedDomainsCommand, List<Domain>>
{
    public async Task<List<Domain>> Handle(CachedDomainsCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var query = dbContext.Domains.AsNoTracking().Include(x => x.Language);
        var queryString = query.ToQueryString();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(queryString));
        var cacheKey = typeof(Domain).ToCacheKey(Convert.ToBase64String(hash));
        return (await cacheService.GetSetCachedItemAsync(cacheKey, async () => await query.ToListAsync(cancellationToken: cancellationToken)))!;
    }
}