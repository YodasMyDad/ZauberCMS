using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
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
    public async Task<HandlerResult<Redirect>> SaveRedirectAsync(SaveRedirectParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var handlerResult = new HandlerResult<Redirect>();
        var isUpdate = false;

        if (parameters.Redirect != null)
        {
            var redirect = dbContext.Redirects.FirstOrDefault(x => x.Id == parameters.Redirect.Id);

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

            return await dbContext.SaveChangesAndLog(redirect, handlerResult, cacheService, extensionManager, cancellationToken);
        }

        handlerResult.AddMessage("Redirect is null", ResultMessageType.Error);
        return handlerResult;
    }

    public async Task<HandlerResult<Redirect>> QueryRedirectsAsync(QueryRedirectsParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var query = dbContext.Redirects.AsQueryable();

        if (parameters.AsNoTracking)
        {
            query = query.AsNoTracking();
        }

        if (!parameters.SearchTerm.IsNullOrWhiteSpace())
        {
            query = query.Where(x => x.OldUrl.Contains(parameters.SearchTerm) || 
                                     x.NewUrl.Contains(parameters.SearchTerm));
        }

        if (parameters.WhereClause != null)
        {
            query = query.Where(parameters.WhereClause);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        if (!parameters.OrderBy.IsNullOrWhiteSpace())
        {
            query = query.OrderBy(parameters.OrderBy);
        }
        else
        {
            query = query.OrderBy(x => x.OldUrl);
        }

        if (parameters.AmountPerPage > 0)
        {
            query = query.Skip(parameters.PageIndex * parameters.AmountPerPage).Take(parameters.AmountPerPage);
        }

        var items = await query.ToListAsync(cancellationToken);

        return new HandlerResult<Redirect>
        {
            Success = true,
            Items = items,
            TotalCount = totalCount
        };
    }

    public async Task<HandlerResult<Redirect>> DeleteRedirectAsync(DeleteRedirectParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var handlerResult = new HandlerResult<Redirect>();

        var redirect = await dbContext.Redirects
            .FirstOrDefaultAsync(x => x.Id == parameters.Id, cancellationToken);

        if (redirect != null)
        {
            dbContext.Redirects.Remove(redirect);
            await dbContext.SaveChangesAsync(cancellationToken);
            handlerResult.Messages.Add(new ResultMessage("Redirect deleted successfully", ResultMessageType.Success));
            handlerResult.Success = true;
        }
        else
        {
            handlerResult.AddMessage("Redirect not found", ResultMessageType.Error);
        }

        return handlerResult;
    }
}