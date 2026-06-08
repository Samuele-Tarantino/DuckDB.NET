[CmdletBinding()]
param(
    [ValidateSet("show", "remote-show", "set", "bump", "build", "pack", "push", "packpush")]
    [string]$Command = "show",

    [string]$Version,

    [ValidateSet("major", "minor", "build", "revision", "prerelease")]
    [string]$Part = "revision",

    [Alias('p')]
    [string[]]$MsBuildProperty,

    [string]$VersionFile = "build/irion.version",
    [string]$Configuration = "Release",
    [string]$NuGetSource = "Repository",
    [string]$ApiKey = "az",
    [string]$DataPackageId = "Irion.DuckDB.NET.Data.Full",
    [string]$BindingsPackageId = "Irion.DuckDB.NET.Bindings.Full",
    [string]$GitRemote = "origin",
    [string]$TagPrefix = "v",
    [switch]$SkipTag,
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

    # Allow SemVer-style prerelease or build metadata (e.g. 1.5.2-alpha.1 or 1.5.2+meta)
    # by extracting the numeric core before any '-' (prerelease) or '+' (build metadata).
    $numericPart = ($trimmed -split '[-+]')[0]

    if (-not [Version]::TryParse($numericPart, [ref]$parsed)) {
        throw "Invalid version '$VersionString'. Expected numeric format like '1.4.4.1' or semver with prerelease like '1.4.4-alpha.1'."
    }

    return (Normalize-Version -ParsedVersion $parsed)
}

function Split-SemVersion {
    param([Parameter(Mandatory = $true)][string]$VersionString)

    $raw = $VersionString.Trim()
    $sepIndex = -1
    for ($i = 0; $i -lt $raw.Length; $i++) {
        if ($raw[$i] -eq '-' -or $raw[$i] -eq '+') { $sepIndex = $i; break }
    }

    if ($sepIndex -ge 0) {
        $numericPart = $raw.Substring(0, $sepIndex)
        $suffix = $raw.Substring($sepIndex) # includes leading '-' or '+'
    }
    else {
        $numericPart = $raw
        $suffix = ''
    }

    $parsed = $null
    if (-not [Version]::TryParse($numericPart, [ref]$parsed)) {
        throw "Invalid version '$VersionString'. Expected numeric format like '1.4.4.1' or semver with prerelease like '1.4.4-alpha.1'."
    }

    return [PSCustomObject]@{
        Raw = $raw
        Numeric = (Normalize-Version -ParsedVersion $parsed)
        Suffix = $suffix
    }
}

function Get-VersionFilePath {
    return (Resolve-RepoPath -Path $VersionFile)
}

function Get-NuGetPackageVersion {
    param([Parameter(Mandatory = $true)][string]$VersionString)

    $parts = Split-SemVersion -VersionString $VersionString
    $version = $parts.Numeric
    $suffix = $parts.Suffix

    # NuGet supports prerelease suffixes (with '-') but not build metadata ('+' section),
    # so drop '+'-prefixed suffixes and keep '-'-prefixed prerelease identifiers.
    if ($suffix.StartsWith('+')) { $suffix = '' }

    if ($version.Revision -eq 0) {
        return ($version.ToString(3) + $suffix)
    }

    return ($version.ToString(4) + $suffix)
}

function Get-CurrentVersion {
    $versionFilePath = Get-VersionFilePath

    if (-not (Test-Path $versionFilePath)) {
        throw "Version file '$versionFilePath' not found. Create it or use 'set' with -Version."
    }

    $rawValue = Get-Content -Path $versionFilePath -Raw
    return $rawValue.Trim()
}

function Save-Version {
    param([Parameter(Mandatory = $true)][string]$VersionToSave)

    $trimmed = $VersionToSave.Trim()
    # Validate semver but preserve the original string (including prerelease) in file
    [void](Split-SemVersion -VersionString $trimmed)

    $versionFilePath = Get-VersionFilePath
    $parentDir = Split-Path -Parent $versionFilePath

    if (-not (Test-Path $parentDir)) {
        New-Item -Path $parentDir -ItemType Directory -Force | Out-Null
    }

    Set-Content -Path $versionFilePath -Value $trimmed -NoNewline
    return $trimmed
}

