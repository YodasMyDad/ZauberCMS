using Blazored.Modal;
using Blazored.Modal.Services;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Radzen;
using ZauberCMS.Components.Admin.ContentSection.Dialogs;
using ZauberCMS.Core;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared;

namespace ZauberCMS.Components.Trees.ContextMenus.ContentMenus;

public class RestrictAccessContextMenu(
    IServiceProvider serviceProvider,
    NotificationService notificationService,
    IMediator mediator,
    AppState appState)
    : ITreeContextMenu
{
    public List<string> Sections => [Constants.Sections.ContentSection];
    public List<string> TreeAlias => [];
    public string Text(TreeItemContextMenuEventArgs args) => "Public Access";

    public string Icon(TreeItemContextMenuEventArgs args) => "lock";

    public string IconColor(TreeItemContextMenuEventArgs args) => string.Empty;

    public bool CanShowContextMenu(TreeItemContextMenuEventArgs args)
    {
        if (args.Value is Content content)
        {
            return content.Published && !content.ViewComponent.IsNullOrWhiteSpace();
        }

        return false;
    }

    private IModalReference? Modal { get; set; }


    public async Task ContextMenuAction(TreeItemContextMenuEventArgs args, MenuItemEventArgs e,
        NavigationManager navigationManager,
        ContextMenuService contextMenuService, IModalService modalService)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var currentUser = await mediator.GetCurrentUser();
        contextMenuService.Close();
        var content = (Content)args.Value!;
        var parameters = new Dictionary<string, object>
        {
            { nameof(RestrictAccess.ContentId), content.Id }
        };

        Modal = modalService.OpenSidePanel<RestrictAccess>("Public Access", parameters);
        var result = await Modal.Result;
        if (result is { Confirmed: true, Data: (bool alreadyHadContentRoles, List<Role> selectedRoles) })
        {
            // Need to check if the roles are defined by an ancestor
            // If so we ignore this and return a message
            var ancestorHasRoles = false;
            var parentId = content.ParentId;
            if (parentId != null)
            {
                while (parentId != null)
                {
                    var parent = await dbContext.Contents.Include(x => x.ContentRoles).FirstOrDefaultAsync(c => c.Id == parentId);
                    if (parent is { ContentRoles.Count: > 0 })
                    {
                        ancestorHasRoles = true;
                        break;
                    }
                    parentId = parent?.ParentId;
                }   
            }
            
            if (ancestorHasRoles)
            {
                notificationService.ShowErrorNotification(
                    "Cannot change access to this content as it has roles already defined by an ancestor.");
            }
            
            // Do any ancestors have roles on them
            else
            {
                // Fetch all descendants and the original content, as these need to be added or removed too
                var descendants = dbContext.Contents
                    .WherePathLike(content.Id)
                    .Include(x => x.ContentRoles)
                    .ToList();

                if (alreadyHadContentRoles && selectedRoles.Count == 0)
                {
                    // Remove all the content roles
                    foreach (var descendant in descendants)
                    {
                        descendant.ContentRoles.Clear();
                    }

                    await dbContext.SaveChangesAsync();
                    notificationService.ShowSuccessNotification("Public access removed");
                }
                else
                {
                    var wasError = false;
                    
                    // Add the content roles to the content and descendants 
                    foreach (var descendant in descendants)
                    {
                        var saveResult = await mediator.Send(new SaveContentCommand
                        {
                            Content = descendant,
                            Roles = selectedRoles,
                            UpdateContentRoles = true,
                            ExcludePropertyData = true
                        });
                        
                        if (saveResult.Success == false)
                        {
                            notificationService.ShowNotifications(saveResult.Messages);
                            wasError = true;
                            break;
                        }
                    }

                    if (wasError == false)
                    {
                        notificationService.ShowSuccessNotification("Public access updated");   
                    }
                }
            }
            
            await appState.NotifyContentChanged(content, currentUser?.Name ?? "Unknown");
        }
    }

    public int SortOrder => 50;
}