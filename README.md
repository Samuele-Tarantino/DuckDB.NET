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
(`bridge_*` C functions and the `bridge_scan` SQL function live in the same `duckdb.dll` / `libduckdb.so`).

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
| `RegisterDataSource` / `UnregisterDataSource` | `bridge_register_datasource` / `bridge_unregister_datasource` |
| `RegisterLink` | `bridge_register_link` |
| `RegisterLinkMaterialized` | `bridge_register_link_materialized` |
| `RegisterLinkView` | `bridge_register_link_view` |
| `UnregisterLink` | `bridge_unregister_link` |

```csharp
using DuckDB.NET.Native;
using DuckDB.NET.Native.Bridge;

using var delt = new DuckDBConnection("DataSource=:memory:");          // reads, no credentials
using var connector = new DuckDBConnection("DataSource=:memory:");     // owns the source (secrets, ATTACH...)
delt.Open(); connector.Open();

BridgeMethods.RegisterDataSource(delt.NativeConnection, "A", connector.NativeConnection, out var error);
BridgeMethods.RegisterLink(delt.NativeConnection, "cl_t", "A", "SELECT * FROM t", out error);
// SELECT * FROM bridge_scan('cl_t') on the delt
BridgeMethods.UnregisterLink(delt.NativeConnection, "cl_t", out error);
```

Rules (full details in the bridge `HOST-GUIDE.md`): register DataSource and links before any SQL that uses them;
Connector and Delt must be different instances; set up the Connector at instance level; close the Delt before the
Connector; for materialized links unregister the link before closing the transaction connection.

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
