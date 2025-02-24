using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Handlers;

public class CopyContentHandler(
    IServiceProvider serviceProvider,
    IMapper mapper,
    IMediator mediator,
    AuthenticationStateProvider authenticationStateProvider) 
    : IRequestHandler<CopyContentCommand, HandlerResult<Models.Content>>
{
    public async Task<HandlerResult<Models.Content>> Handle(CopyContentCommand request,
        CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = await userManager.GetUserAsync(authState.User);

        var handlerResult = new HandlerResult<Models.Content>();

        // Retrieve the content to copy
        var contentToCopy = await dbContext.Contents
            .AsNoTracking()
            .Include(content => content.PropertyData)
            .FirstOrDefaultAsync(x => x.Id == request.ContentToCopy, cancellationToken);

        if (contentToCopy == null)
        {
            handlerResult.Success = false;
            handlerResult.AddMessage("Unable to copy, as no Content with that ID exists", ResultMessageType.Error);
            return handlerResult;
        }

        // Prepare a dictionary to map old IDs to new IDs
        var idMap = new Dictionary<Guid, Guid>();

        // Copy the root content to a new item
        var newParentId = request.CopyTo ?? contentToCopy.ParentId;
        var copiedContent = CreateCopy(contentToCopy, newParentId);

        // Add the root content's old ID and new ID to idMap
        idMap[contentToCopy.Id] = copiedContent.Id;

        // Add the copied content's ID to its path
        if (newParentId.HasValue)
        {
            // If copying to a new parent, update its path
            var parentContent = await dbContext.Contents
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == newParentId, cancellationToken);

            if (parentContent != null)
            {
                copiedContent.Path = [..parentContent.Path, copiedContent.Id];
            }
        }
        else
        {
            // If no parent is provided, the copied content becomes root
            copiedContent.Path = [copiedContent.Id];
        }

        // Add the root content to the database
        dbContext.Add(copiedContent);

        // Include descendants if specified
        if (request.IncludeDescendants)
        {
            // Fetch all descendants of the original content
            var descendants = await dbContext.Contents
                .WherePathLike(contentToCopy.Id)
                .AsNoTracking()
                .Include(content => content.PropertyData)
                .ToListAsync(cancellationToken);
            
            foreach (var descendant in descendants.Where(x => x.Id != contentToCopy.Id))
            {
                // Get the parent ID of the descendant from the idMap
                if (descendant.ParentId != null)
                {
                    var newParentIdForDescendant = idMap[descendant.ParentId.Value];

                    // Create a copy for each descendant
                    var copiedDescendant = CreateCopy(descendant, newParentIdForDescendant);

                    // Update the descendant's path in the tree using idMap
                    copiedDescendant.Path = descendant.Path
                        .Select(id => idMap.TryGetValue(id, out var value) ? value : id)
                        .ToList();

                    // Add the descendant's old ID and new ID to idMap
                    idMap[descendant.Id] = copiedDescendant.Id;

                    // Add the copied descendant to the database
                    dbContext.Add(copiedDescendant);
                }
            }
        }

        // Save everything to the database
        await dbContext.SaveChangesAsync(cancellationToken);
        await user.AddAudit(contentToCopy, contentToCopy.Name, AuditExtensions.AuditAction.Copy, mediator, cancellationToken);

        // Return the root copied content as the result
        handlerResult.Success = true;
        handlerResult.AddMessage("Content copied successfully.", ResultMessageType.Success);
        return handlerResult;


// Helper function to create a copy of a content item
        Models.Content CreateCopy(Models.Content original, Guid? parentId = null)
        {
            // Step 1: Map using the existing ContentMapper
            var copy = mapper.Map<Models.Content>(original);

            // Step 2: Manually override or configure properties not handled in ContentMapper
            copy.Id = Guid.NewGuid(); // Generate a new ID
            copy.Name = original.Name + " (Copy)"; // Change the name for the copy
            copy.Url = original.Url + "-copy"; // Adjust the URL
            copy.LastUpdatedById = user?.Id; // Set the user who updates this
            copy.ParentId = parentId; // Assign the new parent ID
            copy.DateCreated = DateTime.UtcNow; // Default the created time to now
            copy.DateUpdated = DateTime.UtcNow; // Default the updated time to now
            copy.Path = []; // Assign an empty list for now (to be set later)
            copy.Published = false; // Default the copied content to unpublished
            copy.Deleted = false; // New content is not deleted
            copy.PropertyData = original.PropertyData.Select(p => new ContentPropertyValue
            {
                Id = Guid.NewGuid(), // Generate new ID for each property
                DateUpdated = p.DateUpdated,
                DateCreated = p.DateCreated,
                ContentTypePropertyId = p.ContentTypePropertyId,
                Value = p.Value,
                ContentId = p.ContentId,
                Alias = p.Alias
            }).ToList();

            return copy;
        }
    }
}