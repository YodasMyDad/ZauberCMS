namespace ZauberCMS.Core.Shared.Services;

using System.Collections.Concurrent;
using Microsoft.AspNetCore.Components;

public class LayoutResolverService
{
    private readonly ConcurrentDictionary<string, Type> _layoutCache = new();

    public Type GetLayoutType(Type componentType)
    {
        if (componentType == null)
            throw new ArgumentNullException(nameof(componentType));

        // Use GetOrAdd to ensure thread safety when accessing or adding to the cache
        return _layoutCache.GetOrAdd(
            componentType.FullName ?? throw new InvalidOperationException("Component type must have a valid FullName."),
            key =>
            {
                // Use reflection to find the LayoutAttribute if not cached
                var layoutAttribute = componentType
                    .GetCustomAttributes(typeof(LayoutAttribute), true)
                    .FirstOrDefault() as LayoutAttribute;

                // Return the layout type or throw if none is specified
                return layoutAttribute?.LayoutType ?? throw new InvalidOperationException($"No layout type found for {key}.");
            });
    }
}
