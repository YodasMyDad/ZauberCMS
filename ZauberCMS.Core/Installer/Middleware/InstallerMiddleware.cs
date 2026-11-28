using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Installer.Components;
using ZauberCMS.Core.Installer.Interfaces;
using ZauberCMS.Core.Installer.Models;
using ZauberCMS.Core.Installer.Parameters;
using ZauberCMS.Core.Languages;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Settings;

namespace ZauberCMS.Core.Installer.Middleware;

/// <summary>
/// The installer. While the site is not installed it answers <see cref="Path"/> itself and sends every other
/// request there, so nothing else in the application runs without a database or an administrator. Once the site
/// is installed it does nothing at all: <see cref="Path"/> is an ordinary address again (a content page may use
/// it) and the installer cannot be reached.
/// <para>
/// It sits in front of authentication on purpose. A site without a database cannot validate a sign-in cookie,
/// and the installer is open to whoever gets there first (Roadmap D20), so there is nobody to authenticate.
/// Forms carry an antiforgery token, posts are rate limited per address, and the pages are served with a
/// Content-Security-Policy that allows only their own inline style and script.
/// </para>
/// </summary>
internal class InstallerMiddleware(
    RequestDelegate next,
    InstallState installState,
    ILoggerFactory loggerFactory)
{
    /// <summary>The address of the installer, below the application's path base.</summary>
    public const string Path = "/install";

    // How often a request that is not for the installer looks in the database for the installed flag. It is
    // there for a second server that started before the first one finished the install.
    private static readonly TimeSpan RecheckInterval = TimeSpan.FromSeconds(3);

    private static readonly Lazy<IReadOnlyList<InstallerOption>> AllLanguages = new(() =>
        CultureInfo.GetCultures(CultureTypes.SpecificCultures)
            .Where(c => !string.IsNullOrEmpty(c.Name))
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(c => c.EnglishName, StringComparer.OrdinalIgnoreCase)
            .Select(c => new InstallerOption(c.Name, c.EnglishName))
            .ToList());

    private readonly ILogger _logger = loggerFactory.CreateLogger<InstallerMiddleware>();
    private long _lastCheck;

    // Every post can make the server open a database connection or hash a password: a handful per address is
    // plenty for a person and useless for a script.
    private readonly PartitionedRateLimiter<string> _postLimiter = PartitionedRateLimiter.Create<string, string>(
        address => RateLimitPartition.GetSlidingWindowLimiter(address, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(5),
            SegmentsPerWindow = 5,
            QueueLimit = 0
        }));

    public async Task InvokeAsync(HttpContext context)
    {
        // The error page for a request that failed (ZA-098) goes through: turning it into a redirect to the installer
        // would hide the failure
        if (installState.IsInstalled || context.Features.Get<IExceptionHandlerFeature>() != null)
        {
            await next(context);
            return;
        }

        var isInstaller = IsInstallerPath(context.Request.Path);
        if (isInstaller)
        {
            // The installer's pages and messages are in the admin's language (Roadmap D95), whatever the
            // request asked for: a site without a database has no other
            AdminCulture.Apply(context.RequestServices.GetRequiredService<IOptions<ZauberSettings>>().Value);
        }

        var installService = context.RequestServices.GetRequiredService<IInstallService>();

        if (installState.IsDatabaseConfigured)
        {
            try
            {
                await RefreshAsync(installService, alwaysCheck: isInstaller, context.RequestAborted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "The installer could not use the configured database");
                if (!isInstaller)
                {
                    RedirectToInstaller(context);
                    return;
                }

                var text = Text(context);
                await RenderAsync(context, new InstallerPageModel
                {
                    Step = InstallerStep.Message,
                    MessageTitle = text["installer_databaseUnusable"],
                    MessageIsProblem = true,
                    MessageLines = [text["installer_databaseUnusableReason", Innermost(ex).Message]],
                    MessageLinkText = text["installer_tryAgain"],
                    MessageLinkUrl = context.Request.PathBase + Path
                }, StatusCodes.Status503ServiceUnavailable);
                return;
            }

            if (installState.IsInstalled)
            {
                if (isInstaller)
                {
                    // Somebody else finished the install while this page was open
                    Redirect(context, context.Request.PathBase + "/");
                    return;
                }

                await next(context);
                return;
            }
        }

        if (!isInstaller)
        {
            RedirectToInstaller(context);
            return;
        }

        await HandleAsync(context, installService);
    }

    private static bool IsInstallerPath(PathString path) =>
        path.Equals(Path, StringComparison.OrdinalIgnoreCase) ||
        path.Equals(Path + "/", StringComparison.OrdinalIgnoreCase);

    // The database may have been configured after start-up (by hand, while the installer waited): prepare it
    // once. Otherwise look for the installed flag, on every installer request and now and then on the others.
    private async Task RefreshAsync(IInstallService installService, bool alwaysCheck, CancellationToken cancellationToken)
    {
        if (!installState.IsDatabaseInitialised)
        {
            // Runs once however many requests arrive together (the service serialises it)
            await installService.InitialiseDatabaseAsync(cancellationToken);
            return;
        }

        var now = Environment.TickCount64;
        if (alwaysCheck || now - Interlocked.Read(ref _lastCheck) >= (long)RecheckInterval.TotalMilliseconds)
        {
            Interlocked.Exchange(ref _lastCheck, now);
            await installService.CheckInstalledAsync(cancellationToken);
        }
    }

    private static void RedirectToInstaller(HttpContext context)
    {
        if (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
        {
            Redirect(context, context.Request.PathBase + Path, StatusCodes.Status302Found);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.Headers.RetryAfter = "60";
    }

    private static void Redirect(HttpContext context, string url, int statusCode = StatusCodes.Status303SeeOther)
    {
        context.Response.StatusCode = statusCode;
        context.Response.Headers.Location = url;
        context.Response.Headers.CacheControl = "no-store";
    }

    private async Task HandleAsync(HttpContext context, IInstallService installService)
    {
        var request = context.Request;
        var text = Text(context);
        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
        {
            try
            {
                await RenderCurrentStepAsync(context, installService, new InstallerPageModel());
            }
            catch (Exception ex) when (ex is not OperationCanceledException && !context.Response.HasStarted)
            {
                // Answered here, not by the site's error page: that page is behind this middleware and
                // would only be sent back to the installer
                _logger.LogError(ex, "The installer page could not be shown");
                await RenderAsync(context, new InstallerPageModel
                {
                    Step = InstallerStep.Message,
                    MessageTitle = text["installer_somethingWentWrong"],
                    MessageIsProblem = true,
                    MessageLines = [text["installer_pageFailed"]],
                    ConfiguredDatabase = ConfiguredDatabaseName()
                }, StatusCodes.Status500InternalServerError);
            }

            return;
        }

        if (!HttpMethods.IsPost(request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            context.Response.Headers.Allow = "GET, HEAD, POST";
            return;
        }

        using var lease = _postLimiter.AttemptAcquire(context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        if (!lease.IsAcquired)
        {
            context.Response.Headers.RetryAfter = "300";
            await RenderAsync(context, new InstallerPageModel
            {
                Step = InstallerStep.Message,
                MessageTitle = text["installer_tooManyAttempts"],
                MessageLines = [text["installer_tooManyAttemptsWait"]],
                MessageLinkText = text["installer_backToInstaller"],
                MessageLinkUrl = request.PathBase + Path,
                ConfiguredDatabase = ConfiguredDatabaseName()
            }, StatusCodes.Status429TooManyRequests);
            return;
        }

        var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
        if (!request.HasFormContentType || !await antiforgery.IsRequestValidAsync(context))
        {
            await RenderCurrentStepAsync(context, installService, new InstallerPageModel
            {
                Errors = [text["installer_pageExpired"]]
            }, StatusCodes.Status400BadRequest);
            return;
        }

        var form = await request.ReadFormAsync(context.RequestAborted);
        try
        {
            if (string.Equals(form["step"], "database", StringComparison.Ordinal))
            {
                await PostDatabaseAsync(context, installService, form);
            }
            else
            {
                await PostSetupAsync(context, installService, form);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "The installer failed");
            if (context.Response.HasStarted)
            {
                throw;
            }

            await RenderAsync(context, new InstallerPageModel
            {
                Step = InstallerStep.Message,
                MessageTitle = text["installer_somethingWentWrong"],
                MessageIsProblem = true,
                // What went wrong is in the log; this page is public
                MessageLines = [text["installer_stepFailed"]],
                MessageLinkText = text["installer_backToInstaller"],
                MessageLinkUrl = request.PathBase + Path,
                ConfiguredDatabase = ConfiguredDatabaseName()
            }, StatusCodes.Status500InternalServerError);
        }
    }

    private async Task PostDatabaseAsync(HttpContext context, IInstallService installService, IFormCollection form)
    {
        if (installState.IsDatabaseConfigured)
        {
            // The database step is behind us (a second tab, or the Back button)
            Redirect(context, context.Request.PathBase + Path);
            return;
        }

        var parameters = new DatabaseSetupParameters
        {
            Provider = form["provider"].ToString(),
            UseConnectionString = IsChecked(form, "useConnectionString"),
            ConnectionString = form["connectionString"].ToString(),
            SqliteFile = form["sqliteFile"].ToString(),
            Server = form["server"].ToString(),
            Port = int.TryParse(form["port"], NumberStyles.None, CultureInfo.InvariantCulture, out var port) ? port : null,
            Database = form["database"].ToString(),
            IntegratedSecurity = IsChecked(form, "integratedSecurity"),
            Username = form["username"].ToString(),
            Password = form["password"].ToString(),
            TrustServerCertificate = IsChecked(form, "trustServerCertificate")
        };

        var model = new InstallerPageModel { Step = InstallerStep.Database, Database = parameters };

        if (string.Equals(form["action"], "test", StringComparison.Ordinal))
        {
            model.TestResult = await installService.TestDatabaseAsync(parameters, context.RequestAborted);
            await RenderAsync(context, model);
            return;
        }

        var result = await installService.SetUpDatabaseAsync(parameters, context.RequestAborted);
        if (result.Success)
        {
            // A database that already holds an installed site makes this site installed: there is nothing to set up
            Redirect(context, context.Request.PathBase + (installState.IsInstalled ? "/" : Path));
            return;
        }

        if (result.ManualConfigurationRequired)
        {
            model.Step = InstallerStep.ManualConfiguration;
            model.ManualConfiguration = result;
            await RenderAsync(context, model);
            return;
        }

        model.Errors = result.Errors;
        await RenderAsync(context, model, StatusCodes.Status400BadRequest);
    }

    private async Task PostSetupAsync(HttpContext context, IInstallService installService, IFormCollection form)
    {
        if (!installState.IsDatabaseConfigured)
        {
            Redirect(context, context.Request.PathBase + Path);
            return;
        }

        var parameters = new InstallParameters
        {
            AdminName = form["adminName"].ToString(),
            AdminEmail = form["adminEmail"].ToString(),
            AdminPassword = form["adminPassword"].ToString(),
            SiteName = form["siteName"].ToString(),
            Language = form["language"].ToString(),
            AllowRegistration = IsChecked(form, "allowRegistration"),
            StarterKit = form["starterKit"].ToString()
        };

        var result = await installService.InstallAsync(parameters, context.RequestAborted);
        if (result.AlreadyInstalled)
        {
            Redirect(context, context.Request.PathBase + "/");
            return;
        }

        if (!result.Success)
        {
            await RenderCurrentStepAsync(context, installService, new InstallerPageModel
            {
                Setup = parameters,
                Errors = result.Errors
                // Always opens on the administrator part: the password is never sent back, so it has to
                // be typed again whichever part was refused
            }, StatusCodes.Status400BadRequest);
            return;
        }

        // The person who installed the site is its administrator: sign them in. The site is installed whether
        // or not that works; without it the admin simply asks them to sign in.
        try
        {
            var signInManager = context.RequestServices.GetRequiredService<SignInManager<User>>();
            await signInManager.SignInAsync(result.Administrator!, isPersistent: false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "The installer could not sign the new administrator in");
        }

        var adminUrl = context.Request.PathBase + Urls.AdminBaseUrl;
        if (result.Warnings.Count == 0)
        {
            Redirect(context, adminUrl);
            return;
        }

        var text = Text(context);
        await RenderAsync(context, new InstallerPageModel
        {
            Step = InstallerStep.Message,
            MessageTitle = text["installer_installedWithWarnings"],
            MessageIsProblem = true,
            MessageLines = result.Warnings,
            MessageLinkText = text["installer_openAdmin"],
            MessageLinkUrl = adminUrl,
            ConfiguredDatabase = ConfiguredDatabaseName()
        });
    }

    // Fills in what the step for the site's present state needs and renders it
    private async Task RenderCurrentStepAsync(HttpContext context, IInstallService installService,
        InstallerPageModel model, int statusCode = StatusCodes.Status200OK)
    {
        if (!installState.IsDatabaseConfigured)
        {
            model.Step = InstallerStep.Database;
            await RenderAsync(context, model, statusCode);
            return;
        }

        var text = Text(context);
        model.Step = InstallerStep.Setup;
        model.ConfiguredDatabase = ConfiguredDatabaseName();
        model.Languages = AllLanguages.Value;
        model.PasswordRules = DescribePasswordRules(
            context.RequestServices.GetRequiredService<IOptions<IdentityOptions>>().Value.Password, text);

        var kits = new List<InstallerOption>
        {
            new(string.Empty, text["installer_emptySite"], text["installer_emptySiteInfo"])
        };
        kits.AddRange(installService.GetStarterKits().Select(k => new InstallerOption(k.Key, k.Value.Name, k.Value.Description)));
        model.StarterKits = kits;

        if (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
        {
            // First visit: suggest the visitor's language and, when the project has some, starter content
            model.Setup.Language = PreferredLanguage(context, model.Languages);
            model.Setup.StarterKit = kits.Count > 1 ? kits[1].Value : string.Empty;
        }
        else if (model.Languages.All(l => l.Value != model.Setup.Language))
        {
            model.Setup.Language = PreferredLanguage(context, model.Languages);
        }

        await RenderAsync(context, model, statusCode);
    }

    private async Task RenderAsync(HttpContext context, InstallerPageModel model, int statusCode = StatusCodes.Status200OK)
    {
        var response = context.Response;
        // Hex: nothing in it that HTML attribute encoding would change
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        model.Nonce = nonce;
        model.FormAction = context.Request.PathBase + Path;
        model.Version ??= typeof(InstallerMiddleware).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0];

        response.Headers.XFrameOptions = "DENY";
        response.Headers.XContentTypeOptions = "nosniff";
        response.Headers["Referrer-Policy"] = "no-referrer";
        response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        response.Headers.ContentSecurityPolicy =
            $"default-src 'none'; style-src 'nonce-{nonce}'; script-src 'nonce-{nonce}'; form-action 'self'; " +
            "base-uri 'none'; frame-ancestors 'none'";

        if (model.Step is InstallerStep.Database or InstallerStep.Setup)
        {
            var tokens = context.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(context);
            model.AntiforgeryFieldName = tokens.FormFieldName;
            model.AntiforgeryToken = tokens.RequestToken ?? string.Empty;
        }

        // The pages show database details and are different for every visitor and moment
        response.Headers.CacheControl = "no-store";

        string html;
        await using (var renderer = new HtmlRenderer(context.RequestServices, loggerFactory))
        {
            html = await renderer.Dispatcher.InvokeAsync(async () =>
            {
                var output = await renderer.RenderComponentAsync<InstallerPage>(ParameterView.FromDictionary(
                    new Dictionary<string, object?> { [nameof(InstallerPage.Model)] = model }));
                return output.ToHtmlString();
            });
        }

        response.StatusCode = statusCode;
        response.ContentType = "text/html; charset=utf-8";
        if (!HttpMethods.IsHead(context.Request.Method))
        {
            await response.WriteAsync(html, context.RequestAborted);
        }
    }

    private string? ConfiguredDatabaseName() =>
        installState.IsDatabaseConfigured ? DatabaseSetupParameters.DisplayName(installState.DatabaseProvider) : null;

    private static bool IsChecked(IFormCollection form, string name) =>
        string.Equals(form[name], "true", StringComparison.OrdinalIgnoreCase);

    private static string PreferredLanguage(HttpContext context, IReadOnlyList<InstallerOption> languages)
    {
        if (languages.Count == 0)
        {
            return string.Empty;
        }

        var accepted = context.Request.GetTypedHeaders().AcceptLanguage
            .OrderByDescending(x => x.Quality ?? 1)
            .Select(x => x.Value.Value);
        foreach (var name in accepted)
        {
            var match = languages.FirstOrDefault(l => string.Equals(l.Value, name, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                return match.Value;
            }
        }

        return languages.FirstOrDefault(l => l.Value == "en-US")?.Value ?? languages[0].Value;
    }

    // One sentence per rule, so a translation never has to fit a rule into another sentence
    private static string DescribePasswordRules(PasswordOptions options, IStringLocalizer<ZauberText> text)
    {
        var rules = new List<string> { text["installer_passwordLength", options.RequiredLength] };
        if (options.RequireDigit)
        {
            rules.Add(text["installer_passwordDigit"]);
        }

        if (options.RequireLowercase)
        {
            rules.Add(text["installer_passwordLowercase"]);
        }

        if (options.RequireUppercase)
        {
            rules.Add(text["installer_passwordUppercase"]);
        }

        if (options.RequireNonAlphanumeric)
        {
            rules.Add(text["installer_passwordSymbol"]);
        }

        return string.Join(" ", rules);
    }

    private static IStringLocalizer<ZauberText> Text(HttpContext context) =>
        context.RequestServices.GetRequiredService<IStringLocalizer<ZauberText>>();

    private static Exception Innermost(Exception exception)
    {
        while (exception.InnerException != null)
        {
            exception = exception.InnerException;
        }

        return exception;
    }
}
