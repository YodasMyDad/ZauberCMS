using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Installer;
using ZauberCMS.Core.Languages.Interfaces;
using ZauberCMS.Core.Languages.Models;
using ZauberCMS.Core.Languages.Parameters;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Languages;

/// <summary>
/// Keeps the request cultures (<c>UseRequestLocalization</c>) the site's languages as they are now, so a language
/// added in Settings &gt; Languages, or by the installer on a site that started without a database, is the culture
/// of the account pages, the fallback pages and the sign-out page (D95) without a restart (ZA-307). It chooses no
/// culture itself: the providers after it (query string, cookie, <c>Accept-Language</c>) choose among the cultures
/// it keeps. The list is the languages' cached query, cleared with the <c>Language</c> prefix when a language is
/// saved (on every server, with Redis), so a request costs one cache lookup. Umbraco adds its languages to the
/// supported cultures at request time the same way (<c>DynamicRequestCultureProviderBase</c>).
/// </summary>
internal sealed class SiteLanguagesRequestCultureProvider(RequestLocalizationOptions options, string adminLanguage)
    : RequestCultureProvider
{
    // The Language prefix: a language's save clears it (SaveChangesAndLog, LanguageService)
    private static readonly string CacheKey = typeof(Language).ToCacheKey("RequestCultures");

    private readonly Lock _lock = new();
    private string[]? _applied;

    public override async Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        var services = httpContext.RequestServices;
        if (!services.GetRequiredService<InstallState>().IsDatabaseInitialised)
        {
            return null; // the installer's requests: no languages yet
        }

        try
        {
            var codes = await services.GetRequiredService<ICacheService>().GetSetCachedItemAsync(CacheKey, async () =>
            {
                var languages = await services.GetRequiredService<ILanguageService>().QueryLanguageAsync(
                    new QueryLanguageParameters { AmountPerPage = 200 }, httpContext.RequestAborted);
                return languages.Items.Select(l => l.LanguageIsoCode).OfType<string>().ToArray();
            });
            if (codes != null && !ReferenceEquals(codes, Volatile.Read(ref _applied)))
            {
                Apply(codes);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The cultures it already has stay: a request is never refused for this
            services.GetService<ILoggerFactory>()?.CreateLogger<SiteLanguagesRequestCultureProvider>()
                .LogWarning(ex, "The site's languages could not be read for the request cultures; the ones read " +
                                "before are used");
        }

        return null;
    }

    private void Apply(string[] codes)
    {
        lock (_lock)
        {
            if (ReferenceEquals(codes, _applied))
            {
                return;
            }

            var cultures = new[] { adminLanguage }.Concat(codes)
                .Select(Culture).OfType<CultureInfo>()
                .DistinctBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            // New lists, never a change to the ones a request may be reading
            options.SupportedCultures = cultures;
            options.SupportedUICultures = cultures.ToList();
            Volatile.Write(ref _applied, codes);
        }
    }

    private static CultureInfo? Culture(string name)
    {
        try
        {
            return CultureInfo.GetCultureInfo(name);
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }
}
