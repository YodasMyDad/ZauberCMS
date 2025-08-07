using System.Security.Cryptography;
using System.Text;
using AutoMapper;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Media.Interfaces;
using ZauberCMS.Core.Media.Models;
using ZauberCMS.Core.Media.Parameters;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Providers;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Shared;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Media.Services;

public class MediaService(
    IServiceProvider serviceProvider,
    ICacheService cacheService,
    IMapper mapper,
    AppState appState,
    IOptions<ZauberSettings> settings,
    AuthenticationStateProvider authenticationStateProvider,
    ProviderService providerService,
    ExtensionManager extensionManager) : IMediaService
{
    public async Task<Models.Media?> GetMediaAsync(GetMediaParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var cacheKey = GenerateCacheKey(parameters, dbContext);

        if (parameters.Cached)
        {
            return await cacheService.GetSetCachedItemAsync(cacheKey, async () => await FetchMediaAsync(parameters, dbContext, cancellationToken));
        }

        return await FetchMediaAsync(parameters, dbContext, cancellationToken);
    }

    public async Task<HandlerResult<Models.Media>> SaveMediaAsync(SaveMediaParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();

        var result = new HandlerResult<Models.Media>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.GetUserAsync(authState.User);

        // If we are either creating a new file or over writing the current one
        if (parameters.FileStream != null)
        {
            result = await providerService.StorageProvider!.SaveFile(parameters.FileStream, parameters.Media);
            if (!result.Success)
            {
                return result;
            }
        }
        else if (parameters.Media != null)
        {
            result.Entity = parameters.Media;
        }

        if (result.Entity != null)
        {
            if (parameters.ParentFolderId != null)
            {
                result.Entity.ParentId = parameters.ParentFolderId;
            }

            result.Entity.LastUpdatedById = user!.Id;

            // Now update or add the media item
            if (parameters.IsUpdate)
            {
                // Get the DB version
                var dbMedia = dbContext.Medias
                    .FirstOrDefault(x => x.Id == result.Entity.Id);
                if (dbMedia != null)
                {
                    // Map the updated properties
                    mapper.Map(result.Entity, dbMedia);
                    dbMedia.DateUpdated = DateTime.UtcNow;

                    if (result.Entity.Url.IsNullOrWhiteSpace() && result.Entity.MediaType != MediaType.Folder)
                    {
                        result.AddMessage("Url cannot be empty", ResultMessageType.Error);
                        return result;
                    }

                    // Calculate and set the Path property
                    dbMedia.Path = result.Entity.BuildPath(dbContext, parameters.IsUpdate, settings);
                    await user.AddAudit(result.Entity, result.Entity.Name, AuditExtensions.AuditAction.Update, null,
                        cancellationToken);
                    result = await dbContext.SaveChangesAndLog(result.Entity, result, cacheService, extensionManager,
                        cancellationToken);
                    await appState.NotifyMediaSaved(dbMedia, authState.User.Identity?.Name!);
                }
                else
                {
                    result.AddMessage("Unable to update, as no Media with that id exists", ResultMessageType.Warning);
                    return result;
                }
            }
            else
            {
                // Calculate and set the Path property
                result.Entity.Path = result.Entity.BuildPath(dbContext, parameters.IsUpdate, settings);
                dbContext.Medias.Add(result.Entity);
                await user.AddAudit(result.Entity, result.Entity.Name, AuditExtensions.AuditAction.Create, null,
                    cancellationToken);
                result = await dbContext.SaveChangesAndLog(result.Entity, result, cacheService, extensionManager,
                    cancellationToken);
                await appState.NotifyMediaSaved(result.Entity, authState.User.Identity?.Name!);
            }
        }
        else
        {
            result.AddMessage("There is no media to save?", ResultMessageType.Error);
            result.Success = false;
        }

        return result;
    }

    public async Task<PaginatedList<Models.Media>> QueryMediaAsync(QueryMediaParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var query = BuildQuery(parameters, dbContext);
        var cacheKey = query.GenerateCacheKey(typeof(Models.Media));

        if (parameters.Cached)
        {
            return (await cacheService.GetSetCachedItemAsync(cacheKey, async () => await FetchMediaListAsync(parameters, dbContext, cancellationToken)))!;
        }

        return await FetchMediaListAsync(parameters, dbContext, cancellationToken);
    }

    public async Task<HandlerResult<Models.Media>> DeleteMediaAsync(DeleteMediaParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = await userManager.GetUserAsync(authState.User);
        var handlerResult = new HandlerResult<Models.Media>();
        
        var media = dbContext.Medias.FirstOrDefault(x => x.Id == parameters.Id);
        if (media != null)
        {
            //Check if it has children
            var children = dbContext.Medias.AsNoTracking().Where(x => x.ParentId == media.Id);
            if (children.Any())
            {
                handlerResult.AddMessage("Unable to delete media with child content, delete or move those items first", ResultMessageType.Error);
                return handlerResult;
            }

            var filePathToDelete = media.Url;
            await user.AddAudit(media, media.Name, AuditExtensions.AuditAction.Delete, null, cancellationToken);
            dbContext.Medias.Remove(media);
            await appState.NotifyMediaDeleted(null, authState.User.Identity?.Name!);
            var result = await dbContext.SaveChangesAndLog(media, handlerResult, cacheService, extensionManager, cancellationToken);
            if (result.Success && parameters.DeleteFile)
            {
                await providerService.StorageProvider!.DeleteFile(filePathToDelete);
            }

            return result;
        }

        handlerResult.AddMessage("Unable to delete, as no Media with that id exists", ResultMessageType.Warning);
        return handlerResult;
    }

    public async Task<bool> HasChildMediaAsync(HasChildMediaParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        return await dbContext.Medias.AsNoTracking().AnyAsync(c => c.ParentId == parameters.Id, cancellationToken: cancellationToken);
    }

    public async Task<Dictionary<string, Guid>> GetRestrictedMediaUrlsAsync(GetRestrictedMediaUrlsParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var cacheKey = GenerateRestrictedMediaCacheKey(dbContext);

        if (parameters.Cached)
        {
            return await cacheService.GetSetCachedItemAsync(cacheKey, async () => await FetchRestrictedMediaUrlsAsync(dbContext, cancellationToken)) ?? new Dictionary<string, Guid>();
        }

        return await FetchRestrictedMediaUrlsAsync(dbContext, cancellationToken);
    }

    // Private helper methods
    private static string GenerateCacheKey(GetMediaParameters parameters, IZauberDbContext dbContext)
    {
        var query = BuildMediaQuery(parameters, dbContext);
        var queryString = query.ToQueryString();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(queryString));
        return typeof(Models.Media).ToCacheKey(Convert.ToBase64String(hash));
    }

    private static string GenerateRestrictedMediaCacheKey(IZauberDbContext dbContext)
    {
        var query = BuildRestrictedMediaQuery(dbContext);
        var queryString = query.ToQueryString();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(queryString));
        return typeof(Models.Media).ToCacheKey(Convert.ToBase64String(hash));
    }

    private static IQueryable<Models.Media> BuildMediaQuery(GetMediaParameters parameters, IZauberDbContext dbContext)
    {
        var query = dbContext.Medias.AsQueryable();

        if (parameters.AsNoTracking)
        {
            query = query.AsNoTracking();
        }

        if (parameters.IncludeParent)
        {
            query = query.Include(x => x.Parent);
        }

        if (parameters.IncludeChildren)
        {
            query = query.Include(x => x.Children);

            if (parameters.IncludeParent)
            {
                query = query.AsSplitQuery();
            }
        }

        if (parameters.MediaType != null)
        {
            query = query.Where(x => x.MediaType == parameters.MediaType);
        }

        if (parameters.Id != null)
        {
            query = query.Where(x => x.Id == parameters.Id);
        }

        return query;
    }

    private static IQueryable<Models.Media> BuildQuery(QueryMediaParameters parameters, IZauberDbContext dbContext)
    {
        var query = dbContext.Medias.Include(x => x.Parent).AsQueryable();

        if (parameters.Query != null)
        {
            query = parameters.Query.Invoke();
        }
        else
        {
            if (parameters.IncludeChildren)
            {
                query = query.Include(x => x.Children).AsSplitQuery();
            }

            if (parameters.AsNoTracking)
            {
                query = query.AsNoTracking();
            }

            if (parameters.Ids.Count != 0)
            {
                query = query.Where(x => parameters.Ids.Contains(x.Id));
                parameters.AmountPerPage = parameters.Ids.Count;
            }

            if (parameters.MediaTypes.Count != 0)
            {
                query = query.Where(x => parameters.MediaTypes.Contains(x.MediaType));
            }

            if (parameters.ParentId.HasValue)
            {
                query = query.Where(x => x.ParentId == parameters.ParentId);
            }

            if (!parameters.SearchTerm.IsNullOrWhiteSpace())
            {
                query = query.Where(x => x.Name.Contains(parameters.SearchTerm) || 
                                         x.Url.Contains(parameters.SearchTerm));
            }
        }

        if (parameters.WhereClause != null)
        {
            query = query.Where(parameters.WhereClause);
        }

        query = parameters.OrderBy switch
        {
            GetMediaOrderBy.DateUpdated => query.OrderBy(p => p.DateUpdated),
            GetMediaOrderBy.DateUpdatedDescending => query.OrderByDescending(p => p.DateUpdated),
            GetMediaOrderBy.DateCreated => query.OrderBy(p => p.DateCreated),
            GetMediaOrderBy.DateCreatedDescending => query.OrderByDescending(p => p.DateCreated),
            GetMediaOrderBy.Name => query.OrderBy(p => p.Name),
            GetMediaOrderBy.NameDescending => query.OrderByDescending(p => p.Name),
            _ => query.OrderByDescending(p => p.DateUpdated)
        };

        return query;
    }

    private static IQueryable<Models.Media> BuildRestrictedMediaQuery(IZauberDbContext dbContext)
    {
        return dbContext.Medias.AsNoTracking().Where(x => x.RequiresAuthentication);
    }

    private static async Task<Models.Media?> FetchMediaAsync(GetMediaParameters parameters, IZauberDbContext dbContext, CancellationToken cancellationToken)
    {
        var query = BuildMediaQuery(parameters, dbContext);
        return await query.FirstOrDefaultAsync(cancellationToken: cancellationToken);
    }

    private static Task<PaginatedList<Models.Media>> FetchMediaListAsync(QueryMediaParameters parameters, IZauberDbContext dbContext, CancellationToken cancellationToken)
    {
        var query = BuildQuery(parameters, dbContext);
        return Task.FromResult(query.ToPaginatedList(parameters.PageIndex, parameters.AmountPerPage));
    }

    private static async Task<Dictionary<string, Guid>> FetchRestrictedMediaUrlsAsync(IZauberDbContext dbContext, CancellationToken cancellationToken)
    {
        var query = BuildRestrictedMediaQuery(dbContext);
        return await query
            .Select(x => new { x.Url, x.Id })
            .ToDictionaryAsync(x => x.Url ?? string.Empty, x => x.Id, cancellationToken: cancellationToken);
    }
}