function Get-BumpedVersion {
    param(
        [Parameter(Mandatory = $true)][string]$CurrentVersion,
        [Parameter(Mandatory = $true)][string]$BumpPart
    )

    $parts = Split-SemVersion -VersionString $CurrentVersion
    $version = $parts.Numeric
    $suffix = $parts.Suffix

    switch ($BumpPart) {
        "major" { return [Version]::new($version.Major + 1, 0, 0, 0).ToString(4) }
        "minor" { return [Version]::new($version.Major, $version.Minor + 1, 0, 0).ToString(4) }
        "build" { return [Version]::new($version.Major, $version.Minor, $version.Build + 1, 0).ToString(4) }
        "revision" { return [Version]::new($version.Major, $version.Minor, $version.Build, $version.Revision + 1).ToString(4) }
        "prerelease" {
            if ([string]::IsNullOrEmpty($suffix) -or $suffix.StartsWith('+')) {
                throw "No prerelease suffix to bump for version '$CurrentVersion'."
            }

            # strip leading '-'
            $s = $suffix.Substring(1)
            $lastDot = $s.LastIndexOf('.')
            if ($lastDot -lt 0) {
                # no numeric tail, append .1
                $newSuffix = "$s.1"
            }
            else {
                $label = $s.Substring(0, $lastDot)
                $tail = $s.Substring($lastDot + 1)
                if ([int]::TryParse($tail, [ref]$null)) {
                    $num = [int]$tail
                    $newSuffix = "$label.$($num + 1)"
                }
                else {
                    # tail is not numeric; append .1
                    $newSuffix = "$s.1"
                }
            }

            return ($version.ToString(4) + '-' + $newSuffix)
        }
        default { throw "Unsupported bump part '$BumpPart'." }
    }
}

function Format-CommandArgument {
    param([Parameter(Mandatory = $true)][string]$Argument)

    if ($Argument -notmatch '[\s"]') {
        return $Argument
    }

    return '"' + ($Argument -replace '"', '\"') + '"'
}

function Write-CommandInvocation {
    param(
        [Parameter(Mandatory = $true)][string]$Executable,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    $formattedArguments = @($Arguments | ForEach-Object { Format-CommandArgument -Argument $_ })
    $commandLine = (@($Executable) + $formattedArguments) -join ' '
    Write-Host "Executing: $commandLine"
}

function Invoke-DotNet {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    Write-CommandInvocation -Executable "dotnet" -Arguments $Arguments
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed: dotnet $($Arguments -join ' ')"
    }
}

function Invoke-DotNetCapture {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    Write-CommandInvocation -Executable "dotnet" -Arguments $Arguments
    $output = & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed: dotnet $($Arguments -join ' ')"
    }

    return $output
}

function Invoke-Git {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    Write-CommandInvocation -Executable "git" -Arguments $Arguments
    & git -C $repoRoot @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed: git $($Arguments -join ' ')"
    }
}

function Invoke-GitCapture {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    Write-CommandInvocation -Executable "git" -Arguments $Arguments
    $output = & git -C $repoRoot @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed: git $($Arguments -join ' ')"
    }

    return $output
}

function Test-GitRefExists {
    param([Parameter(Mandatory = $true)][string]$Ref)

    & git -C $repoRoot rev-parse --verify --quiet $Ref *> $null
    return ($LASTEXITCODE -eq 0)
}

function Get-GitRefCommit {
    param([Parameter(Mandatory = $true)][string]$Ref)

    $output = Invoke-GitCapture -Arguments @("rev-list", "-n", "1", $Ref)
    return (($output -join [Environment]::NewLine).Trim())
}

function Invoke-TagAndPush {
    param([Parameter(Mandatory = $true)][string]$PackageVersion)

    $tagName = "$TagPrefix$PackageVersion"
    $headCommit = Get-GitRefCommit -Ref "HEAD"

    if (Test-GitRefExists -Ref "refs/tags/$tagName") {
        $tagCommit = Get-GitRefCommit -Ref "refs/tags/$tagName"
        if ($tagCommit -ne $headCommit) {
            throw "Tag '$tagName' already exists locally at $tagCommit, but HEAD is $headCommit."
        }

        Write-Host "Tag '$tagName' already exists locally at HEAD."
    }
    else {
        Invoke-Git -Arguments @("tag", $tagName)
    }

    Invoke-Git -Arguments @("push", $GitRemote, "refs/tags/$tagName")
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

function Get-MsBuildPropertyArguments {
    if ($null -eq $MsBuildProperty -or $MsBuildProperty.Count -eq 0) {
        return @()
    }

    $args = New-Object System.Collections.Generic.List[string]
    foreach ($property in $MsBuildProperty) {
        $trimmed = $property.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed)) {
            continue
        }

        if ($trimmed.StartsWith("/p:") -or $trimmed.StartsWith("-p:")) {
            [void]$args.Add($trimmed)
        }
        else {
            [void]$args.Add("/p:$trimmed")
        }
    }

    return @($args)
}

function Invoke-Clean {
    param([Parameter(Mandatory = $true)][string]$PackageVersion)

    $projects = Get-ProjectPaths
    $msBuildPropertyArgs = Get-MsBuildPropertyArguments
    Set-PackageVersionEnvironment -PackageVersion $PackageVersion

    $bindingsArgs = @(
        "clean",
        $projects.Bindings,
        "-c", $Configuration,
        "/p:BuildType=Full"
    )
    $bindingsArgs += $msBuildPropertyArgs
    Invoke-DotNet -Arguments $bindingsArgs

    $dataArgs = @(
        "clean",
        $projects.Data,
        "-c", $Configuration,
        "/p:BuildType=Full"
    )
    $dataArgs += $msBuildPropertyArgs
    Invoke-DotNet -Arguments $dataArgs
}

