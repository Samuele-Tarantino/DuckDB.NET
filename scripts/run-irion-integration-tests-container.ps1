[CmdletBinding()]
param(
    [string]$Image = "mcr.microsoft.com/dotnet/sdk:10.0",
    [string]$Project = "Irion.DuckDB.NET.Test/Irion.DuckDB.NET.Test.csproj",
    [string]$Filter,
    [int]$MsSqlAttachCloseIterations = 10
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptDirectory "..")).Path

$dockerArgs = @(
    "run",
    "--rm",
    "-v", "${repoRoot}:/workspace",
    "-w", "/workspace",
    "-e", "IRION_DUCKDB_MSSQL_ATTACH_CLOSE_ITERATIONS=$MsSqlAttachCloseIterations",
    "-e", "TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal",
    "-v", "/var/run/docker.sock:/var/run/docker.sock",
    $Image,
    "dotnet",
    "test",
    $Project,
    "--logger", "console;verbosity=minimal"
)

if (-not [string]::IsNullOrWhiteSpace($Filter)) {
    $dockerArgs += @("--filter", $Filter)
}

Write-Host "Executing: docker $($dockerArgs -join ' ')"
& docker @dockerArgs
if ($LASTEXITCODE -ne 0) {
    throw "Containerized test run failed."
}
