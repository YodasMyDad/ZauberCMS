using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace ZauberCMS.Core.Plugins;

/// <summary>
/// Warnings found while <c>AddZauberCms(builder)</c> sets the site up, before logging exists (ZA-120): bootstrap
/// discovery that skipped a type, an extension type that discovery cannot see. They are kept here and written to the
/// log by <c>AddZauberCms(app)</c>, once the host and its logger are built.
/// </summary>
internal sealed class StartupMessages
{
    private readonly ConcurrentQueue<string> _warnings = new();
    private readonly ConcurrentDictionary<string, bool> _seen = new(StringComparer.Ordinal);

    /// <summary>Adds a warning; the same text twice (an assembly scanned for two interfaces) is kept once.</summary>
    public void Warning(string message)
    {
        if (_seen.TryAdd(message, true))
        {
            _warnings.Enqueue(message);
        }
    }

    /// <summary>The warnings not yet written.</summary>
    public IReadOnlyCollection<string> Warnings => _warnings.ToArray();

    /// <summary>Writes each warning once; a second call writes only what was added since.</summary>
    public void WriteTo(ILogger logger)
    {
        while (_warnings.TryDequeue(out var warning))
        {
            logger.LogWarning("{StartupWarning}", warning);
        }
    }
}
