# Releasing, Packaging & the Template

## Packages

`ZauberCMS.Core` ← `ZauberCMS.Components` ← `ZauberCMS.Routing` ← `ZauberCMS` (umbrella), plus the
`ZauberCMS.Template` template package (`dotnet new zaubercms`).

- All four library csprojs set `GeneratePackageOnBuild`; a Release build of the solution produces the nupkgs and the
  `CopyNuGetPackage` target copies them to `NugetSource/` (gitignored). No separate `dotnet pack` for them.
- The four depend on each other by `ProjectReference` only; `dotnet pack` turns that into a package dependency at the
  same version. (Until Phase 11b each also had a `PackageReference` to its sibling, stamped at release.)
- `Directory.Build.props` holds the one `<Version>` and the shared metadata (authors, MIT licence, URLs, tags) and
  the `CopyNuGetPackage` target; a csproj keeps only its `PackageId`, `Description` and its own settings. Every
  dependency's version is in `Directory.Packages.props` (central package management), so the four packages ship the
  same versions of what they share.
- `ZauberCMS.csproj` also packs `appsettings.json` → `build/settings.json`, `ZauberCMS.targets` (copies it to the
  consumer's `appsettings.json` **only if missing**) and `ReadMe.md`.
- Versions: never hard-code in docs. Source of truth is `<Version>` in `Directory.Build.props` / the latest
  `vX.Y.Z` tag.

## CI — `.github/workflows/ci.yml` (primary path)

- Every push (any branch) and PRs to `main-static`: build, the tests (including the PostgreSQL and Redis tests:
  Docker containers on the runner; they fail rather than skip on CI), then a scan that fails on any package with a
  known vulnerability, direct or transitive. The SDK comes from `global.json`. A push or PR that changes only
  documentation (Markdown, `Docs/`, `.audit/`, `.claude/`, IDE folders, `LICENSE`) runs nothing, so it does not
  publish; a `ReadMe.md` change ships with the next release.
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
     template; all five must be there.
  4. Push in dependency order: Core, Components, Routing, ZauberCMS, Template (secret `NUGET_API_KEY`). On the
     first run a version that already exists on nuget.org fails the push; on a rerun of the tagged commit the
     packages pushed before are skipped (`--skip-duplicate`).
  5. Commit `Bump version to X.Y.Z [skip ci]` back to `main-static` when the stamped files changed. Pull before
     your next push.
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
  the installer asks, → [Installer.md](Installer.md)), launch settings, `ZauberCMSTemplate.sln`, and the starter
  kit: `Starter/StarterSite.cs` (`IStarterKit`), `Views/`, `Layouts/`, `_Imports.razor`, `wwwroot/css/site.css`.
  Keep its `appsettings.json` in step with `ZauberCMS/appsettings.json` and the Web one (the template and
  Web files have the same keys, the packaged one still lacks `ImageResize`, ZA-118; the Web one keeps its SQLite
  values).
- The template site references the **published** package, so nothing in this repository compiles its `.cs` and
  `.razor` files. After changing them (or an API they use), build a copy against the source: copy the site folder
  somewhere outside the repo, replace the `ZauberCMS` `PackageReference` with a `ProjectReference` to
  `ZauberCMS/ZauberCMS.csproj`, `dotnet build`, run it and go through the installer with the starter kit chosen.

Test locally:

```ps
dotnet nuget locals all --clear                 # avoid stale package cache
cd ZauberCMS.Template/template
dotnet new install ./ ; dotnet new zaubercms -n MyTest ; dotnet new uninstall ./
```

To test unreleased packages in a consumer, add `NugetSource/` as a local feed.

## Consumers

Always point users at the template / NuGet — never at building from source:

```ps
dotnet new install ZauberCMS.Template
dotnet new zaubercms -n YourSiteName
```
