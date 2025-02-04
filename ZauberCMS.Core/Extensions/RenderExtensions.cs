using System.Reflection;
using Microsoft.AspNetCore.Razor.Hosting;

namespace ZauberCMS.Core.Extensions;

public static class RenderExtensions
{
    public static string? GetViewIdentifier(this Type viewType)
    {
        // Look for the RazorCompiledItemAttribute on the view type.
        var attribute = viewType.GetCustomAttribute<RazorCompiledItemAttribute>();
        return attribute?.Identifier;
    }

    public static (string Name, string Path) GetViewDetails(this Type compiledViewName)
    {
        return compiledViewName.Name.GetViewDetails();
    }
    
    public static (string Name, string Path) GetViewDetails(this string compiledViewName)
    {
        if (string.IsNullOrWhiteSpace(compiledViewName) || !compiledViewName.StartsWith("Views_"))
            throw new ArgumentException("Invalid compiled view name", nameof(compiledViewName));

        // Remove "Views_" prefix
        var trimmedName = compiledViewName[6..]; // "Views_" is 6 characters long

        // Find the last underscore, which separates folders from the actual view name
        var lastUnderscoreIndex = trimmedName.LastIndexOf('_');

        if (lastUnderscoreIndex == -1)
        {
            // No underscores found, meaning it's a direct view like "Views_Blog"
            return (trimmedName, $"/Views/{trimmedName}.cshtml");
        }

        // Extract everything after the last underscore as the view name
        var viewName = trimmedName[(lastUnderscoreIndex + 1)..];

        // Extract the path before the last underscore
        var folderPath = trimmedName[..lastUnderscoreIndex];

        // Ensure double underscores remain in the view name
        if (folderPath.EndsWith('_'))
        {
            folderPath = folderPath[..^1];
            viewName = "_" + viewName;
        }

        // Convert underscores to folder separators
        var fullPath = $"/Views/{folderPath.Replace('_', '/')}/{viewName}.cshtml";

        return (viewName, fullPath);
    }
}