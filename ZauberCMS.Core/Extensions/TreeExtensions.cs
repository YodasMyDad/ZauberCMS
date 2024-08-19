using Microsoft.AspNetCore.Components;
using Radzen.Blazor;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Extensions;

public static class TreeExtensions
{
    public static RenderFragment<T> CreateStandardTreeTemplate<T>() where T : class
    {
        return context => builder =>
        {
            var treeItem = context as RadzenTreeItem;
            switch (treeItem?.Value)
            {
                case TreeStub treeStub:
                    builder.OpenComponent<RadzenIcon>(0);
                    builder.AddAttribute(1, "Icon", treeStub.Icon);
                    builder.AddAttribute(2, "style", "font-weight: 300; color: dimgray;");
                    builder.CloseComponent();
                    builder.AddContent(3, treeStub.Name);
                    break;
                case TreeBranch branch:
                    builder.OpenComponent<RadzenIcon>(0);
                    builder.AddAttribute(1, "Icon", branch.Icon);
                    builder.AddAttribute(2, "style", "font-weight: 300; color: dimgray;");
                    builder.CloseComponent();
                    builder.AddContent(3, branch.Name);
                    break;
            }
        };
    }
}