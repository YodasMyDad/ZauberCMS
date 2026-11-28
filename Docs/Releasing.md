# Releasing, Packaging & the Template

## Packages

`ZauberCMS.Core` ← `ZauberCMS.Components` ← `ZauberCMS.Routing` ← `ZauberCMS` (umbrella), plus the
`ZauberCMS.Template` template package (`dotnet new zaubercms`).

- All four library csprojs set `GeneratePackageOnBuild`; a Release build of the solution produces the nupkgs and
  their symbol packages (`.snupkg`), and the `CopyNuGetPackage` target copies both to `NugetSource/` (gitignored). No
  separate `dotnet pack` for them.
- The four depend on each other by `ProjectReference` only; `dotnet pack` turns that into a package dependency at the
  same version. (Until Phase 11b each also had a `PackageReference` to its sibling, stamped at release.)
- A package lists only the packages its `ProjectReference`s do not already bring (Phase 11c, D45): Core has the
  data, identity, UI and editor packages; Components adds BlazorMonaco, Humanizer and TinyMCE.Blazor; Routing adds
  none; ZauberCMS adds Serilog.Sinks.Async. A site gets the rest through Core. Moving a package out of Core (11e)
  means adding it to the package that still uses it. `PackageContentsTests` checks this.
- `Directory.Build.props` holds the one `<Version>`, the shared metadata (authors, MIT licence, URLs, tags), the
  package page (`ReadMe.md` as the readme, `ZauberCMS.Template/images/logo.png` as the icon), Source Link and
  symbols, and the `CopyNuGetPackage` target; a csproj keeps only its `PackageId`, `Description` and its own
  settings. Every dependency's version is in `Directory.Packages.props` (central package management), so the four
  packages ship the same versions of what they share.
- Debugging into the packages (D45): Source Link (part of the .NET SDK) writes the commit and GitHub address into
  each PDB, the PDBs go into the `.snupkg` that nuget.org's symbol server serves, and sources not in git (generated
  Razor code) are embedded. `ContinuousIntegrationBuild` is on only on GitHub Actions, so CI's PDBs have no
  build-machine paths and a local build's still point at the local files.
