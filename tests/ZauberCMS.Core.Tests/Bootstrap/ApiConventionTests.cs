using System.Reflection;
using ZauberCMS.Components.Editors;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Data.Parameters;
using ZauberCMS.Core.Media.Parameters;
using ZauberCMS.Core.Membership.Parameters;
using ZauberCMS.Core.Seo.Parameters;
using ZauberCMS.Core.Shared.Services;
using ZauberCMS.Core.Tags.Parameters;

namespace ZauberCMS.Core.Tests.Bootstrap;

/// <summary>
/// Roadmap Phase 11f, ZA-124 (D54): the API conventions of Docs/Standards.md §11, checked over the public types of
/// the packages so that new code keeps to them.
/// </summary>
public class ApiConventionTests
{
    private static readonly Assembly Core = typeof(ZauberSetup).Assembly;
    private static readonly Assembly Components = typeof(TextProperty).Assembly;
    private static readonly Assembly Routing = Assembly.Load(new AssemblyName("ZauberCMS.Routing"));
    private static readonly Assembly[] Packages = [Core, Components, Routing];

    private static IEnumerable<Type> ParameterClasses => Core.GetExportedTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false } && t.Name.Contains("Parameters", StringComparison.Ordinal) &&
                    t.GetConstructor(Type.EmptyTypes) != null);

    // ---- The recycle-bin flags ----

    [Fact]
    public void ZA124_no_parameter_class_returns_recycled_items_by_default()
    {
        var checkedFlags = 0;
        foreach (var type in ParameterClasses)
        {
            var instance = Activator.CreateInstance(type)!;
            foreach (var name in new[] { "IncludeDeleted", "OnlyDeleted" })
            {
                if (type.GetProperty(name) is { } property)
                {
                    Assert.Equal(typeof(bool), property.PropertyType);
                    Assert.False((bool)property.GetValue(instance)!, $"{type.Name}.{name} defaults to true");
                    checkedFlags++;
                }
            }
        }

        // Get and Query of Content and Media, the user query, and OnlyDeleted on the content query
        Assert.True(checkedFlags >= 6, $"only {checkedFlags} flags found");
        Assert.False(new QueryContentParameters().IncludeDeleted);
        Assert.False(new QueryContentParameters().OnlyDeleted);
        Assert.False(new GetContentParameters().IncludeDeleted);
        Assert.False(new GetMediaParameters().IncludeDeleted);
        Assert.False(new QueryMediaParameters().IncludeDeleted);
        Assert.False(new QueryUsersParameters().IncludeDeleted);
        Assert.False(new DataGridParameters<ZauberCMS.Core.Membership.Models.User>().IncludeDeleted);
    }

    [Fact]
    public void ZA124_the_recycle_bin_flags_are_named_IncludeDeleted_and_OnlyDeleted()
    {
        // IsDeleted stays only as the obsolete alias on the content query
        var other = ParameterClasses
            .SelectMany(t => t.GetProperties().Select(p => (Type: t, Property: p)))
            .Where(x => x.Property.Name.Contains("Deleted", StringComparison.Ordinal) &&
                        x.Property.Name is not ("IncludeDeleted" or "OnlyDeleted") &&
                        !x.Property.IsDefined(typeof(ObsoleteAttribute)))
            .Select(x => $"{x.Type.Name}.{x.Property.Name}")
            .ToList();
        Assert.Empty(other);
    }

    // ---- Cached defaults ----

    // Changing one of these changes what callers get (stale or extra queries): it is a breaking change
    public static TheoryData<Type, bool> CachedDefaults => new()
    {
        { typeof(GetContentParameters), false },
        { typeof(QueryContentParameters), false },
        { typeof(GetMediaParameters), false },
        { typeof(QueryMediaParameters), false },
        { typeof(GetUserParameters), false },
        { typeof(QueryUsersParameters), false },
        { typeof(QueryTagParameters), false },
        { typeof(HasChildContentParameters), true },
        { typeof(HasChildContentTypeParameters), true },
        { typeof(GetGlobalDataParameters), true },
        { typeof(GetRestrictedMediaUrlsParameters), true },
        { typeof(QueryRedirectsParameters), true },
    };

    [Theory]
    [MemberData(nameof(CachedDefaults))]
    public void ZA124_Cached_defaults_are_pinned(Type parameters, bool expected)
    {
        var property = parameters.GetProperty("Cached");
        Assert.NotNull(property);
        Assert.Equal(expected, (bool)property.GetValue(Activator.CreateInstance(parameters))!);
    }

    [Fact]
    public void ZA124_every_parameter_class_with_Cached_is_in_the_pinned_list()
    {
        var pinned = CachedDefaults.Select(row => row.Data.Item1).ToHashSet();
        var unpinned = ParameterClasses
            .Where(t => t.GetProperty("Cached")?.PropertyType == typeof(bool) && !pinned.Contains(t))
            .Select(t => t.Name)
            .ToList();
        Assert.Empty(unpinned);
    }

    // ---- Cancellation tokens ----

    [Fact]
    public void ZA124_public_methods_take_a_CancellationToken_last_and_optional_never_a_nullable_one()
    {
        var wrong = new List<string>();
        foreach (var type in Packages.SelectMany(a => a.GetExportedTypes()))
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (var method in type.GetMethods(flags).Cast<MethodBase>().Concat(type.GetConstructors()))
            {
                if (ImplementsForeignContract(type, method))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                for (var i = 0; i < parameters.Length; i++)
                {
                    var parameterType = parameters[i].ParameterType;
                    if (parameterType == typeof(CancellationToken?))
                    {
                        wrong.Add($"{type.Name}.{method.Name}: CancellationToken?");
                    }
                    else if (parameterType == typeof(CancellationToken) && method is MethodInfo)
                    {
                        if (i != parameters.Length - 1)
                        {
                            wrong.Add($"{type.Name}.{method.Name}: the token is not last");
                        }
                        else if (!parameters[i].HasDefaultValue && !HasOverloadWithoutToken(type, (MethodInfo)method))
                        {
                            wrong.Add($"{type.Name}.{method.Name}: the token has no default");
                        }
                    }
                }
            }
        }

        Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong.Distinct()));
    }

    [Fact]
    public void ZA124_every_async_method_of_a_service_interface_takes_a_CancellationToken()
    {
        // ICacheService's load function closes over the caller's token, and one load serves every waiter
        var missing = Core.GetExportedTypes()
            .Where(t => t.IsInterface && t.Name.EndsWith("Service", StringComparison.Ordinal) && t != typeof(ICacheService))
            .SelectMany(t => t.GetMethods().Select(m => (Type: t, Method: m)))
            .Where(x => typeof(Task).IsAssignableFrom(x.Method.ReturnType) &&
                        x.Method.GetParameters().All(p => p.ParameterType != typeof(CancellationToken)))
            .Select(x => $"{x.Type.Name}.{x.Method.Name}")
            .ToList();
        Assert.True(missing.Count == 0, string.Join(Environment.NewLine, missing));
    }

    // The pair an extension point keeps for implementations written before the token (ISeoCheck.Check): the
    // overload without the token is the one callers without a token use
    private static bool HasOverloadWithoutToken(Type type, MethodInfo method)
    {
        var withoutToken = method.GetParameters().SkipLast(1).Select(p => p.ParameterType).ToArray();
        return type.GetMethod(method.Name, withoutToken) is { } overload && overload != method;
    }

    // A method whose signature a framework type fixes (an Identity store, a hosted service, a circuit handler)
    private static bool ImplementsForeignContract(Type type, MethodBase method)
    {
        if (method is not MethodInfo info)
        {
            return false;
        }

        var baseDefinition = info.GetBaseDefinition();
        if (baseDefinition.DeclaringType != type && !IsOurs(baseDefinition.DeclaringType))
        {
            return true;
        }

        if (type.IsInterface)
        {
            return false;
        }

        foreach (var contract in type.GetInterfaces().Where(i => !IsOurs(i)))
        {
            var map = type.GetInterfaceMap(contract);
            if (map.TargetMethods.Contains(info))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsOurs(Type? type) => type != null && Packages.Contains(type.Assembly);
}
