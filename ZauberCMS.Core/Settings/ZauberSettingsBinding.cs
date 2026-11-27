using System.Collections;
using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace ZauberCMS.Core.Settings;

/// <summary>
/// Reads the <c>Zauber</c> configuration section into <see cref="ZauberSettings"/>, the same way for the bootstrap's
/// own copy and for <c>IOptions&lt;ZauberSettings&gt;</c>.
/// </summary>
internal static class ZauberSettingsBinding
{
    /// <summary>A new <see cref="ZauberSettings"/> with <paramref name="section"/> bound over its defaults.</summary>
    public static ZauberSettings Read(IConfiguration section)
    {
        var settings = new ZauberSettings();
        section.Bind(settings);
        ReplaceConfiguredLists(settings, section);
        return settings;
    }

    /// <summary>
    /// A list the configuration sets replaces the default list instead of being added to it (ZA-167). The
    /// configuration binder adds the items it finds to a list that already has some, so a default list
    /// (<c>ImageResize:ContentRoots</c>) could never be changed, only lengthened. A list the configuration does not set
    /// keeps its default. Call after binding; walks every settings object under <paramref name="target"/>.
    /// Dictionaries are left as bound: their keys are merged on purpose.
    /// </summary>
    public static void ReplaceConfiguredLists(object target, IConfiguration section)
    {
        foreach (var property in target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            var type = property.PropertyType;
            if (type == typeof(string) || type.IsValueType)
            {
                continue;
            }

            var configured = section.GetSection(property.Name);
            if (!configured.Exists())
            {
                continue;
            }

            if (typeof(IEnumerable).IsAssignableFrom(type))
            {
                if (property.CanWrite && !IsDictionary(type) && configured.GetChildren().Any())
                {
                    property.SetValue(target, configured.Get(type));
                }
            }
            else if (property.GetValue(target) is { } child)
            {
                ReplaceConfiguredLists(child, configured);
            }
        }
    }

    private static bool IsDictionary(Type type) =>
        typeof(IDictionary).IsAssignableFrom(type) ||
        type.GetInterfaces().Append(type).Any(i =>
            i.IsGenericType && (i.GetGenericTypeDefinition() == typeof(IDictionary<,>) ||
                                i.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)));
}
