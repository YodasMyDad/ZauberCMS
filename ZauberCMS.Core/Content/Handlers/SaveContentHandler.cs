﻿using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Content.Handlers;

public class SaveContentHandler(
    IServiceProvider serviceProvider,
    IMapper mapper,
    IOptions<ZauberSettings> settings,
    IMediator mediator,
    ICacheService cacheService,
    AuthenticationStateProvider authenticationStateProvider,
    ExtensionManager extensionManager)
    : IRequestHandler<SaveContentCommand, HandlerResult<Models.Content>>
{
    private readonly SlugHelper _slugHelper = new();

    public async Task<HandlerResult<Models.Content>> Handle(SaveContentCommand request,
        CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = await userManager.GetUserAsync(authState.User);
        var isUpdate = true;
        var handlerResult = new HandlerResult<Models.Content>();

        if (request.Content != null)
        {
            // ReSharper disable once EntityFramework.NPlusOne.IncompleteDataQuery
            var unpublishedContent = dbContext.UnpublishedContent.FirstOrDefault(x => x.Id == request.Content.UnpublishedContentId);
            
            if (request.SaveUnpublishedOnly)
            {
                var isNew = request.Content.UnpublishedContentId == null;
                
                unpublishedContent ??= new UnpublishedContent();
                
                // ReSharper disable once EntityFramework.NPlusOne.IncompleteDataUsage
                mapper.Map(request.Content, unpublishedContent.JsonContent);
                
                // Slight hack. Because we mapped, it removed the property data
                // ReSharper disable once EntityFramework.NPlusOne.IncompleteDataUsage
                unpublishedContent.JsonContent.PropertyData = request.Content.PropertyData;
                // ReSharper disable once EntityFramework.NPlusOne.IncompleteDataUsage
                unpublishedContent.JsonContent.UnpublishedContentId = unpublishedContent.Id;

                if (isNew)
                {
                    var dbContent = dbContext.Contents.FirstOrDefault(x => request.Content.Id == x.Id);
                    if (dbContent != null)
                    {
                        // This should never be null!
                        dbContent.UnpublishedContentId = unpublishedContent.Id;
                    }
                    dbContext.UnpublishedContent.Add(unpublishedContent);
                }
                
                return await dbContext.SaveChangesAndLog(null, handlerResult, cacheService, extensionManager, cancellationToken);
            }

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
                .Include(x => x.ContentRoles).ThenInclude(x => x.Role)
                .FirstOrDefault(x => x.Id == request.Content.Id);
            
            if (content == null)
            {
                isUpdate = false;
                content = request.Content;
                content.LastUpdatedById = user!.Id;
                dbContext.Contents.Add(content);
            }
            else
            {
                // Map the updated properties
                mapper.Map(request.Content, content);
                content.LastUpdatedById = user!.Id;
                content.DateUpdated = DateTime.UtcNow;

                if (!request.ExcludePropertyData)
                {
                    // Update ContentPropertyValues
                    UpdateContentPropertyValues(dbContext, content, request.Content.PropertyData);   
                }
            }

            if (request.UpdateContentRoles)
            {
                UpdateContentRoles(dbContext, content, request);   
            }
            
            // If we get here delete any unpublished content
            if (unpublishedContent != null)
            {
                dbContext.UnpublishedContent.Remove(unpublishedContent);
            }
            
            // Calculate and set the Path property
            content.Path = content.BuildPath(dbContext, isUpdate, settings);
            
            await user.AddAudit(content, content.Name, isUpdate ? AuditExtensions.AuditAction.Update : AuditExtensions.AuditAction.Create, mediator, cancellationToken);
            return await dbContext.SaveChangesAndLog(content, handlerResult, cacheService, extensionManager, cancellationToken);
        }

        handlerResult.AddMessage("Content is null", ResultMessageType.Error);
        return handlerResult;
    }

    private void UpdateContentRoles(ZauberDbContext dbContext, Models.Content content, SaveContentCommand request)
    {
        // Fetch existing ContentRoles for the content
        var existingRoles = dbContext.ContentRoles
            .Where(r => r.ContentId == content.Id)
            .ToList();

        // Remove roles that are no longer in the new list
        var rolesToRemove = existingRoles
            .Where(er => request.Roles.All(rr => rr.Id != er.RoleId))
            .ToList();

        if (rolesToRemove.Count != 0)
        {
            dbContext.ContentRoles.RemoveRange(rolesToRemove);
        }

        // Add roles that are in the new list but not already present in the DB
        var rolesToAdd = request.Roles
            .Where(rr => existingRoles.All(er => er.RoleId != rr.Id))
            .ToList();

        if (rolesToAdd.Count != 0)
        {
            foreach (var role in rolesToAdd)
            {
                var contentRole = new ContentRole
                {
                    ContentId = content.Id,
                    RoleId = role.Id
                };
                dbContext.ContentRoles.Add(contentRole);
            }
        }
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