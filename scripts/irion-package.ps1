[CmdletBinding()]
param(
    [ValidateSet("show", "remote-show", "set", "bump", "build", "pack", "push", "packpush")]
    [string]$Command = "show",

    [string]$Version,

    [ValidateSet("major", "minor", "build", "revision")]
    [string]$Part = "revision",

    [string]$VersionFile = "build/irion.version",
    [string]$Configuration = "Release",
    [string]$NuGetSource = "Repository",
    [string]$ApiKey = "az",
    [string]$DataPackageId = "Irion.DuckDB.NET.Data.Full",
    [string]$BindingsPackageId = "Irion.DuckDB.NET.Bindings.Full",
    [switch]$IncludePrerelease,
    [switch]$Interactive,
    [string]$ConfigFile
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptDirectory "..")).Path

function Resolve-RepoPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    return (Join-Path $repoRoot $Path)
}

function Normalize-Version {
    param([Parameter(Mandatory = $true)][Version]$ParsedVersion)

    $major = $ParsedVersion.Major
    $minor = if ($ParsedVersion.Minor -ge 0) { $ParsedVersion.Minor } else { 0 }
    $build = if ($ParsedVersion.Build -ge 0) { $ParsedVersion.Build } else { 0 }
    $revision = if ($ParsedVersion.Revision -ge 0) { $ParsedVersion.Revision } else { 0 }

    return [Version]::new($major, $minor, $build, $revision)
}

function Parse-VersionString {
    param([Parameter(Mandatory = $true)][string]$VersionString)

    $trimmed = $VersionString.Trim()
    $parsed = $null

    if (-not [Version]::TryParse($trimmed, [ref]$parsed)) {
        throw "Invalid version '$VersionString'. Expected format like '1.4.4.1'."
    }

    return (Normalize-Version -ParsedVersion $parsed)
}

function Get-VersionFilePath {
    return (Resolve-RepoPath -Path $VersionFile)
}

function Get-CurrentVersion {
    $versionFilePath = Get-VersionFilePath

    if (-not (Test-Path $versionFilePath)) {
        throw "Version file '$versionFilePath' not found. Create it or use 'set' with -Version."
    }

    $rawValue = Get-Content -Path $versionFilePath -Raw
    return (Parse-VersionString -VersionString $rawValue).ToString(4)
}

function Save-Version {
    param([Parameter(Mandatory = $true)][string]$VersionToSave)

    $normalizedVersion = (Parse-VersionString -VersionString $VersionToSave).ToString(4)
    $versionFilePath = Get-VersionFilePath
    $parentDir = Split-Path -Parent $versionFilePath

    if (-not (Test-Path $parentDir)) {
        New-Item -Path $parentDir -ItemType Directory -Force | Out-Null
    }

    Set-Content -Path $versionFilePath -Value $normalizedVersion -NoNewline
    return $normalizedVersion
}

function Get-BumpedVersion {
    param(
        [Parameter(Mandatory = $true)][string]$CurrentVersion,
        [Parameter(Mandatory = $true)][string]$BumpPart
    )

    $version = Parse-VersionString -VersionString $CurrentVersion

    switch ($BumpPart) {
        "major" { return [Version]::new($version.Major + 1, 0, 0, 0).ToString(4) }
        "minor" { return [Version]::new($version.Major, $version.Minor + 1, 0, 0).ToString(4) }
        "build" { return [Version]::new($version.Major, $version.Minor, $version.Build + 1, 0).ToString(4) }
        "revision" { return [Version]::new($version.Major, $version.Minor, $version.Build, $version.Revision + 1).ToString(4) }
        default { throw "Unsupported bump part '$BumpPart'." }
    }
}

function Invoke-DotNet {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed: dotnet $($Arguments -join ' ')"
    }
}

function Invoke-DotNetCapture {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    $output = & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed: dotnet $($Arguments -join ' ')"
    }

    return $output
}

function Get-ProjectPaths {
    return @{
        Bindings = (Resolve-RepoPath -Path "DuckDB.NET.Bindings/Bindings.csproj")
        Data = (Resolve-RepoPath -Path "DuckDB.NET.Data/Data.csproj")
    }
}

function Set-PackageVersionEnvironment {
    param([Parameter(Mandatory = $true)][string]$PackageVersion)

    $env:DUCKDB_VERSION_BUILD = $PackageVersion
    Write-Host "DUCKDB_VERSION_BUILD=$env:DUCKDB_VERSION_BUILD"
}

