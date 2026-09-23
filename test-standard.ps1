[CmdletBinding()]
param([string]$DotNetRoot)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/bootstrap.ps1" -DotNetRoot $DotNetRoot
Invoke-BuildStep (@('HDTTests/HDTTests.csproj', '/restore') + $common)
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vstest = & $vswhere -latest -products * -find 'Common7/IDE/Extensions/TestPlatform/vstest.console.exe' | Select-Object -First 1
if (!$vstest) { throw 'Visual Studio Test Platform is required.' }
& $vstest "$PSScriptRoot/HDTTests/bin/x64/Debug/HDTTests.dll" /Platform:x64 '/TestCaseFilter:FullyQualifiedName~StandardTrackerIsolationTests' /Logger:trx
if ($LASTEXITCODE -ne 0) { throw "Isolation tests failed: $LASTEXITCODE" }
