using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Membership.Middleware;
using ZauberCMS.Core.Settings;

namespace ZauberCMS.Core.Languages;

/// <summary>
/// The admin runs in <c>Zauber:AdminDefaultLanguage</c>, whatever language the site's content is in.
/// </summary>
public static class AdminCulture
{
    /// <summary>The culture of the admin, from <see cref="ZauberSettings.AdminDefaultLanguage"/>.</summary>
    public static CultureInfo Get(ZauberSettings settings) => CultureInfo.GetCultureInfo(settings.AdminDefaultLanguage);

    /// <summary>Makes the admin culture the current culture of the calling code and everything it calls.</summary>
    public static CultureInfo Apply(ZauberSettings settings)
    {
        var culture = Get(settings);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        return culture;
    }
}

/// <summary>
/// Keeps an admin circuit in the admin culture. A circuit takes its culture from the request that opened the
/// connection, which is not an admin request: without this, dates and numbers in the admin followed the language
/// of the site's root content (ZA-141).
/// </summary>
internal sealed class AdminCultureCircuitHandler(NavigationManager navigation, IOptions<ZauberSettings> settings)
    : CircuitHandler
{
    /// <inheritdoc />
    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(
        Func<CircuitInboundActivityContext, Task> next)
    {
        return context =>
        {
            // Set before the activity runs, in this (not async) method, so the activity and what it starts see it.
            if (IsAdminCircuit())
            {
                AdminCulture.Apply(settings.Value);
            }

            return next(context);
        };
    }

    private bool IsAdminCircuit()
    {
        try
        {
            return AdminAccess.IsAdminPath(navigation.Uri);
        }
        catch (InvalidOperationException)
        {
            // The circuit has not been given its address yet.
            return false;
        }
    }
}