function Invoke-Build {
    param([Parameter(Mandatory = $true)][string]$PackageVersion)

    $projects = Get-ProjectPaths
    Set-PackageVersionEnvironment -PackageVersion $PackageVersion

    Invoke-DotNet -Arguments @(
        "build",
        $projects.Bindings,
        "-c", $Configuration,
        "/p:BuildType=Full",
        "/p:Version=$PackageVersion",
        "/p:FileVersion=$PackageVersion",
        "/p:PackageVersion=$PackageVersion"
    )

    Invoke-DotNet -Arguments @(
        "build",
        $projects.Data,
        "-c", $Configuration,
        "/p:BuildType=Full",
        "/p:Version=$PackageVersion",
        "/p:FileVersion=$PackageVersion",
        "/p:PackageVersion=$PackageVersion"
    )
}

function Invoke-Pack {
    param([Parameter(Mandatory = $true)][string]$PackageVersion)

    $projects = Get-ProjectPaths
    Set-PackageVersionEnvironment -PackageVersion $PackageVersion

    Invoke-DotNet -Arguments @(
        "pack",
        $projects.Bindings,
        "-c", $Configuration,
        "/p:BuildType=Full",
        "/p:Version=$PackageVersion",
        "/p:FileVersion=$PackageVersion",
        "/p:PackageVersion=$PackageVersion"
    )

    Invoke-DotNet -Arguments @(
        "pack",
        $projects.Data,
        "-c", $Configuration,
        "/p:BuildType=Full",
        "/p:Version=$PackageVersion",
        "/p:FileVersion=$PackageVersion",
        "/p:PackageVersion=$PackageVersion"
    )
}

function Invoke-Push {
    param([Parameter(Mandatory = $true)][string]$PackageVersion)

    $dataPackage = Resolve-RepoPath -Path "DuckDB.NET.Data/bin/$Configuration/$DataPackageId.$PackageVersion.nupkg"
    $bindingsPackage = Resolve-RepoPath -Path "DuckDB.NET.Bindings/bin/$Configuration/$BindingsPackageId.$PackageVersion.nupkg"

    if (-not (Test-Path $dataPackage)) {
        throw "Package not found: $dataPackage. Run 'pack' first."
    }

    if (-not (Test-Path $bindingsPackage)) {
        throw "Package not found: $bindingsPackage. Run 'pack' first."
    }

    Invoke-DotNet -Arguments @("nuget", "push", "--source", $NuGetSource, "--api-key", $ApiKey, $dataPackage)
    Invoke-DotNet -Arguments @("nuget", "push", "--source", $NuGetSource, "--api-key", $ApiKey, $bindingsPackage)
}

function Get-RemoteVersions {
    param([Parameter(Mandatory = $true)][string]$PackageId)

    $args = @(
        "package",
        "search",
        $PackageId,
        "--exact-match",
        "--source", $NuGetSource,
        "--format", "json",
        "--verbosity", "minimal"
    )

    if ($IncludePrerelease) {
        $args += "--prerelease"
    }

    if ($Interactive) {
        $args += "--interactive"
    }

    if (-not [string]::IsNullOrWhiteSpace($ConfigFile)) {
        $args += @("--configfile", (Resolve-RepoPath -Path $ConfigFile))
    }

    $output = Invoke-DotNetCapture -Arguments $args
    $outputText = ($output -join [Environment]::NewLine).Trim()
    $jsonStart = $outputText.IndexOf("{")
    if ($jsonStart -lt 0) {
        throw "Unexpected output while querying '$PackageId' from '$NuGetSource'."
    }

    $jsonText = $outputText.Substring($jsonStart)
    $result = $jsonText | ConvertFrom-Json

    $searchResults = @()
    if ($null -ne $result.searchResult) {
        $searchResults = @($result.searchResult)
    }

    $versions = New-Object System.Collections.Generic.List[string]
    foreach ($searchResult in $searchResults) {
        if ($null -eq $searchResult.packages) {
            continue
        }

        foreach ($package in @($searchResult.packages)) {
            if ($package.id -eq $PackageId -and -not [string]::IsNullOrWhiteSpace($package.version)) {
                [void]$versions.Add($package.version)
            }
        }
    }

    return @($versions)
}

