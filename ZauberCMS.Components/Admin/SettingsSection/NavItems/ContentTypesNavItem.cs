using MediatR;
using Microsoft.AspNetCore.Components;
using ZauberCMS.Core;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Sections.Interfaces;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Components.Admin.SettingsSection.NavItems;

public class ContentTypesNavItem : ISectionNavItem
{
    private readonly IMediator _mediator;
    private readonly NavigationManager _navigationManager;

    public ContentTypesNavItem(IMediator mediator, NavigationManager navigationManager)
    {
        _mediator = mediator;
        _navigationManager = navigationManager;
        Trees = PrepareStructureTree();
    }
    public string SectionNavAlias => "StructureSectionNav";
    public int SortOrder => 1;
    public IEnumerable<TreeStub> Trees { get; set; }
    
    public void OnChange(object? selection)
    {
        switch (selection)
        {
            case TreeStub treeStub:
            {
                if (!treeStub.Url.IsNullOrWhiteSpace())
                {
                    _navigationManager.NavigateTo(treeStub.Url);
                }

                break;
            }
            case TreeBranch treeBranch:
            {
                if (treeBranch.BranchType == typeof(ContentType))
                {
                    _navigationManager.NavigateTo($"/admin/updatecontentype/{treeBranch.Id}");
                }

                break;
            }
        }
    }
    
    public void Dispose()
    {
        // TODO release managed resources here
    }
    
    private IEnumerable<TreeStub> PrepareStructureTree()
    {
        var items = _mediator.Send(new QueryContentTypesCommand
        {
            OrderBy = GetContentTypesOrderBy.Name,
            AmountPerPage = 1000 // Meh
        }).GetAwaiter().GetResult();
        var contentTypes = items.Items.Where(x => x.IsElementType != true).ToList();
        var elementTypes = items.Items.Where(x => x.IsElementType).ToList();

        var tree = new List<TreeStub>();

        var contentTypeTree = new TreeStub
        {
            Id = Constants.Guids.ContentTypeTreeRootId,
            Name = "Content Types",
            Icon = "content_copy",
            SortOrder = 1,
            StubType = typeof(ContentType)
        };

        foreach (var contentType in contentTypes)
        {
            contentTypeTree.Branches.Add(new TreeBranch
            {
                Id = contentType.Id,
                Icon = contentType.Icon,
                Name = contentType.Name,
                BranchType = typeof(ContentType)
            });
        }

        var elementTypeTree = new TreeStub
        {
            Id = Constants.Guids.ElementTypeTreeRootId,
            Name = "Element Types",
            Icon = "explicit",
            SortOrder = 2,
            StubType = typeof(ContentType)
        };

        foreach (var contentType in elementTypes)
        {
            elementTypeTree.Branches.Add(new TreeBranch
            {
                Id = contentType.Id,
                Icon = contentType.Icon,
                Name = contentType.Name,
                BranchType = typeof(ContentType)
            });
        }

        tree.Add(contentTypeTree);
        tree.Add(elementTypeTree);

        return tree;
    }
}