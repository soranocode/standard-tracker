# Package the standalone desktop build without upstream installers or release services.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*$')]
    [string]$PackageVersion,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
$binPath = [IO.Path]::GetFullPath((Join-Path $repoPath "Hearthstone Deck Tracker/bin/x64/$Configuration"))
$binPrefix = $binPath + [IO.Path]::DirectorySeparatorChar
$fileList = Join-Path $repoPath "Hearthstone Deck Tracker/obj/x64/$Configuration/Hearthstone Deck Tracker.csproj.FileListAbsolute.txt"
$packageName = "StandardTracker-$PackageVersion-win-x64"
$artifactsPath = Join-Path $repoPath 'artifacts'
$archivePath = Join-Path $artifactsPath ($packageName + '.zip')
$stagingPath = Join-Path $artifactsPath ($packageName + '-' + [Guid]::NewGuid().ToString('N'))
if (!(Test-Path -LiteralPath $fileList)) { throw "Build $Configuration before packaging." }
if (Test-Path -LiteralPath $archivePath) { throw "Archive already exists: $archivePath" }
$runtimeFiles = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($file in Get-Content -LiteralPath $fileList) {
    $path = [IO.Path]::GetFullPath($file)
    if ($path.StartsWith($binPrefix, [StringComparison]::OrdinalIgnoreCase) -and
        [IO.Path]::GetExtension($path) -in @('.dll', '.config', '.ico', '.py', '.gz')) {
        [void]$runtimeFiles.Add($path)
    }
}
[void]$runtimeFiles.Add((Join-Path $binPath 'HearthstoneDeckTracker.exe'))
foreach ($file in @('api-ms-win-crt-convert-l1-1-0.dll', 'api-ms-win-crt-heap-l1-1-0.dll',
    'api-ms-win-crt-runtime-l1-1-0.dll', 'api-ms-win-crt-stdio-l1-1-0.dll', 'api-ms-win-crt-string-l1-1-0.dll',
    'msvcp140.dll', 'ucrtbase.dll', 'vcruntime140.dll', 'vcruntime140_1.dll')) {
    [void]$runtimeFiles.Add((Join-Path $binPath $file))
}
$themeSource = Join-Path $repoPath 'Hearthstone Deck Tracker/Images/Themes'
foreach ($file in Get-ChildItem -LiteralPath $themeSource -Recurse -File) {
    $relative = $file.FullName.Substring($themeSource.Length + 1)
    [void]$runtimeFiles.Add((Join-Path $binPath (Join-Path 'Images/Themes' $relative)))
}
foreach ($file in $runtimeFiles) {
    if (!(Test-Path -LiteralPath $file -PathType Leaf)) { throw "Missing build output: $file" }
}
New-Item -ItemType Directory -Path $stagingPath -Force | Out-Null
foreach ($file in $runtimeFiles) {
    $destination = Join-Path $stagingPath $file.Substring($binPrefix.Length)
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $file -Destination $destination
}
$licensePath = Join-Path $stagingPath 'licenses'
New-Item -ItemType Directory -Path $licensePath | Out-Null
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $repoPath 'licenses') -File) {
    Copy-Item -LiteralPath $file.FullName -Destination $licensePath
}
Copy-Item -LiteralPath (Join-Path $repoPath 'Hearthstone Deck Tracker/Utility/Markdown.Xaml/License.txt') `
    -Destination (Join-Path $licensePath 'Markdown.Xaml-License.txt')
$commit = (& git -C $repoPath rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot determine source commit.' }
$readme = @"
Standard Tracker — $PackageVersion ($Configuration, Windows x64)

Распакуйте весь архив в отдельную папку и запустите HearthstoneDeckTracker.exe.
Требуется .NET Framework 4.7.2 или новее.
Ваши колоды и история хранятся в %APPDATA%\StandardTracker, отдельно от приложения.

Это сборка для проверки текущих изменений, а не окончательный стабильный релиз.
Для извлечения изображений из установленного Hearthstone дополнительно нужны
Python, UnityPy и Pillow. Без них приложение использует обычные изображения-заглушки.

Исходники: https://github.com/soranocode/standard-tracker
Коммит: $commit
Based on HearthSim Hearthstone Deck Tracker v1.57.7.
Original copyright and third-party notices are retained; see licenses/.
"@
[IO.File]::WriteAllText((Join-Path $stagingPath 'README.txt'), $readme, [Text.UTF8Encoding]::new($true))
$metadata = [ordered]@{
    packageVersion = $PackageVersion
    configuration = $Configuration
    sourceCommit = $commit
    executableSha256 = (Get-FileHash -LiteralPath (Join-Path $stagingPath 'HearthstoneDeckTracker.exe') -Algorithm SHA256).Hash.ToLowerInvariant()
    files = @(Get-ChildItem -LiteralPath $stagingPath -Recurse -File | ForEach-Object { $_.FullName.Substring($stagingPath.Length + 1).Replace('\', '/') } | Sort-Object)
}
[IO.File]::WriteAllText((Join-Path $stagingPath 'BUILD-INFO.json'), ($metadata | ConvertTo-Json -Depth 4), [Text.UTF8Encoding]::new($false))
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($stagingPath, $archivePath, [IO.Compression.CompressionLevel]::Optimal, $false)
$archiveHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText(($archivePath + '.sha256'), "$archiveHash  $packageName.zip`n", [Text.UTF8Encoding]::new($false))
Write-Output "Packaged $($metadata.files.Count + 1) files: $archivePath"
Write-Output "SHA256: $archiveHash"
