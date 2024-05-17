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
    private readonly SlugHelper _slugHelper = new(new SlugHelper.Config
    {
        CharacterReplacements = new Dictionary<string, string> {{" ", "."}}
    });
    
    public async Task<HandlerResult<ContentType>> Handle(SaveContentTypeCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        
        var handlerResult = new HandlerResult<ContentType>();

        if (request.ContentType != null)
        {
            if (request.ContentType.Alias.IsNullOrWhiteSpace())
            {
                request.ContentType.Alias = _slugHelper.GenerateSlug(request.ContentType.Name);
            }
            
            // Get the DB version
            var contentType = dbContext.ContentTypes
                .FirstOrDefault(x => x.Id == request.ContentType.Id);

            if (contentType == null)
            {
                contentType = request.ContentType;
                dbContext.ContentTypes.Add(contentType);
            }
            else
            {
                // Map the updated properties
                mapper.Map(request.ContentType, contentType);   
            }
            
            return await dbContext.SaveChangesAndLog(contentType, handlerResult, cancellationToken);
        }

        handlerResult.AddMessage("ContentType is null", ResultMessageType.Error);
        return handlerResult;
    }
}