namespace ZauberCMS.Core.Settings.Interfaces;

/// <summary>
/// The site's API keys, by name (Roadmap D73). A key is set in configuration as <c>Zauber:ApiKeys:&lt;name&gt;</c>
/// (appsettings, environment variables, user secrets, a key vault) or by an administrator in Global Settings;
/// configuration wins for the same name. Stored values and editors refer to a key by its name, and its value is
/// read here, on the server, where it is used: the admin never shows a key's value once it is saved. Registered by
/// <c>AddZauberCms</c>; register your own after it to take keys from somewhere else.
/// </summary>
public interface IApiKeyService
{
    /// <summary>The names of every key that has a value, configuration's and Global Settings', in name order.</summary>
    Task<IReadOnlyList<string>> GetApiKeyNamesAsync(CancellationToken cancellationToken = default);

    /// <summary>The value of the key named <paramref name="name"/> (case ignored), or null when no key has it.</summary>
    Task<string?> GetApiKeyAsync(string? name, CancellationToken cancellationToken = default);

    /// <summary>
    /// The key that a value stored by the API Key Picker stands for. The picker stores the key's name, so this is
    /// the value of the key with that name. A value stored before v5 is the key itself and is returned as it is,
    /// so pages and editor settings saved before the upgrade keep working. Null for an empty value.
    /// </summary>
    Task<string?> GetPickedApiKeyAsync(string? pickedValue, CancellationToken cancellationToken = default);
}
