
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Linq.Dynamic.Core;
using ZauberCMS.Core.Audit.Interfaces;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Languages.Interfaces;
using ZauberCMS.Core.Languages.Models;
using ZauberCMS.Core.Languages.Parameters;
using ZauberCMS.Core.Languages.Mapping;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Languages.Services;

public class LanguageService(
    IServiceScopeFactory serviceScopeFactory,
    ICacheService cacheService,
    AuthenticationStateProvider authenticationStateProvider,
    ExtensionManager extensionManager)
    : ILanguageService
{
    /// <summary>
    /// Retrieves a language by id or ISO code.
    /// </summary>
    /// <param name="parameters">Id or ISO code, and tracking flag.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Language or null.</returns>
    public async Task<Language?> GetLanguageAsync(GetLanguageParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var query = dbContext.Languages.AsQueryable();

        if (parameters.AsNoTracking)
        {
            query = query.AsNoTracking();
        }

        if (parameters.LanguageIsoCode != null)
        {
            return await query.FirstOrDefaultAsync(x => x.LanguageIsoCode == parameters.LanguageIsoCode, cancellationToken: cancellationToken);
        }

        if (parameters.Id != null)
        {
            return await query.FirstOrDefaultAsync(x => x.Id == parameters.Id, cancellationToken: cancellationToken);
        }

        // Should never get here
        return await query.FirstOrDefaultAsync(cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Creates or updates a language from a CultureInfo. Prevents duplicates and logs audit.
    /// </summary>
    /// <param name="parameters">CultureInfo and optional id to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result including success and messages.</returns>
    public async Task<HandlerResult<Language>> SaveLanguageAsync(SaveLanguageParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.GetUserAsync(authState.User);
        var handlerResult = new HandlerResult<Language>();

        if (parameters.CultureInfo != null)
        {
            var isUpdate = false;

            var language = new Language();
            if (parameters.Id != null)
            {
                var lang = dbContext.Languages.FirstOrDefault(x => x.Id == parameters.Id);
                if (lang != null)
                {
                    if (parameters.CultureInfo.Name == lang.LanguageIsoCode)
                    {
                        // Just return if they are trying to save the same culture
                        handlerResult.Success = true;
                        return handlerResult;
                    }

                    isUpdate = true;
                    language = lang;
                }
            }

            // Does this already exist
            var existing = dbContext.Languages.FirstOrDefault(x => x.LanguageIsoCode == parameters.CultureInfo.Name);
            if (existing != null)
            {
                handlerResult.AddMessage("Language already exists", ResultMessageType.Error);
                return handlerResult;
            }

            language.LanguageCultureName = parameters.CultureInfo.EnglishName;
            language.LanguageIsoCode = parameters.CultureInfo.Name;

            if (!isUpdate)
            {
                dbContext.Languages.Add(language);
            }
            else
            {
                language.DateUpdated = DateTime.UtcNow;
            }

            var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
            await user.AddAudit(language, $"Language ({language.LanguageCultureName})",
                isUpdate ? AuditExtensions.AuditAction.Update : AuditExtensions.AuditAction.Create, auditService,
                cancellationToken);
            return await dbContext.SaveChangesAndLog(language, handlerResult, cacheService, extensionManager, cancellationToken);
        }

        handlerResult.AddMessage("CultureInfo is null", ResultMessageType.Error);
        return handlerResult;
    }

    /// <summary>
    /// Queries languages with filtering, ordering and paging.
    /// </summary>
    /// <param name="parameters">Query options including ids and ISO codes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged list of languages.</returns>
    public Task<PaginatedList<Language>> QueryLanguageAsync(QueryLanguageParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
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

            if (parameters.LanguageIsoCodes.Count != 0)
            {
                query = query.Where(x =>
                    x.LanguageIsoCode != null && parameters.LanguageIsoCodes.Contains(x.LanguageIsoCode));
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

        return Task.FromResult(query.ToPaginatedList(parameters.PageIndex, parameters.AmountPerPage));
    }

    /// <summary>
    /// Deletes a language by id or ISO code. Logs audit.
    /// </summary>
    /// <param name="parameters">Id or ISO code to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result including success and messages.</returns>
    public async Task<HandlerResult<Language?>> DeleteLanguageAsync(DeleteLanguageParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.GetUserAsync(authState.User);
        var handlerResult = new HandlerResult<Language>();

        Language? language = null;
        if (parameters.Id != null)
        {
            language =
                await dbContext.Languages.FirstOrDefaultAsync(l => l.Id == parameters.Id,
                    cancellationToken: cancellationToken);
            if (language != null)
            {
                var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
                await user.AddAudit(language, $"Language ({language.LanguageCultureName})",
                    AuditExtensions.AuditAction.Delete, auditService,
                    cancellationToken);
                dbContext.Languages.Remove(language);
            }
        }
        else
        {
            language = await dbContext.Languages.FirstOrDefaultAsync(
                l => l.LanguageIsoCode == parameters.LanguageIsoCode, cancellationToken: cancellationToken);
            if (language != null)
            {
                var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
                await user.AddAudit(language, $"Language ({language.LanguageCultureName})",
                    AuditExtensions.AuditAction.Delete, auditService,
                    cancellationToken);
                dbContext.Languages.Remove(language);
            }
        }

        return (await dbContext.SaveChangesAndLog(language, handlerResult, cacheService, extensionManager, cancellationToken))!;
    }

    /// <summary>
    /// Creates or updates a language dictionary and its texts. Clears relevant cache.
    /// </summary>
    /// <param name="parameters">Dictionary with texts to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result including success and messages.</returns>
    public async Task<HandlerResult<LanguageDictionary>> SaveLanguageDictionaryAsync(SaveLanguageDictionaryParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = await userManager.GetUserAsync(authState.User);
        var handlerResult = new HandlerResult<LanguageDictionary>();

        if (parameters.LanguageDictionary != null)
        {
            var langTexts = parameters.LanguageDictionary.Texts;

            parameters.LanguageDictionary.Texts = [];

            // Save the language dictionary first
            var langDictionary =
                dbContext.LanguageDictionaries.FirstOrDefault(x => x.Id == parameters.LanguageDictionary.Id);
            if (langDictionary == null)
            {
                langDictionary = parameters.LanguageDictionary;
                dbContext.LanguageDictionaries.Add(langDictionary);
            }
            else
            {
                parameters.LanguageDictionary.MapTo(langDictionary);
            }

            var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
            await user.AddAudit(langDictionary, $"Language Dictionary ({langDictionary.Key})",
                AuditExtensions.AuditAction.Update, auditService,
                cancellationToken);
            handlerResult = await dbContext.SaveChangesAndLog(langDictionary, handlerResult, cacheService, extensionManager, cancellationToken);
            if (handlerResult.Success)
            {
                var langTextResult = new HandlerResult<LanguageText>();
                foreach (var languageText in langTexts)
                {
                    var lt = dbContext.LanguageTexts.FirstOrDefault(x => x.Id == languageText.Id);
                    if (lt == null)
                    {
                        lt = languageText;
                        dbContext.LanguageTexts.Add(lt);
                    }
                    else
                    {
                        languageText.MapTo(lt);
                    }

                    var saveResult = await dbContext.SaveChangesAndLog(lt, langTextResult, cacheService, extensionManager, cancellationToken);
                    if (!saveResult.Success)
                    {
                        handlerResult.Success = false;
                        handlerResult.Messages = saveResult.Messages;
                        return handlerResult;
                    }
                }
            }
            else
            {
                return handlerResult;
            }

            // Clear Cache
            cacheService.ClearCachedItemsWithPrefix(nameof(LanguageDictionary));
            return handlerResult;
        }

        handlerResult.Success = false;
        return handlerResult;
    }

    /// <summary>
    /// Deletes a language dictionary by id and logs audit.
    /// </summary>
    /// <param name="parameters">Dictionary id to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result including success and messages.</returns>
    public async Task<HandlerResult<LanguageDictionary?>> DeleteLanguageDictionaryAsync(DeleteLanguageDictionaryParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = await userManager.GetUserAsync(authState.User);
        var handlerResult = new HandlerResult<LanguageDictionary>();

        LanguageDictionary? langDict = null;
        if (parameters.Id != null)
        {
            langDict = await dbContext.LanguageDictionaries.FirstOrDefaultAsync(l => l.Id == parameters.Id,
                cancellationToken: cancellationToken);
            if (langDict != null)
            {
                await user.AddAudit(langDict, $"Language Dictionary ({langDict.Key})",
                    AuditExtensions.AuditAction.Delete, auditService,
                    cancellationToken);
                dbContext.LanguageDictionaries.Remove(langDict);
            }
        }

        return (await dbContext.SaveChangesAndLog(langDict, handlerResult, cacheService, extensionManager, cancellationToken))!;
    }

    /// <summary>
    /// Returns a nested dictionary of all language keys and values per language from cache.
    /// </summary>
    /// <param name="parameters">Unused. Reserved for future options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary ISO -> (Key -> Value).</returns>
    public async Task<Dictionary<string, Dictionary<string, string>>> GetCachedAllLanguageDictionariesAsync(GetCachedAllLanguageDictionariesParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
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

    /// <summary>
    /// Returns language dictionaries for a data grid with filtering, sort and paging.
    /// </summary>
    /// <param name="parameters">Grid options including filter and order.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Data grid result including total count and items.</returns>
    public async Task<DataGridResult<LanguageDictionary>> GetDataGridLanguageDictionaryAsync(DataGridLanguageDictionaryParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        
        var result = new DataGridResult<LanguageDictionary>();

        // Now you have the DbSet<T> and can query it
        var query = dbContext.LanguageDictionaries
            .Include(x => x.Texts)
            .AsSplitQuery()
            .AsTracking()
            .AsQueryable();
        
        if (!string.IsNullOrEmpty(parameters.Filter))
        {
            // Filter via the Where method
            query = query.Where(parameters.Filter);
        }

        if (!string.IsNullOrEmpty(parameters.Order))
        {
            // Sort via the OrderBy method
            query = query.OrderBy(parameters.Order);
        }
        else
        {
            query = query.OrderBy(x => x.Key);
        }

        // Important!!! Make sure the Count property of RadzenDataGrid is set.
        result.Count = await query.CountAsync(cancellationToken);

        // Perform paging via Skip and Take.
        result.Items = await query.Skip(parameters.Skip).Take(parameters.Take).ToListAsync(cancellationToken: cancellationToken);

        return result;
    }
}
