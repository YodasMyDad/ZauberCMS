# Template smoke test (Roadmap Phase 11c). Builds the five packages from this repository at a throw-away version
# into a local feed, creates a site with `dotnet new zaubercms` from that template package, publishes it against the
# local feed, starts it, goes through the installer over HTTP (SQLite, the template's starter kit) and checks:
# the installer, the starter site's home page, the framework script (_framework/blazor.web.js), the admin page and
# every script and stylesheet it loads, and the not-found page.
#
# Leaves the machine as it was: the template is installed in a private template-engine hive (an installed
# ZauberCMS.Template stays as it is), the throw-away version is removed from the NuGet cache and the repository's
# bin folders, and the work folder is deleted (keep it with -KeepWorkFolder).
#
# Runs on Windows PowerShell 5.1 and PowerShell 7 (CI). From the repository root:
#   ./tests/template-smoke.ps1
# Needs the .NET SDK and nuget.org (the site's other packages come from there).

param(
    # Where the feed, the site and its published copy go. Default: a new folder in the system temp folder.
    [string]$WorkFolder,
    [switch]$KeepWorkFolder,
    # Seconds to wait for the site to answer after it starts.
    [int]$StartTimeoutSeconds = 120
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not (Test-Path (Join-Path $repositoryRoot 'ZauberCMS.sln'))) {
    throw "ZauberCMS.sln not found in $repositoryRoot"
}

$stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMddHHmmss')
if (-not $WorkFolder) {
    $WorkFolder = Join-Path ([System.IO.Path]::GetTempPath()) "zaubercms-template-smoke-$stamp"
}
New-Item -ItemType Directory -Force -Path $WorkFolder | Out-Null
$WorkFolder = (Resolve-Path $WorkFolder).Path

$feed = Join-Path $WorkFolder 'feed'
$hive = Join-Path $WorkFolder 'template-hive'
$siteRoot = Join-Path $WorkFolder 'site'
$publishFolder = Join-Path $WorkFolder 'publish'
$siteName = 'SmokeSite'
$packageIds = @('ZauberCMS.Core', 'ZauberCMS.Components', 'ZauberCMS.Routing', 'ZauberCMS', 'ZauberCMS.Template')

# A version nuget.org does not have, so the site can only restore what was built here (a published version could
# also come from the NuGet cache).
$propsXml = [xml](Get-Content (Join-Path $repositoryRoot 'Directory.Build.props') -Raw)
$baseVersion = ($propsXml.SelectSingleNode('/Project/PropertyGroup/Version').InnerText -split '-')[0]
$version = "$baseVersion-smoke.$stamp"

$failures = New-Object System.Collections.Generic.List[string]
$siteProcess = $null
$client = $null

function Write-Step([string]$Text) { Write-Host "==> $Text" -ForegroundColor Cyan }

function Assert-That([bool]$Condition, [string]$Message) {
    if ($Condition) {
        Write-Host "    ok    $Message"
    } else {
        Write-Host "    FAIL  $Message" -ForegroundColor Red
        $failures.Add($Message)
    }
}

# Runs dotnet and stops the script when it fails. The native command's stderr is not turned into errors.
function Invoke-Dotnet {
    param([string[]]$Arguments, [string]$WorkingDirectory = $repositoryRoot)
    Write-Host "    dotnet $($Arguments -join ' ')"
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    Push-Location $WorkingDirectory
    try {
        & dotnet @Arguments | Out-Host
        $exitCode = $LASTEXITCODE
    } finally {
        Pop-Location
        $ErrorActionPreference = $previous
    }
    if ($exitCode -ne 0) {
        throw "dotnet $($Arguments[0]) failed with exit code $exitCode"
    }
}

