# PowerShell script to release new NuGet packages for ZauberCMS

param(
    [string]$NewVersion
)

# Function to get current version from a .csproj file
function Get-CurrentVersion {
    param([string]$CsprojPath)

    if (!(Test-Path $CsprojPath)) {
        Write-Error "Project file not found: $CsprojPath"
        exit 1
    }

    $xml = [xml](Get-Content $CsprojPath)
    $versionNode = $xml.Project.PropertyGroup.Version
    if ($versionNode) {
        return $versionNode
    } else {
        Write-Error "Version not found in $CsprojPath"
        exit 1
    }
}

# Function to update version in a .csproj file
function Update-VersionInCsproj {
    param([string]$CsprojPath, [string]$NewVersion)

    Write-Host "Updating version in $CsprojPath to $NewVersion..."

    $content = Get-Content $CsprojPath -Raw

    # Update the Version or PackageVersion tag
    $content = $content -replace '<Version>.*?</Version>', "<Version>$NewVersion</Version>"
    $content = $content -replace '<PackageVersion>.*?</PackageVersion>', "<PackageVersion>$NewVersion</PackageVersion>"

    # Update package references to internal packages
    $projectName = [System.IO.Path]::GetFileNameWithoutExtension($CsprojPath)

    switch ($projectName) {
        "ZauberCMS.Components" {
            $content = $content -replace 'PackageReference Include="ZauberCMS\.Core" Version="[^"]*"', "PackageReference Include=`"ZauberCMS.Core`" Version=`"$NewVersion`""
        }
        "ZauberCMS.Routing" {
            $content = $content -replace 'PackageReference Include="ZauberCMS\.Components" Version="[^"]*"', "PackageReference Include=`"ZauberCMS.Components`" Version=`"$NewVersion`""
        }
        "ZauberCMS" {
            $content = $content -replace 'PackageReference Include="ZauberCMS\.Components" Version="[^"]*"', "PackageReference Include=`"ZauberCMS.Components`" Version=`"$NewVersion`""
            $content = $content -replace 'PackageReference Include="ZauberCMS\.Routing" Version="[^"]*"', "PackageReference Include=`"ZauberCMS.Routing`" Version=`"$NewVersion`""
        }
        "ZauberCMSTemplate.Site" {
            $content = $content -replace 'PackageReference Include="ZauberCMS" Version="[^"]*"', "PackageReference Include=`"ZauberCMS`" Version=`"$NewVersion`""
        }
    }

    Set-Content -Path $CsprojPath -Value $content -Encoding UTF8
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
    # Get current version from ZauberCMS.Core.csproj
    $currentVersion = Get-CurrentVersion -CsprojPath "ZauberCMS.Core\ZauberCMS.Core.csproj"
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

    # List of projects to update
    $projectsToUpdate = @(
        "ZauberCMS.Core\ZauberCMS.Core.csproj",
        "ZauberCMS.Components\ZauberCMS.Components.csproj",
        "ZauberCMS.Routing\ZauberCMS.Routing.csproj",
        "ZauberCMS\ZauberCMS.csproj",
        "ZauberCMS.Template\ZauberCMS.Template.csproj",
        "ZauberCMS.Template\template\ZauberCMSTemplate.Site\ZauberCMSTemplate.Site.csproj"
    )

    # Update versions in all projects
    foreach ($project in $projectsToUpdate) {
        Update-VersionInCsproj -CsprojPath $project -NewVersion $NewVersion
    }

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
