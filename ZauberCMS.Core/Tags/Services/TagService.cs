using MediatR;
using ZauberCMS.Core.Tags.Commands;
using ZauberCMS.Core.Tags.Interfaces;
using ZauberCMS.Core.Tags.Models;
using ZauberCMS.Core.Tags.Parameters;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Tags.Services;

public class TagService(IMediator mediator) : ITagService
{
    public async Task<HandlerResult<Tag>> SaveTagAsync(SaveTagParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new SaveTagCommand
        {
            Tag = parameters.Tag,
            UpdatedBy = parameters.UpdatedBy
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<Tag>> QueryTagAsync(QueryTagParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new QueryTagCommand
        {
            AmountPerPage = parameters.AmountPerPage,
            PageIndex = parameters.PageIndex,
            OrderBy = parameters.OrderBy,
            WhereClause = parameters.WhereClause,
            AsNoTracking = parameters.AsNoTracking,
            SearchTerm = parameters.SearchTerm,
            ContentId = parameters.ContentId,
            IncludeTagItems = parameters.IncludeTagItems
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<Tag>> DeleteTagAsync(DeleteTagParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new DeleteTagCommand
        {
            Id = parameters.Id,
            UpdatedBy = parameters.UpdatedBy
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<TagItem>> SaveTagItemAsync(SaveTagItemParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new SaveTagItemCommand
        {
            TagItem = parameters.TagItem,
            UpdatedBy = parameters.UpdatedBy
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<TagItem>> DeleteTagItemAsync(DeleteTagItemParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new DeleteTagItemCommand
        {
            Id = parameters.Id,
            UpdatedBy = parameters.UpdatedBy
        };
        
        return await mediator.Send(command, cancellationToken);
    }
}