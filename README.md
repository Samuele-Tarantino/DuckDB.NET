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

Use this workflow to build and publish Irion packages with bundled native DuckDB runtimes.
The standard flow uses:

- `build/irion.version` as the single source of truth for the package version
- `scripts/irion-package.ps1` to show/set/bump, check remote, build, pack and push

#### 1. Prerequisites

1. Build DuckDB runtimes for `linux-x64` and `win-x64`.
2. Make sure the runtime archives are available as `win-x64.zip` and `linux-x64.zip`.
3. Set `DuckDbArtifactRoot` in `DuckDB.NET.Bindings/Bindings.csproj` to the folder/share containing those archives.

`DuckDB.NET.Bindings/Bindings.csproj` expects:

```text
$(DuckDbArtifactRoot)/win-x64.zip
$(DuckDbArtifactRoot)/linux-x64.zip
```

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
.\scripts\irion-package.ps1 -Command build -p "NativeDownloadRetries=30","NativeDownloadRetryDelayMilliseconds=15000"
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

1. `DuckDbArtifactRoot` points to the correct runtime artifacts.
2. `build/irion.version` contains the final release version.
3. Both `dotnet pack` commands completed successfully.
4. Both packages were pushed to the target feed.

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

A big thanks to [DuckDB Labs](https://duckdblabs.com/) and [AWS Open Source Software Fund](https://github.com/aws/dotnet-foss) for sponsoring the project!

[![DuckDB Labs](https://raw.githubusercontent.com/Giorgi/DuckDB.NET/main/.github/sponsors/duckdb-labs-logo.png)](https://duckdblabs.com/)

[![AWS](https://raw.githubusercontent.com/Giorgi/DuckDB.NET/main/.github/sponsors/aws-logo-small.png)](https://github.com/aws/dotnet-foss)
