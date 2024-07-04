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
    : IRequestHandler<SaveContentCommand, HandlerResult<Models.Content>>
{
    private readonly SlugHelper _slugHelper = new();

    public async Task<HandlerResult<Models.Content>> Handle(SaveContentCommand request,
        CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();

        var handlerResult = new HandlerResult<Models.Content>();

        if (request.Content != null)
        {
            if (request.Content.Url.IsNullOrWhiteSpace())
            {
                var baseSlug = _slugHelper.GenerateSlug(request.Content.Name);
                request.Content.Url = GenerateUniqueUrl(dbContext, baseSlug);
            }

            if (request.Content.ContentTypeAlias.IsNullOrWhiteSpace())
            {
                var contentType = dbContext.ContentTypes.AsTracking()
                    .FirstOrDefault(x => x.Id == request.Content.ContentTypeId);
                request.Content.ContentTypeAlias = contentType?.Alias;
            }

            // Get the DB version
            var content = dbContext.Contents
                .Include(x => x.PropertyData)
                .FirstOrDefault(x => x.Id == request.Content.Id);

            if (content == null)
            {
                content = request.Content;
                dbContext.Contents.Add(content);
            }
            else
            {
                // Map the updated properties
                mapper.Map(request.Content, content);
                content.DateUpdated = DateTime.UtcNow;

                if (!request.ExcludePropertyData)
                {
                    // Update ContentPropertyValues
                    UpdateContentPropertyValues(dbContext, content, request.Content.PropertyData);   
                }
            }

            // Calculate and set the Path property
            content.Path = BuildPath(content, dbContext);

            return await dbContext.SaveChangesAndLog(null, handlerResult, cancellationToken);
        }

        handlerResult.AddMessage("Content is null", ResultMessageType.Error);
        return handlerResult;
    }

    private void UpdateContentPropertyValues(ZauberDbContext dbContext, Models.Content content,
        List<ContentPropertyValue> newPropertyValues)
    {
        // Remove deleted items
        var deletedItems = content.PropertyData.Where(epv => newPropertyValues.All(npv => npv.Id != epv.Id)).ToList();
        foreach (var deletedItem in deletedItems)
        {
            dbContext.ContentPropertyValues.Remove(deletedItem);
        }

        // Add or update items
        foreach (var newPropertyValue in newPropertyValues)
        {
            var existingPropertyValue = content.PropertyData.FirstOrDefault(epv => epv.Id == newPropertyValue.Id);
            if (existingPropertyValue == null)
            {
                // New property value
                dbContext.ContentPropertyValues.Add(newPropertyValue);
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
            currentContent = currentContent.ParentId.HasValue
                ? dbContext.Contents.FirstOrDefault(c => c.Id == currentContent.ParentId.Value)
                : null;
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