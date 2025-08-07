using MediatR;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Services;

public class ContentService(IMediator mediator) : IContentService
{
    public async Task<Content?> GetContentAsync(GetContentParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new GetContentCommand
        {
            Cached = parameters.Cached,
            IncludeUnpublishedContent = parameters.IncludeUnpublishedContent,
            IncludeContentRoles = parameters.IncludeContentRoles,
            Id = parameters.Id,
            IncludeChildren = parameters.IncludeChildren,
            IncludeParent = parameters.IncludeParent,
            IncludeUnpublished = parameters.IncludeUnpublished,
            AsNoTracking = parameters.AsNoTracking,
            ContentTypeAlias = parameters.ContentTypeAlias
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<Content>> SaveContentAsync(SaveContentParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new SaveContentCommand
        {
            Content = parameters.Content,
            SaveUnpublishedOnly = parameters.SaveUnpublishedOnly,
            UpdatedBy = parameters.UpdatedBy
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<Content>> QueryContentAsync(QueryContentParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new QueryContentCommand
        {
            AmountPerPage = parameters.AmountPerPage,
            PageIndex = parameters.PageIndex,
            OrderBy = parameters.OrderBy,
            WhereClause = parameters.WhereClause,
            IncludeChildren = parameters.IncludeChildren,
            IncludeParent = parameters.IncludeParent,
            IncludeUnpublished = parameters.IncludeUnpublished,
            AsNoTracking = parameters.AsNoTracking,
            ContentTypeAlias = parameters.ContentTypeAlias,
            ContentTypeAliases = parameters.ContentTypeAliases,
            ParentId = parameters.ParentId,
            DomainId = parameters.DomainId,
            LanguageCode = parameters.LanguageCode,
            SearchTerm = parameters.SearchTerm,
            SearchFields = parameters.SearchFields,
            IncludeContentRoles = parameters.IncludeContentRoles,
            IncludeUnpublishedContent = parameters.IncludeUnpublishedContent,
            Tag = parameters.Tag
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<Content>> DeleteContentAsync(DeleteContentParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new DeleteContentCommand
        {
            Id = parameters.Id
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<Content>> CopyContentAsync(CopyContentParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new CopyContentCommand
        {
            ContentId = parameters.ContentId,
            ParentId = parameters.ParentId,
            IncludeChildren = parameters.IncludeChildren
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<Content?> GetContentFromRequestAsync(GetContentFromRequestParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new GetContentFromRequestCommand
        {
            Url = parameters.Url,
            DomainId = parameters.DomainId,
            LanguageCode = parameters.LanguageCode,
            IncludeUnpublished = parameters.IncludeUnpublished,
            AsNoTracking = parameters.AsNoTracking,
            IncludeChildren = parameters.IncludeChildren,
            IncludeParent = parameters.IncludeParent,
            IncludeContentRoles = parameters.IncludeContentRoles,
            IncludeUnpublishedContent = parameters.IncludeUnpublishedContent
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<ContentType?> GetContentTypeAsync(GetContentTypeParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new GetContentTypeCommand
        {
            Id = parameters.Id,
            Alias = parameters.Alias
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<ContentType>> SaveContentTypeAsync(SaveContentTypeParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new SaveContentTypeCommand
        {
            ContentType = parameters.ContentType
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<ContentType>> QueryContentTypesAsync(QueryContentTypesParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new QueryContentTypesCommand
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

    public async Task<HandlerResult<ContentType>> DeleteContentTypeAsync(DeleteContentTypeParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new DeleteContentTypeCommand
        {
            Id = parameters.Id
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<Domain?> GetDomainAsync(GetDomainParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new GetDomainCommand
        {
            Id = parameters.Id,
            DomainName = parameters.DomainName
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<Domain>> SaveDomainAsync(SaveDomainParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new SaveDomainCommand
        {
            Domain = parameters.Domain
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<Domain>> QueryDomainAsync(QueryDomainParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new QueryDomainCommand
        {
            AmountPerPage = parameters.AmountPerPage,
            PageIndex = parameters.PageIndex,
            OrderBy = parameters.OrderBy,
            WhereClause = parameters.WhereClause,
            AsNoTracking = parameters.AsNoTracking
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<Domain>> DeleteDomainAsync(DeleteDomainParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new DeleteDomainCommand
        {
            Id = parameters.Id
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<bool> AnyContentAsync(AnyContentParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new AnyContentCommand
        {
            ContentTypeAlias = parameters.ContentTypeAlias
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<bool> HasChildContentAsync(HasChildContentParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new HasChildContentCommand
        {
            Id = parameters.Id
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<bool> HasChildContentTypeAsync(HasChildContentTypeParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new HasChildContentTypeCommand
        {
            Id = parameters.Id
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<List<string>> GetContentLanguagesAsync(GetContentLanguagesParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new GetContentLanguagesCommand
        {
            ContentId = parameters.ContentId
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<List<Domain>> GetCachedDomainsAsync(CachedDomainsParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new CachedDomainsCommand
        {
            LanguageCode = parameters.LanguageCode
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<Content>> ClearUnpublishedContentAsync(ClearUnpublishedContentParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new ClearUnpublishedContentCommand
        {
            ContentId = parameters.ContentId
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<Content>> GetDataGridContentAsync(DataGridContentParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new DataGridContentCommand
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
}