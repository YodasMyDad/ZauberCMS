# Audit: Security

## Your task

Read-only security audit of the ZauberCMS codebase, focused on
externally-reachable surfaces. Produce a structured findings report. **Do not
edit any code.** Do not run the application. Do not perform live testing —
this is a static review.

> If `/security-review` is available as a Claude Code skill in your harness,
> prefer running that and use this prompt to extend the focus areas. The
> sections below are ZauberCMS-specific and complement a generic review.

## Threat model

ZauberCMS is a CMS deployed as a public web app. Trust boundaries:

1. **Anonymous visitor** → front-end pages (static SSR) and any public API.
2. **Authenticated user** → may or may not have admin role.
3. **Admin user** → full content, media, settings, user management. Admin IP
   whitelist (`GlobalSettings.AllowedAdminIpAddress`) is an additional gate
   enforced in `SectionLayout.razor`.
4. **Server filesystem / DB / outbound network** — the most sensitive
   resources behind the app.

Your job is to look for places where input from one boundary can affect a
more privileged boundary unexpectedly.

## Repo context you need

- ASP.NET Core Identity with tables prefixed `Zauber*`. Login has rate
  limiting (10 / 5 min).
- Three EF providers — SQL injection surface differs slightly per provider,
  but EF parameterization should make it moot. Flag any raw SQL.
- Media stored via `IStorageProvider` (default `DiskStorageProvider` writing
  to `wwwroot/{UploadFolderName}`). Restricted-media middleware
  (`RestrictedMediaMiddleware`) gates access to non-public media.
- Image resize middleware runs **before** static files (intentional ordering).
- OAuth providers ship in-box: Google, Facebook, Microsoft, all implementing
  `IExternalAuthenticationProvider`.
- Rich text rendering uses `ZauberRTE` (TinyMCE-based). HTML stored from the
  editor is rendered into pages.
- Email sender via `IEmailProvider` (default SMTP). Templates may interpolate
  user data.
- Settings split: static (`appsettings.json` → `ZauberSettings`) and
  DB-backed (`GlobalSettings` editable in admin) — including API keys and
  allowed admin emails.

## What to flag

For each finding, classify by severity (Critical / High / Medium / Low) and
confidence (High / Medium / Low). Use CWE references where applicable.

### Authentication & session
- Endpoints that should require auth but don't (`[Authorize]` missing /
  bypassed via convention).
- Endpoints that require auth but no role check, where role is needed
  (e.g. an admin action reachable by any authenticated user).
- Cookie / token handling: `HttpOnly`, `Secure`, `SameSite` settings on
  identity / antiforgery cookies.
