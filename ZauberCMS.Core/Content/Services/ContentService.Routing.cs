using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Core.Content.Services;

// Which page a front-end address shows: the URL lookup, then the site's content finders (Docs/FrontEnd.md).
public partial class ContentService
{
    /// <summary>
    /// Resolves content for a frontend request (domain + slug) and builds the corresponding entry model with language.
    /// When no live page has the URL, the site's <see cref="IContentFinder"/>s are asked in order (Roadmap D55).
    /// </summary>
    /// <param name="parameters">Request details such as Url, Slug and child include flags.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Entry model with content and localization information.</returns>
    public async Task<EntryModel> GetContentFromRequestAsync(GetContentFromRequestParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // URLs are stored lower case without slashes around them, so the slug is compared in that form on every
        // database provider (ZA-066): /About-Us/ finds about-us. An empty slug is the root page.
        var slug = ContentUrl.Normalize(parameters.Slug);
        var request = new GetContentFromRequestParameters
        {
            Slug = slug.Length == 0 ? null : slug,
            IsRootContent = parameters.IsRootContent || slug.Length == 0,
            IncludeChildren = parameters.IncludeChildren,
            IgnoreInternalRedirect = parameters.IgnoreInternalRedirect,
            // Only the host and path pick the page (domains match on them): the query string and fragment stay out
            // of the key, or every ?utm_… link would be its own cache entry (ZA-061)
            Url = RequestUrlForLookup(parameters.Url)
        };

        // The domain the address is on, from the cached domain list (no database on a hit). With the slug and the
        // flags it is all that decides the page, so it is what the cache is keyed on (ZA-187): the host, port and
        // spelling of the address are not. Any number of Host header values, ports and spellings (/about/,
        // /About) of one page share one entry per domain, and a host that is on no domain shares the entry of
        // every other such host, so a visitor cannot grow the cache by inventing addresses.
        var domains = await GetCachedDomainsAsync(new CachedDomainsParameters(), cancellationToken);
        var matchedDomain = MatchDomainWithContent(request.Url ?? string.Empty, domains);

        // Cached for up to 5 minutes (it was 5 seconds by mistake, ZA-061). Content, domain, content type, role and
        // language saves clear the Content prefix; the language keys and the default-language fallback are applied below, outside the cache, because
        // they change without a content save.
        // The key changes every EntryCacheMinutes, so an entry lives at most that long even on a page that is hit
        // all the time (the expiry is sliding), and changes the cache isn't told about show within it. A miss (no
        // page) is not kept, so only addresses of live pages make entries.
        var bucket = DateTime.UtcNow.Ticks / TimeSpan.FromMinutes(EntryCacheMinutes).Ticks;
        var cacheKey = EntryCacheKey(bucket, matchedDomain?.Id, request);
        var cached = (await cacheService.GetSetCachedItemAsync(cacheKey, async () =>
        {
            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
            return await FetchEntryModelAsync(request, matchedDomain, dbContext, cancellationToken);
        }, EntryCacheMinutes, entry => entry.Content != null))!;

        var entryModel = new EntryModel { Content = cached.Content, LanguageIsoCode = cached.LanguageIsoCode };
        if (entryModel.Content == null)
        {
            // No page has this URL: the site's content finders may know which page the address shows (D55). Their
            // answer is not cached here (a finder caches its own search); the page they name is.
            var found = await FindWithContentFindersAsync(request, parameters, matchedDomain, bucket,
                cancellationToken);
            if (found == null)
            {
                return entryModel;
            }

            entryModel = found;
        }

        if (entryModel.LanguageIsoCode.IsNullOrWhiteSpace())
        {
            // The site's default language (set by the installer, or in Global Settings); a site that has none keeps
            // the old fallback, the admin's language
            using var settingsScope = serviceScopeFactory.CreateScope();
            var defaultLanguage = (await settingsScope.ServiceProvider.GetRequiredService<Data.Interfaces.IDataService>()
                .GetGlobalSettings()).DefaultLanguage;
            entryModel.LanguageIsoCode = defaultLanguage.IsNullOrWhiteSpace()
                ? settings.Value.AdminDefaultLanguage
                : defaultLanguage;
        }

        var allLanguageData = await languageService.GetCachedAllLanguageDictionariesAsync(
            new ZauberCMS.Core.Languages.Parameters.GetCachedAllLanguageDictionariesParameters(), cancellationToken);
        if (allLanguageData.TryGetValue(entryModel.LanguageIsoCode!, out var languageKeys) && languageKeys != null)
        {
            entryModel.LanguageKeys = languageKeys;
        }

        return entryModel;
    }