function Get-LatestRemoteVersion {
    param(
        [Parameter(Mandatory = $true)][string[]]$Versions,
        [Parameter(Mandatory = $true)][string]$PackageId
    )

    if ($Versions.Count -eq 0) {
        return $null
    }

    $parsedVersions = New-Object System.Collections.Generic.List[Version]
    foreach ($version in $Versions) {
        try {
            [void]$parsedVersions.Add((Parse-VersionString -VersionString $version))
        }
        catch {
            Write-Warning "Skipping non-numeric version '$version' for package '$PackageId'."
        }
    }

    if ($parsedVersions.Count -eq 0) {
        return $null
    }

    $latest = $parsedVersions | Sort-Object -Descending | Select-Object -First 1
    return $latest.ToString(4)
}

function Write-RemoteComparison {
    param(
        [Parameter(Mandatory = $true)][string]$PackageId,
        [string]$LocalVersion,
        [string]$RemoteVersion
    )

    if ([string]::IsNullOrWhiteSpace($RemoteVersion)) {
        Write-Host "$PackageId latest remote: <not found>"
        return
    }

    Write-Host "$PackageId latest remote: $RemoteVersion"
    if ([string]::IsNullOrWhiteSpace($LocalVersion)) {
        return
    }

    $local = Parse-VersionString -VersionString $LocalVersion
    $remote = Parse-VersionString -VersionString $RemoteVersion

    if ($local -gt $remote) {
        Write-Host "$PackageId local ($LocalVersion) is ahead of remote."
    }
    elseif ($local -lt $remote) {
        Write-Host "$PackageId local ($LocalVersion) is behind remote."
    }
    else {
        Write-Host "$PackageId local matches remote ($LocalVersion)."
    }
}

function Invoke-RemoteShow {
    $localVersion = $null
    try {
        $localVersion = Get-CurrentVersion
    }
    catch {
        # If the local version file is missing, still show remote feed information.
    }

    Write-Host "Source: $NuGetSource"
    if ([string]::IsNullOrWhiteSpace($localVersion)) {
        Write-Host "Local version ($VersionFile): <not available>"
    }
    else {
        Write-Host "Local version ($VersionFile): $localVersion"
    }

    $dataVersions = Get-RemoteVersions -PackageId $DataPackageId
    $bindingsVersions = Get-RemoteVersions -PackageId $BindingsPackageId

    $dataLatest = Get-LatestRemoteVersion -Versions $dataVersions -PackageId $DataPackageId
    $bindingsLatest = Get-LatestRemoteVersion -Versions $bindingsVersions -PackageId $BindingsPackageId

    Write-RemoteComparison -PackageId $DataPackageId -LocalVersion $localVersion -RemoteVersion $dataLatest
    Write-RemoteComparison -PackageId $BindingsPackageId -LocalVersion $localVersion -RemoteVersion $bindingsLatest

    if (-not [string]::IsNullOrWhiteSpace($dataLatest) -and -not [string]::IsNullOrWhiteSpace($bindingsLatest) -and $dataLatest -ne $bindingsLatest) {
        Write-Warning "Remote latest versions differ between '$DataPackageId' and '$BindingsPackageId'."
    }
}

switch ($Command) {
    "show" {
        $currentVersion = Get-CurrentVersion
        Write-Host $currentVersion
    }

    "remote-show" {
        Invoke-RemoteShow
    }

    "set" {
        if ([string]::IsNullOrWhiteSpace($Version)) {
            throw "Use -Version with the 'set' command."
        }

        $newVersion = Save-Version -VersionToSave $Version
        Write-Host "Version set to $newVersion"
    }

    "bump" {
        $currentVersion = Get-CurrentVersion
        $nextVersion = Get-BumpedVersion -CurrentVersion $currentVersion -BumpPart $Part
        $savedVersion = Save-Version -VersionToSave $nextVersion
        Write-Host "Version bumped: $currentVersion -> $savedVersion"
    }

    "build" {
        $currentVersion = Get-CurrentVersion
        Invoke-Build -PackageVersion $currentVersion
        Write-Host "Build completed for version $currentVersion"
    }

    "pack" {
        $currentVersion = Get-CurrentVersion
        Invoke-Pack -PackageVersion $currentVersion
        Write-Host "Pack completed for version $currentVersion"
    }

    "push" {
        $currentVersion = Get-CurrentVersion
        Invoke-Push -PackageVersion $currentVersion
        Write-Host "Push completed for version $currentVersion"
    }

    "packpush" {
        $currentVersion = Get-CurrentVersion
        Invoke-Pack -PackageVersion $currentVersion
        Invoke-Push -PackageVersion $currentVersion
        Write-Host "Pack and push completed for version $currentVersion"
    }

    default {
        throw "Unsupported command '$Command'."
    }
}
