using MediatR;
using ZauberCMS.Core.Data.Commands;
using ZauberCMS.Core.Data.Interfaces;
using ZauberCMS.Core.Data.Parameters;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Data.Services;

public class DataService(IMediator mediator) : IDataService
{
    public async Task<object?> GetGlobalDataAsync(GetGlobalDataParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new GetGlobalDataCommand
        {
            Key = parameters.Key,
            DomainId = parameters.DomainId
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<object>> SaveGlobalDataAsync(SaveGlobalDataParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new SaveGlobalDataCommand
        {
            Key = parameters.Key,
            Value = parameters.Value,
            DomainId = parameters.DomainId
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<object>> MultiQueryAsync(MultiQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new MultiQueryCommand
        {
            Queries = parameters.Queries
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<T>> GetDataGridAsync<T>(DataGridParameters<T> parameters, CancellationToken cancellationToken = default)
    {
        var command = new DataGridCommand<T>
        {
            AmountPerPage = parameters.AmountPerPage,
            PageIndex = parameters.PageIndex,
            OrderBy = parameters.OrderBy,
            WhereClause = parameters.WhereClause,
            AsNoTracking = parameters.AsNoTracking
        };
        
        return await mediator.Send(command, cancellationToken);
    }
}