using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Languages.Commands;
using ZauberCMS.Core.Languages.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Languages.Handlers;

public class GetCachedAllLanguageDictionariesHandler(IServiceProvider serviceProvider, ICacheService cacheService) : 
    IRequestHandler<GetCachedAllLanguageDictionariesCommand, Dictionary<string, Dictionary<string, string>>>
{
    public async Task<Dictionary<string, Dictionary<string, string>>> Handle(GetCachedAllLanguageDictionariesCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var cacheKey = typeof(LanguageDictionary).ToCacheKey("GetCachedAllLanguageDictionaries");
        
        return (await cacheService.GetSetCachedItemAsync(cacheKey, () =>
        {
            var allLanguages = dbContext.Languages.Include(x=>x.LanguageTexts).AsNoTracking().AsSplitQuery();
            var allLanguageDictionaries = dbContext.LanguageDictionaries.AsNoTracking();
            var returnDict = new Dictionary<string, Dictionary<string, string>>();
            foreach (var language in allLanguages)
            {
                var langTextDict = new Dictionary<string, string>();
                foreach (var languageDictionary in allLanguageDictionaries)
                {
                    langTextDict.Add(languageDictionary.Key, language.LanguageTexts.FirstOrDefault(x => x.LanguageDictionaryId == languageDictionary.Id)?.Value ?? string.Empty);
                }

                if (language.LanguageIsoCode != null) returnDict.Add(language.LanguageIsoCode, langTextDict);
            }
            return Task.FromResult(returnDict);
        }))!;
    }
}