# DuckDB.NET

[DuckDB](https://duckdb.org/) bindings for C#

[![GitHub Workflow Status](https://img.shields.io/github/actions/workflow/status/Giorgi/DuckDB.NET/ci.yml?branch=main&logo=GitHub&style=for-the-badge)](https://github.com/Giorgi/DuckDB.NET/actions/workflows/ci.yml)
[![Coveralls](https://img.shields.io/coveralls/github/Giorgi/DuckDB.NET?logo=coveralls&style=for-the-badge)](https://coveralls.io/github/Giorgi/DuckDB.NET)
[![License](https://img.shields.io/badge/License-Mit-blue.svg?style=for-the-badge&logo=mit)](LICENSE.md)
[![Ko-Fi](https://img.shields.io/static/v1?style=for-the-badge&message=Support%20the%20Project&color=success&logo=ko-fi&label=$$)](https://ko-fi.com/U6U81LHU8)
[![Discord](https://img.shields.io/badge/DuckDB-.Net-%23FFF000?logo=DuckDB&style=for-the-badge)](https://discord.com/channels/909674491309850675/1051088721996427265)

[![NuGet DuckDB.NET.Data](https://img.shields.io/nuget/dt/DuckDB.NET.Data.svg?label=DuckDB.NET.Data&style=for-the-badge&logo=NuGet)](https://www.nuget.org/packages/DuckDB.NET.Data/)
[![NuGet DuckDB.NET.Bindings](https://img.shields.io/nuget/dt/DuckDB.NET.Bindings.svg?label=DuckDB.NET.Bindings&style=for-the-badge&logo=NuGet)](https://www.nuget.org/packages/DuckDB.NET.Bindings/)

[![NuGet DuckDB.NET.Data.Full](https://img.shields.io/nuget/dt/DuckDB.NET.Data.Full.svg?label=DuckDB.NET.Data.Full&style=for-the-badge&logo=NuGet)](https://www.nuget.org/packages/DuckDB.NET.Data.Full/)
[![NuGet DuckDB.NET.Bindings.Full](https://img.shields.io/nuget/dt/DuckDB.NET.Bindings.Full.svg?label=DuckDB.NET.Bindings.Full&style=for-the-badge&logo=NuGet)](https://www.nuget.org/packages/DuckDB.NET.Bindings.Full/)

![Project Icon](https://raw.githubusercontent.com/Giorgi/DuckDB.NET/main/Logo.jpg "DuckDB.NET Project Icon")

## Usage

```sh
dotnet add package DuckDB.NET.Data.Full
```

```cs
using (var duckDBConnection = new DuckDBConnection("Data Source=file.db"))
{
  duckDBConnection.Open();

  using var command = duckDBConnection.CreateCommand();

  command.CommandText = "CREATE TABLE integers(foo INTEGER, bar INTEGER);";
  var executeNonQuery = command.ExecuteNonQuery();

  command.CommandText = "INSERT INTO integers VALUES (3, 4), (5, 6), (7, 8);";
  executeNonQuery = command.ExecuteNonQuery();

  command.CommandText = "Select count(*) from integers";
  var executeScalar = command.ExecuteScalar();

  command.CommandText = "SELECT foo, bar FROM integers";
  var reader = command.ExecuteReader();

  PrintQueryResults(reader);
}

private static void PrintQueryResults(DbDataReader queryResult)
{
  for (var index = 0; index < queryResult.FieldCount; index++)
  {
    var column = queryResult.GetName(index);
    Console.Write($"{column} ");
  }

  Console.WriteLine();

  while (queryResult.Read())
  {
    for (int ordinal = 0; ordinal < queryResult.FieldCount; ordinal++)
    {
      var val = queryResult.GetInt32(ordinal);
      Console.Write(val);
      Console.Write(" ");
    }

    Console.WriteLine();
  }
}
```

## DuckDB native runtime

The native DuckDB library is not downloaded from the official release in this fork: it comes from the NuGet package
`Irion.DuckDb.Native.Bridge` (DuckDB with the `bridge` extension), referenced by `DuckDB.NET.Bindings`. It provides the
`win-x64` and `linux-x64` runtimes. See [Native library (DuckDB with the bridge extension)](#native-library-duckdb-with-the-bridge-extension).
The DuckDB version of the native library is the one of that package (the Data/Bindings sources follow DuckDB 2.0.1).

## Tests

Run the library test suite from the repository root:

```powershell
dotnet test .\DuckDB.NET.Test\Test.csproj /p:BuildType=Full
```

### Irion Package Build

Use this workflow to build and publish Irion packages. The native DuckDB library is **not** downloaded or bundled
by this repository any more: it comes from the NuGet package `Irion.DuckDb.Native.Bridge` (see
[Native library (DuckDB with the bridge extension)](#native-library-duckdb-with-the-bridge-extension)).
The standard flow uses:

- `build/irion.version` as the single source of truth for the package version
- `scripts/irion-package.ps1` to show/set/bump, check remote, build, pack and push

#### 1. Prerequisites

1. The `Repository` feed in `nuget.config` must be reachable (it hosts `Irion.DuckDb.Native.Bridge`).
2. The version of the native package is set in `DuckDB.NET.Bindings/Bindings.csproj` (`PackageReference` to `Irion.DuckDb.Native.Bridge`).

#### 2. Version management (recommended)

Current version:

```powershell
.\scripts\irion-package.ps1 -Command show
```

Set explicit version:

```powershell
.\scripts\irion-package.ps1 -Command set -Version 1.5.2.2
```

Bump version in `build/irion.version`:

```powershell
.\scripts\irion-package.ps1 -Command bump -Part prerelease # 1.4.4.0-alpha.1 -> 1.4.4.0-alpha.2
# .\scripts\irion-package.ps1 -Command bump -Part revision  # 1.4.4.1 -> 1.4.4.2
# .\scripts\irion-package.ps1 -Command bump -Part build     # 1.4.4.1 -> 1.4.5.0
# .\scripts\irion-package.ps1 -Command bump -Part minor     # 1.4.4.1 -> 1.5.0.0
# .\scripts\irion-package.ps1 -Command bump -Part major     # 1.4.4.1 -> 2.0.0.0
```

Check latest published versions on the configured feed:

```powershell
.\scripts\irion-package.ps1 -Command remote-show -NuGetSource "Repository"
```

You can override package IDs if needed:

```powershell
.\scripts\irion-package.ps1 -Command remote-show -NuGetSource "Repository" -DataPackageId "Irion.DuckDB.NET.Data.Full" -BindingsPackageId "Irion.DuckDB.NET.Bindings.Full"
```

#### 3. Build (Full)

```powershell
.\scripts\irion-package.ps1 -Command build
```

The build command runs a full `dotnet clean` for both Irion projects before building.

#### 4. Pack Irion NuGet packages

```powershell
.\scripts\irion-package.ps1 -Command pack
```

The script reads `build/irion.version`, sets `DUCKDB_VERSION_BUILD` internally, and uses that value for:

- `/p:Version`
- `/p:FileVersion`

NuGet package versions are normalized when the fourth segment is zero, so `1.5.0.0` becomes package version `1.5.0`.
The script handles that automatically when it sets `/p:PackageVersion` and when it resolves the `.nupkg` filename for `push`.

Generated packages:

- `DuckDB.NET.Bindings/bin/Release/Irion.DuckDB.NET.Bindings.Full.<nuget-version>.nupkg`
- `DuckDB.NET.Data/bin/Release/Irion.DuckDB.NET.Data.Full.<nuget-version>.nupkg`

if `-PackageReleaseNotes` or `-PackageReleaseNotesFile` are provided, the content is included in the generated `.nupkg` metadata and visible on NuGet.org.

```powershell
.\scripts\irion-package.ps1 -Command pack -PackageReleaseNotes "New feature short description" -PackageReleaseNotesFile "RELEASE-NOTE.md"
```


#### 5. Push packages to feed

```powershell
.\scripts\irion-package.ps1 -Command push -NuGetSource "Repository" -ApiKey "az"
```

After both packages are pushed successfully, the script creates a Git tag from `build/irion.version`
using the default format `v<version>` and pushes it to `origin`.
For example, `1.5.0.1` becomes `v1.5.0.1`.

To skip tagging during a retry:

```powershell
.\scripts\irion-package.ps1 -Command push -NuGetSource "Repository" -ApiKey "az" -SkipTag
```

#### 6. One-shot pack + push

```powershell
.\scripts\irion-package.ps1 -Command packpush -NuGetSource "Repository" -ApiKey "az"
```

#### 7. Manual equivalent (for CI or troubleshooting)

```powershell
$buildVersion = (Get-Content .\build\irion.version -Raw).Trim()
$parsedVersion = [Version]$buildVersion
$packageVersion = if ($parsedVersion.Revision -eq 0) { $parsedVersion.ToString(3) } else { $parsedVersion.ToString(4) }

$env:DUCKDB_VERSION_BUILD = $buildVersion
dotnet pack DuckDB.NET.Bindings/Bindings.csproj -c Release /p:BuildType=Full /p:Version=$buildVersion /p:FileVersion=$buildVersion /p:PackageVersion=$packageVersion
dotnet pack DuckDB.NET.Data/Data.csproj -c Release /p:BuildType=Full /p:Version=$buildVersion /p:FileVersion=$buildVersion /p:PackageVersion=$packageVersion
dotnet nuget push --source "Repository" --api-key az .\DuckDB.NET.Data\bin\Release\Irion.DuckDB.NET.Data.Full.$packageVersion.nupkg
dotnet nuget push --source "Repository" --api-key az .\DuckDB.NET.Bindings\bin\Release\Irion.DuckDB.NET.Bindings.Full.$packageVersion.nupkg
git tag "v$buildVersion"
git push origin "refs/tags/v$buildVersion"
```

Quick checklist before publishing:

1. `Irion.DuckDb.Native.Bridge` in `Bindings.csproj` points to the intended native package version.
2. `build/irion.version` contains the final release version.
3. Both `dotnet pack` commands completed successfully.
4. Both packages were pushed to the target feed.

### Native library (DuckDB with the bridge extension)

Until 2.0.0.2 the native DuckDB library was downloaded from the official DuckDB release (`DuckDbArtifactRoot`) by the
`DownloadNativeLibs` target and copied from `DuckDB.NET.Bindings/obj/runtimes`. It is now taken from the NuGet package
**`Irion.DuckDb.Native.Bridge`**, a custom DuckDB build with the `bridge` extension linked inside the library
(the `bridge_*` C functions and the SQL objects of the bridge live in the same `duckdb.dll` / `libduckdb.so`).

Package layout (standard NuGet runtime assets):

```text
runtimes/win-x64/native/duckdb.dll
runtimes/linux-x64/native/libduckdb.so
```

How it is wired:

- `DuckDB.NET.Bindings/Bindings.csproj` has a `PackageReference` to `Irion.DuckDb.Native.Bridge`, **for every `BuildType`**
  (also `ManagedOnly`): the library with the bridge is always the one used.
- `Data` references `Bindings`, so the dependency flows to every consumer. The published
  `Irion.DuckDB.NET.Bindings.Full` nupkg declares the dependency and contains no native file itself; NuGet restores
  the runtime assets and copies them to `runtimes/<rid>/native/` in the output of the consuming project.
- Projects in this repo (tests, benchmarks, samples) get the library transitively. The old `CallExecutable` target and
  the `None` items copying from `obj/runtimes` were removed from `Test.csproj`, `Benchmarks.csproj`,
  `Irion.DuckDB.NET.Test.csproj` and `Irion.DuckDB.NET.Benchmark.csproj`. `NativeLibraryHelper` /
  `NativeLibraryLoader` keep loading from `runtimes/<rid>/native`.
- Removed: `DownloadNativeLibs.targets`, the `DownloadNativeLibs`/`CleanNativeLibs` targets, `DuckDbArtifactRoot`,
  `NoNativeText`, and the nightly step of `.github/workflows/ci.yml` (it pointed `DuckDbArtifactRoot` to the latest
  official nightly; it no longer has any effect).

Changing the native library:

1. Publish a new version of `Irion.DuckDb.Native.Bridge` to the `Repository` feed (same layout as above).
2. Update the `Version` of the `PackageReference` in `Bindings.csproj`.
3. Run the tests, then pack (see *Irion Package Build*).

To try a local build of the library without publishing a package, add a local folder as a NuGet source and use a
pre-release version of the package, or copy the library over `runtimes/<rid>/native` in the output folder of a test project.

Caveat: the library is a single copy of DuckDB per process. Handles (`DuckDBNativeConnection`, ...) created by
DuckDB.NET and passed to the bridge functions are valid because they come from this same library; do not mix them with
another DuckDB library loaded in the process.

### Bridge C API (`DuckDB.NET.Native.Bridge`)

`DuckDB.NET.Bindings/Bridge/BridgeMethods.cs` exposes the C functions exported by the bridge extension. Each function
has a raw overload (`Span<byte> error, int errorSize`) and an overload with `out string error` (buffer of 1024 bytes).

| Method | C function |
|---|---|
| `RegisterInstance` | `bridge_register_instance` |
| `UnregisterInstance` | `bridge_unregister_instance` |
| `WriteStats` | `bridge_write_stats` (since 2.0.0-alpha10) |

Since `Irion.DuckDb.Native.Bridge` 2.0.0-alpha7 (bridge features 12-13) the registration API is just these two functions. Links,
materialized/unmaterialized links and the transaction connection (`con_tx`) are gone, together with
`bridge_register_datasource`, `bridge_register_link*` and `bridge_unregister_link`. Reading is done in SQL:

```csharp
using DuckDB.NET.Native;
using DuckDB.NET.Native.Bridge;

using var delt = new DuckDBConnection("DataSource=:memory:");          // reads, no credentials
using var instance = new DuckDBConnection("DataSource=:memory:");      // owns the source (secrets, ATTACH...)
delt.Open(); instance.Open();

BridgeMethods.RegisterInstance(delt.NativeConnection, "A", instance.NativeConnection, out var error);

// on any connection of the Delt (use DuckDBCommand for these)
//   ATTACH 'bridge:A' AS dba;                        -- add (READ_ONLY) to forbid writes
//   SELECT ... FROM dba.orders o JOIN local l ON ...;
//   CREATE TABLE isoldata.x AS SELECT * FROM dba.orders WHERE ...;
//   DETACH dba;
BridgeMethods.UnregisterInstance(delt.NativeConnection, "A", out error);
```

Also available in SQL on the Delt: `bridge_instances()`, `bridge_remote('<alias>', '<SELECT>')`,
`bridge_explain_analyze(sql [, format])`; settings `bridge_enable_filter_pushdown`, `bridge_enable_projection_pushdown`
and `bridge_batch_queue_max_bytes` (set with `SET` after opening, never with `duckdb_set_config`).

Rules (full details in the bridge `HOST-GUIDE.md`):

- Register the instance before the `ATTACH`; the instance must be a different DuckDB instance from the Delt and its
  setup (secrets, extensions, ATTACHes) is done at instance level.
- There is no per-connection grant: anyone who can open a connection on the Delt can use every registered instance.
  Use `ATTACH ... (READ_ONLY)` and control upstream who gets a connection.
- **Batch** (`CONNECT <alias>` ... `DISCONNECT`, one Delt connection for the whole batch): between `CONNECT` and
  `DISCONNECT` every statement runs on the session of the instance and must be sent with `duckdb_query`
  (`NativeMethods.Query.DuckDBQuery`). `DuckDBCommand` prepares the statement and **cannot be used there** (engine
  limit, duckdb/duckdb#27269, expected fixed in DuckDB 2.0.1); outside the batch `DuckDBCommand` is fine.
- **Writing from the instance**: register the Delt on the instance (`RegisterInstance(instanceConnection, "isoldata",
  deltConnection)`), then on the instance `ATTACH 'bridge:isoldata' AS isoldata` (without `READ_ONLY`) and run
  `CREATE TABLE isoldata.main.t AS SELECT ... FROM <objects of the instance>` or `INSERT INTO isoldata.main.t ...`
  on the instance connection, without `CONNECT`.
- Shutdown: `DETACH` and unregister, then close the Delt, then the instance.
- **Write metrics**: right after a write from the instance (even a failed one) call
  `BridgeMethods.WriteStats(instanceConnection, out var stats, out var error)` on the **same instance connection** that
  ran the statement: `stats.Rows`, `stats.Bytes` (uncompressed bytes copied by the sink, not the size on disk),
  `stats.DurationMs` and `stats.Outcome` (`Committed`, `RolledBack`, `None`). Every statement started on the connection
  resets the numbers, so read them before running anything else (also `ROLLBACK` or a `SELECT`); they are not
  cumulative. A CTAS/INSERT pushed whole to the Delt (constant or Isoldata-only source) does not go through the sink
  and reports `None`.
- Push delivery (`BRIDGE_PUSH`) no longer exists.

The `Irion.DuckDB.NET.Bindings.Full` package ships its own documentation in `docs/` (source in the `docs/` folder of this
repo, Italian like the bridge docs): `DUCKDB-NET-BRIDGE-GUIDE.md` (how to use the bridge from DuckDB.NET),
`duckdb-net-claude-snippet.md` (block to paste in the `CLAUDE.md` of a host project) and
`examples/BridgeQuickStart/Program.cs` (runnable example, text only: it is not compiled in the package). After a restore
they are in `~/.nuget/packages/irion.duckdb.net.bindings.full/<version>/docs/`. Update them in the same change that
modifies `BridgeMethods` or the way the native library is referenced.

The native package ships the bridge documentation: `docs/HOST-GUIDE.md` (the reference for hosts), `docs/host-claude-snippet.md`,
`docs/src/include/bridge_capi.hpp` and the .NET examples under `docs/examples/`, in
`~/.nuget/packages/irion.duckdb.native.bridge/<version>/docs/`. They describe the version of the package: use the
ones of the version referenced in `Bindings.csproj`.

### Merge a Specific Upstream Tag

```powershell
git checkout v1.4.4
git fetch upstream --tags
git merge refs/tags/1.4.4
git push origin v1.4.4
```

```powershell
git checkout <target-branch>
git fetch upstream --tags
git merge refs/tags/<tag-version>
git push origin <target-branch>
```

```powershell
git log --oneline HEAD..refs/tags/<tag-version>
```


### MotherDuck

To connect to [MotherDuck](https://motherduck.com):

```cs
using var duckDBConnection = new DuckDBConnection("DataSource=md:{your_database}?motherduck_token=ey...");
```

## DuckDB Extensions (C#)

If you want to build DuckDB extensions with C#, see [Giorgi/DuckDB.ExtensionKit](https://github.com/Giorgi/DuckDB.ExtensionKit).

## Known Issues

When debugging your project that uses DuckDB.NET library, you may get the following error: **System.AccessViolationException: Attempted to read or write protected memory. This is often an indication that other memory is corrupt**. The error happens due to debugger interaction with the native memory. For a workaround check out [Debugger Options mess up debugging session during Marshalling
](https://youtrack.jetbrains.com/issue/RIDER-114126/Debugger-Options-mess-up-debugging-session-during-Marshalling)

## Documentation

Documentation is available at [https://duckdb.net](https://duckdb.net)

## Support

If you encounter a bug with the library [Create an Issue](https://github.com/Giorgi/DuckDB.NET/issues/new). Join the [DuckDB `dotnet` channel](https://discord.duckdb.org/) for DuckDB.NET-related topics.

## Contributors

[![Contributors](https://contrib.rocks/image?repo=Giorgi/DuckDB.NET)](https://github.com/Giorgi/DuckDB.NET/graphs/contributors)

## Sponsors

A big thanks to [DuckLabs](https://ducklabs.com/) and [AWS Open Source Software Fund](https://github.com/aws/dotnet-foss) for sponsoring the project!

[![DuckLabs](https://raw.githubusercontent.com/Giorgi/DuckDB.NET/main/.github/sponsors/duckdb-labs-logo.png)](https://ducklabs.com/)

[![AWS](https://raw.githubusercontent.com/Giorgi/DuckDB.NET/main/.github/sponsors/aws-logo-small.png)](https://github.com/aws/dotnet-foss)
