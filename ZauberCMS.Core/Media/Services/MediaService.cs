using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Media.Interfaces;
using ZauberCMS.Core.Media.Parameters;
using ZauberCMS.Core.Media.Mapping;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Providers;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Audit.Interfaces;
using ZauberCMS.Core.Shared;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Media.Services;

public class MediaService(
    ProviderService providerService,
    IServiceScopeFactory serviceScopeFactory,
    AppState appState,
    IOptions<ZauberSettings> settings,
    ICacheService cacheService,
    AuthenticationStateProvider authenticationStateProvider,
    ExtensionManager extensionManager)
    : IMediaService
{
    /// <summary>
    /// Retrieves a media item with optional includes and caching.
    /// </summary>
    /// <param name="parameters">Query options including id and include flags.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Media or null.</returns>
    public async Task<Models.Media?> GetMediaAsync(GetMediaParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var query = BuildQuery(parameters, dbContext);
        var cacheKey = query.GenerateCacheKey();

        if (parameters.Cached)
        {
            return await cacheService.GetSetCachedItemAsync(cacheKey, async () => await query.FirstOrDefaultAsync(cancellationToken));
        }

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Creates or updates a media item, saving or deleting the backing file via provider when needed.
    /// </summary>
    /// <param name="parameters">Media to save and storage options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result including success and messages.</returns>
    public async Task<HandlerResult<Models.Media>> SaveMediaAsync(SaveMediaParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();

        var result = new HandlerResult<Models.Media>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.GetUserAsync(authState.User);

        // If we are either creating a new file or over writing the current one
        if (parameters.FileToSave != null)
        {
            result = await providerService.StorageProvider!.SaveFile(parameters.FileToSave, parameters.MediaToSave);
            if (!result.Success)
            {
                return result;
            }
        }
        else if (parameters.MediaToSave != null)
        {
            result.Entity = parameters.MediaToSave;
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
                    result.Entity.MapTo(dbMedia);
                    dbMedia.DateUpdated = DateTime.UtcNow;

                    if (result.Entity.Url.IsNullOrWhiteSpace() && result.Entity.MediaType != Models.MediaType.Folder)
                    {
                        result.AddMessage("Url cannot be empty", ResultMessageType.Error);
                        return result;
                    }

                    // Calculate and set the Path property
                    dbMedia.Path = result.Entity.BuildPath(dbContext, parameters.IsUpdate, settings);
                    var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
                    await user.AddAudit(result.Entity, result.Entity.Name, AuditExtensions.AuditAction.Update, auditService,
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
                var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
                await user.AddAudit(result.Entity, result.Entity.Name, AuditExtensions.AuditAction.Create, auditService,
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

    /// <summary>
    /// Queries media items with filtering, ordering and paging. Can use cache.
    /// </summary>
    /// <param name="parameters">Query options including includes and paging.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged list of media.</returns>
#pragma warning disable CS1998
    public async Task<PaginatedList<Models.Media>> QueryMediaAsync(QueryMediaParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var query = BuildQuery(parameters, dbContext);
        var cacheKey = $"{query.GenerateCacheKey<Models.Media>()}_Page{parameters.PageIndex}_Amount{parameters.AmountPerPage}";

        if (parameters.Cached)
        {
            return (await cacheService.GetSetCachedItemAsync(cacheKey, async () => query.ToPaginatedList(parameters.PageIndex, parameters.AmountPerPage)))!;
        }

        return query.ToPaginatedList(parameters.PageIndex, parameters.AmountPerPage);
    }
#pragma warning restore CS1998

    /// <summary>
    /// Deletes a media item and optionally its physical file. Logs audit and notifies state.
    /// </summary>
    /// <param name="parameters">Media id and whether to delete the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result including success and messages.</returns>
    public async Task<HandlerResult<Models.Media>> DeleteMediaAsync(DeleteMediaParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = await userManager.GetUserAsync(authState.User);
        var handlerResult = new HandlerResult<Models.Media>();
        
        var media = dbContext.Medias.FirstOrDefault(x => x.Id == parameters.MediaId);
        if (media != null)
        {
            //Check if it has children
            var children = dbContext.Medias.AsNoTracking().Where(x => x.ParentId == media.Id);
            if (await children.AnyAsync(cancellationToken))
            {
                handlerResult.AddMessage("Unable to delete media with child content, delete or move those items first", ResultMessageType.Error);
                return handlerResult;
            }

            
            var filePathToDelete = media.Url;
            var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
            await user.AddAudit(media, media.Name, AuditExtensions.AuditAction.Delete, auditService, cancellationToken);
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

    /// <summary>
    /// Checks if a media item has child media.
    /// </summary>
    /// <param name="parameters">Parent id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when children exist.</returns>
    public async Task<bool> HasChildMediaAsync(HasChildMediaParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        return await dbContext.Medias.AsNoTracking().AnyAsync(c => c.ParentId == parameters.ParentId && !c.Deleted, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Returns a dictionary of restricted media URLs to ids. Uses cache.
    /// </summary>
    /// <param name="parameters">Caching flag.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary URL -> MediaId.</returns>
    public async Task<Dictionary<string, Guid>> GetRestrictedMediaUrlsAsync(GetRestrictedMediaUrlsParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var query = BuildQuery(dbContext);
        var cacheKey = query.GenerateCacheKey<Models.Media>();

        if (parameters.Cached)
        {
            return await cacheService.GetSetCachedItemAsync(cacheKey, async () => await query.Select(x => new { x.Url, x.Id }).ToDictionaryAsync(x => x.Url ?? string.Empty, x => x.Id, cancellationToken: cancellationToken)) ?? new Dictionary<string, Guid>();
        }

        return await query.Select(x => new { x.Url, x.Id }).ToDictionaryAsync(x => x.Url ?? string.Empty, x => x.Id, cancellationToken: cancellationToken);
    }

    private static IQueryable<Models.Media> BuildQuery(GetMediaParameters parameters, IZauberDbContext dbContext)
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

    private static IQueryable<Models.Media> BuildQuery(IZauberDbContext dbContext)
    {
        return dbContext.Medias.AsNoTracking().Where(x => x.RequiresAuthentication);
    }
}
