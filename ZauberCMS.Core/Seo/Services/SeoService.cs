using AutoMapper;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Seo.Interfaces;
using ZauberCMS.Core.Seo.Models;
using ZauberCMS.Core.Seo.Parameters;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Seo.Services;

public class SeoService(
    IServiceProvider serviceProvider,
    ICacheService cacheService,
    IMapper mapper,
    ExtensionManager extensionManager) : ISeoService
{
    public async Task<HandlerResult<SeoRedirect>> SaveRedirectAsync(SaveRedirectParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var authState = scope.ServiceProvider.GetRequiredService<AuthenticationStateProvider>();
        var auth = await authState.GetAuthenticationStateAsync();
        var user = await userManager.GetUserAsync(auth.User);
        var handlerResult = new HandlerResult<SeoRedirect>();
        var isUpdate = false;

        if (parameters.Redirect != null)
        {
            var redirect = dbContext.Redirects
                .FirstOrDefault(x => x.Id == parameters.Redirect.Id);

            if (redirect == null)
            {
                redirect = parameters.Redirect;
                dbContext.Redirects.Add(redirect);
            }
            else
            {
                isUpdate = true;
                mapper.Map(parameters.Redirect, redirect);
                redirect.DateUpdated = DateTime.UtcNow;
            }

            await user.AddAudit(redirect, redirect.FromUrl, isUpdate ? AuditExtensions.AuditAction.Update : AuditExtensions.AuditAction.Create, mediator, cancellationToken);
            return await dbContext.SaveChangesAndLog(redirect, handlerResult, cacheService, extensionManager, cancellationToken);
        }

        handlerResult.AddMessage("Redirect is null", ResultMessageType.Error);
        return handlerResult;
    }

    public async Task<List<SeoRedirect>> QueryRedirectsAsync(QueryRedirectsParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var query = dbContext.Redirects.AsQueryable();

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
            
            if (parameters.Ids.Count > 0)
            {
                query = query.Where(p => parameters.Ids.Contains(p.Id));
            }
        }
        
        query = parameters.OrderBy switch
        {
            GetSeoRedirectOrderBy.DateCreated => query.OrderBy(p => p.DateCreated),
            GetSeoRedirectOrderBy.DateCreatedDescending => query.OrderByDescending(p => p.DateCreated),
            GetSeoRedirectOrderBy.DateUpdated => query.OrderBy(p => p.DateUpdated),
            GetSeoRedirectOrderBy.DateUpdatedDescending => query.OrderBy(p => p.DateUpdated),
            _ => query.OrderByDescending(p => p.FromUrl)
        };

        return await query.Take(parameters.Amount).ToListAsync(cancellationToken: cancellationToken);
    }

    public async Task<HandlerResult<SeoRedirect?>> DeleteRedirectAsync(DeleteRedirectParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var authState = scope.ServiceProvider.GetRequiredService<AuthenticationStateProvider>();
        var auth = await authState.GetAuthenticationStateAsync();
        var user = await userManager.GetUserAsync(auth.User);
        var handlerResult = new HandlerResult<SeoRedirect>();

        SeoRedirect? redirect = null;
        if (parameters.Id != null)
        {
            redirect =
                await dbContext.Redirects.FirstOrDefaultAsync(l => l.Id == parameters.Id,
                    cancellationToken: cancellationToken);
            if (redirect != null)
            {
                await user.AddAudit(redirect, $"Redirect ({redirect.FromUrl} -> {redirect.ToUrl})",
                    AuditExtensions.AuditAction.Delete, mediator,
                    cancellationToken);
                dbContext.Redirects.Remove(redirect);
            }
        }

        return (await dbContext.SaveChangesAndLog(redirect, handlerResult, cacheService, extensionManager, cancellationToken))!;
    }
}