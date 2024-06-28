using AutoMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Content.Models;
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
                    var baseSlug = _slugHelper.GenerateSlug(requestContent.Name);
                    requestContent.Url = GenerateUniqueUrl(dbContext, baseSlug);
                }

                if (requestContent.ContentTypeAlias.IsNullOrWhiteSpace())
                {
                    var contentType = dbContext.ContentTypes.AsTracking()
                        .FirstOrDefault(x => x.Id == requestContent.ContentTypeId);
                    requestContent.ContentTypeAlias = contentType?.Alias;
                }
            
                // Get the DB version
                var content = dbContext.Contents
                    .FirstOrDefault(x => x.Id == requestContent.Id);

                if (content == null)
                {
                    content = requestContent;
                    dbContext.Contents.Add(content);
                }
                else
                {
                    // Map the updated properties
                    mapper.Map(requestContent, content);
                    content.DateUpdated = DateTime.UtcNow;
                    
                    // Update ContentPropertyValues
                    UpdateContentPropertyValues(dbContext, content, requestContent.PropertyData);
                }
                
                // Calculate and set the Path property
                content.Path = BuildPath(content, dbContext);
                
                return await dbContext.SaveChangesAndLog(null, handlerResult, cancellationToken);
            }
        }

        handlerResult.AddMessage("Content is null", ResultMessageType.Error);
        return handlerResult;
    }

    private void UpdateContentPropertyValues(ZauberDbContext dbContext, Models.Content content, List<ContentPropertyValue> newPropertyValues)
    {
        var existingPropertyValues = content.PropertyData;

        // Remove deleted items
        var deletedItems = existingPropertyValues.Where(epv => newPropertyValues.All(npv => npv.Id != epv.Id)).ToList();
        foreach (var deletedItem in deletedItems)
        {
            dbContext.ContentPropertyValues.Remove(deletedItem);
        }

        // Add or update items
        foreach (var newPropertyValue in newPropertyValues)
        {
            var existingPropertyValue = existingPropertyValues.FirstOrDefault(epv => epv.Id == newPropertyValue.Id);
            if (existingPropertyValue == null)
            {
                // New property value
                content.PropertyData.Add(newPropertyValue);
            }
            else
            {
                // Existing property value, update its properties
                mapper.Map(newPropertyValue, existingPropertyValue);
            }
        }
    }
    
    private static List<Guid> BuildPath(Models.Content content, ZauberDbContext dbContext)
    {
        var path = new List<Guid>();
        var currentContent = content;
        while (currentContent != null)
        {
            path.Insert(0, currentContent.Id);
            currentContent = currentContent.ParentId.HasValue ? dbContext.Contents.FirstOrDefault(c => c.Id == currentContent.ParentId.Value) : null;
        }
        return path;
    }
    
    private static string GenerateUniqueUrl(ZauberDbContext dbContext, string baseSlug)
    {
        var url = baseSlug;

        // Check if the base URL already exists
        if (!dbContext.Contents.Any(c => c.Url == url))
        {
            return url;
        }

        // If it does, append a number to the base slug to make it unique
        var counter = 1;
        while (dbContext.Contents.Any(c => c.Url == url))
        {
            url = $"{baseSlug}-{counter}";
            counter++;
        }

        return url;
    }

}