using System.Globalization;
using AutoMapper;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Languages.Interfaces;
using ZauberCMS.Core.Languages.Models;
using ZauberCMS.Core.Languages.Parameters;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Languages.Services;

public class LanguageService(
    IServiceProvider serviceProvider,
    ICacheService cacheService,
    IMapper mapper,
    AuthenticationStateProvider authenticationStateProvider,
    ExtensionManager extensionManager) : ILanguageService
{
    public async Task<Language?> GetLanguageAsync(GetLanguageParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var query = dbContext.Languages.AsQueryable();

        if (parameters.AsNoTracking)
        {
            query = query.AsNoTracking();
        }

        if (!parameters.IsoCode.IsNullOrWhiteSpace())
        {
            return await query.FirstOrDefaultAsync(x => x.LanguageIsoCode == parameters.IsoCode, cancellationToken: cancellationToken);
        }

        if (parameters.Id != null)
        {
            return await query.FirstOrDefaultAsync(x => x.Id == parameters.Id, cancellationToken: cancellationToken);
        }

        return await query.FirstOrDefaultAsync(cancellationToken: cancellationToken);
    }

    public async Task<HandlerResult<Language>> SaveLanguageAsync(SaveLanguageParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.GetUserAsync(authState.User);
        var handlerResult = new HandlerResult<Language>();

        if (parameters.Language != null)
        {
            var isUpdate = false;

            var language = new Language();
            if (parameters.Language.Id != Guid.Empty)
            {
                var lang = dbContext.Languages.FirstOrDefault(x => x.Id == parameters.Language.Id);
                if (lang != null)
                {
                    if (parameters.Language.LanguageIsoCode == lang.LanguageIsoCode)
                    {
                        // Just return if they are trying to save the same culture
                        handlerResult.Success = true;
                        handlerResult.Entity = lang;
                        return handlerResult;
                    }

                    isUpdate = true;
                    language = lang;
                }
            }

            // Does this already exist
            var existing = dbContext.Languages.FirstOrDefault(x => x.LanguageIsoCode == parameters.Language.LanguageIsoCode);
            if (existing != null && existing.Id != parameters.Language.Id)
            {
                handlerResult.AddMessage("Language already exists", ResultMessageType.Error);
                return handlerResult;
            }

            if (isUpdate)
            {
                mapper.Map(parameters.Language, language);
                language.DateUpdated = DateTime.UtcNow;
            }
            else
            {
                language = parameters.Language;
                dbContext.Languages.Add(language);
            }

            await user.AddAudit(language, $"Language ({language.LanguageCultureName})",
                isUpdate ? AuditExtensions.AuditAction.Update : AuditExtensions.AuditAction.Create, null,
                cancellationToken);
            return await dbContext.SaveChangesAndLog(language, handlerResult, cacheService, extensionManager, cancellationToken);
        }

        handlerResult.AddMessage("Language is null", ResultMessageType.Error);
        return handlerResult;
    }

    public async Task<PaginatedList<Language>> QueryLanguageAsync(QueryLanguageParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var query = dbContext.Languages.AsQueryable();

        if (parameters.Query != null)
        {
            query = parameters.Query.Invoke();
        }
        else
        {
            if (parameters.AsNoTracking)
            {
                query = query.AsNoTracking();
            }

            var idCount = parameters.Ids.Count;
            if (parameters.Ids.Count != 0)
            {
                query = query.Where(x => parameters.Ids.Contains(x.Id));
                parameters.AmountPerPage = idCount;
            }

            if (!parameters.SearchTerm.IsNullOrWhiteSpace())
            {
                query = query.Where(x => x.LanguageCultureName.Contains(parameters.SearchTerm) || 
                                         x.LanguageIsoCode.Contains(parameters.SearchTerm));
            }
        }

        if (parameters.WhereClause != null)
        {
            query = query.Where(parameters.WhereClause);
        }

        query = parameters.OrderBy switch
        {
            GetLanguageOrderBy.DateCreated => query.OrderBy(p => p.DateCreated),
            GetLanguageOrderBy.DateCreatedDescending => query.OrderByDescending(p => p.DateCreated),
            GetLanguageOrderBy.LanguageIsoCode => query.OrderBy(p => p.LanguageIsoCode),
            GetLanguageOrderBy.LanguageCultureName => query.OrderBy(p => p.LanguageCultureName),
            _ => query.OrderByDescending(p => p.DateCreated)
        };

        return query.ToPaginatedList(parameters.PageIndex, parameters.AmountPerPage);
    }

    public async Task<HandlerResult<Language>> DeleteLanguageAsync(DeleteLanguageParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = await userManager.GetUserAsync(authState.User);
        var handlerResult = new HandlerResult<Language>();

        var language = await dbContext.Languages
            .FirstOrDefaultAsync(x => x.Id == parameters.Id, cancellationToken);

        if (language != null)
        {
            // Check if this language is being used by content
            var hasContent = await dbContext.Contents.AnyAsync(x => x.LanguageCode == language.LanguageIsoCode, cancellationToken);
            if (hasContent)
            {
                handlerResult.AddMessage("Cannot delete language that has content", ResultMessageType.Error);
                return handlerResult;
            }

            await user.AddAudit(language, $"Language ({language.LanguageCultureName})", AuditExtensions.AuditAction.Delete, null, cancellationToken);
            dbContext.Languages.Remove(language);
            await dbContext.SaveChangesAsync(cancellationToken);
            handlerResult.Messages.Add(new ResultMessage("Language deleted successfully", ResultMessageType.Success));
            handlerResult.Success = true;
        }
        else
        {
            handlerResult.AddMessage("Language not found", ResultMessageType.Error);
        }

        return handlerResult;
    }

    public async Task<HandlerResult<LanguageDictionary>> SaveLanguageDictionaryAsync(SaveLanguageDictionaryParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var handlerResult = new HandlerResult<LanguageDictionary>();

        if (parameters.LanguageDictionary != null)
        {
            var isUpdate = false;
            var languageDictionary = parameters.LanguageDictionary;

            // Check if it exists
            var existing = await dbContext.LanguageDictionaries
                .FirstOrDefaultAsync(x => x.Id == languageDictionary.Id, cancellationToken);

            if (existing != null)
            {
                isUpdate = true;
                mapper.Map(languageDictionary, existing);
                existing.DateUpdated = DateTime.UtcNow;
                languageDictionary = existing;
            }
            else
            {
                dbContext.LanguageDictionaries.Add(languageDictionary);
            }

            return await dbContext.SaveChangesAndLog(languageDictionary, handlerResult, cacheService, extensionManager, cancellationToken);
        }

        handlerResult.AddMessage("LanguageDictionary is null", ResultMessageType.Error);
        return handlerResult;
    }

    public async Task<HandlerResult<LanguageDictionary>> DeleteLanguageDictionaryAsync(DeleteLanguageDictionaryParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var handlerResult = new HandlerResult<LanguageDictionary>();

        var languageDictionary = await dbContext.LanguageDictionaries
            .FirstOrDefaultAsync(x => x.Id == parameters.Id, cancellationToken);

        if (languageDictionary != null)
        {
            dbContext.LanguageDictionaries.Remove(languageDictionary);
            await dbContext.SaveChangesAsync(cancellationToken);
            handlerResult.Messages.Add(new ResultMessage("Language dictionary deleted successfully", ResultMessageType.Success));
            handlerResult.Success = true;
        }
        else
        {
            handlerResult.AddMessage("Language dictionary not found", ResultMessageType.Error);
        }

        return handlerResult;
    }

    public async Task<Dictionary<string, Dictionary<string, string>>> GetCachedAllLanguageDictionariesAsync(GetCachedAllLanguageDictionariesParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
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

    public async Task<PaginatedList<LanguageDictionary>> GetDataGridLanguageDictionaryAsync(DataGridLanguageDictionaryParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var query = dbContext.LanguageDictionaries.AsQueryable();

        if (parameters.AsNoTracking)
        {
            query = query.AsNoTracking();
        }

        if (!parameters.SearchTerm.IsNullOrWhiteSpace())
        {
            query = query.Where(x => x.Key.Contains(parameters.SearchTerm) || x.Value.Contains(parameters.SearchTerm));
        }

        if (parameters.WhereClause != null)
        {
            query = query.Where(parameters.WhereClause);
        }

        if (!parameters.OrderBy.IsNullOrWhiteSpace())
        {
            query = query.OrderBy(parameters.OrderBy);
        }
        else
        {
            query = query.OrderBy(x => x.Key);
        }

        return query.ToPaginatedList(parameters.PageIndex, parameters.AmountPerPage);
    }
}