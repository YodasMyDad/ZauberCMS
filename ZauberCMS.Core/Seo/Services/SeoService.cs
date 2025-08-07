using MediatR;
using ZauberCMS.Core.Seo.Commands;
using ZauberCMS.Core.Seo.Interfaces;
using ZauberCMS.Core.Seo.Models;
using ZauberCMS.Core.Seo.Parameters;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Seo.Services;

public class SeoService(IMediator mediator) : ISeoService
{
    public async Task<HandlerResult<Redirect>> SaveRedirectAsync(SaveRedirectParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new SaveRedirectCommand
        {
            Redirect = parameters.Redirect
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<Redirect>> QueryRedirectsAsync(QueryRedirectsParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new QueryRedirectsCommand
        {
            AmountPerPage = parameters.AmountPerPage,
            PageIndex = parameters.PageIndex,
            OrderBy = parameters.OrderBy,
            WhereClause = parameters.WhereClause,
            AsNoTracking = parameters.AsNoTracking,
            SearchTerm = parameters.SearchTerm
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<Redirect>> DeleteRedirectAsync(DeleteRedirectParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new DeleteRedirectCommand
        {
            Id = parameters.Id
        };
        
        return await mediator.Send(command, cancellationToken);
    }
}