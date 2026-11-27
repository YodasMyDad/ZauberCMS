# PowerShell script to release new NuGet packages for ZauberCMS

param(
    [string]$NewVersion
)

# Function to get the current version: <Version> in Directory.Build.props (shared by the four packages)
function Get-CurrentVersion {
    param([string]$PropsPath)

    if (!(Test-Path $PropsPath)) {
        Write-Error "File not found: $PropsPath"
        exit 1
    }

    $xml = [xml](Get-Content $PropsPath -Raw)
    $versionNode = $xml.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ } | Select-Object -First 1
    if ($versionNode) {
        return $versionNode
    } else {
        Write-Error "Version not found in $PropsPath"
        exit 1
    }
}

# Function to set the version in one file. The file is written back byte for byte except for the replaced
# versions: same encoding (with or without BOM) and no extra line at the end (Set-Content added one per run).
function Update-VersionInFile {
    param([string]$Path, [string]$Pattern, [string]$Replacement)

    Write-Host "Updating version in $Path..."

    $fullPath = (Resolve-Path $Path).Path
    $bytes = [System.IO.File]::ReadAllBytes($fullPath)
    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $content = [System.IO.File]::ReadAllText($fullPath)

    $updated = [regex]::Replace($content, $Pattern, $Replacement)
    if ($updated -eq $content -and -not ($content -match [regex]::Escape($Replacement))) {
        Write-Error "No version found to update in $Path (pattern: $Pattern)"
        exit 1
    }

    [System.IO.File]::WriteAllText($fullPath, $updated, (New-Object System.Text.UTF8Encoding($hasBom)))
}

# Function to run dotnet build
function Invoke-ReleaseBuild {
    Write-Host "Running dotnet build ZauberCMS.sln -c Release..."
    dotnet build ZauberCMS.sln -c Release

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed with exit code $LASTEXITCODE"
        exit 1
    }

    Write-Host "Build completed successfully!"
}

# Function to pack the template
function Invoke-TemplatePack {
    param([string]$Version)
    
    Write-Host "Packing ZauberCMS.Template..."
    
    Push-Location "ZauberCMS.Template"
    
    try {
        dotnet pack -c Release
        
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Template pack failed with exit code $LASTEXITCODE"
            exit 1
        }
        
        Write-Host "Template packed successfully!"
        
        # Move only the package matching the current version to NugetSource folder
        $packageFileName = "ZauberCMS.Template.$Version.nupkg"
        $packagePath = Join-Path "bin\Release" $packageFileName
        
        if (!(Test-Path $packagePath)) {
            Write-Error "Expected package file not found: $packagePath"
            exit 1
        }
        
        $nugetSourcePath = "..\NugetSource"
        
        if (!(Test-Path $nugetSourcePath)) {
            Write-Host "Creating NugetSource folder..."
            New-Item -Path $nugetSourcePath -ItemType Directory | Out-Null
        }
        
        $destination = Join-Path $nugetSourcePath $packageFileName
        Write-Host "Moving $packageFileName to NugetSource folder..."
        Move-Item -Path $packagePath -Destination $destination -Force
        
        Write-Host "Template package moved to NugetSource folder."
    }
    finally {
        Pop-Location
    }
}

# Main script logic
try {
    # Get the current version from Directory.Build.props
    $currentVersion = Get-CurrentVersion -PropsPath "Directory.Build.props"
    Write-Host "Current version: $currentVersion"

    # Prompt for new version if not provided
    if (-not $NewVersion) {
        $NewVersion = Read-Host "Enter new version (e.g., 4.0.0-beta5)"
    }

    if ([string]::IsNullOrWhiteSpace($NewVersion)) {
        Write-Error "Version cannot be empty."
        exit 1
    }

    # Validate version format (basic check)
    if ($NewVersion -notmatch '^\d+\.\d+\.\d+(-.*)?$') {
        Write-Warning "Version '$NewVersion' doesn't match expected format (x.y.z or x.y.z-suffix). Proceeding anyway..."
    }

    # Confirm version change
    $confirmation = Read-Host "Update version from '$currentVersion' to '$NewVersion'? (y/N)"
    if ($confirmation -ne 'y' -and $confirmation -ne 'Y') {
        Write-Host "Release cancelled."
        exit 0
    }

    # The four packages share <Version> in Directory.Build.props and depend on each other through ProjectReference
    # (the packed dependencies get the same version). The template's two csprojs keep their own copies, because
    # the template site is copied to consumers by `dotnet new`.
    Update-VersionInFile -Path "Directory.Build.props" `
        -Pattern '<Version>[^<]*</Version>' -Replacement "<Version>$NewVersion</Version>"
    Update-VersionInFile -Path "ZauberCMS.Template\ZauberCMS.Template.csproj" `
        -Pattern '<PackageVersion>[^<]*</PackageVersion>' -Replacement "<PackageVersion>$NewVersion</PackageVersion>"
    Update-VersionInFile -Path "ZauberCMS.Template\template\ZauberCMSTemplate.Site\ZauberCMSTemplate.Site.csproj" `
        -Pattern 'PackageReference Include="ZauberCMS" Version="[^"]*"' -Replacement "PackageReference Include=`"ZauberCMS`" Version=`"$NewVersion`""

    Write-Host "All project versions updated to $NewVersion"

    # Run the release build
    Invoke-ReleaseBuild

    # Pack and move the template
    Invoke-TemplatePack -Version $NewVersion

    Write-Host "Release completed successfully! NuGet packages generated in NugetSource folder."
}
catch {
    Write-Error "An error occurred: $_"
    exit 1
}
