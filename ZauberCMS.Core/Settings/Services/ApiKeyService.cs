using Microsoft.Extensions.Options;
using ZauberCMS.Core.Data.Interfaces;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Settings.Interfaces;

namespace ZauberCMS.Core.Settings.Services;

/// <summary>
/// The default <see cref="IApiKeyService"/>: <see cref="GlobalSettings.ApiKeys"/>, with
/// <see cref="ZauberSettings.ApiKeys"/> (configuration) over them. A key with an empty value is left out, so the
/// empty <c>TinyMCE</c> entry of the shipped <c>appsettings.json</c> hides nothing set in Global Settings.
/// </summary>
public class ApiKeyService(IDataService dataService, IOptions<ZauberSettings> settings) : IApiKeyService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetApiKeyNamesAsync(CancellationToken cancellationToken = default) =>
        (await GetKeysAsync()).Keys.Order(StringComparer.OrdinalIgnoreCase).ToList();

    /// <inheritdoc />
    public async Task<string?> GetApiKeyAsync(string? name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return (await GetKeysAsync()).GetValueOrDefault(name.Trim());
    }

    /// <inheritdoc />
    public async Task<string?> GetPickedApiKeyAsync(string? pickedValue, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pickedValue))
        {
            return null;
        }

        // Not a name: the picker stored the key itself before v5 (ZA-214)
        return await GetApiKeyAsync(pickedValue, cancellationToken) ?? pickedValue;
    }

    private async Task<Dictionary<string, string>> GetKeysAsync()
    {
        var keys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in (await dataService.GetGlobalSettings()).ApiKeys)
        {
            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrEmpty(value))
            {
                keys.TryAdd(name.Trim(), value);
            }
        }

        foreach (var (name, value) in settings.Value.ApiKeys)
        {
            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrEmpty(value))
            {
                // Removed first, so the name is spelt as configuration spells it
                keys.Remove(name.Trim());
                keys[name.Trim()] = value;
            }
        }

        return keys;
    }
}