- `ZauberCMS.csproj` also packs `appsettings.json` → `build/settings.json` and `ZauberCMS.targets` (copies it to the
  consumer's `appsettings.json` **only if missing**).
- The Components package's `package.json` / `package-lock.json` (the Tailwind build of the admin CSS) are `None`
  items, so they are not packed into sites (ZA-087).
- Versions: never hard-code in docs. Source of truth is `<Version>` in `Directory.Build.props` / the latest
  `vX.Y.Z` tag.

## CI — `.github/workflows/ci.yml` (primary path)

- Every push (any branch) and PRs to `main-static`: build, the tests (including the PostgreSQL and Redis tests:
  Docker containers on the runner; they fail rather than skip on CI), a scan that fails on any package with a
  known vulnerability, direct or transitive, then the template smoke test (`tests/template-smoke.ps1`, below). The
  SDK comes from `global.json`. A push or PR that changes only documentation (Markdown, `Docs/`, `.audit/`,
  `.claude/`, IDE folders, `LICENSE`) runs nothing, so it does not publish. That includes `ReadMe.md`, although it
  is the packages' readme: a package's readme is part of its version, so the new text ships with the next release
  instead of a release of its own (D45).
- The workflow reads the repository only (`permissions: contents: read`); the publish job alone gets
  `contents: write`. Actions are pinned by commit with the tag in a comment: to update one, take the commit of the
  new release tag (`gh api repos/<owner>/<action>/git/ref/tags/<tag>`) and change both.
- Push to `main-static` (**publishes**), one version from one commit (D44):
  1. Version. A version tag already on HEAD (a rerun, or a release tagged by hand) is published again. Otherwise
     `<Version>` in `Directory.Build.props` is used as written when it is above the latest stable tag; at or below
     it, the latest stable tag's patch is counted up. A prerelease (`X.Y.Z-label`) is published as written, once:
     bump it before the next push (the run fails otherwise). A version that is not `X.Y.Z[-label]` fails.
  2. The tag `vX.Y.Z` is created and pushed **before anything is built**. If it exists on another commit, or another
     run pushes it first, the run stops. So a failure later never lets the next commit reuse the version.
  3. Stamp the version into `Directory.Build.props`, the template csproj (`PackageVersion`) and the template
     site's `ZauberCMS` reference; `dotnet build ZauberCMS.sln -c Release` packs the four; `dotnet pack` the
     template; all five must be there, and the four libraries' symbol packages.
  4. Push in dependency order: Core, Components, Routing, ZauberCMS, Template (secret `NUGET_API_KEY`);
     `dotnet nuget push` sends each `.snupkg` with its package. On the first run a version that already exists on
     nuget.org fails the push; on a rerun of the tagged commit the packages pushed before are skipped
     (`--skip-duplicate`).
  5. Commit `Bump version to X.Y.Z [skip ci]` back to `main-static` when the stamped files changed, on top of
     whatever `main-static` has by then (D82): the bump is replayed on the newest `main-static`, and where only this
     job's earlier bumps changed the same lines, its version wins (two pushes in flight conflicted here before,
     ZA-195). It commits nothing when `main-static` already has a higher version (a rerun of an older release), and
     only warns when a later commit of a person changed one of the three files (that commit's own run stamps it). A
     push that loses a race is fetched and replayed again, up to five times. Pull before your next push.
     The publish jobs run one at a time in push order (a `concurrency` group); a run still waiting when a newer push
     arrives is cancelled, and the newer one publishes both changes.
- After a failed publish: rerun the job for the same commit (it finds its tag and finishes the push), or push a
  new commit (it gets the next version; the reserved one stays unused or partly published). Never delete a
  version tag whose packages reached nuget.org.
- Major/minor release: set `<Version>` in `Directory.Build.props` (for example the next major) and push; or create
  and push the tag on HEAD first, which publishes that version.

## Local fallback — `release.ps1`

`./release.ps1 -NewVersion X.Y.Z`: stamps the same three files as CI (`Directory.Build.props`, the template csproj,
the template site's `ZauberCMS` reference; each keeps its encoding), builds, packs the template, drops all five
nupkgs in `NugetSource/` for a manual `dotnet nuget push` (push them in the dependency order above). Prompts for
confirmation (`Read-Host`), never tags or commits. Use only when CI is unavailable, and tag the commit you built.

## Template

- The template is outside the repository's build settings: `ZauberCMS.Template/Directory.Build.props` (empty) and
  `Directory.Packages.props` (central package management off) stop the root files for everything under
  `ZauberCMS.Template/`. They sit beside `template/`, so they are not packed. Keep the template site's csproj
  self-contained: explicit `Version`s, its own `TargetFramework`.
- `ZauberCMS.Template/ZauberCMS.Template.csproj`: `PackageType=Template`, `PackageVersion` (not `Version`),
  `IncludeBuildOutput=false`, `Compile Remove="**\*"`, content from `template/`. It is in `ZauberCMS.sln` but compiles
  nothing.
- `template/.template.config/template.json`: `shortName: zaubercms`, `sourceName: ZauberCMSTemplate`.
- Scaffold `template/ZauberCMSTemplate.Site/`: minimal `Program.cs`, full `appsettings.json` (database keys empty:
  the installer asks, → [Installer.md](Installer.md)), launch settings (profiles `https`, `http`, `IIS Express`),
  `ZauberCMSTemplate.sln`, `template/.gitignore` (build output, the SQLite file, `wwwroot/media/`, the resize cache,
  `Logs/`: D47), and the starter kit: `Starter/StarterSite.cs` (`IStarterKit`), `Views/`, `Layouts/`,
  `_Imports.razor`, `wwwroot/css/site.css`. A file that starts with a dot is packed because the template csproj sets
  `NoDefaultExcludes`. Keep its `appsettings.json` in step with `ZauberCMS/appsettings.json` and the Web one: the
  three have the same `Zauber` keys and the same `Serilog` section (`AppSettingsParityTests`); the Web one keeps its
  SQLite values.
- The template site sets `RequiresAspNetWebAssets` (ZA-094): the SDK then adds the framework's scripts
  (`_framework/blazor.web.js`) of the ASP.NET Core version it ships, so a site gets the current script whenever it
  is built with a newer SDK. Do not pin `Microsoft.AspNetCore.App.Internal.Assets` (it was pinned at 10.0.0 and never
  moved). A site made without the template gets the same from the SDK as long as it has a `.razor` file of its own;
  otherwise it needs that property too.
- The template site references the **published** package, so nothing in this repository compiles its `.cs` and
  `.razor` files. After changing them (or an API they use), build a copy against the source: copy the site folder
  somewhere outside the repo, replace the `ZauberCMS` `PackageReference` with a `ProjectReference` to
  `ZauberCMS/ZauberCMS.csproj`, `dotnet build`, run it and go through the installer with the starter kit chosen.

### Template smoke test — `tests/template-smoke.ps1`

Run from the repository root (Windows PowerShell 5.1 or PowerShell 7; needs the .NET SDK and nuget.org):
`./tests/template-smoke.ps1` (`-KeepWorkFolder` keeps the feed, site and logs). CI runs it in the build job (D46).

1. Builds the four packages and packs the template at a throw-away version (`<Version>-smoke.<UTC time>`) into a
   local feed in a work folder (the system temp folder by default). The version is one nuget.org does not have, so
   the site can only restore what was built.
2. Installs the template into a private template-engine hive (`--debug:custom-hive`): a ZauberCMS.Template already
   installed on the machine is not touched. Creates `SmokeSite` with `dotnet new zaubercms` and checks its files
   (`.gitignore`, no `.template.config`, the launch profiles, `RequiresAspNetWebAssets`).
3. Points the site at that exact version, builds and publishes it, starts the published site in Production on a
   free port, and goes through the installer over HTTP (SQLite, the template's starter kit).
4. Checks the home page, `_framework/blazor.web.js` (the fingerprinted and the plain address), a 404 with its page,
   the admin (signed in by the installer) and every script and stylesheet it loads, and that the site logged no
   error other than the expected "no database" one at first start.
5. Stops the site, removes the throw-away version from the NuGet cache and the repository's `bin` folders, and
   deletes the work folder. It ends with exit code 1 and the list of failures when a check fails.

To try unreleased packages in a site of your own, add `NugetSource/` as a local feed.

## Consumers

Always point users at the template / NuGet — never at building from source:

```ps
dotnet new install ZauberCMS.Template
dotnet new zaubercms -n YourSiteName
```
