using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ZauberCMS.Core.Data.Models;

namespace ZauberCMS.Core.Extensions;

public static class ValueConversionExtensions
{
    /// <summary>
    /// Retrieves the value associated with a specified alias from a GlobalData object.
    /// </summary>
    /// <param name="content">The GlobalData object containing the data.</param>
    /// <typeparam name="T">The type to which the value should be converted.</typeparam>
    /// <returns>The value associated with the specified alias, converted to the specified type.</returns>
    public static T? GetValue<T>(this GlobalData content)
    {
        return content.Data.IsNullOrWhiteSpace() ? default : content.Data.ToValue<T>();
    }

    /// <summary>
    /// Sets the value associated with a GlobalData object by serializing the provided data.
    /// </summary>
    /// <param name="content">The GlobalData object where the data will be set.</param>
    /// <param name="data">The data to be serialized and assigned to the GlobalData object.</param>
    /// <typeparam name="T">The type of the data to be serialized.</typeparam>
    public static void SetValue<T>(this GlobalData content, T data)
    {
        content.Data = JsonSerializer.Serialize(data);
    }

    public static void ToJsonConversion<T>(this PropertyBuilder<T> propertyBuilder, int? columnSize)
        where T : class, new()
    {
        // Explicitly set JsonSerializerOptions to prevent indentation
        var options = new JsonSerializerOptions
        {
            WriteIndented = false
        };

        var converter = new ValueConverter<T, string>
        (
            v => JsonSerializer.Serialize(v, options), // Serialize in the most compact form
            v => JsonSerializer.Deserialize<T>(v, options) ?? new T()
        );

        var comparer = new ValueComparer<T>
        (
            (l, r) => JsonSerializer.Serialize(l, options) == JsonSerializer.Serialize(r, options),
            v => v == null ? 0 : JsonSerializer.Serialize(v, options).GetHashCode(),
            v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, options), options)!
        );

        propertyBuilder.HasConversion(converter);
        propertyBuilder.Metadata.SetValueConverter(converter);
        propertyBuilder.Metadata.SetValueComparer(comparer);

        if (columnSize != null)
        {
            propertyBuilder.HasMaxLength(columnSize.Value);
        }
    }
}