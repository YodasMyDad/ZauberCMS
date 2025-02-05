using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Data.Interfaces;
using ZauberCMS.Core.Settings;

namespace ZauberCMS.Core.Plugins;

public class ExtensionManager(IServiceProvider serviceProvider)
{
    // Cache the implementations for a given type as a Dictionary keyed by the type's full name.
    private static readonly ConcurrentDictionary<Type, Dictionary<string, Type>> TypeCache = new();
    private static readonly ConcurrentDictionary<string, Type?> NameCache = new();

    // Returns the first implementation for type T (if any).
    public Type? GetImplementation<T>(bool useCaching = false)
    {
        return GetImplementation<T>(null, useCaching);
    }

    public Type? GetImplementation<T>(Func<Assembly?, bool>? predicate, bool useCaching = false)
    {
        // Since GetImplementations now returns a dictionary, we take the first value.
        return GetImplementations<T>(predicate, useCaching).Values.FirstOrDefault();
    }

    // Returns a dictionary of implementations for type T keyed by full type name.
    public Dictionary<string, Type> GetImplementations<T>(bool useCaching = false, bool onlyExportedTypes = true)
    {
        return GetImplementations<T>(null, useCaching, onlyExportedTypes);
    }

    public Dictionary<string, Type> GetImplementations<T>(Func<Assembly?, bool>? predicate, bool useCaching = false, bool onlyExportedTypes = true)
    {
        var targetType = typeof(T);

        if (useCaching && TypeCache.TryGetValue(targetType, out var cachedTypes))
            return cachedTypes;

        var implementations = new Dictionary<string, Type>();

        foreach (var assembly in GetAssemblies(predicate))
        {
            // Choose between exported types or all types.
            var typesToCheck = onlyExportedTypes ? assembly?.GetExportedTypes() : assembly?.GetTypes();
            foreach (var exportedType in typesToCheck ?? Array.Empty<Type>())
            {
                // If T is assignable from the type, and the type is a non-abstract class...
                if (targetType.IsAssignableFrom(exportedType) && exportedType.IsClass && !exportedType.IsAbstract)
                {
                    var key = exportedType.FullName ?? exportedType.Name;
                    if (!implementations.ContainsKey(key))
                    {
                        implementations[key] = exportedType;
                    }
                }
            }
        }

        if (useCaching)
        {
            TypeCache[targetType] = implementations;
        }

        return implementations;
    }

    public Type? GetTypeFromName(Func<Assembly?, bool>? predicate, string fullyQualifiedName, bool useCaching = false)
    {
        if (useCaching && NameCache.TryGetValue(fullyQualifiedName, out var cachedType))
        {
            return cachedType;
        }

        var type = Type.GetType(fullyQualifiedName);
        if (type == null)
        {
            foreach (var assembly in GetAssemblies(predicate))
            {
                type = assembly?.GetType(fullyQualifiedName);
                if (type != null)
                    break;
            }
        }

        if (useCaching && type != null)
        {
            NameCache[fullyQualifiedName] = type;
        }

        return type;
    }

    public RenderFragment CreateComponent(string componentName, Dictionary<string, object>? parameters = null) => builder =>
    {
        parameters ??= new Dictionary<string, object>();
        var componentType = GetTypeFromName(null, componentName, true);

        if (componentType != null)
        {
            builder.OpenComponent(0, componentType);
            foreach (var parameter in parameters)
            {
                builder.AddAttribute(0, parameter.Key, parameter.Value);
            }
            builder.CloseComponent();
        }
    };

    // Methods to get instances of a given type.
    public T? GetInstance<T>(bool useCaching = false)
    {
        return GetInstance<T>(null, useCaching, Array.Empty<object>());
    }

    public T? GetInstance<T>(bool useCaching = false, params object[] args)
    {
        return GetInstance<T>(null, useCaching, args);
    }

    public T? GetInstance<T>(Func<Assembly?, bool>? predicate, bool useCaching = false)
    {
        // Since GetInstances returns a dictionary, use its Values.
        return GetInstances<T>(predicate, useCaching).Values.FirstOrDefault();
    }

    public T? GetInstance<T>(Func<Assembly?, bool>? predicate, bool useCaching = false, params object[] args)
    {
        return GetInstances<T>(predicate, useCaching, args).Values.FirstOrDefault();
    }

    // Returns a dictionary of instances of type T (keyed by the type's full name).
    public Dictionary<string, T> GetInstances<T>(bool useCaching = false)
    {
        return GetInstances<T>(null, useCaching, Array.Empty<object>());
    }

    public Dictionary<string, T> GetInstances<T>(bool useCaching = false, params object[] args)
    {
        return GetInstances<T>(null, useCaching, args);
    }

    public Dictionary<string, T> GetInstances<T>(Func<Assembly?, bool>? predicate, bool useCaching = false, params object[] args)
    {
        var instances = new Dictionary<string, T>();

        // Iterate over the dictionary returned by GetImplementations.
        foreach (var kvp in GetImplementations<T>(predicate, useCaching))
        {
            var implementation = kvp.Value;
            if (!implementation.IsAbstract)
            {
                var instance = args.Any()
                    ? (T)Activator.CreateInstance(implementation, args)!
                    : (T)ActivatorUtilities.CreateInstance(serviceProvider, implementation);
                instances[kvp.Key] = instance;
            }
        }

        return instances;
    }

    // Returns assemblies to check based on an optional predicate.
    public static IEnumerable<Assembly?> GetAssemblies(Func<Assembly?, bool>? predicate)
    {
        return predicate == null
            ? AssemblyManager.Assemblies
            : AssemblyManager.Assemblies.Where(predicate);
    }

    public static Assembly?[] GetFilteredAssemblies(Func<Assembly?, bool>? predicate)
    {
        // Filter assemblies (excluding "ZauberCMS").
        var assemblies = GetAssemblies(predicate).Where(a => a?.GetName().Name != "ZauberCMS").ToArray();

        // Find the assembly named "ZauberCMS.Routing".
        var routingAssembly = assemblies.FirstOrDefault(a => a?.GetName().Name == "ZauberCMS.Routing");

        // Exclude "ZauberCMS.Routing" from the filtered list.
        assemblies = assemblies.Where(a => a != routingAssembly).ToArray();

        // Append "ZauberCMS.Routing" at the end if it exists.
        if (routingAssembly != null)
        {
            assemblies = assemblies.Append(routingAssembly).ToArray();
        }

        return assemblies.Where(x => x != null).ToArray();
    }
}