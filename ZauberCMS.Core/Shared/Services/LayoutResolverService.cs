using Microsoft.AspNetCore.Components;

namespace ZauberCMS.Core.Shared.Services;

public class LayoutResolverService
{
    private readonly Dictionary<string, Type> _layoutCache = new();

    public Type GetLayoutType(Type componentType)
    {
        // Check if the layout type is already cached
        if (_layoutCache.TryGetValue(componentType.FullName, out var layoutType))
        {
            return layoutType;
        }

        // Use reflection to find the LayoutAttribute if not cached
        var layoutAttribute = componentType.GetCustomAttributes(typeof(LayoutAttribute), true)
            .FirstOrDefault() as LayoutAttribute;

        // Cache the layout type
        layoutType = layoutAttribute?.LayoutType;
        _layoutCache[componentType.FullName] = layoutType;

        // Return the layout type or a default layout if none is specified
        return layoutType;
    }
}