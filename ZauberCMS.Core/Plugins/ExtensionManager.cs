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
    private static readonly ConcurrentDictionary<Type, List<Type>> TypeCache = new();
    private static readonly ConcurrentDictionary<string, Type?> NameCache = new();

    public Type? GetImplementation<T>(bool useCaching = false)
    {
        return GetImplementation<T>(null, useCaching);
    }

    public Type? GetImplementation<T>(Func<Assembly?, bool>? predicate, bool useCaching = false)
    {
        return GetImplementations<T>(predicate, useCaching).FirstOrDefault();
    }

    public IEnumerable<Type> GetImplementations<T>(bool useCaching = false, bool onlyExportedTypes = true)
    {
        return GetImplementations<T>(null, useCaching, onlyExportedTypes);
    }

    public IEnumerable<Type> GetImplementations<T>(Func<Assembly?, bool>? predicate, bool useCaching = false, bool onlyExportedTypes = true)
    {
        var targetType = typeof(T);

        if (useCaching && TypeCache.TryGetValue(targetType, out var cachedTypes))
            return cachedTypes;

        var implementations = new List<Type>();

        foreach (var assembly in GetAssemblies(predicate))
        {
            var typesToCheck = (onlyExportedTypes ? assembly?.GetExportedTypes() : assembly?.GetTypes()) ?? [];
            
            foreach (var exportedType in typesToCheck)
            {
                if (targetType.IsAssignableFrom(exportedType) && exportedType is { IsClass: true, IsAbstract: false })
                {
                    implementations.Add(exportedType);
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
                if (type != null) break;
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
        return GetInstances<T>(predicate, useCaching).FirstOrDefault().Value;
    }

    public T? GetInstance<T>(Func<Assembly?, bool>? predicate, bool useCaching = false, params object[] args)
    {
        return GetInstances<T>(predicate, useCaching, args).FirstOrDefault().Value;
    }

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

        foreach (var implementation in GetImplementations<T>(predicate, useCaching))
        {
            if (!implementation.IsAbstract)
            {
                var instance = args.Any()
                    ? (T)Activator.CreateInstance(implementation, args)!
                    : (T)ActivatorUtilities.CreateInstance(serviceProvider, implementation);

                instances[implementation.FullName ?? implementation.Name] = instance;
            }
        }

        return instances;
    }

    public static IEnumerable<Assembly?> GetAssemblies(Func<Assembly?, bool>? predicate)
    {
        return predicate == null
            ? AssemblyManager.Assemblies
            : AssemblyManager.Assemblies.Where(predicate);
    }

    public static Assembly?[] GetFilteredAssemblies(Func<Assembly?, bool>? predicate)
    {
        // Apply the predicate to filter assemblies, or use all assemblies if predicate is null
        var assemblies = GetAssemblies(predicate).Where(a => a?.GetName().Name != "ZauberCMS").ToArray();

        // Find the assembly with the name "ZauberCMS.Routing"
        var routingAssembly = assemblies.FirstOrDefault(a => a?.GetName().Name == "ZauberCMS.Routing");

        // Exclude the "ZauberCMS.Routing" from the filtered list
        assemblies = assemblies.Where(a => a != routingAssembly).ToArray();

        // Add the "ZauberCMS.Routing" to the end of the list if it exists
        if (routingAssembly != null)
        {
            assemblies = assemblies.Append(routingAssembly).ToArray();
        }

        return assemblies.Where(x => x != null).ToArray();
    }

    public void ApplyMigrations(ZauberDbContext dbContext)
    {
        var settings = serviceProvider.GetRequiredService<IOptions<ZauberSettings>>();
        var migrationTypes = settings.Value.DatabaseProvider switch
        {
            "Sqlite" => GetImplementations<ISqLiteMigration>(),
            "SqlServer" => GetImplementations<IMsSqlMigration>(),
            _ => Array.Empty<Type>()
        };

        var migrator = dbContext.Database.GetService<IMigrator>();
        var appliedMigrations = dbContext.Database.GetAppliedMigrations().ToHashSet();

        var migrations = migrationTypes
            .Where(type => typeof(Migration).IsAssignableFrom(type))
            .Select(type => ActivatorUtilities.CreateInstance(serviceProvider, type) as Migration)
            .Where(migration => migration != null)
            .ToList();

        foreach (var migration in migrations)
        {
            if (migration != null && !appliedMigrations.Contains(migration.GetType().Name))
            {
                migrator.Migrate(migration.GetType().Name);
            }
        }
    }
}
