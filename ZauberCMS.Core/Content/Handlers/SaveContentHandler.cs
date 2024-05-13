using AutoMapper;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Handlers;

public class SaveContentHandler(
    IServiceProvider serviceProvider,
    IMapper mapper)
    : IRequestHandler<SaveContentCommand, HandlerResult<Models.Content>>
{
    private readonly SlugHelper _slugHelper = new(new SlugHelper.Config
    {
        CharacterReplacements = new Dictionary<string, string> {{" ", "."}}
    });
    
    public async Task<HandlerResult<Models.Content>> Handle(SaveContentCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        
        var handlerResult = new HandlerResult<Models.Content>();

        if (request.Content != null)
        {
            if (request.Content.Url.IsNullOrWhiteSpace())
            {
                //TODO - Need to check url doesn't already exist
                request.Content.Url = _slugHelper.GenerateSlug(request.Content.Name);
            }
            
            // Get the DB version
            var content = dbContext.Content
                .FirstOrDefault(x => x.Id == request.Content.Id);

            if (content == null)
            {
                content = request.Content;
                dbContext.Content.Add(content);
            }
            else
            {
                // Map the updated properties
                mapper.Map(request.Content, content);   
            }
            
            return await dbContext.SaveChangesAndLog(content, handlerResult, cancellationToken);
        }

        handlerResult.AddMessage("Content is null", ResultMessageType.Error);
        return handlerResult;
    }
}