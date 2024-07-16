# Path to the directory of the target project
$projectDir = $installPath

# Path to the source files in the NuGet package
$sourceDir = Join-Path $PSScriptRoot '..\content'

# Copy appsettings.json to the target project
Copy-Item -Path "$sourceDir\appsettings.json" -Destination $projectDir -Force

# Copy wwwroot folder contents to the target project's wwwroot folder
$wwwrootDest = Join-Path $projectDir 'wwwroot'
if (-Not (Test-Path -Path $wwwrootDest)) {
    New-Item -ItemType Directory -Path $wwwrootDest
}
Copy-Item -Path "$sourceDir\wwwroot\*" -Destination $wwwrootDest -Recurse -Force

Write-Host "appsettings.json and wwwroot contents have been copied to $projectDir"