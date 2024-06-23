using AutoMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Handlers;

public class SaveContentHandler(
    IServiceProvider serviceProvider,
    IMapper mapper)
    : IRequestHandler<SaveContentCommand, HandlerResult<List<Models.Content>>>
{
    private readonly SlugHelper _slugHelper = new();
    
    public async Task<HandlerResult<List<Models.Content>>> Handle(SaveContentCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        
        var handlerResult = new HandlerResult<List<Models.Content>>();

        if (request.Content.Count != 0)
        {
            foreach (var requestContent in request.Content)
            {
                if (requestContent.Url.IsNullOrWhiteSpace())
                {
                    //TODO - Need to check url doesn't already exist
                    requestContent.Url = _slugHelper.GenerateSlug(requestContent.Name);
                }

                if (requestContent.ContentTypeAlias.IsNullOrWhiteSpace())
                {
                    var contentType = dbContext.ContentTypes.AsTracking()
                        .FirstOrDefault(x => x.Id == requestContent.ContentTypeId);
                    requestContent.ContentTypeAlias = contentType?.Alias;
                }
            
                // Get the DB version
                var content = dbContext.Content
                    .FirstOrDefault(x => x.Id == requestContent.Id);

                if (content == null)
                {
                    content = requestContent;
                    dbContext.Content.Add(content);
                }
                else
                {
                    // Map the updated properties
                    mapper.Map(requestContent, content);
                    content.DateUpdated = DateTime.UtcNow;
                }
                
                return await dbContext.SaveChangesAndLog(null, handlerResult, cancellationToken);
            }
        }

        handlerResult.AddMessage("Content is null", ResultMessageType.Error);
        return handlerResult;
    }
}