# Installer, first administrator & registration

How a site gets its database and its first administrator, what "installed" means, and the rules for public
registration. Code: `ZauberCMS.Core/Installer/`, `ZauberCMS.Core/Security/`, `ZauberCMS.Core/Extensions/RoleExtensions.cs`,
`ZauberCMS.Components/Account/Pages/Register.razor`. Owner decisions: Roadmap D2, D19–D24.

## The two states before a site is installed

| State | Meaning | What the site does |
| --- | --- | --- |
| No database | `Zauber:DatabaseProvider` or `Zauber:ConnectionString` is empty (`InstallState.IsDatabaseConfigured`) | Starts without touching a database; every request goes to `/install`, database step |
| Not installed | Database is there (migrated at start-up), no installed flag | Every request goes to `/install`, set-up form |

- A new site from the template (and the packaged `appsettings.json`) ships both keys **empty**. A missing
  `DatabaseProvider` key still means SQLite, as before; an unknown provider name is still a start-up error.
- **Installed** is a `GlobalData` row with alias `ZauberInstalled` (`InstallFlag`). It is written once and never
  removed; `InstallState.IsInstalled` latches for the life of the process. The installer does not reopen, even
  when every user is deleted.
- **Upgrade:** a database that has any user row (deleted ones count) and no flag gets the flag at start-up
  (`Source: "Upgrade"`). An existing site never sees the installer. The same rule is the route for an automated
  set-up: an `ISeedData` that creates a user makes the site count as installed (see "Recovery" for making that
  user an administrator).
- **The installer is open to whoever reaches it first** (D20, as in Umbraco and WordPress). Install a site before
  it is public.
- **There is no switch to turn the installer off (D24).** Empty database settings always mean "show the
  installer". So a live site that **loses** its database settings shows the installer to the public, and the first
  visitor to finish it runs an empty site on that address (the real database is not touched). The usual way to
  lose them: the installer wrote them into `appsettings.json` on the server, and the next publish overwrote that
  file with the project's copy. **After installing on a server, put the two settings into the project's
  `appsettings.json` or into environment variables before publishing again.** A start without a database is
  logged as an error for this reason.
- Before a site is installed, the database step lets a visitor make the server try database connections and
  check file paths (20 posts per address per 5 minutes; a SQLite file on a network share is refused). One more
  reason to install before the site is public.

## Migrations and seed data: who runs them, when

Start-up used to be the only place the schema was created. With a site that can start without a connection
string there are three places, all through `IInstallService.InitialiseDatabaseAsync` (pending migrations → every
`ISeedData` → installed check → `RecoverAdminUser`; the first two run once per process, behind a lock), so the
result is the same whichever one runs:

| Situation | Migrations and seed data run |
| --- | --- |
| Connection string present at start-up (every existing site; a new site configured by environment variables) | At start-up, before the pipeline is built, exactly as before. A failed migration stops the host. |
| No connection string at start-up, database chosen in the installer | In the database step: the migrations on the installer's own context **before** `appsettings.json` is written (a failure is shown on the page and nothing has been written), then seed data on the site's context. |
| No connection string at start-up, set by hand later while the site runs | On the first request after configuration shows it (`InstallerMiddleware`). A failure shows "The database cannot be used" and is tried again on the next request. |

- A failing seeder is logged and skipped in all three, as it always was.
- `SeedEditorRole` (Roadmap D69) creates the back-office role `Editor`, with the Content and Media sections, once per
  database: a `GlobalData` row `ZauberEditorRoleSeeded` records that it ran, so a deleted or renamed `Editor` does not
  come back, and a role of that name the site already has (perhaps for front-end members) is left as it is. So a new
  site has `Editor` before the installer creates `Admin` and the starting role, and an upgraded one gets it at its
  first start, with nobody in it.
- EF creates the database itself when it is missing (SQLite file; `CREATE DATABASE` on SQL Server and
  PostgreSQL, when the login may). `DatabaseSetupOnServerTests` runs this against real servers.
- What a site without a database skips at start-up, and what happens instead: the language list for request
  localisation is just the admin language until the next start (content pages set their culture themselves, so
  they are not affected); `DailyJob` (first run five minutes after start-up) and the sitemap generator do nothing
  on their first run and work from their next one (24 and 6 hours later), unless `Zauber:Jobs` turns them off. Nothing else at start-up touches the database.
- **Plugins:** asking for `IZauberDbContext` while there is no database throws `InvalidOperationException` naming
  `/install`. The installer middleware keeps every request away, so only code that runs outside a request can
  meet it. A hosted service that uses the database when it starts must check
  `InstallState.IsDatabaseConfigured` first (inject `InstallState`), or it stops the host of a new site. An
  `IStartupPlugin` runs before the container exists: `new InstallState(configuration).IsDatabaseConfigured`.
- The provider is fixed for the process once a database has been prepared (`InstallState.DatabaseProvider`);
  until then it follows configuration, so a wrong value typed by hand can be corrected without a restart.
- The design-time factories (`migrations.ps1`) read `ZauberCMS/appsettings.json`, whose connection string is now
  empty. They never connect, so adding migrations works as before (checked with
  `dotnet ef migrations has-pending-model-changes` for all three contexts).