function Invoke-Build {
    param([Parameter(Mandatory = $true)][string]$PackageVersion)

    $projects = Get-ProjectPaths
    $msBuildPropertyArgs = Get-MsBuildPropertyArguments
    $versionParts = Split-SemVersion -VersionString $PackageVersion
    $assemblyVersion = $versionParts.Numeric.ToString(4)
    $nuGetPackageVersion = Get-NuGetPackageVersion -VersionString $PackageVersion
    Set-PackageVersionEnvironment -PackageVersion $PackageVersion
    Write-Host "AssemblyVersion=$assemblyVersion"
    Write-Host "NuGetPackageVersion=$nuGetPackageVersion"

    $bindingsArgs = @(
        "build",
        $projects.Bindings,
        "-c", $Configuration,
        "/p:BuildType=Full",
        "/p:Version=$assemblyVersion",
        "/p:FileVersion=$assemblyVersion",
        "/p:InformationalVersion=$PackageVersion",
        "/p:PackageVersion=$nuGetPackageVersion"
    )
    $bindingsArgs += $msBuildPropertyArgs
    Invoke-DotNet -Arguments $bindingsArgs

    $dataArgs = @(
        "build",
        $projects.Data,
        "-c", $Configuration,
        "/p:BuildType=Full",
        "/p:Version=$assemblyVersion",
        "/p:FileVersion=$assemblyVersion",
        "/p:InformationalVersion=$PackageVersion",
        "/p:PackageVersion=$nuGetPackageVersion"
    )
    $dataArgs += $msBuildPropertyArgs
    Invoke-DotNet -Arguments $dataArgs
}

function Invoke-Pack {
    param([Parameter(Mandatory = $true)][string]$PackageVersion)

    $projects = Get-ProjectPaths
    $msBuildPropertyArgs = Get-MsBuildPropertyArguments
    $versionParts = Split-SemVersion -VersionString $PackageVersion
    $assemblyVersion = $versionParts.Numeric.ToString(4)
    $nuGetPackageVersion = Get-NuGetPackageVersion -VersionString $PackageVersion
    Set-PackageVersionEnvironment -PackageVersion $PackageVersion
    Write-Host "AssemblyVersion=$assemblyVersion"
    Write-Host "NuGetPackageVersion=$nuGetPackageVersion"

    $bindingsArgs = @(
        "pack",
        $projects.Bindings,
        "-c", $Configuration,
        "/p:BuildType=Full",
        "/p:Version=$assemblyVersion",
        "/p:FileVersion=$assemblyVersion",
        "/p:InformationalVersion=$PackageVersion",
        "/p:PackageVersion=$nuGetPackageVersion"
    )
    $bindingsArgs += $msBuildPropertyArgs
    Invoke-DotNet -Arguments $bindingsArgs

    $dataArgs = @(
        "pack",
        $projects.Data,
        "-c", $Configuration,
        "/p:BuildType=Full",
        "/p:Version=$assemblyVersion",
        "/p:FileVersion=$assemblyVersion",
        "/p:InformationalVersion=$PackageVersion",
        "/p:PackageVersion=$nuGetPackageVersion"
    )
    $dataArgs += $msBuildPropertyArgs
    Invoke-DotNet -Arguments $dataArgs
}

function Invoke-Push {
    param([Parameter(Mandatory = $true)][string]$PackageVersion)

    $nuGetPackageVersion = Get-NuGetPackageVersion -VersionString $PackageVersion
    Write-Host "NuGetPackageVersion=$nuGetPackageVersion"

    $dataPackage = Resolve-RepoPath -Path "DuckDB.NET.Data/bin/$Configuration/$DataPackageId.$nuGetPackageVersion.nupkg"
    $bindingsPackage = Resolve-RepoPath -Path "DuckDB.NET.Bindings/bin/$Configuration/$BindingsPackageId.$nuGetPackageVersion.nupkg"

    if (-not (Test-Path $dataPackage)) {
        throw "Package not found: $dataPackage. Run 'pack' first."
    }

    if (-not (Test-Path $bindingsPackage)) {
        throw "Package not found: $bindingsPackage. Run 'pack' first."
    }

    Invoke-DotNet -Arguments @("nuget", "push", "--source", $NuGetSource, "--api-key", $ApiKey, $dataPackage)
    Invoke-DotNet -Arguments @("nuget", "push", "--source", $NuGetSource, "--api-key", $ApiKey, $bindingsPackage)

    if (-not $SkipTag) {
        Invoke-TagAndPush -PackageVersion $PackageVersion
    }
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
        Write-Host "Local NuGet package version: $(Get-NuGetPackageVersion -VersionString $localVersion)"
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
        Invoke-Clean -PackageVersion $currentVersion
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
