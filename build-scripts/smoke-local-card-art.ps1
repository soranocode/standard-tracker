# Optional integration check: requires Hearthstone, Python, UnityPy, and Pillow.
param(
    [string]$GameDir = 'C:\Program Files (x86)\Hearthstone',
    [string]$CardId = 'CORE_EX1_011'
)
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
$binPath = Join-Path $repoPath 'Hearthstone Deck Tracker/bin/x64/Debug'
$framework = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$sourcePath = Join-Path $repoPath '.smoke-profile/local-card-art-smoke.cs'
$output = Join-Path $binPath 'LocalCardArt.Smoke.exe'
$profilePath = Join-Path $repoPath '.smoke-profile/local-card-art-profile'
New-Item -ItemType Directory -Path (Split-Path $sourcePath) -Force | Out-Null
$netstandardPath = (Get-ChildItem (Join-Path $repoPath 'packages/microsoft.netframework.referenceassemblies.net472') -Filter netstandard.dll -Recurse | Select-Object -First 1).FullName
$source = @'
using System;
using System.IO;
using System.Reflection;
using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Utility.Assets;
class Smoke {
    [STAThread] static int Main(string[] args) {
        try {
            Directory.CreateDirectory(args[0]);
            typeof(Config).GetField("AppDataPath").SetValue(null, args[0]);
            Config.Instance.HearthstoneDirectory = args[1];
            var downloader = AssetDownloaders.cardTileDownloader;
            if(downloader == null) throw new Exception("Card tile loader is unavailable");
            var image = downloader.GetAssetData(new Card(args[2])).GetAwaiter().GetResult();
            var file = Path.Combine(args[0], "Images", "CardTiles", args[2] + ".jpg");
            if(image == null || !File.Exists(file)) throw new Exception("Local card art was not extracted");
            Console.WriteLine("PASS: card tile extracted from game files through application loader");
            return 0;
        } catch(Exception error) { Console.WriteLine(error); return 1; }
    }
}
'@
Set-Content -LiteralPath $sourcePath -Value $source -Encoding UTF8
$references = @(
    (Join-Path $framework 'WPF/PresentationCore.dll'),
    (Join-Path $framework 'WPF/WindowsBase.dll'),
    (Join-Path $framework 'System.Xaml.dll'),
    (Join-Path $framework 'System.Core.dll'),
    $netstandardPath,
    (Join-Path $binPath 'HearthstoneDeckTracker.exe'),
    (Join-Path $binPath 'HearthDb.dll')
) | ForEach-Object { '/reference:' + $_ }
& (Join-Path $framework 'csc.exe') /nologo /target:exe /platform:x64 "/out:$output" @references $sourcePath
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $output $profilePath $GameDir $CardId
exit $LASTEXITCODE
