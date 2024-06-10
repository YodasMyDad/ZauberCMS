using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Configuration;
using ZauberCMS.Core.Data;

namespace ZauberCMS.Core.Extensions;

public static class ValueConversionExtensions
{
    public static void ToJsonConversion<T>(this PropertyBuilder<T> propertyBuilder, int? columnSize = null)
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

        //TODO - This is likely to be an issue for extension developers?
            
        // Get an instance of your context
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        
        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../ZauberCMS.Web"))
            .AddJsonFile("appSettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appSettings.{environment}.json", optional: true)
            .Build();
        
        // Get the connection string and provider from configuration
        var section = configuration.GetSection("Zauber");
        var databaseProvider = section.GetValue<string>("DatabaseProvider");

        if (databaseProvider?.EndsWith("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
        {
            propertyBuilder.HasColumnType("TEXT");
        }
        else
        {
            if (columnSize == null)
            {
                // Determine the provider
                propertyBuilder.HasColumnType("nvarchar(MAX)");
            }
            else
            {
                propertyBuilder.HasColumnType($"nvarchar({columnSize})");
                propertyBuilder.HasMaxLength(columnSize.Value);   
            }   
        }
    }
}