[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$UseLocalDependencies,
    [switch]$UpdateLocalizations,
    [switch]$DisableCrashReporting,
    [string]$DotNetRoot
)

$ErrorActionPreference = 'Stop'
# The standalone product always uses explicitly provisioned dependencies.
if ($UpdateLocalizations) { throw 'Automatic upstream localization updates are disabled.' }
if (!$DotNetRoot) {
    $dotnetCommand = Get-Command dotnet -ErrorAction Stop
    $DotNetRoot = Split-Path -Parent $dotnetCommand.Source
}
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

if ($DotNetRoot) {
    $DotNetRoot = (Resolve-Path -LiteralPath $DotNetRoot).Path
    $env:DOTNET_ROOT = $DotNetRoot
    $env:DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR = $DotNetRoot
    $buildPath = "$DotNetRoot;$env:PATH"
    Remove-Item Env:PATH -ErrorAction SilentlyContinue
    $env:Path = $buildPath
    $sdkVersion = & (Join-Path $DotNetRoot 'dotnet.exe') --version
    if ($LASTEXITCODE -ne 0) { throw 'Could not resolve a .NET SDK in DotNetRoot.' }
    # This net472 solution does not use optional .NET workloads.
    $env:MSBuildEnableWorkloadResolver = 'false'
    $env:MSBuildSDKsPath = Join-Path $DotNetRoot "sdk\$sdkVersion\Sdks"
}

if (!(Get-Command dotnet -ErrorAction SilentlyContinue) -or !(dotnet --list-sdks)) {
    throw 'A .NET SDK is required. Install a compatible SDK or pass -DotNetRoot with a local SDK directory.'
}
if (!(Get-Command git -ErrorAction SilentlyContinue)) {
    throw 'Git is required to prepare dependencies.'
}

$msbuild = Get-Command msbuild -ErrorAction SilentlyContinue
if ($msbuild) {
    $msbuildPath = $msbuild.Source
} else {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (!(Test-Path -LiteralPath $vswhere)) {
        throw 'Visual Studio MSBuild is required (Desktop development with .NET workload).'
    }
    $msbuildPath = & $vswhere -latest -products * -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
    if (!$msbuildPath) { throw 'Could not locate Visual Studio MSBuild.' }
}

# Match the x64 WPF build to avoid launching a separate resource task host.
$x64MsbuildPath = Join-Path (Split-Path -Parent $msbuildPath) 'amd64\MSBuild.exe'
if (Test-Path -LiteralPath $x64MsbuildPath) {
    $msbuildPath = $x64MsbuildPath
}

function Invoke-BuildStep([string[]]$BuildArguments) {
    & $msbuildPath @BuildArguments
    if ($LASTEXITCODE -ne 0) { throw "MSBuild failed with exit code $LASTEXITCODE." }
}

Push-Location $PSScriptRoot
try {
    $common = @('/nologo', '/verbosity:minimal', '/nodeReuse:false', '/p:UseSharedCompilation=false', "/p:Configuration=$Configuration", '/p:Platform=x64')
    if ($DisableCrashReporting) {
        $common += '/p:SentryDsn='
    }
    Invoke-BuildStep (@('Bootstrap/Bootstrap.csproj', '/t:Bootstrap', "/p:UseLocalDependencies=true", "/p:UpdateLocalizations=$($UpdateLocalizations.IsPresent)") + $common)
    Invoke-BuildStep (@('Hearthstone Deck Tracker/Hearthstone Deck Tracker.csproj', '/restore') + $common)
} finally {
    Pop-Location
}
