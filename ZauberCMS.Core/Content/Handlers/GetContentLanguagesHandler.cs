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

public class GetContentLanguagesHandler(IServiceProvider serviceProvider, ICacheService cacheService)  : IRequestHandler<GetContentLanguagesCommand, Dictionary<object, string>>
{
    public async Task<Dictionary<object, string>> Handle(GetContentLanguagesCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        
        var query = dbContext.Contents.AsNoTracking()
            .Include(x => x.Language)
            .Select(c => new { c.Id, c.Url, c.Language })
            .Where(x => x.Language != null && x.Url != null);
        
        var queryString = query.ToQueryString();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(queryString));
        var cacheKey = typeof(Models.Content).ToCacheKey(Convert.ToBase64String(hash));
        
        return (await cacheService.GetSetCachedItemAsync(cacheKey, async () =>
        {
            //return contentLanguages.ToDictionary(x => x.Url!.ToString(), x => x.Language!.LanguageIsoCode);
            
            var contentLanguages = await query.ToListAsync(cancellationToken: cancellationToken);
            var dict = new Dictionary<object, string>();

            // Set the Urls first
            foreach (var c in contentLanguages)
            {
                dict.Add(c.Url!, c.Language!.LanguageIsoCode!);
                dict.Add(c.Id, c.Language!.LanguageIsoCode!);
            }
            
            return dict;
        }))!;
    }
}