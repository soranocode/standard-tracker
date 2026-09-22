# Local development build

The current application targets .NET Framework 4.7.2 and Windows x64.
Install Visual Studio 2022 with the .NET desktop development workload, a
compatible .NET SDK, and the .NET Framework 4.7.2 developer/targeting pack.
Git is required for the initial localization checkout.

From PowerShell:

```powershell
.\bootstrap.ps1
# Optional: use an SDK extracted outside Program Files
.\bootstrap.ps1 -DotNetRoot 'C:\path\to\dotnet'
```

The script locates Visual Studio MSBuild, prepares binary dependencies and UI
strings, restores NuGet packages, and builds the Debug x64 application. It stops
at the first failed step. It does not launch the tracker.

## Reuse local dependencies

```powershell
.\bootstrap.ps1 -UseLocalDependencies
```

This prevents **Bootstrap** from downloading binaries or fetching translations.
It is not a fully offline build: NuGet restore may still require network access.
Provide the complete dependency payload in `lib/`, including native runtime DLLs,
and either `HDT-Localization/*.resx` or the already copied
`Hearthstone Deck Tracker/Properties/Strings*.resx` files.

HearthDb and HearthMirror are core dependencies. The current source still requires
HSReplay and BobsBuddy until their features and references are removed together.
The local-dependency switch does not disable any runtime cloud feature.

## Update translations explicitly

Existing translation checkouts are reused by default. To request an update:

```powershell
.\bootstrap.ps1 -UpdateLocalizations
```

This uses `git pull --ff-only`; Bootstrap no longer runs `git reset --hard`.
Local modifications are not forcibly discarded. Resolve divergent histories in
the localization checkout manually. `-UseLocalDependencies` takes precedence
over `-UpdateLocalizations`.

Equivalent Bootstrap properties for solution/CI builds are
`/p:UseLocalDependencies=true` and `/p:UpdateLocalizations=true`.
