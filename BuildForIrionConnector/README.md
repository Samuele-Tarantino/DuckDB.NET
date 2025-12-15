## BuildForIrionConnector

This console app exists purely to verify the locally packed `DuckDB.NET.Data.Full`
NuGet that includes the native runtimes. Follow these steps to regenerate a
package from a DuckDB tag (Irion.Connector currently targets `1.3.2`), install it
into this project, and confirm that the resulting `deps.json` carries the expected
`runtimeTargets` entries.

1. Pick the DuckDB version once per build session. Using `DUCKDB_VERSION` keeps the
   commands consistent (Windows `cmd`):


2. From the repo root, pack the Full build while overriding the version metadata:

   ```powershell
	 $env:DUCKDB_VERSION_BUILD="1.3.2"
   dotnet pack DuckDB.NET.Data/Data.csproj -c Release /p:BuildType=Full /p:Version=$env:DUCKDB_VERSION_BUILD /p:FileVersion=$env:DUCKDB_VERSION_BUILD /p:PackageVersion=$env:DUCKDB_VERSION_BUILD
   ```

   The `.nupkg` lands in `DuckDB.NET.Data/bin/Release`.

3. Restore this app against that local folder so it can pull
   `DuckDB.NET.Data.Full` `%DUCKDB_VERSION%` (and its transitive
   `DuckDB.NET.Bindings.Full`):

   ```bash
   dotnet restore BuildForIrionConnector --source ..\DuckDB.NET.Data\bin\Release
   ```

4. Build or publish the app as usual:

   ```bash
   dotnet build BuildForIrionConnector  -c Release 
   ```

5. Inspect `bin/Release/net8.0/BuildForIrionConnector.deps.json`. You should now
   see a `runtimeTargets` section under `DuckDB.NET.Bindings.Full/%DUCKDB_VERSION%`
   containing native assets for `win-x64`, `win-arm64`, `linux-x64`,
   `linux-arm64`, and `osx`. Copy that entire JSON object (everything between
   `"DuckDB.NET.Bindings.Full/%DUCKDB_VERSION%": { ... }`) into the Irion.Connector
   documentation or configuration as needed.

This workflow mirrors how the Irion connector (or any consumer) restores the
package from a feed: once the package is restored, the consuming app's `.deps`
file records the native binaries that the DuckDB connector needs to load.
