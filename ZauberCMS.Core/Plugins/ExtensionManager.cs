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
    // The implementations of a type, keyed by the type's full name, for each (type, exported types only) pair. Only
    // a search of every scanned assembly is cached: a predicate's result depends on the predicate, which cannot be
    // a key (ZA-120). Both caches are emptied when AssemblyManager.SetAssemblies changes the assemblies.
    private static readonly ConcurrentDictionary<(Type Type, bool OnlyExportedTypes), Dictionary<string, Type>> TypeCache = new();
    private static readonly ConcurrentDictionary<string, Type> NameCache = new();

    /// <summary>Empties the discovery caches; called when the scanned assemblies change.</summary>
    internal static void ClearCaches()
    {
        TypeCache.Clear();
        NameCache.Clear();
    }

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
        var cacheKey = (targetType, onlyExportedTypes);
        var cacheable = useCaching && predicate == null;

        // A copy, so a caller that changes the dictionary does not change what the next caller gets
        if (cacheable && TypeCache.TryGetValue(cacheKey, out var cachedTypes))
            return new Dictionary<string, Type>(cachedTypes);

        var implementations = new Dictionary<string, Type>();

        foreach (var assembly in GetAssemblies(predicate))
        {
            // Choose between exported types or all types. The public types of every scanned assembly load (the
            // bootstrap checks: EnsurePublicTypesLoad); a non-public type that cannot load is skipped (ZA-176).
            var typesToCheck = assembly == null ? null
                : onlyExportedTypes ? assembly.GetExportedTypes() : LoadableTypes(assembly, null);
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

        if (cacheable)
        {
            TypeCache[cacheKey] = new Dictionary<string, Type>(implementations);
        }

        return implementations;
    }

    public Type? GetTypeFromName(Func<Assembly?, bool>? predicate, string fullyQualifiedName, bool useCaching = false)
    {
        var cacheable = useCaching && predicate == null;
        if (cacheable && NameCache.TryGetValue(fullyQualifiedName, out var cachedType))
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

        if (cacheable && type != null)
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
                var instance = args.Length != 0
                    ? (T)Activator.CreateInstance(implementation, args)!
                    : (T)ActivatorUtilities.CreateInstance(serviceProvider, implementation);
                instances[kvp.Key] = instance;
            }
        }

        return instances;
    }

    /// <summary>
    /// One instance of a discovered <paramref name="implementation"/>, built with constructor injection from this
    /// scope; for callers that must survive one extension's constructor throwing.
    /// </summary>
    internal object CreateInstance(Type implementation) =>
        ActivatorUtilities.CreateInstance(serviceProvider, implementation);

    // Returns assemblies to check based on an optional predicate.
    public static IEnumerable<Assembly?> GetAssemblies(Func<Assembly?, bool>? predicate)
    {
        var assemblies = AssemblyManager.Assemblies;
        return predicate == null ? assemblies : assemblies.Where(predicate);
    }

    /// <summary>
    /// Discovers and instantiates implementations of <typeparamref name="T"/> from registered
    /// assemblies without going through DI. Intended for bootstrap-time interfaces (such as
    /// <c>IStartupPlugin</c> and <c>IExternalAuthenticationProvider</c>) that must run before
    /// the service provider is built. Implementers must have a parameterless constructor.
    /// </summary>
    /// <exception cref="InvalidOperationException">An implementation's constructor threw.</exception>
    public static IEnumerable<T> GetBootstrapInstances<T>() => GetBootstrapInstances<T>(null);

    /// <summary>
    /// <see cref="GetBootstrapInstances{T}()"/>, telling <paramref name="messages"/> what it skips: types of an
    /// assembly that could not be loaded, and implementations without a parameterless constructor (ZA-120). The
    /// bootstrap runs before logging exists, so <c>AddZauberCms(app)</c> writes them to the log later.
    /// </summary>
    internal static IEnumerable<T> GetBootstrapInstances<T>(StartupMessages? messages)
    {
        foreach (var assembly in AssemblyManager.Assemblies)
        {
            if (assembly is null) continue;

            foreach (var type in LoadableTypes(assembly, messages))
            {
                if (type.IsAbstract || type.IsInterface) continue;
                if (!typeof(T).IsAssignableFrom(type)) continue;
                if (type.GetConstructor(Type.EmptyTypes) is null)
                {
                    messages?.Warning($"{type.FullName} implements {typeof(T).Name} but has no parameterless " +
                                      "constructor, so it is skipped: it is created before dependency injection exists.");
                    continue;
                }

                T instance;
                try
                {
                    instance = (T)Activator.CreateInstance(type)!;
                }
                catch (TargetInvocationException ex) when (ex.InnerException != null)
                {
                    throw new InvalidOperationException(
                        $"{type.FullName} ({typeof(T).Name}) threw in its constructor: {ex.InnerException.Message}",
                        ex.InnerException);
                }

                yield return instance;
            }
        }
    }

    /// <summary>
    /// The discovery interfaces whose implementations are found among exported (public) types only, by full name of
    /// the interface or of its generic definition (Components' are not visible to Core by type).
    /// <c>IStartupPlugin</c> and <c>IExternalAuthenticationProvider</c> are not here: the bootstrap also finds
    /// non-public ones. FluentValidation's <c>IValidator&lt;T&gt;</c> is: its own scan of the scanned assemblies
    /// (<c>EditContextFluentValidationExtensions</c>) also finds public validators only (ZA-182).
    /// </summary>
    internal static readonly IReadOnlySet<string> DiscoveryInterfaces = new HashSet<string>(StringComparer.Ordinal)
    {
        "ZauberCMS.Core.Content.Interfaces.IContentView",
        "ZauberCMS.Core.Content.Interfaces.IContentBlockView",
        "ZauberCMS.Core.Content.Interfaces.IContentBlockPreview",
        "ZauberCMS.Core.Content.Interfaces.IContentProperty",
        "ZauberCMS.Core.Content.Interfaces.IContentPropertySettings`1",
        "ZauberCMS.Core.Content.Interfaces.INestedContentReferenceProvider",
        "ZauberCMS.Core.Content.Interfaces.ICustomContentComponent",
        "ZauberCMS.Core.Content.Interfaces.IContentFinder",
        "ZauberCMS.Core.Content.Interfaces.IDataListSource",
        "ZauberCMS.Core.Sections.Interfaces.ISection",
        "ZauberCMS.Core.Sections.Interfaces.ISectionNavGroup",
        "ZauberCMS.Core.Sections.Interfaces.ISectionNav",
        "ZauberCMS.Core.Sections.Interfaces.ISectionNavGroupAction",
        "ZauberCMS.Core.Sections.Interfaces.ISectionDashboard",
        "ZauberCMS.Components.ContextMenus.ITreeContextMenu",
        "ZauberCMS.Core.Seo.Models.ISeoCheck",
        "ZauberCMS.Core.Membership.Interfaces.IAccountLayout",
        "ZauberCMS.Core.Providers.IStorageProvider",
        "ZauberCMS.Core.Plugins.Interfaces.IEmailProvider",
        "ZauberCMS.Core.Security.Interfaces.ICaptchaProvider",
        "ZauberCMS.Core.Installer.Interfaces.IStarterKit",
        "ZauberCMS.Core.Data.Interfaces.ISeedData",
        "ZauberCMS.Core.Shared.Validation.Interfaces.IValidate`1",
        "ZauberCMS.Core.Tags.Interfaces.ITagReferenceProvider",
        "ZauberCMS.Core.Shared.Interfaces.IBeforeEntitySave`1",
        "ZauberCMS.Core.Shared.Interfaces.IAfterEntitySave`1",
        "ZauberCMS.Core.Shared.Interfaces.IAfterEntityCommit`1",
        "FluentValidation.IValidator`1"
    };

    /// <summary>
    /// Tells <paramref name="messages"/> about each class in <paramref name="assemblies"/> that implements a discovery
    /// interface but is not public (or is nested in a type that is not), so discovery never sees it (ZA-120). Meant
    /// for a site's and its plugins' assemblies, not ZauberCMS's own.
    /// </summary>
    internal static void ReportHiddenExtensionTypes(IEnumerable<Assembly> assemblies, StartupMessages messages)
    {
        foreach (var assembly in assemblies)
        {
            foreach (var type in LoadableTypes(assembly, messages))
            {
                if (type.IsVisible || !type.IsClass || type.IsAbstract ||
                    type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false))
                {
                    continue;
                }

                var extensionPoint = type.GetInterfaces()
                    .Select(i => i.IsGenericType ? i.GetGenericTypeDefinition() : i)
                    .FirstOrDefault(i => i.FullName != null && DiscoveryInterfaces.Contains(i.FullName));
                if (extensionPoint != null)
                {
                    messages.Warning($"{type.FullName} implements {extensionPoint.Name} but is not public, so " +
                                     "ZauberCMS does not find it. Make it public (and any type it is nested in).");
                }
            }
        }
    }

    /// <summary>
    /// Stops the start when a public type of <paramref name="assembly"/> cannot be loaded: discovery, and Blazor's
    /// router, read an assembly's public types all at once, so one such type makes every use of the assembly throw.
    /// It happens to a plugin built against another ZauberCMS version (v5 moved some public types from
    /// ZauberCMS.Core to ZauberCMS.Components). A type that is not public and cannot load does not stop it: that is
    /// a start-up warning, and discovery skips the type (ZA-176).
    /// </summary>
    /// <exception cref="InvalidOperationException">A public type cannot be loaded; names the assembly and the type.</exception>
    internal static void EnsurePublicTypesLoad(Assembly assembly) =>
        EnsurePublicTypesLoad(assembly.GetName().Name ?? assembly.FullName ?? "An assembly", assembly.GetExportedTypes);

    /// <summary><see cref="EnsurePublicTypesLoad(Assembly)"/> over the assembly's name and its public types.</summary>
    internal static void EnsurePublicTypesLoad(string assemblyName, Func<Type[]> exportedTypes)
    {
        try
        {
            _ = exportedTypes();
        }
        catch (Exception ex) when (ex is TypeLoadException or FileNotFoundException or FileLoadException)
        {
            var missing = ex switch
            {
                TypeLoadException { TypeName.Length: > 0 } typeLoad => $"the type {typeLoad.TypeName}",
                FileNotFoundException { FileName.Length: > 0 } notFound => $"the assembly {notFound.FileName}",
                FileLoadException { FileName.Length: > 0 } loadFailed => $"the assembly {loadFailed.FileName}",
                _ => "a type it uses"
            };
            throw new InvalidOperationException(
                $"{assemblyName} cannot be used: its public types need {missing}, which cannot be loaded " +
                $"({ex.Message}). It was probably built against another ZauberCMS version (v5 moved some types from " +
                $"ZauberCMS.Core to ZauberCMS.Components): rebuild it against this version, or remove it.", ex);
        }
    }

    // Every type of the assembly that could be loaded; the ones that could not are reported and skipped
    private static Type[] LoadableTypes(Assembly assembly, StartupMessages? messages)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            if (messages != null)
            {
                var reasons = ex.LoaderExceptions
                    .Where(e => e != null)
                    .Select(e => e!.Message)
                    .Distinct(StringComparer.Ordinal)
                    .Take(5);
                messages.Warning($"{ex.Types.Count(t => t is null)} types of {assembly.GetName().Name} could not be " +
                                 $"loaded, so discovery skips them: {string.Join(" | ", reasons)}");
            }

            return ex.Types.Where(t => t is not null).Cast<Type>().ToArray();
        }
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