using AutoMapper;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Handlers;

public class SaveContentTypeHandler(
    IServiceProvider serviceProvider,
    IMapper mapper)
    : IRequestHandler<SaveContentTypeCommand, HandlerResult<ContentType>>
{
    public async Task<HandlerResult<ContentType>> Handle(SaveContentTypeCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        
        var handlerResult = new HandlerResult<ContentType>();

        if (request.ContentType != null)
        {
            if (request.ContentType.Alias.IsNullOrWhiteSpace())
            {
                request.ContentType.Alias = request.ContentType.Name.ToAlias();
            }
            
            // Get the DB version
            var contentType = dbContext.ContentTypes
                .FirstOrDefault(x => x.Id == request.ContentType.Id);

            if (contentType == null)
            {
                // Check if the ContentType.Alias is unique
                var containsAlias = dbContext.ContentTypes.Any(x => x.Alias == request.ContentType.Alias);
                if (containsAlias)
                {
                    // If the ContentType.Alias is not unique
                    handlerResult.AddMessage("Content Type Alias already exists, change the content type name", ResultMessageType.Error);
                    return handlerResult;
                }
                
                contentType = request.ContentType;
                dbContext.ContentTypes.Add(contentType);
            }
            else
            {
                // Map the updated properties
                mapper.Map(request.ContentType, contentType);  
                contentType.DateUpdated = DateTime.UtcNow;
            }
            
            return await dbContext.SaveChangesAndLog(contentType, handlerResult, cancellationToken);
        }

        handlerResult.AddMessage("ContentType is null", ResultMessageType.Error);
        return handlerResult;
    }
}