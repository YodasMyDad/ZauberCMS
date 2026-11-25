# Releasing, Packaging & the Template

## Packages

`ZauberCMS.Core` ← `ZauberCMS.Components` ← `ZauberCMS.Routing` ← `ZauberCMS` (umbrella), plus the
`ZauberCMS.Template` template package (`dotnet new zaubercms`).

- All four library csprojs set `GeneratePackageOnBuild`; a Release build of the solution produces the nupkgs and the
  `CopyNuGetPackage` target copies them to `NugetSource/` (gitignored). No separate `dotnet pack` for them.
- Razor projects carry **both** a `ProjectReference` (local dev) and a `PackageReference` (packaged dependency chain)
  to their sibling — intentional; version numbers in the `PackageReference`s are rewritten at release.
- Shared metadata (authors, MIT licence, repo URL) is repeated per csproj; keep the four in step.
- `ZauberCMS.csproj` also packs `appsettings.json` → `build/settings.json`, `ZauberCMS.targets` (copies it to the
  consumer's `appsettings.json` **only if missing**) and `ReadMe.md`.
- Versions: never hard-code in docs. Source of truth is `<Version>` in the csprojs / latest `vX.Y.Z` tag.

## CI — `.github/workflows/ci.yml` (primary path)

- Every push (any branch) and PRs to `main-static`: build and run the tests, including the PostgreSQL and Redis tests
  (Docker containers on the runner; they fail rather than skip on CI).
- Push to `main-static` (**publishes**):
  1. Version: reuse a `vX.Y.Z` tag already on HEAD; else `max(csproj <Version>, latest tag)` + patch bump.
  2. sed-stamp the version into the six csprojs (four libs, template, template site).
  3. `dotnet build ZauberCMS.sln -c Release`; `dotnet pack` the template into `NugetSource/`.
  4. Push every `*.<version>.nupkg` to nuget.org (`--skip-duplicate`, secret `NUGET_API_KEY`).
  5. Tag `vX.Y.Z`, then commit `Bump csproj versions to X.Y.Z [skip ci]` back to `main-static` (needs
     `contents: write` and push rights for the bot). Pull before your next push.
- Major/minor release: bump `<Version>` in the csprojs (it is a floor), or pre-create and push the tag on HEAD before
  pushing — with a pre-existing tag the version is still stamped for the build, but the tag and
  commit-back steps are skipped (csprojs in the repo keep the old version until the next push).

## Local fallback — `release.ps1`

`./release.ps1 -NewVersion X.Y.Z`: stamps the same six csprojs and the PackageReference chain, builds, packs the
template, drops all five nupkgs in `NugetSource/` for a manual `dotnet nuget push`. Prompts for confirmation
(`Read-Host`), never tags or commits. Use only when CI is unavailable.

## Template

- `ZauberCMS.Template/ZauberCMS.Template.csproj`: `PackageType=Template`, `PackageVersion` (not `Version`),
  `IncludeBuildOutput=false`, `Compile Remove="**\*"`, content from `template/`. It is in `ZauberCMS.sln` but compiles
  nothing.
- `template/.template.config/template.json`: `shortName: zaubercms`, `sourceName: ZauberCMSTemplate`.
- Scaffold `template/ZauberCMSTemplate.Site/`: minimal `Program.cs`, full `appsettings.json` (SQLite), launch settings,
  `ZauberCMSTemplate.sln`. Keep its `appsettings.json` in step with `ZauberCMS/appsettings.json` and the Web one.

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
