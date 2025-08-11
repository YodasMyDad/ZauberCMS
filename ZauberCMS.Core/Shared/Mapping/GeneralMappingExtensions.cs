using System.Reflection;

namespace ZauberCMS.Core.Shared.Mapping;

/// <summary>
/// General mapping extension methods that can be used for any entity type
/// Provides fallback mapping when specific mapping extensions don't exist
/// </summary>
public static class GeneralMappingExtensions
{
    /// <summary>
    /// Generic mapping method that maps all public properties from source to target
    /// Excludes navigation properties (properties that are not primitive types, strings, or collections of primitive types)
    /// </summary>
    /// <typeparam name="T">The type of entity to map</typeparam>
    /// <param name="source">Source entity</param>
    /// <param name="target">Target entity to update</param>
    /// <returns>Updated target entity</returns>
    public static T MapToGeneric<T>(this T source, T target) where T : class
    {
        if (source == null || target == null)
            return target!;

        var type = typeof(T);
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            // Skip properties that can't be set
            if (!property.CanWrite)
                continue;

            // Skip navigation properties (complex objects, collections of complex objects)
            if (ShouldSkipProperty(property))
                continue;

            try
            {
                var value = property.GetValue(source);
                property.SetValue(target, value);
            }
            catch
            {
                // Skip properties that can't be read or written
                continue;
            }
        }

        return target;
    }

    /// <summary>
    /// Creates a new entity with properties mapped from source using generic mapping
    /// </summary>
    /// <typeparam name="T">The type of entity to map</typeparam>
    /// <param name="source">Source entity</param>
    /// <returns>New entity with mapped properties</returns>
    public static T MapToNewGeneric<T>(this T source) where T : class, new()
    {
        var target = new T();
        return source.MapToGeneric(target);
    }

    /// <summary>
    /// Determines if a property should be skipped during generic mapping
    /// </summary>
    /// <param name="property">Property info to check</param>
    /// <returns>True if the property should be skipped</returns>
    private static bool ShouldSkipProperty(PropertyInfo property)
    {
        var propertyType = property.PropertyType;

        // Skip if it's a navigation property (complex object)
        if (propertyType.IsClass && propertyType != typeof(string))
        {
            // Allow collections of primitive types
            if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(List<>))
            {
                var elementType = propertyType.GetGenericArguments()[0];
                return elementType.IsClass && elementType != typeof(string);
            }
            return true;
        }

        // Skip if it's an interface (likely a navigation property)
        if (propertyType.IsInterface)
            return true;

        return false;
    }
}
