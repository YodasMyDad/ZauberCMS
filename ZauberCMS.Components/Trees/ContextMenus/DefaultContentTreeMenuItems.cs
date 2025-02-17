using Blazored.Modal.Services;
using MediatR;
using Microsoft.AspNetCore.Components;
using Radzen;
using ZauberCMS.Components.Admin.ContentSection.Dialogs;
using ZauberCMS.Components.Admin.Shared;
using ZauberCMS.Core;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Components.Trees.ContextMenus;

public class DefaultContentTreeMenuItems(IMediator mediator, 
    DialogService confirmService, 
    NotificationService notificationService,
    AppState appState,
    TreeState treeState,
    ICacheService cacheService) : ITreeContextMenu
{
    public List<string> Sections => [Constants.Sections.ContentSection];
    public List<string> TreeAlias { get; } = [];
    
    public List<ContextMenuItem> ContentMenuItems(TreeItemContextMenuEventArgs args)
    {
        var globalMenuItems = new List<ContextMenuItem>
        {
            new() { Text = "Create", Value = 1 },
            new() { Text = "Delete", Value = 2 },
            new() { Text = "Sort", Value = 3 }
        };
        
        var items = new List<ContextMenuItem>();
        
        if (args.Value is Content content)
        {
            items.AddRange(globalMenuItems);

            var domainCultureMenuText = content.IsRootContent ? "Culture & Domains" : "Culture";
            items.Add(new ContextMenuItem { Text = domainCultureMenuText, Value = 4 });

            if (content.IsRootContent)
            {
                items.Add(new ContextMenuItem { Text = "Refresh", Value = 5});
            }
        }
        
        return items;
    }
    
    public async Task ContextMenuEvents(TreeItemContextMenuEventArgs args,
        MenuItemEventArgs e,
        NavigationManager navigationManager, 
        ContextMenuService contextMenuService,
        IModalService modalService)
    {
        if (args.Value is Content content)
        {
            var currentUser = await mediator.GetCurrentUser();
            var dbContent = await mediator.Send(new GetContentCommand { Id = content.Id, IncludeChildren = true });
                //var text = args.Text;
                switch(e.Value)
                {
                    case 1:
                        // Handle Create
                        contextMenuService.Close();
                        navigationManager.NavigateTo($"/admin/createcontent/{content.Id}");
                        break;
                    case 2:
                        // Confirm dialogue, say if there are children, and confirm then delete all
                            var hasChildren = dbContent!.Children.Count != 0;
                            var message = hasChildren
                                ? "Move this content and it's children to the recycle bin?"
                                : "Move this content to the recycle bin?";
                            var delete = await confirmService.Confirm(message, "Move to recycle bin", new ConfirmOptions { OkButtonText = "Yes", CancelButtonText = "No" });
                            if (delete == true)
                            {
                                var result = await mediator.Send(new DeleteContentCommand{ContentId = dbContent.Id, MoveToRecycleBin = true});
                                notificationService.Notify(new NotificationMessage { 
                                    Severity = result.Success ? NotificationSeverity.Success : NotificationSeverity.Error, 
                                    Summary = result.Success ? "Success" : "Error", 
                                    Detail = result.Messages.MessagesAsString(), Duration = 4000 });
                                if (result.Success)
                                {
                                    await appState.NotifyContentChanged(dbContent, currentUser?.Name ?? "Unknown");
                                }
                            }
            
                        break;
                    case 3:
                            if (!dbContent!.Children.Any())
                            {
                                // Show message if no children
                                notificationService.Notify(new NotificationMessage { Severity = NotificationSeverity.Warning, Summary = "Hmmmm", Detail = "Sorry, nothing to sort as this content has no children", Duration = 4000 });
                            }
                            else
                            {
                                contextMenuService.Close();
                                var dialog = modalService.OpenSidePanel<SortContent>("Sort Children", 
                                    new Dictionary<string, object>{ { "ContentId", dbContent.Id }, {"Content", dbContent.Children.OrderBy(x => x.SortOrder).ToList()} });
                                var result = await dialog.Result;
                                if (result is { Confirmed: true, Data: List<Core.Content.Models.Content> sortedContent })
                                {
                                    foreach (var c in sortedContent)
                                    {
                                        var saveResult = await mediator.Send(new SaveContentCommand {Content = c, ExcludePropertyData = true});
                                        if (!saveResult.Success)
                                        {
                                            notificationService.Notify(new NotificationMessage { Severity = NotificationSeverity.Error, Summary = "Error", Detail = saveResult.Messages.MessagesAsString(), Duration = 4000 });
                                        }
                                    }
                                    await appState.NotifyContentChanged(dbContent, currentUser?.Name ?? "Unknown");
                                }
                            }
         
                        break;
                    case 4:
                        contextMenuService.Close();
                        var languageDialog = modalService.OpenSidePanel<CultureDomains>("Domains & Culture", 
                            new Dictionary<string, object>
                            {
                                {"Content", content}
                            });
                        var languageDialogResult = await languageDialog.Result;
                        if (languageDialogResult is { Confirmed: true })
                        {
                            // Do we need to do anything here?     
                            var savedContent = languageDialogResult.Data as Content;
                            if (savedContent?.LanguageId != null)
                            {
                                // Save the content
                                var saveResult = await mediator.Send(new SaveContentCommand {Content = savedContent, ExcludePropertyData = true});
                                if (!saveResult.Success)
                                {
                                    notificationService.Notify(new NotificationMessage { Severity = NotificationSeverity.Error, Summary = "Error", Detail = saveResult.Messages.MessagesAsString(), Duration = 4000 });
                                }
                                else
                                {
                                    notificationService.Notify(new NotificationMessage { Severity = NotificationSeverity.Success, Summary = "Culture Updated", Detail = string.Empty, Duration = 4000 });
                                    await appState.NotifyContentChanged(dbContent, currentUser?.Name ?? "Unknown");
                                }
                            }
                        }    
         
                        break;
                    case 5:
                        contextMenuService.Close();
                        cacheService.ClearCachedItemsWithPrefix(nameof(Core.Content));
                        treeState.ClearChildCache(null);
                        await appState.NotifyContentChanged(dbContent, currentUser?.Name ?? "Unknown");
                        break;
                }   
        }
    }

    public int SortOrder => -50;
}