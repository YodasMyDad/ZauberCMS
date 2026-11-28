namespace ZauberCMS.Components;

/// <summary>
/// The marker of the one-key <c>Resources/SharedResource.resx</c> (<c>Heading.Settings</c>), kept so
/// <c>IStringLocalizer&lt;SharedResource&gt;</c> still resolves. ZauberCMS's text is
/// <c>IStringLocalizer&lt;ZauberText&gt;</c> now (<see cref="ZauberCMS.Core.Localization.ZauberText"/>, Roadmap D90).
/// </summary>
[Obsolete("Use IStringLocalizer<ZauberCMS.Core.Localization.ZauberText>; the settings heading is \"sections_settingsGroup\". Will be removed in a later major version.")]
public class SharedResource
{
}
