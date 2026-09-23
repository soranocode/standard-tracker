# Run with Windows PowerShell (-STA), which hosts .NET Framework/WPF.
param([string]$Configuration = 'Debug')
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
$binPath = Join-Path $repoPath "Hearthstone Deck Tracker/bin/x64/$Configuration"
$binPath = [IO.Path]::GetFullPath($binPath)
$profilePath = Join-Path $repoPath '.smoke-profile'
$output = Join-Path $binPath 'StandardTracker.Smoke.exe'
if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output }
$source = @'
using System;
using System.IO;
using System.Reflection;
using Hearthstone_Deck_Tracker;
class Smoke {
    [STAThread] static int Main(string[] args) {
        try {
            typeof(System.Windows.Application).GetField("_resourceAssembly", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, typeof(App).Assembly);
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            Directory.CreateDirectory(args[0]);
            typeof(Config).GetField("AppDataPath").SetValue(null, args[0]);
            Config.Load();
            HearthDb.Config.AutoLoadCardDefs = false;
            HearthDb.Cards.LoadBaseData(HearthDb.Cards.GetBundledBaseData());
            var app = new App();
            app.InitializeComponent();
            var window = Core.MainWindow;
            if(!window.Title.StartsWith("Standard Tracker")) throw new Exception("Incorrect identity");
            if(DeckList.Instance.Decks.Count != 0) throw new Exception("Profile is not empty");
            if(Hearthstone_Deck_Tracker.Stats.DeckStatsList.Instance.DeckStats.Count != 0) throw new Exception("Stats are not empty");
            ((Hearthstone_Deck_Tracker.FlyoutControls.OptionsMain)window.FindName("Options")).Load(Core.Game);
            window.UpdateLayout();
            Console.WriteLine("PASS: WPF main window and options loaded; empty standalone profile; " + window.Title);
            return 0;
        } catch(Exception e) { Console.WriteLine(e); return 1; }
    }
}
'@
$framework = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$sourcePath = Join-Path $binPath 'StandardTracker.Smoke.cs'
Set-Content -LiteralPath $sourcePath -Value $source -Encoding UTF8
$references = @((Join-Path $framework 'WPF/PresentationFramework.dll'),
    (Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies/Microsoft/Framework/.NETFramework/v4.7.2/Facades/netstandard.dll'),
    (Join-Path $framework 'WPF/PresentationCore.dll'), (Join-Path $framework 'WPF/WindowsBase.dll'),
    (Join-Path $framework 'System.Xaml.dll'), (Join-Path $framework 'System.Core.dll'),
    (Join-Path $binPath 'HearthstoneDeckTracker.exe'), (Join-Path $binPath 'HearthDb.dll'),
    (Join-Path $binPath 'MahApps.Metro.dll')) | ForEach-Object { '/reference:' + $_ }
& (Join-Path $framework 'csc.exe') /nologo /target:exe /platform:x64 "/out:$output" @references $sourcePath
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $output $profilePath
exit $LASTEXITCODE
