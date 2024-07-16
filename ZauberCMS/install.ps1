param($installPath, $toolsPath, $package, $project)

# Path to the NuGetFiles\wwwroot in the NuGet package
$nugetFilesWwwroot = Join-Path $installPath "contentFiles\any\any\www"

# Path to the target project's wwwroot
$targetWwwroot = Join-Path $project "wwwroot"

# Ensure target wwwroot exists
if (!(Test-Path $targetWwwroot)) {
    New-Item -ItemType Directory -Force -Path $targetWwwroot
}

# Copy files and directories recursively
Copy-Item -Path (Join-Path $nugetFilesWwwroot "*") -Destination $targetWwwroot -Recurse -Force

# Path to the appsettings.json in the NuGet package
$nugetAppSettings = Join-Path $installPath "contentFiles\any\any\NugetFiles\appsettings.json"

# Path to the target project's appsettings.json
$targetAppSettings = Join-Path $project "appsettings.json"

# Copy and overwrite appsettings.json
Copy-Item -Path $nugetAppSettings -Destination $targetAppSettings -Force