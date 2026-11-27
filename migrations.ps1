# PowerShell

param(
    [string]$ProjectPath = "ZauberCMS.Core",
    [string]$StartupProject = "ZauberCMS.Core",
    [string]$MigrationName
)

# Ensure dotnet-ef is available
Write-Host "Checking dotnet-ef tool..."
dotnet tool update --global dotnet-ef | Out-Null

# Prompt for migration name
if (-not $MigrationName) {
    $MigrationName = Read-Host "Enter migration name (e.g., AddUsersTable)"
}

if ([string]::IsNullOrWhiteSpace($MigrationName)) {
    Write-Error "Migration name cannot be empty."
    exit 1
}

# Helper to run EF migration command
function New-Migration {
    param(
        [Parameter(Mandatory=$true)][string]$Context,
        [Parameter(Mandatory=$true)][string]$OutputDir,
        [Parameter(Mandatory=$true)][string]$ProviderName
    )

    Write-Host "Creating $ProviderName migration '$MigrationName'..."
    $cmd = @(
        "ef", "migrations", "add", $MigrationName,
        "--context", $Context,
        "-o", $OutputDir,
        "--project", $ProjectPath,
        "--startup-project", $StartupProject
    )
    dotnet $cmd
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to create $ProviderName migration."
    }

    # EF generates the migration class public; ZauberCMS's migrations are internal (ZA-092, Docs/Data.md), and EF
    # finds internal ones. A test (PublicApiTests) fails on a public migration.
    $folder = Join-Path $ProjectPath $OutputDir
    Get-ChildItem -Path $folder -Filter "*_$MigrationName.cs" | ForEach-Object {
        $text = [System.IO.File]::ReadAllText($_.FullName)
        $fixed = $text -replace 'public partial class (\w+) : Migration', 'internal partial class $1 : Migration'
        if ($fixed -ne $text) {
            [System.IO.File]::WriteAllText($_.FullName, $fixed, [System.Text.UTF8Encoding]::new($true))
        }
    }
}

try {
    # SQL Server
    New-Migration -Context "ZauberDbContext" -OutputDir "Data/Migrations/SqlServer" -ProviderName "SQL Server"

    # SQLite
    New-Migration -Context "SqliteZauberDbContext" -OutputDir "Data/Migrations/SqLite" -ProviderName "SQLite"

    # PostgreSQL
    New-Migration -Context "PostgreSqlZauberDbContext" -OutputDir "Data/Migrations/PostgreSql" -ProviderName "PostgreSQL"

    Write-Host "All migrations created successfully."
}
catch {
    Write-Error $_
    exit 1
}