    /// <summary>How long a front-end URL → page lookup stays cached (sliding; saves clear it sooner).</summary>
    private const int EntryCacheMinutes = 5;

    /// <summary>Scheme, host, port and path of a request URL, without the query string and fragment.</summary>
    private static string? RequestUrlForLookup(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.GetLeftPart(UriPartial.Path).ToLowerInvariant() : url;

    /// <summary>
    /// The cache key of a URL lookup: the time window, the matched domain (none: every host on no domain), the
    /// normalised slug and the flags, which are everything <see cref="FetchEntryModelAsync"/> reads (ZA-187).
    /// Under the Content prefix, so the saves that clear pages clear it.
    /// </summary>
    internal static string EntryCacheKey(long bucket, Guid? domainId, GetContentFromRequestParameters request) =>
        string.Create(CultureInfo.InvariantCulture,
                $"{EntryCacheKeyPrefix}{bucket}-{domainId}-{request.IsRootContent}-{request.IncludeChildren}-" +
                $"{request.IgnoreInternalRedirect}-{request.Slug}")
            .GenerateCacheKey<Models.Content>();

    /// <summary>What every URL lookup's cache key starts with, after the Content prefix.</summary>
    internal const string EntryCacheKeyPrefix = "GetContentFromRequest-";

    /// <summary>
    /// True while the content finders of this async flow run: a lookup a finder makes (directly or through code it
    /// calls) does the URL lookup only, so a finder that looks an address up cannot start the finders again
    /// (ZA-173).
    /// </summary>
    private static readonly AsyncLocal<bool> RunningContentFinders = new();

    /// <summary>The finder language codes already warned about (finder type and code), so each is logged once.</summary>
    private static readonly ConcurrentDictionary<(string Finder, string Code), byte> WarnedFinderLanguages = new();

    /// <summary>
    /// The page the first content finder (by <see cref="IContentFinder.SortOrder"/>, then type name) names for the
    /// address, loaded as the URL lookup loads a page; null when none names a live page. A finder that cannot be
    /// created or throws is logged and passed over: one broken plugin does not take the not-found page down.
    /// The finders are built for this lookup and disposed after it (ZA-172).
    /// </summary>
    private async Task<EntryModel?> FindWithContentFindersAsync(GetContentFromRequestParameters request,
        GetContentFromRequestParameters asked, Domain? matchedDomain, long bucket, CancellationToken cancellationToken)
    {
        var originalUrl = asked.Url;
        if (RunningContentFinders.Value)
        {
            // A finder's own lookup: the finders are already running for this request (ZA-173)
            logger.LogDebug("{Address} was looked up from inside a content finder, so the finders are not asked",
                originalUrl ?? asked.Slug);
            return null;
        }

        var finderTypes = extensionManager.GetImplementations<IContentFinder>(true);
        if (finderTypes.Count == 0)
        {
            return null;
        }

        var finders = new List<(string Name, IContentFinder Finder)>(finderTypes.Count);
        RunningContentFinders.Value = true;
        try
        {
            return await RunContentFindersAsync(finderTypes, finders, request, asked, matchedDomain, bucket,
                cancellationToken);
        }
        finally
        {
            await DisposeContentFindersAsync(finders);
            RunningContentFinders.Value = false;
        }
    }

    private async Task<EntryModel?> RunContentFindersAsync(Dictionary<string, Type> finderTypes,
        List<(string Name, IContentFinder Finder)> finders, GetContentFromRequestParameters request,
        GetContentFromRequestParameters asked, Domain? matchedDomain, long bucket,
        CancellationToken cancellationToken)
    {
        var originalUrl = asked.Url;
        foreach (var (name, type) in finderTypes)
        {
            try
            {
                finders.Add((name, (IContentFinder)extensionManager.CreateInstance(type)));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "The content finder {ContentFinder} could not be created, so it is skipped", name);
            }
        }

        var context = new ContentFinderContext
        {
            Url = originalUrl,
            Slug = request.Slug ?? string.Empty,
            Path = request.Slug == null ? string.Empty : asked.Slug!.Trim().Replace('\\', '/').Trim('/'),
            Domain = matchedDomain
        };

        foreach (var (name, finder) in finders
                     .OrderBy(f => f.Finder.SortOrder)
                     .ThenBy(f => f.Name, StringComparer.Ordinal))
        {
            ContentFinderResult? result;
            try
            {
                result = await finder.FindAsync(context, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "The content finder {ContentFinder} failed for {Address}; the next one is asked",
                    name, originalUrl ?? context.Slug);
                continue;
            }

            if (result == null)
            {
                continue;
            }

            var contentId = result.ContentId;
            var cacheKey = string.Create(CultureInfo.InvariantCulture,
                    $"ContentFinder-{bucket}-{contentId}-{request.IncludeChildren}-{request.IgnoreInternalRedirect}-" +
                    $"{matchedDomain?.Id}")
                .GenerateCacheKey<Models.Content>();
            var entry = await cacheService.GetSetCachedItemAsync(cacheKey, async () =>
            {
                using var scope = serviceScopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
                var liveContent = LiveContent(dbContext);
                var page = await liveContent.Where(c => c.Id == contentId)
                    .Select(c => new LivePage(c.Id, c.InternalRedirectId, c.ContentType!.IncludeChildren, c.Path,
                        c.Url))
                    .FirstOrDefaultAsync(cancellationToken);
                // The page's own URL picks its language, as if it had been asked for by it
                return await LoadEntryAsync(dbContext, page, request, matchedDomain, page?.Url, cancellationToken);
            }, EntryCacheMinutes, e => e.Content != null);

            if (entry?.Content == null)
            {
                // Unpublished, in the recycle bin, a block or gone: never served through a finder
                logger.LogDebug(
                    "The content finder {ContentFinder} named {ContentId} for {Address}, which is not a live page; " +
                    "the next one is asked", name, contentId, originalUrl ?? context.Slug);
                continue;
            }

            return new EntryModel
            {
                Content = entry.Content,
                // The finder's language wins over the domain's and the page's own, when it is a culture
                LanguageIsoCode = IsFinderLanguage(name, result.LanguageIsoCode)
                    ? result.LanguageIsoCode
                    : entry.LanguageIsoCode,
                ContentFinder = name,
                RouteValues = result.RouteValues ?? new Dictionary<string, object?>()
            };
        }

        return null;
    }

