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

public class GetContentLanguagesHandler(IServiceProvider serviceProvider, ICacheService cacheService)  : IRequestHandler<GetContentLanguagesCommand, Dictionary<Guid, string>>
{
    public async Task<Dictionary<Guid, string>> Handle(GetContentLanguagesCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        
        var query = dbContext.Contents.AsNoTracking()
            .Include(x => x.Language)
            .Select(c => new { c.Id, c.Language })
            .Where(x => x.Language != null);
        
        var queryString = query.ToQueryString();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(queryString));
        var cacheKey = typeof(Models.Content).ToCacheKey(Convert.ToBase64String(hash));
        
        return (await cacheService.GetSetCachedItemAsync(cacheKey, async () =>
        {
            var contentLanguages = await query.ToListAsync(cancellationToken: cancellationToken);
            return contentLanguages.ToDictionary(x => x.Id, x => x.Language!.LanguageIsoCode);
        }))!;
    }
}