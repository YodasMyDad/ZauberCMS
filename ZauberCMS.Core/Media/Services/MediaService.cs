using MediatR;
using ZauberCMS.Core.Media.Commands;
using ZauberCMS.Core.Media.Interfaces;
using ZauberCMS.Core.Media.Models;
using ZauberCMS.Core.Media.Parameters;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Media.Services;

public class MediaService(IMediator mediator) : IMediaService
{
    public async Task<Media?> GetMediaAsync(GetMediaParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new GetMediaCommand
        {
            Id = parameters.Id,
            IncludeChildren = parameters.IncludeChildren,
            IncludeParent = parameters.IncludeParent,
            AsNoTracking = parameters.AsNoTracking
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<Media>> SaveMediaAsync(SaveMediaParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new SaveMediaCommand
        {
            Media = parameters.Media,
            UpdatedBy = parameters.UpdatedBy,
            FileStream = parameters.FileStream
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<Media>> QueryMediaAsync(QueryMediaParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new QueryMediaCommand
        {
            AmountPerPage = parameters.AmountPerPage,
            PageIndex = parameters.PageIndex,
            OrderBy = parameters.OrderBy,
            WhereClause = parameters.WhereClause,
            IncludeChildren = parameters.IncludeChildren,
            IncludeParent = parameters.IncludeParent,
            AsNoTracking = parameters.AsNoTracking,
            ParentId = parameters.ParentId,
            SearchTerm = parameters.SearchTerm
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<Media>> DeleteMediaAsync(DeleteMediaParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new DeleteMediaCommand
        {
            Id = parameters.Id
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<bool> HasChildMediaAsync(HasChildMediaParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new HasChildMediaCommand
        {
            Id = parameters.Id
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<List<string>> GetRestrictedMediaUrlsAsync(GetRestrictedMediaUrlsParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new GetRestrictedMediaUrls
        {
            UserId = parameters.UserId
        };
        
        return await mediator.Send(command, cancellationToken);
    }
}