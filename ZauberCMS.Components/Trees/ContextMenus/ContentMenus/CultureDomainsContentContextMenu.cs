using Blazored.Modal.Services;
using MediatR;
using Microsoft.AspNetCore.Components;
using Radzen;
using ZauberCMS.Components.Admin.ContentSection.Dialogs;
using ZauberCMS.Core;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared;

namespace ZauberCMS.Components.Trees.ContextMenus.ContentMenus;

public class CultureDomainsContentContextMenu(
    IMediator mediator,
    NotificationService notificationService,
    AppState appState) : ITreeContextMenu
{
    public List<string> Sections => [Constants.Sections.ContentSection];
    public List<string> TreeAlias { get; } = [];

    public string Text(TreeItemContextMenuEventArgs args)
    {
        var content = (Content)args.Value!;
        return content.IsRootContent ? "Culture & Domains" : "Culture";
    }

    public string Icon(TreeItemContextMenuEventArgs args) => "bungalow";

    public string IconColor(TreeItemContextMenuEventArgs args) => string.Empty;

    public bool CanShowContextMenu(TreeItemContextMenuEventArgs args)
    {
        if(args.Value is Content content)
        {
            return content.Published;
        }
        return false;
    }

    public async Task ContextMenuAction(TreeItemContextMenuEventArgs args, MenuItemEventArgs e,
        NavigationManager navigationManager,
        ContextMenuService contextMenuService, IModalService modalService)
    {
        var content = (Content)args.Value!;
        var dbContent = await mediator.Send(new GetContentCommand { Id = content.Id, IncludeChildren = true });
        var currentUser = await mediator.GetCurrentUser();
        contextMenuService.Close();
        var languageDialog = modalService.OpenSidePanel<CultureDomains>("Domains & Culture",
            new Dictionary<string, object>
            {
                { "Content", content }
            });
        var languageDialogResult = await languageDialog.Result;
        if (languageDialogResult is { Confirmed: true })
        {
            // Do we need to do anything here?     
            var savedContent = languageDialogResult.Data as Content;
            if (savedContent?.LanguageId != null)
            {
                // Save the content
                var saveResult = await mediator.Send(new SaveContentCommand
                    { Content = savedContent, ExcludePropertyData = true });
                if (!saveResult.Success)
                {
                    notificationService.ShowErrorNotification("Error", saveResult.Messages.MessagesAsString());
                }
                else
                {
                    notificationService.ShowSuccessNotification("Culture Updated");
                    await appState.NotifyContentChanged(dbContent, currentUser?.Name ?? "Unknown");
                }
            }
        }
    }

    public int SortOrder => -70;
}