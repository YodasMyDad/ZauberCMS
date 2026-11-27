using System.Text.Json;
using ZauberCMS.Core.Content.Interfaces;

// In ZauberCMS.Components with the namespace it had in Core, like IContentProperty itself (Roadmap D51): an editor's
// `this.GetPropertySettings<T>()` keeps compiling. It was PropertyExtensions.GetPropertySettings in Core.
namespace ZauberCMS.Core.Extensions;

public static class ContentPropertyExtensions
{
    /// <summary>The editor's settings (<see cref="IContentProperty.Settings"/>, JSON) as <typeparamref name="T"/>;
    /// a new <typeparamref name="T"/> when there are none.</summary>
    public static T GetPropertySettings<T>(this IContentProperty cp)
        where T : class, new()
    {
        if (!cp.Settings.IsNullOrWhiteSpace())
        {
            return JsonSerializer.Deserialize<T>(cp.Settings) ?? new T();
        }
        return new T();
    }
}