    /// <summary>
    /// True when a finder's <see cref="ContentFinderResult.LanguageIsoCode"/> is set and a culture name. One that is
    /// not is ignored (the page's language is used), with a warning once per finder and code rather than an error on
    /// every request (ZA-174).
    /// </summary>
    private bool IsFinderLanguage(string finder, string? languageIsoCode)
    {
        if (languageIsoCode.IsNullOrWhiteSpace())
        {
            return false;
        }

        try
        {
            CultureInfo.GetCultureInfo(languageIsoCode!);
            return true;
        }
        catch (CultureNotFoundException)
        {
            // Bounded: a finder that makes a new code per request is not remembered past this many
            if (WarnedFinderLanguages.Count < 256 && WarnedFinderLanguages.TryAdd((finder, languageIsoCode!), 0))
            {
                logger.LogWarning(
                    "The content finder {ContentFinder} gave the language {LanguageIsoCode}, which is not a culture; " +
                    "the page's own language is used", finder, languageIsoCode);
            }
            else
            {
                logger.LogDebug("The content finder {ContentFinder} gave the language {LanguageIsoCode}, which is " +
                                "not a culture", finder, languageIsoCode);
            }

            return false;
        }
    }

    /// <summary>Disposes the finders a lookup built (ZauberCMS made them, so nothing else will).</summary>
    private async Task DisposeContentFindersAsync(List<(string Name, IContentFinder Finder)> finders)
    {
        foreach (var (name, finder) in finders)
        {
            try
            {
                if (finder is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync();
                }
                else if (finder is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "The content finder {ContentFinder} threw when it was disposed", name);
            }
        }
    }

    /// <summary>What the lookup needs of a page before it loads it whole.</summary>
    private sealed record LivePage(Guid Id, Guid? InternalRedirectId, bool IncludeChildren, List<Guid> Path, string? Url);

    /// <summary>Only live pages are served: published, not in the recycle bin, and not a block (ZA-065, ZA-067).</summary>
    private static IQueryable<Models.Content> LiveContent(IZauberDbContext dbContext) =>
        dbContext.Contents.AsNoTracking().Include(x => x.ContentType)
            .Where(c => c.Published && !c.Deleted && !c.IsNestedContent);