function Get-FreeTcpPort {
    $listener = New-Object System.Net.Sockets.TcpListener([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $port = ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
    $listener.Stop()
    return $port
}

function Get-Page([string]$Path) {
    $response = $client.GetAsync($Path).GetAwaiter().GetResult()
    $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    return [pscustomobject]@{ Status = [int]$response.StatusCode; Response = $response; Body = $body }
}

function Send-Form([string]$Path, [hashtable]$Fields) {
    $values = New-Object 'System.Collections.Generic.Dictionary[string,string]'
    foreach ($key in $Fields.Keys) { $values[$key] = [string]$Fields[$key] }
    $content = New-Object System.Net.Http.FormUrlEncodedContent($values)
    $response = $client.PostAsync($Path, $content).GetAwaiter().GetResult()
    $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    return [pscustomobject]@{ Status = [int]$response.StatusCode; Response = $response; Body = $body }
}

function Get-RedirectTarget([object]$Page) {
    if ($Page.Response.Headers.Location) { return $Page.Response.Headers.Location.OriginalString }
    return ''
}

# A redirect (302, or 303 after a form post) to an address that ends with $Ending
function Test-RedirectTo([object]$Page, [string]$Ending) {
    return (@(302, 303) -contains $Page.Status) -and ((Get-RedirectTarget $Page) -match "$([regex]::Escape($Ending))$")
}

# The antiforgery field of the installer's form: the hidden input before name="step"
function Get-AntiforgeryField([string]$Html) {
    $match = [regex]::Match($Html, '<input type="hidden" name="([^"]+)" value="([^"]+)"\s*/?>\s*<input type="hidden" name="step"')
    if (-not $match.Success) { throw 'The installer page has no antiforgery field.' }
    return @{ Name = $match.Groups[1].Value; Value = [System.Net.WebUtility]::HtmlDecode($match.Groups[2].Value) }
}

function Remove-SmokeVersion {
    # The throw-away version in the NuGet cache and in the repository's bin folders
    $globalPackages = $null
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $line = & dotnet nuget locals global-packages --list 2>$null | Select-Object -First 1
        if ($line -match 'global-packages:\s*(.+)$') { $globalPackages = $Matches[1].Trim() }
    } finally {
        $ErrorActionPreference = $previous
    }
    if ($globalPackages) {
        foreach ($id in $packageIds) {
            $cached = Join-Path (Join-Path $globalPackages $id.ToLowerInvariant()) $version.ToLowerInvariant()
            if (Test-Path $cached) { Remove-Item -Recurse -Force $cached }
        }
    }
    foreach ($id in $packageIds | Where-Object { $_ -ne 'ZauberCMS.Template' }) {
        foreach ($extension in @('nupkg', 'snupkg')) {
            $built = Join-Path $repositoryRoot "$id/bin/Release/$id.$version.$extension"
            if (Test-Path $built) { Remove-Item -Force $built }
        }
    }
}

try {
    Write-Host "Template smoke test: version $version, work folder $WorkFolder"

    Write-Step 'Build the four packages and pack the template into a local feed'
    New-Item -ItemType Directory -Force -Path $feed | Out-Null
    # Building ZauberCMS builds and packs Core, Components and Routing too; CopyNuGetPackage (Directory.Build.props)
    # copies each package and symbol package to LocalNugetSource.
    Invoke-Dotnet @('build', 'ZauberCMS/ZauberCMS.csproj', '-c', 'Release', '-v', 'minimal', '-nologo',
        "-p:Version=$version", "-p:LocalNugetSource=$feed")
    Invoke-Dotnet @('pack', 'ZauberCMS.Template/ZauberCMS.Template.csproj', '-c', 'Release', '-v', 'minimal', '-nologo',
        "-p:PackageVersion=$version", '-o', $feed)
    foreach ($id in $packageIds) {
        Assert-That (Test-Path (Join-Path $feed "$id.$version.nupkg")) "package $id $version built"
    }
    foreach ($id in $packageIds | Where-Object { $_ -ne 'ZauberCMS.Template' }) {
        Assert-That (Test-Path (Join-Path $feed "$id.$version.snupkg")) "symbol package $id $version built"
    }

    Write-Step 'Install the template (private hive) and create a site'
    Invoke-Dotnet @('new', 'install', (Join-Path $feed "ZauberCMS.Template.$version.nupkg"), '--debug:custom-hive', $hive)
    Invoke-Dotnet @('new', 'zaubercms', '-n', $siteName, '-o', $siteRoot, '--debug:custom-hive', $hive)

    $siteProject = Join-Path $siteRoot "$siteName.Site/$siteName.Site.csproj"
    Assert-That (Test-Path $siteProject) 'the site project was created'
    Assert-That (Test-Path (Join-Path $siteRoot "$siteName.sln")) 'the solution was created'
    Assert-That (Test-Path (Join-Path $siteRoot '.gitignore')) 'the site has a .gitignore'
    Assert-That (-not (Test-Path (Join-Path $siteRoot '.template.config'))) 'the template configuration is not copied'
    $launchSettings = Get-Content (Join-Path $siteRoot "$siteName.Site/Properties/launchSettings.json") -Raw
    Assert-That ($launchSettings -notmatch 'Umbraco') 'the launch profiles are not named after Umbraco'
    $projectText = Get-Content $siteProject -Raw
    Assert-That ($projectText -match '<RequiresAspNetWebAssets>true</RequiresAspNetWebAssets>') 'the site asks the SDK for the framework scripts'
    Assert-That ($projectText -notmatch 'Microsoft\.AspNetCore\.App\.Internal\.Assets') 'the site pins no framework asset package'

    # Restore exactly the version built above, from the local feed first
    $pattern = '(<PackageReference Include="ZauberCMS" Version=")[^"]*(")'
    Assert-That ($projectText -match $pattern) 'the site references the ZauberCMS package'
    $projectText = [regex]::Replace($projectText, $pattern, "`${1}[$version]`${2}")
    [System.IO.File]::WriteAllText($siteProject, $projectText)
    $nugetConfig = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="smoke-feed" value="$feed" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
"@
    [System.IO.File]::WriteAllText((Join-Path $siteRoot 'nuget.config'), $nugetConfig)

    Write-Step 'Build and publish the site'
    Invoke-Dotnet @('build', "$siteName.Site/$siteName.Site.csproj", '-c', 'Release', '-v', 'minimal', '-nologo') $siteRoot
    Invoke-Dotnet @('publish', "$siteName.Site/$siteName.Site.csproj", '-c', 'Release', '-v', 'minimal', '-nologo',
        '--no-build', '-o', $publishFolder) $siteRoot
    Assert-That (Test-Path (Join-Path $publishFolder 'appsettings.json')) 'the published site has its appsettings.json'
    Assert-That (@(Get-ChildItem (Join-Path $publishFolder 'wwwroot/_framework') -Filter 'blazor.web*.js' -ErrorAction SilentlyContinue).Count -gt 0) 'the published site contains _framework/blazor.web.js'

    Write-Step 'Start the published site (Production)'
    $port = Get-FreeTcpPort
    $baseUrl = "http://127.0.0.1:$port"
    $previousEnvironment = $env:ASPNETCORE_ENVIRONMENT
    $env:ASPNETCORE_ENVIRONMENT = 'Production'
    try {
        $siteProcess = Start-Process -FilePath 'dotnet' -ArgumentList @("$siteName.Site.dll", '--urls', $baseUrl) `
            -WorkingDirectory $publishFolder -PassThru `
            -RedirectStandardOutput (Join-Path $WorkFolder 'site-stdout.log') -RedirectStandardError (Join-Path $WorkFolder 'site-stderr.log')
    } finally {
        $env:ASPNETCORE_ENVIRONMENT = $previousEnvironment
    }
    # Windows PowerShell gives no ExitCode for a Start-Process process unless its handle was read while it ran
    $null = $siteProcess.Handle

    Add-Type -AssemblyName System.Net.Http
    $handler = New-Object System.Net.Http.HttpClientHandler
    $handler.AllowAutoRedirect = $false
    $handler.UseCookies = $true
    $handler.CookieContainer = New-Object System.Net.CookieContainer
    $client = New-Object System.Net.Http.HttpClient($handler)
    $client.BaseAddress = New-Object System.Uri($baseUrl)
    $client.Timeout = [TimeSpan]::FromSeconds(60)

    $deadline = (Get-Date).AddSeconds($StartTimeoutSeconds)
    $started = $false
    while (-not $started -and (Get-Date) -lt $deadline) {
        if ($siteProcess.HasExited) { throw "The site stopped with exit code $($siteProcess.ExitCode) (its output and log follow)" }
        try { $null = Get-Page '/'; $started = $true } catch { Start-Sleep -Milliseconds 500 }
    }
    if (-not $started) { throw "The site did not answer within $StartTimeoutSeconds seconds" }

    Write-Step 'Installer'
    $page = Get-Page '/'
    Assert-That (Test-RedirectTo $page '/install') "a new site sends / to the installer ($($page.Status) $(Get-RedirectTarget $page))"
    $page = Get-Page '/install'
    Assert-That ($page.Status -eq 200 -and $page.Body -match 'name="step" value="database"') "the installer shows the database step ($($page.Status))"

    $token = Get-AntiforgeryField $page.Body
    $page = Send-Form '/install' @{ $token.Name = $token.Value; step = 'database'; provider = 'Sqlite'; sqliteFile = 'app.db'; action = 'save' }
    Assert-That (Test-RedirectTo $page '/install') "the SQLite database is set up ($($page.Status) $(Get-RedirectTarget $page))"

    $page = Get-Page '/install'
    Assert-That ($page.Status -eq 200 -and $page.Body -match 'name="step" value="setup"') "the installer shows the set-up step ($($page.Status))"
    $kits = @([regex]::Matches($page.Body, 'name="starterKit" value="([^"]+)"') | ForEach-Object { [System.Net.WebUtility]::HtmlDecode($_.Groups[1].Value) })
    Assert-That ($kits.Count -eq 1) "the template's starter kit is offered ($($kits -join ', '))"
    $token = Get-AntiforgeryField $page.Body
    $fields = @{
        $token.Name = $token.Value; step = 'setup'
        adminName = 'smoke'; adminEmail = 'smoke@example.com'; adminPassword = 'Smoke-test-2026!'
        siteName = 'Smoke'; language = 'en-US'
    }
    if ($kits.Count -gt 0) { $fields.starterKit = $kits[0] }
    $page = Send-Form '/install' $fields
    Assert-That (Test-RedirectTo $page '/admin') "the site is installed and opens the admin ($($page.Status) $(Get-RedirectTarget $page))"

    Write-Step 'Front end and framework script'
    $page = Get-Page '/'
    Assert-That ($page.Status -eq 200) "the home page answers 200 ($($page.Status))"
    $frameworkScript = [regex]::Match($page.Body, '<script src="([^"]*_framework/blazor\.web[^"]*\.js)"')
    Assert-That $frameworkScript.Success 'the home page loads the framework script'
    if ($frameworkScript.Success) {
        $script = Get-Page ('/' + $frameworkScript.Groups[1].Value.TrimStart('/'))
        Assert-That ($script.Status -eq 200 -and $script.Body.Length -gt 10000) "$($frameworkScript.Groups[1].Value) is served ($($script.Status), $($script.Body.Length) characters)"
    }
    $script = Get-Page '/_framework/blazor.web.js'
    Assert-That ($script.Status -eq 200) "_framework/blazor.web.js is served ($($script.Status))"
    $page = Get-Page '/smoke-test-no-such-page'
    Assert-That ($page.Status -eq 404 -and $page.Body.Length -gt 0) "an unknown address answers 404 with a page ($($page.Status), $($page.Body.Length) characters)"

    Write-Step 'Admin (signed in by the installer) and its scripts and styles'
    # /admin sends each user to the first section they may open (Roadmap D67): the administrator to Content
    $page = Get-Page '/admin'
    Assert-That (Test-RedirectTo $page '/admin/content') "the admin address opens the first section ($($page.Status) $(Get-RedirectTarget $page))"
    $page = Get-Page '/admin/content'
    Assert-That ($page.Status -eq 200) "the admin page answers 200 ($($page.Status) $(Get-RedirectTarget $page))"
    $assets = @([regex]::Matches($page.Body, '<(?:script src|link rel="stylesheet" href)="([^"]+)"') | ForEach-Object { $_.Groups[1].Value } | Where-Object { $_ -notmatch '^https?:' })
    Assert-That ($assets.Count -ge 5) "the admin page loads its scripts and styles ($($assets.Count))"
    foreach ($asset in $assets) {
        $file = Get-Page ('/' + $asset.TrimStart('/'))
        Assert-That ($file.Status -eq 200) "$asset is served ($($file.Status))"
    }

    Write-Step 'Site log'
    $logs = @(Get-ChildItem (Join-Path $publishFolder 'Logs') -Filter '*.txt' -ErrorAction SilentlyContinue)
    # Expected: the error a site without database settings logs at start-up (Roadmap D24)
    $errors = @($logs | ForEach-Object { Get-Content $_.FullName } |
        Where-Object { $_ -match '\[(ERR|FTL)\]' -and $_ -notmatch 'This site has no database' })
    Assert-That ($errors.Count -eq 0) "nothing else logged as an error ($($errors.Count))"
    $errors | Select-Object -First 10 | ForEach-Object { Write-Host "      $_" }
}
catch {
    $failures.Add("Stopped: $($_.Exception.Message)")
    Write-Host "    FAIL  $($_.Exception.Message)" -ForegroundColor Red
}
finally {
    if ($client) { $client.Dispose() }
    if ($siteProcess -and -not $siteProcess.HasExited) {
        Stop-Process -Id $siteProcess.Id -Force -ErrorAction SilentlyContinue
        $siteProcess.WaitForExit(10000) | Out-Null
    }
    # The work folder is deleted below: print what explains the failure first (ZA-181). An unhandled start-up
    # exception goes to stderr; what the site logged goes to its Logs folder.
    if ($failures.Count -gt 0) {
        foreach ($output in @(@('site-stdout.log', 'site output'), @('site-stderr.log', 'site error output'))) {
            $outputFile = Join-Path $WorkFolder $output[0]
            if ((Test-Path $outputFile) -and (Get-Item $outputFile).Length -gt 0) {
                Write-Host "--- $($output[1]) (last 40 lines) ---"
                Get-Content $outputFile -Tail 40 | ForEach-Object { Write-Host $_ }
            }
        }
        $siteLog = Get-ChildItem (Join-Path $publishFolder 'Logs') -Filter '*.txt' -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime | Select-Object -Last 1
        if ($siteLog) {
            Write-Host "--- site log $($siteLog.Name) (last 60 lines) ---"
            Get-Content $siteLog.FullName -Tail 60 | ForEach-Object { Write-Host $_ }
        }
    }
    Remove-SmokeVersion
    if ($KeepWorkFolder) {
        Write-Host "Work folder kept: $WorkFolder"
    } else {
        Remove-Item -Recurse -Force $WorkFolder -ErrorAction SilentlyContinue
    }
}

if ($failures.Count -gt 0) {
    Write-Host "Template smoke test FAILED ($($failures.Count)):" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}
Write-Host 'Template smoke test passed.' -ForegroundColor Green
exit 0
