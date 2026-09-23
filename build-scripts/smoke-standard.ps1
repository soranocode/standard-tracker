# Run with Windows PowerShell (-STA), which hosts .NET Framework/WPF.
param([string]$Configuration = 'Debug', [switch]$LibraryPreview)
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
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Data;
using System.Xml.Serialization;
using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Controls.DeckPicker;
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
            System.Threading.SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext());
            var window = Core.MainWindow;
            if(!window.Title.StartsWith("Standard Tracker")) throw new Exception("Incorrect identity");
            if(DeckList.Instance.Decks.Count != 0) throw new Exception("Profile is not empty");
            if(Hearthstone_Deck_Tracker.Stats.DeckStatsList.Instance.DeckStats.Count != 0) throw new Exception("Stats are not empty");
            ((Hearthstone_Deck_Tracker.FlyoutControls.OptionsMain)window.FindName("Options")).Load(Core.Game);
            window.UpdateLayout();
            if(args.Length > 1) {
                var picker = (DeckPicker)window.FindName("DeckPickerList");
                var names = new[] { "Драконы на рассвете", "Ледяной контроль", "Темпо маг", "Контроль воин" };
                var classes = new[] { "Druid", "Mage", "Mage", "Warrior" };
                var archetypes = new[] { "Драконы", "Контроль", "Темпо", "Контроль" };
                for(int i = 0; i < names.Length; i++) {
                    var deck = new Deck { Name = names[i], Class = classes[i], Archetype = archetypes[i], IsArenaDeck = false, IsDungeonDeck = false, IsDuelsDeck = false };
                    deck.Cards.Add(new Card(HearthDb.Cards.All.Values.First(c => c.Set == HearthDb.Enums.CardSet.CORE && c.Class == HearthDb.Enums.CardClass.NEUTRAL)));
                    DeckList.Instance.Decks.Add(deck);
                }
                var first = DeckList.Instance.Decks[0];
                if(((Deck)first.Clone()).Archetype != first.Archetype) throw new Exception("Clone lost archetype");
                var serializer = new XmlSerializer(typeof(Deck));
                using(var buffer = new MemoryStream()) {
                    serializer.Serialize(buffer, first); buffer.Position = 0;
                    if(((Deck)serializer.Deserialize(buffer)).Archetype != first.Archetype) throw new Exception("Serialization lost archetype");
                }
                picker.SelectedClasses.Clear();
                picker.SelectedClasses.Add(Hearthstone_Deck_Tracker.Enums.HeroClassAll.All);
                Config.Instance.SelectedTags = new System.Collections.Generic.List<string> { "All" };
                Config.Instance.SelectedDeckPickerDeckType = Hearthstone_Deck_Tracker.Enums.DeckType.Standard;
                picker.UpdateDecks();
                if(picker.DisplayedDecks.Count != 4) throw new Exception("Expected four decks: " + picker.DisplayedDecks.Count);
                picker.DeckNameFilter = "контроль"; picker.UpdateDecks();
                if(picker.DisplayedDecks.Count != 2) throw new Exception("Archetype search failed");
                picker.DeckNameFilter = "нет такой колоды"; picker.UpdateDecks();
                if(picker.DisplayedDecks.Count != 0) throw new Exception("Empty search failed");
                picker.DeckNameFilter = null; picker.UpdateDecks();
                if(CollectionViewSource.GetDefaultView(picker.DisplayedDecks).Groups.Count != 4) throw new Exception("Archetype groups failed");
                var item = picker.DisplayedDecks.First(x => x.Deck == first);
                typeof(DeckPicker).GetMethod("Favorite_OnClick", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(picker, new object[] { new Button { DataContext = item }, new RoutedEventArgs() });
                if(!item.Favorite) throw new Exception("Favorite action failed");
                typeof(DeckPicker).GetField("_favoritesOnly", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(picker, true);
                picker.UpdateDecks();
                if(picker.DisplayedDecks.Count != 1) throw new Exception("Favorite filter failed");
                typeof(DeckPicker).GetField("_favoritesOnly", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(picker, false);
                picker.UpdateDecks();
                // Render the real WPF library without showing a window or starting the game tracker.
                ((Panel)picker.Parent).Children.Remove(picker);
                var surface = new Border { Background = new SolidColorBrush(Color.FromRgb(18, 23, 33)), Child = picker, Padding = new Thickness(20) };
                surface.Measure(new Size(820, 940)); surface.Arrange(new Rect(0, 0, 820, 940)); surface.UpdateLayout();
                var bitmap = new RenderTargetBitmap(820, 940, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(surface);
                var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
                using(var output = File.Create(args[1])) png.Save(output);
                Console.WriteLine("PASS: archetype search, grouping, favorites, clone and XML round trip; library rendered");
            }
            Console.WriteLine("PASS: WPF main window and options loaded; isolated standalone profile; " + window.Title);
            return 0;
        } catch(Exception e) { Console.WriteLine(e); return 1; }
    }
}
'@
$framework = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$sourcePath = Join-Path $binPath 'StandardTracker.Smoke.cs'
Set-Content -LiteralPath $sourcePath -Value $source -Encoding UTF8
$netstandardPath = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies/Microsoft/Framework/.NETFramework/v4.7.2/Facades/netstandard.dll'
if (!(Test-Path -LiteralPath $netstandardPath)) {
    $netstandardPath = (Get-ChildItem (Join-Path $repoPath 'packages/microsoft.netframework.referenceassemblies.net472') -Filter netstandard.dll -Recurse | Select-Object -First 1).FullName
}
$references = @((Join-Path $framework 'WPF/PresentationFramework.dll'),
    $netstandardPath,
    (Join-Path $framework 'WPF/PresentationCore.dll'), (Join-Path $framework 'WPF/WindowsBase.dll'),
    (Join-Path $framework 'System.Xaml.dll'), (Join-Path $framework 'System.Core.dll'),
    (Join-Path $binPath 'HearthstoneDeckTracker.exe'), (Join-Path $binPath 'HearthDb.dll'),
    (Join-Path $binPath 'MahApps.Metro.dll')) | ForEach-Object { '/reference:' + $_ }
& (Join-Path $framework 'csc.exe') /nologo /target:exe /platform:x64 "/out:$output" @references $sourcePath
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
if ($LibraryPreview) {
    $profilePath = Join-Path ([IO.Path]::GetTempPath()) ('standard-library-' + [Guid]::NewGuid())
    & $output $profilePath (Join-Path $repoPath 'library-preview.png')
} else {
    & $output $profilePath
}
exit $LASTEXITCODE