- Two servers: each has its own `appsettings.json`, so the second one shows the database step too. Pointing it
  at the same database finds the installed flag and the site is simply up. Two people finishing the set-up form
  on two servers in the same instant would both create an administrator; within one server it is serialised.

## Request flow

`InstallerMiddleware` (after forwarded headers and HTTPS redirect, **before authentication**):

- Installed → passes everything through. `/install` is then an ordinary address (content or 404).
- Not installed → answers `/install` itself; any other GET is redirected there, any other method gets 503.
  Nothing else in the pipeline runs (a site's own endpoints and static files included), so nothing needs a
  database or a signed-in user.
- While not installed it looks for the flag in the database on every installer request and every few seconds
  otherwise, so a second server notices that the first one finished.

The pages are Razor components rendered to a string with `HtmlRenderer` (`Installer/Components/`), not routable
pages: no router, no layout, no static files, no interactivity. Style and script are inline and allowed by a
per-response CSP nonce. Forms carry an antiforgery token; POSTs are limited to 20 per address per 5 minutes.
The components and `InstallerPageModel` are public only because Razor components are; they are not an
extension point. The pages cannot be restyled or extended from a site today (open question in the roadmap);
`IInstallService` can be replaced, and `InstallService`'s methods are virtual.

## Steps

1. **Database** (only in the "no database" state). SQLite / SQL Server / PostgreSQL, from fields or a typed
   connection string (`DatabaseSetupParameters.BuildConnectionString`, built by the provider's own builder).
   "Test the connection" changes nothing that stays. "Set up the database" (`IInstallService.SetUpDatabaseAsync`):
   test → run the migrations on a context of its own → write the two keys into `appsettings.json` in the content
   root (`AppSettingsWriter`: other keys kept, atomic replace, the file's permissions kept) →
   `IConfigurationRoot.Reload()` → seed data.
   - **No restart.** The DbContexts read the connection string from configuration, and the provider is resolved
     per scope (private `ZauberSetup.ActiveContextType`). A site that starts without a database registers all
     three DbContexts; a site that starts with one registers only its own, as before. (D19 said "writes
     appsettings.json and restarts"; the restart turned out not to be needed.)
   - **Cannot write `appsettings.json`, or another configuration source overrides it:** the database is still
     prepared, and the page shows the two values to set (JSON and `Zauber__DatabaseProvider` /
     `Zauber__ConnectionString`), once. When the site sees them, the installer continues.
2. **Administrator, site, starter content, review**: one form, one POST (`IInstallService.InstallAsync`). Nothing
   is saved before the last button. Order: check the form and the password rules → create the `Admin` and
   starting roles → save `GlobalSettings` (`SiteName`, `DefaultLanguage`, `AllowRegistration`) → create the
   administrator, email confirmed (Identity's own user checks run here; a refusal leaves only the roles and the
   settings behind) → add the role → write the flag. From the account onwards the request's cancellation is
   ignored, and an account that could not be finished is removed. Then the site is installed; the language and
   the starter kit follow, and a failure in those is logged and shown once, not rolled back.
3. The administrator is signed in and sent to `/admin`.

No unattended install (D19).

## Recovery: no administrator left

Set `Zauber:Identity:RecoverAdminUser` to the email address or the user name of an **existing** user and restart
(a value with an `@` is matched as an email address only). That user is unlocked, marked as confirmed, and added
to `Admin`; one deleted before v5 is restored. A user deleted since then cannot be: deleting erases the account
(Roadmap D35), and the key logs an error instead. Remove the key afterwards: while it is set, every start logs a warning and
re-applies it, and if it names nobody, whoever later creates an account with that name is promoted at the next
start. It does not create accounts.

## Starter kits (`IStarterKit`)

Discovered (public class in a scanned assembly, constructor injection). The installer lists every kit plus "An
empty site"; a kit that cannot be created is logged and left out. `InstallAsync(StarterKitContext)` runs after
the flag is written, inside `ICurrentUserAccessor.RunAs` for the new administrator, so the saves of the services
injected into the kit are attributed to them. Nobody is signed in to the request: read the user through
`ICurrentUserAccessor` (or `StarterKitContext.Administrator`), not `AuthenticationStateProvider`, which has no
state here and throws.

The template project ships one (`Starter/StarterSite.cs`: Home Page and Text Page types, two pages, views in
`Views/`, layout in `Layouts/`). ZauberCMS packages ship none: a kit needs views, and views belong to the site.
Take view and editor names from the types (`typeof(HomeView).FullName`, `new TextProperty().Alias`), never as
literals.

## Who becomes an administrator

- The installer's account. Nobody else by being first: `AssignStartingRoleAsync` gives `Zauber:NewUserStartingRole`.
- `GlobalSettings.AdminEmailAddresses` (compared without case): an account with a listed address gets `Admin`
  only when **the person holding the account has proven the address is theirs**:
  - a first sign-in through an external provider that states the address is verified; or
  - setting the account's password from an emailed link (`ResetPasswordAsync`, which also marks the address
    confirmed). Someone who registers with a password and a listed address gets the starting role and is
    emailed that link ("Finish setting up your account"); "Forgot your password" sends it again.

  **Opening a confirmation link does not promote.** The password of a registered account was chosen by whoever
  registered it, who need not be the owner of the address; a link that the owner (or a mail scanner) opens
  would hand that person an administrator account. The registration page says nothing different for a listed
  address. A session that is already signed in gets the new role when its sign-in is next refreshed.
- `RecoverAdminUser` (above), and an administrator editing a user's roles.

Everyone else who works in the admin gets there through a role with sections (Roadmap D38, D66): the seeded
`Editor` (Content and Media) or any role an administrator ticks sections for on its page in Users
([AdminUI.md](AdminUI.md) "Access"). They open only those sections; one who may open Users still cannot change an
administrator or give the `Admin` role, or any role or section they do not have themselves (D68). The starting role
(`Zauber:NewUserStartingRole`, `Member` by default) opens no section whatever is stored on it, and its page refuses
sections (D71): whoever registers gets it.

## Public registration

`GlobalSettings.AllowRegistration` (admin: Settings → Global Settings → Identity). Existing sites: on. The
installer asks and suggests off.

- Off: `GET /Account/Register` answers 404 (the site's `NotFoundComponent`; a POST is refused with 400), the link
  on the login page is hidden, and a first sign-in through an external provider creates no account
  (`ZauberSignInManager.CreateExternalUserAsync` and `IMembershipService.ExternalLoginAsync`).
  `IMembershipService.RegisterUserAsync` itself does not check the switch: a site's own registration form
  decides for itself (open question in the roadmap).
- On, the registration page applies, in this order:
  1. Rate limit policy `ZauberSetup.RegisterRateLimitPolicy` (`"register"`): 5 POSTs per address per 10 minutes
     by default; GETs are free. (It no longer shares `"login"`.) Set in `Zauber:Identity:RegisterRateLimit`
     (`PermitLimit`, `WindowMinutes`; a `PermitLimit` of 0 turns it off); the login pages' `"login"` policy is
     `Zauber:Identity:LoginRateLimit` (10 per 5 minutes). Both count per visitor address, so behind a proxy set
     `Zauber:ForwardedHeaders`.
  2. `IFormProtectionService` (replaceable singleton): a honeypot field and a signed timestamp. A filled
     honeypot, a form back in under `Zauber:FormProtection:MinimumFillSeconds` (default 3, 0 = off) or a token
     older than 12 hours is refused. `<FormProtectionFields />` (`ZauberCMS.Components.Shared`) renders the two
     fields; a site can use both on its own static forms. These checks stop scripts that post blindly. A token
     sends one form (ZA-150): once the checks pass, the page calls `TryUseToken`, and the token is refused
     (`AlreadyUsed`) for the rest of its 12 hours; `Validate` alone does not use it up, so a form shown again after
     a refused post keeps its timing. Used tokens are remembered in each server's memory: behind a load balancer a
     token could be used once per server. A site's own form calls `TryUseToken` the same way; a replacement
     service that does not implement it (the default interface method) remembers nothing.
  3. `ICaptchaProvider`, when the site has one.

### CAPTCHA providers (`ICaptchaProvider`)

ZauberCMS ships none (no new dependency, D23). A site adds a public class; with several,
`Zauber:Plugins:CaptchaProvider` names the one to use (a wrong name stops the site at start-up, naming the
setting and the providers found; it never means "no CAPTCHA").

```csharp
public class TurnstileCaptcha(IHttpClientFactory http, IConfiguration config) : ICaptchaProvider
{
    public Type Component => typeof(TurnstileWidget);   // static markup: the widget div + its <script>

    public async Task<bool> VerifyAsync(HttpContext context, CancellationToken ct = default)
    {
        var response = await http.CreateClient().PostAsync(
            "https://challenges.cloudflare.com/turnstile/v0/siteverify",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["secret"] = config["Turnstile:Secret"]!,
                ["response"] = context.Request.Form["cf-turnstile-response"].ToString()
            }), ct);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        return result.GetProperty("success").GetBoolean();
    }
}
```

The widget component is rendered inside the static form above the submit button; it must not need interactivity.
`VerifyAsync` returning false or throwing refuses the registration.

## Rules for changes here

- The installer must keep working with **no database, no static files and no network**: no CDN assets, no
  `IZauberDbContext` before the database step has succeeded, no service that reads the signed-in user before the
  administrator exists.
- Never add a way back from installed to not installed, and never promote an account because a link was opened.
- A new installer question that is stored goes into `InstallParameters` + `GlobalSettings` (with a default that
  keeps existing sites unchanged), not into a new table.
- `IInstallService` returns its own result types (`InstallResult`, `DatabaseSetupResult`), not `HandlerResult<T>`:
  they carry warnings and a "set it by hand" outcome that `HandlerResult<T>` has no place for.
- Tests: `tests/ZauberCMS.Core.Tests/Installer/`, `Membership/StartingRoleTests.cs`, `Security/FormProtectionTests.cs`.
  `TestSite.CreateAsync(createUser: false)` gives a site that is not installed.