- Session fixation, missing rotation on privilege change.
- Lockout bypass paths (e.g. password reset that doesn't respect lockout).
- Weak password policy / password reset tokens.
- The login rate limit (10/5min) — verify it's actually wired and not bypassed
  via an alternate login path.

### Authorization
- Direct object references where the user's authorization to act on the
  object isn't checked (IDOR / CWE-639). Particularly on content,
  media, user, and role endpoints.
- Admin-only operations exposed via API endpoints, SignalR hubs, or Blazor
  component handlers without checks.
- Admin IP whitelist enforcement gaps — any admin entry point that doesn't
  hit `SectionLayout.razor`'s check.
- Tenant-style separation (if any) crossed.

### Injection
- Raw SQL anywhere: `FromSqlRaw`, string-concatenated SQL, `ExecuteSqlRaw`
  with interpolated user input.
- LINQ Dynamic / `System.Linq.Dynamic.Core` with user-controlled expressions.
- Reflection / `Type.GetType(userInput)` patterns.
- Command injection: `Process.Start` with user input, shell calls.
- Path injection (see file handling).
- LDAP / XPath / NoSQL injection (unlikely here, but check).

### XSS
- User-controlled strings rendered into HTML without encoding. Razor
  `@variable` is encoded; `@((MarkupString)...)` and `@Html.Raw(...)` are not.
- Rich text content from `ZauberRTE` rendered without sanitization.
  Confirm the sanitizer is applied at render OR at save (and not bypassable).
- Reflected XSS via query string / route values rendered back into pages.
- Stored XSS via content fields, dictionary entries, settings, audit logs,
  user display names.
- DOM-based XSS via JS interop with `eval` / `innerHTML` patterns.

### CSRF
- State-changing endpoints without antiforgery validation (POST / PUT /
  DELETE handlers, API endpoints, Blazor `EventCallback` handlers exposed via
  SSR forms).
- CORS configuration that allows credentialed cross-origin requests.

### File upload / media
- Allowed file types enforced by **extension only** (should also be content
  type / magic bytes).
- Upload size enforcement (`GlobalSettings.MaxUploadSize`) gaps.
- Path traversal: filename / folder name accepted with `..`, absolute paths,
  or alternate data streams.
- Files written under web-served paths in a way that lets users upload
  executable content (`.aspx`, `.razor`, `.html`, `.svg` with embedded JS).
- Restricted media middleware bypass: confirm it actually blocks
  unauthenticated requests for restricted assets.
- Image resize endpoint accepting arbitrary URLs (SSRF) or arbitrary
  dimensions (DoS via huge resize).

### SSRF / outbound requests
- Code that fetches a URL provided by the user (image import, OAuth
  redirects, webhook configuration) without an allowlist or scheme/host check.
- Localhost / metadata endpoint reachable from any user-supplied URL.

### Open redirect
- Login / logout redirect targets that take a `returnUrl` and don't validate
  it against the site origin.
- OAuth callback `state` / `returnUrl` handling.

### Secrets handling
- Secrets in `appsettings.json` (should be in user secrets / env vars / KMS).
- API keys / DB connection strings logged.
- Secrets returned in error responses.
- `GlobalSettings` storing secrets that shouldn't round-trip back through the
  admin UI in plaintext.

### Cryptography
- `MD5` / `SHA1` for anything security-relevant.
- Hand-rolled crypto (custom HMAC, custom token format).
- Insecure RNG (`Random` instead of `RandomNumberGenerator`) for tokens.
- Hardcoded keys / IVs.
- Identity password hashing — verify it's the framework default and not
  overridden weakly.

### Email
- Email templates that interpolate user-controlled HTML without encoding.
- Email header injection (CRLF in subject / from / to fields).
- Verification / password-reset links signed correctly and tied to the user.

### OAuth
- `IExternalAuthenticationProvider` implementations that:
  - Don't validate `state` parameter.
  - Trust email claim without validating verification status.
  - Auto-link external accounts to existing local accounts based on email
    alone.
  - Don't enforce HTTPS on callback.

### Logging / error handling
- Sensitive data in logs (passwords, tokens, full request bodies).
- Detailed error pages enabled in production paths (stack traces leaked).
- PII logged at high volume.

### Dependency / supply chain
- Pinned versions of high-risk dependencies (Identity, Auth, BlazorMonaco,
  TinyMCE) — list any obviously stale ones, but do not flag every minor
  version bump. Mark Low confidence if you can't tell whether a known CVE
  applies.

### Anti-automation / DoS
- Endpoints without rate limiting that could be abused (registration,
  password reset, content creation, search).
- Image resize / file upload paths that could be used to consume disk or
  memory.
- Regex from user input (ReDoS) — patterns compiled from settings or content.

## What NOT to flag

- Generic "use HTTPS in production" / "set HSTS" unless the deployment
  config in this repo explicitly disables it.
- Theoretical issues with no reachable trigger.
- The committed `ZauberCMS.Web/app.db` — it's a dev DB.
- Issues already covered by ASP.NET Core defaults that haven't been
  overridden.

## Workflow

This is a **find-and-fix** audit. Two phases:

### Phase 1 — Audit
Walk the codebase, identify findings, and write them to
`.audit-reports/05-security.md` (structure below). Order by severity then
confidence.

### Phase 2 — Fix
Security fixes are sensitive — over-fixing or wrong-fixing creates new
holes. Apply fixes ONLY when ALL of these hold:
- Severity is **Critical** or **High** with **High** confidence.
- The vulnerability has a concrete, described attack scenario in your
  finding (no "could potentially").
- The fix is well-known and mechanical: add `[Authorize]`, add antiforgery
  validation, encode HTML output, validate `returnUrl` against the site
  origin, replace `MD5`/`SHA1` with SHA-256 for non-password hashing,
  swap `Random` for `RandomNumberGenerator`, parameterize raw SQL, sanitize
  a path to prevent traversal.
- The fix doesn't change auth flow, identity behaviour, or session
  semantics in a way that needs design review.
- You can verify the fix by reading code — no runtime check required.

**Skip fixing** (leave in the report, marked `Status: not fixed`) when:
- Severity is Medium or Low.
- Confidence is Medium or Low.
- The fix would change identity / auth flows, OAuth handling, password
  hashing, lockout policy, or rate limit configuration. Flag, do not fix.
- The fix would change cookie / cookie-policy / antiforgery / CORS
  configuration globally. Flag, do not fix.
- The fix requires adding a sanitizer library or HTML allowlist policy.
  Flag, do not fix.
- The vulnerability is in dependency / third-party code.
- The fix needs an architectural decision (e.g. should media be served
  through a controller instead of static files?).

**When in doubt: do not fix.** A flagged finding the human reviews is
strictly better than a wrong fix shipped under the security label.

After all fixes:
- Run `dotnet build ZauberCMS.sln -c Release` and confirm it succeeds.
- If the build fails, fix the build error, build again, repeat.
- Do **not** run migrations or the app.

## Output

Write a single file: `.audit-reports/05-security.md`

```markdown
# Security Audit

_Generated: <UTC timestamp>_
_Build status after fixes: PASSED / FAILED_

## Summary
| | Found | Fixed | Skipped |
|--|--|--|--|
| Critical | N | N | N |
| High | N | N | N |
| Medium | N | N | N |
| Low | N | N | N |

## Findings

### [SEVERITY] Short title
- **File:** `path/to/file.cs:line`
- **CWE:** CWE-XXX (if applicable)
- **Confidence:** High / Medium / Low
- **Status:** FIXED / not fixed
- **Issue:** One paragraph.
- **Attack scenario:** Concrete steps an attacker would take.
- **Why it matters:** What the attacker gains.
- **Fix applied:** What you changed. Omit if not fixed.
- **Why not fixed:** Reason. Omit if fixed.
- **Notes:** Anything you weren't sure about.

(repeat per finding)

## Surfaces reviewed
List the entry points / endpoints / handlers you specifically inspected.

## Files modified
List every file you edited with a one-line summary.

## Areas you did not fully cover
List any directories / files you didn't get to and why.
```

If a category has no findings, write "No issues found."

## Hard rules

- Edits allowed only for findings that pass the strict Phase 2 criteria.
- Default to **flag, don't fix**. Security is high-blast-radius — a wrong
  fix is worse than a flagged finding.
- No changes to identity / auth / cookie / CORS / antiforgery global
  configuration. Flag those, leave them.
- No new dependencies (no new sanitizer libs, no new auth providers).
- `dotnet build` only.
- If a fix breaks the build and you can't repair it in scope, **revert** and
  mark `Status: not fixed — build broke, reverted`.
- Every Critical / High finding must include a concrete attack scenario,
  fixed or not.
