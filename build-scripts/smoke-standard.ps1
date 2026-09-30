# Run with Windows PowerShell (-STA), which hosts .NET Framework/WPF.
param([string]$Configuration = 'Debug', [switch]$LibraryPreview)
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
$binPath = Join-Path $repoPath "Hearthstone Deck Tracker/bin/x64/$Configuration"
$binPath = [IO.Path]::GetFullPath($binPath)
$profilePath = Join-Path ([IO.Path]::GetTempPath()) ('standard-smoke-' + [Guid]::NewGuid())
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
            var deckPicker = (DeckPicker)window.FindName("DeckPickerList");
            var addDeckButton = deckPicker.FindName("LibraryAddDeck") as Button;
            if(addDeckButton == null || addDeckButton.Content == null || addDeckButton.Visibility != Visibility.Visible)
                throw new Exception("Deck-library add button is missing");
            var deckCodeDialog = new Hearthstone_Deck_Tracker.Windows.DeckCodeImportWindow();
            if(deckCodeDialog.Title != "Вставьте код колоды" || !(deckCodeDialog.FindName("DeckCodeInput") is TextBox))
                throw new Exception("Deck-code import dialog did not load");
            deckCodeDialog.Close();
            Console.WriteLine("Checking Wild deck save and library...");
            var wildDeck = new Deck { Name = "Wild import regression", Class = "Druid" };
            wildDeck.Cards.Add(new Card("LOE_077") { Count = 2 });
            foreach(var card in HearthDb.Cards.Collectible.Values.Where(c => c.Class == HearthDb.Enums.CardClass.DRUID
                && Helper.WildOnlySets.Contains(HearthDbConverter.SetConverter(c.Set))).Take(14))
                wildDeck.Cards.Add(new Card(card) { Count = 2 });
            if(wildDeck.StandardViable) throw new Exception("Wild fixture must fail the old Standard gate");
            var editor = new Hearthstone_Deck_Tracker.FlyoutControls.DeckEditor.DeckEditorViewModel();
            editor.SetDeck(wildDeck, true);
            if(!editor.CanSave) throw new Exception("Wild deck cannot be saved in the editor");
            DeckManager.SaveDeck(wildDeck);
            if(!DeckList.Instance.Decks.Contains(wildDeck)) throw new Exception("Wild deck was not saved");
            if(!XmlManager<DeckList>.Load(Config.Instance.DataDir + "PlayerDecks.xml").Decks.Any(d => d.DeckId == wildDeck.DeckId))
                throw new Exception("Wild deck was not persisted to the test profile");
            deckPicker.SelectedClasses.Clear();
            deckPicker.SelectedClasses.Add(Hearthstone_Deck_Tracker.Enums.HeroClassAll.All);
            Config.Instance.SelectedTags = new System.Collections.Generic.List<string> { "All" };
            Config.Instance.SelectedDeckPickerDeckType = Hearthstone_Deck_Tracker.Enums.DeckType.Standard;
            deckPicker.UpdateDecks();
            if(!deckPicker.DisplayedDecks.Any(x => x.Deck == wildDeck)) throw new Exception("Legacy Standard filter hid Wild deck");
            var wildDialog = new Hearthstone_Deck_Tracker.Windows.DeckCodeImportWindow { Left = -10000, Top = -10000, WindowStartupLocation = WindowStartupLocation.Manual, ShowInTaskbar = false };
            var wildCode = HearthDb.Deckstrings.DeckSerializer.Serialize(HearthDbConverter.ToHearthDbDeck(wildDeck), false);
            ((TextBox)wildDialog.FindName("DeckCodeInput")).Text = wildCode;
            wildDialog.Loaded += (sender, e) => {
                Console.WriteLine("Checking Wild deck-code import...");
                typeof(Hearthstone_Deck_Tracker.Windows.DeckCodeImportWindow)
                    .GetMethod("AddDeck_OnClick", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(wildDialog, new object[] { null, new RoutedEventArgs() });
                if(wildDialog.ImportedDeck == null) wildDialog.DialogResult = false;
            };
            if(wildDialog.ShowDialog() != true || wildDialog.ImportedDeck == null || wildDialog.ImportedDeck.StandardViable)
                throw new Exception("Wild deck-code import was rejected: " + ((TextBlock)wildDialog.FindName("ValidationMessage")).Text);
            wildDialog.Close();
            DeckList.Instance.ActiveDeck = null;
            DeckList.Instance.Decks.Remove(wildDeck);
            DeckList.Save();
            Console.WriteLine("PASS: Wild deck-code dialog, editor saving, persistence, and library visibility with legacy Standard filter");
            var game = Core.Game;
            game.IsInMenu = false;
            game.IsRunning = true;
            var stats = new Hearthstone_Deck_Tracker.Stats.GameStats(Hearthstone_Deck_Tracker.Enums.GameResult.None, "Mage", "Druid") { Turns = 7 };
            game.CurrentGameStats = stats;
            game.CurrentSelectedDeck = new HearthMirror.Objects.Deck {
                Id = 4242, Name = "Detected in progress", Hero = HearthDb.CardIds.Collectible.Druid.MalfurionStormrageHeroHeroSkins,
                Cards = wildDeck.Cards.Select(c => new HearthMirror.Objects.Card((string)typeof(Card).GetProperty("Id").GetValue(c, null), c.Count, 0)).ToList(),
                Sideboards = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<HearthMirror.Objects.Card>>()
            };
            var sentinel = new Hearthstone_Deck_Tracker.Hearthstone.Entities.Entity(4242);
            game.Entities.Add(sentinel.Id, sentinel);
            var privateFields = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(GameV2).GetMethod("ResumeInProgressMatch", privateFields).Invoke(game, null);
            if(game.CurrentGameStats != stats || stats.Turns != 7 || !stats.IsReconnect || !game.Entities.ContainsKey(sentinel.Id))
                throw new Exception("Reconnect recovery reset existing game state");
            typeof(GameV2).GetField("_currentGameType", privateFields).SetValue(game, HearthDb.Enums.GameType.GT_RANKED);
            typeof(GameV2).GetField("_currentFormatType", privateFields).SetValue(game, HearthDb.Enums.FormatType.FT_WILD);
            typeof(GameV2).GetField("_spectator", privateFields).SetValue(game, false);
            Config.Instance.ConstructedAutoImportNew = false;
            Config.Instance.AutoDeckDetection = true;
            deckPicker.DeckNameFilter = "hide detected deck";
            typeof(DeckPicker).GetField("_favoritesOnly", privateFields).SetValue(deckPicker, true);
            var sync = typeof(GameV2).GetMethod("TrySyncConstructedDeck", privateFields);
            sync.Invoke(game, new object[] { false });
            var detected = DeckList.Instance.ActiveDeck;
            if(detected == null || detected.HsId != 4242 || detected.Cards.Sum(c => c.Count) != 30)
                throw new Exception("Full in-progress deck was not imported");
            if(game.CurrentGameStats != stats || stats.Turns != 7 || !game.Entities.ContainsKey(sentinel.Id)
                || stats.DeckId != detected.DeckId || stats.PlayerCards.Sum(c => c.Count) != 30)
                throw new Exception("Mid-game deck selection reset game state or failed to bind statistics");
            if(!deckPicker.DisplayedDecks.Any(x => x.Deck == detected)) throw new Exception("Detected deck is hidden by library filters");
            sync.Invoke(game, new object[] { false });
            if(DeckList.Instance.Decks.Count != 1) throw new Exception("Repeated detection duplicated the deck");
            game.IsRunning = false;
            game.IsInMenu = true;
            game.CurrentSelectedDeck = null;
            DeckList.Instance.ActiveDeck = null;
            DeckList.Instance.Decks.Remove(detected);
            DeckList.Save();
            Console.WriteLine("PASS: full mid-game import with bulk auto-import disabled, no duplicates, library reveal, statistics binding, and game-state preservation");
            const string sampleCode = "AAECAQcC69YHstgHDuPmBqr8Bqv8BqWFB+iHB9KXB7etB+yyB7XAB5XCB5vCB5zCB6ngB/vgBwAA";
            var codeUtility = typeof(Deck).Assembly.GetType("Hearthstone_Deck_Tracker.Hearthstone.DeckCodeUtility");
            var parse = codeUtility.GetMethod("Import", BindingFlags.Static | BindingFlags.Public);
            var namedDeck = (Deck)parse.Invoke(null, new object[] { "### Dragon Warrior " + sampleCode + " ### You can view this deck at https://www.hsguru.com/deck/41753794" });
            if(namedDeck.Name != "Dragon Warrior" || namedDeck.Class != "Warrior") throw new Exception("Deck title was not parsed");
            var unnamedDeck = (Deck)parse.Invoke(null, new object[] { sampleCode });
            if(unnamedDeck.Name != "Воин") throw new Exception("Unnamed deck did not use the class name");
            var copy = codeUtility.GetMethod("CopyText", BindingFlags.Static | BindingFlags.Public);
            var copiedDeckCode = (string)copy.Invoke(null, new object[] { namedDeck });
            if(!copiedDeckCode.StartsWith("### Dragon Warrior\r\n"))
                throw new Exception("Copied code has no library title");
            if(((Deck)parse.Invoke(null, new object[] { copiedDeckCode })).Name != "Dragon Warrior")
                throw new Exception("Copied deck code did not preserve its title on import");
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
    (Join-Path $binPath 'HearthstoneDeckTracker.exe'), (Join-Path $binPath 'HearthDb.dll'), (Join-Path $binPath 'HearthMirror.dll'),
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
