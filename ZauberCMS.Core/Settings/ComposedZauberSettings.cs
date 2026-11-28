namespace ZauberCMS.Core.Settings;

/// <summary>
/// The <c>Zauber</c> settings as <c>AddZauberCms(builder)</c> read them from configuration while it registered the
/// services. Only the settings that decide what is registered (the database, Redis on or off) are taken from this
/// copy; <c>AddZauberCms(app)</c> compares them with <c>IOptions&lt;ZauberSettings&gt;</c>, so a change made to
/// them in code is a start-up warning instead of being ignored without a word (ZA-192, Roadmap D81).
/// </summary>
internal sealed class ComposedZauberSettings(ZauberSettings settings)
{
    public ZauberSettings Settings { get; } = settings;
}
