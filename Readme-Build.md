

```bash
set DUCKDB_VERSION=1.3.2
dotnet build DuckDB.NET.Data/Data.csproj --configuration Release /p:BuildType=Full ^
  /p:Version=%DUCKDB_VERSION% /p:FileVersion=%DUCKDB_VERSION%
```

To build locally with the correct GitVersion metadata (so the DLLs show the tag
version), either set `CI=true` before building or continue using the explicit
`/p:Version` approach above.

After the build, run `dotnet pack ... --no-build` with the same version overrides
to generate the `DuckDB.NET.Data.Full.%DUCKDB_VERSION%.nupkg`.

This is the standard flow Irion.Connector uses: once the package is packed,
follow `BuildForIrionConnector/README.md` to restore the helper console app
against the freshly produced `.nupkg`, then copy the
`"DuckDB.NET.Bindings.Full/%DUCKDB_VERSION%"` JSON object (including its
`runtimeTargets`) from `BuildForIrionConnector/bin/Debug/net8.0/BuildForIrionConnector.deps.json`
into the Irion documentation/configuration.


```bash
$env:DUCKDB_VERSION = '1.3.2'
dotnet pack DuckDB.NET.Data/Data.csproj -c Release `
  /p:BuildType=Full `
  /p:Version=$env:DUCKDB_VERSION `
  /p:FileVersion=$env:DUCKDB_VERSION `
  /p:PackageVersion=$env:DUCKDB_VERSION `
  --no-build    # only if you already ran the matching build
```