    private async Task<EntryModel> FetchEntryModelAsync(GetContentFromRequestParameters request,
        Domain? matchedDomain, IZauberDbContext dbContext, CancellationToken cancellationToken)
    {
        var liveContent = LiveContent(dbContext);
        var content = request.IsRootContent
            ? matchedDomain != null
                ? await liveContent.Where(c => c.Id == matchedDomain.ContentId)
                    .Select(c => new LivePage(c.Id, c.InternalRedirectId, c.ContentType!.IncludeChildren, c.Path, c.Url))
                    .FirstOrDefaultAsync(cancellationToken)
                // The first root page in tree order, the same one every time (ZA-115)
                : await liveContent.Where(c => c.IsRootContent)
                    .OrderBy(c => c.SortOrder).ThenBy(c => c.Id)
                    .Select(c => new LivePage(c.Id, c.InternalRedirectId, c.ContentType!.IncludeChildren, c.Path, c.Url))
                    .FirstOrDefaultAsync(cancellationToken)
            : await liveContent.Where(c => c.Url == request.Slug)
                .OrderBy(c => c.SortOrder).ThenBy(c => c.Id)
                .Select(c => new LivePage(c.Id, c.InternalRedirectId, c.ContentType!.IncludeChildren, c.Path, c.Url))
                .FirstOrDefaultAsync(cancellationToken);

        return await LoadEntryAsync(dbContext, content, request, matchedDomain, request.Slug, cancellationToken);
    }

    /// <summary>
    /// <paramref name="page"/> (or the page its internal redirect points at) loaded whole for a view, with its
    /// language: the matched domain's, else the one of the page with URL <paramref name="languageUrl"/>, else the
    /// nearest set on the page's path. An empty model when there is no live page.
    /// </summary>
    private async Task<EntryModel> LoadEntryAsync(IZauberDbContext dbContext, LivePage? page,
        GetContentFromRequestParameters request, Domain? matchedDomain, string? languageUrl,
        CancellationToken cancellationToken)
    {
        var entryModel = new EntryModel();
        var content = page;
        // A page request shows the page the internal redirect points at; IgnoreInternalRedirect asks for the page
        // itself (ZA-191, D80). Either way only a live page is served.
        if (!request.IgnoreInternalRedirect && content?.InternalRedirectId != null &&
            content.InternalRedirectId != Guid.Empty)
        {
            var internalRedirectIdValue = content.InternalRedirectId.Value;
            content = await LiveContent(dbContext).Where(c => c.Id == internalRedirectIdValue)
                .Select(c => new LivePage(c.Id, c.InternalRedirectId, c.ContentType!.IncludeChildren, c.Path, c.Url))
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (content == null)
        {
            return entryModel;
        }

        var contentWithLanguages =
            await GetContentLanguagesAsync(new GetContentLanguagesParameters(), cancellationToken);

        var query = dbContext.Contents.AsNoTracking().AsSplitQuery()
            .Include(x => x.PropertyData)
            .Include(x => x.Parent)
            .Include(x => x.ContentType)
            .Include(x => x.Language)
            .Include(x => x.ContentRoles).ThenInclude(x => x.Role)
            .AsQueryable();

        if (request.IncludeChildren || content.IncludeChildren)
        {
            // A view lists its children: only the ones a visitor may see (ZA-065)
            query = query.Include(x => x.Children.Where(c => c.Published && !c.Deleted && !c.IsNestedContent));
        }

        var fullContent =
            await query.FirstOrDefaultAsync(c => c.Id == content.Id, cancellationToken: cancellationToken);
        entryModel.Content = fullContent;

        string? languageIsoCode = null;
        if (matchedDomain?.Language?.LanguageIsoCode != null)
        {
            languageIsoCode = matchedDomain.Language.LanguageIsoCode;
        }
        else if (contentWithLanguages.TryGetValue(languageUrl ?? string.Empty, out var contentLanguage))
        {
            languageIsoCode = contentLanguage;
        }

        if (languageIsoCode.IsNullOrWhiteSpace())
        {
            foreach (var guid in content.Path)
            {
                if (contentWithLanguages.TryGetValue(guid, out var language))
                {
                    languageIsoCode = language;
                    break;
                }
            }
        }

        // Null when neither a domain nor the page or an ancestor sets one: the caller falls back to the default
        entryModel.LanguageIsoCode = languageIsoCode.IsNullOrWhiteSpace() ? null : languageIsoCode;
        return entryModel;
    }

    private static Domain? MatchDomainWithContent(string url, List<Domain> domains)
    {
        // A caller without a full URL (code, a test) matches no domain instead of throwing
        if (domains.Count == 0 || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        // The same rule the redirect middleware uses for a redirect's domain (ZA-165)
        return DomainMatching.FindDomain(domains, uri.Host, uri.AbsolutePath, uri.Port < 0 ? null : uri.Port);
    }
}
