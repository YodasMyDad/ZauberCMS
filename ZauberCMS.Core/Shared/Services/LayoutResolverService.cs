using Microsoft.AspNetCore.Components;

namespace ZauberCMS.Core.Shared.Services;

public class LayoutResolverService
{
    public Type GetLayoutType(Type componentType)
    {
        // Use reflection to find the LayoutAttribute
        var layoutAttribute = componentType.GetCustomAttributes(typeof(LayoutAttribute), true)
            .FirstOrDefault() as LayoutAttribute;

        // Return the layout type or a default layout if none is specified
        return layoutAttribute?.LayoutType;
    }
}