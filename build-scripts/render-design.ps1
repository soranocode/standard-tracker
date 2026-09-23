# Render the actual WPF workspace with synthetic data, without opening a window or touching the user's profile.
param([int]$Width = 1440, [int]$Height = 760, [string]$Output = 'design/workspace-review.png', [switch]$Empty, [switch]$Expanded)
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
$binPath = Join-Path $repoPath 'Hearthstone Deck Tracker/bin/x64/Debug'
$outputPath = [IO.Path]::GetFullPath((Join-Path $repoPath $Output))
$source = @'
using System;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Stats;
using Hearthstone_Deck_Tracker.Controls;
using Hearthstone_Deck_Tracker.Controls.DeckPicker;
using Hearthstone_Deck_Tracker.Windows.MainWindowControls;
class DesignRender {
    [STAThread] static int Main(string[] args) {
        try {
            typeof(Application).GetField("_resourceAssembly", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, typeof(App).Assembly);
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            Directory.CreateDirectory(args[0]);
            typeof(Config).GetField("AppDataPath").SetValue(null, args[0]);
            Config.Load();
            Config.Instance.Localization = Language.ruRU;
            HearthDb.Config.AutoLoadCardDefs = false;
            HearthDb.Cards.LoadBaseData(HearthDb.Cards.GetBundledBaseData());
            var app = new App(); app.InitializeComponent();
            Hearthstone_Deck_Tracker.Utility.LocUtil.UpdateCultureInfo();
            System.Threading.SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            var window = Core.MainWindow;
            var picker = (DeckPicker)window.FindName("DeckPickerList");
            var panel = (SelectedDeckPanel)window.FindName("SelectedDeckPanel");
            var history = (DeckChartsView)window.FindName("DeckCharts");
            var surface = (FrameworkElement)window.Content;
            int width = int.Parse(args[2]), height = int.Parse(args[3]);
            surface.Width = width; surface.Height = height;
            surface.Measure(new Size(width, height)); surface.Arrange(new Rect(0, 0, width, height)); surface.UpdateLayout();
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => {}));
            if(args.Length < 5 || args[4] != "empty") {
                var names = new[] { "Драконы на рассвете", "Ледяной контроль", "Темпо маг", "Контроль воин" };
                var classes = new[] { "Druid", "Mage", "Mage", "Warrior" };
                var archetypes = new[] { "Драконы", "Контроль", "Темпо", "Контроль" };
                for(int i = 0; i < names.Length; i++) {
                    var deck = new Deck { Name = names[i], Class = classes[i], Archetype = archetypes[i] };
                    foreach(var card in HearthDb.Cards.All.Values.Where(c => c.Set == HearthDb.Enums.CardSet.CORE && c.Collectible && (c.Class == HearthDb.Enums.CardClass.NEUTRAL || c.Class == HearthDb.Enums.CardClass.DRUID)).GroupBy(c => c.Cost).OrderBy(g => g.Key).SelectMany(g => g.OrderByDescending(c => c.Class == HearthDb.Enums.CardClass.DRUID).Take(2)).Take(15))
                        deck.Cards.Add(new Hearthstone_Deck_Tracker.Hearthstone.Card(card) { Count = 2 });
                    DeckList.Instance.Decks.Add(deck);
                }
                picker.SelectedClasses.Clear(); picker.SelectedClasses.Add(HeroClassAll.All);
                Config.Instance.SelectedTags = new List<string> { "All" };
                Config.Instance.SelectedDeckPickerDeckType = DeckType.Standard;
                picker.UpdateDecks();
                var selected = DeckList.Instance.Decks[0];
                panel.SetDeck(selected);
                window.GetType().GetProperty("SelectedDeckName").SetValue(window, selected.Name, null);
                window.GetType().GetMethod("OnPropertyChanged", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new[] { typeof(string) }, null).Invoke(window, new object[] { "SelectedDeckName" });
                var model = (DeckChartsViewModel)history.DataContext;
                model.HasDeck = true;
                model.Games = Enumerable.Range(0, 8).Select(i => new GameStats {
                    OpponentHero = classes[i % classes.Length], Result = i % 3 == 0 ? GameResult.Loss : GameResult.Win,
                    StartTime = DateTime.Today.AddDays(-i / 3).AddHours(18).AddMinutes(-i * 13),
                    EndTime = DateTime.Today.AddDays(-i / 3).AddHours(18).AddMinutes(-i * 13 + 9),
                    Turns = 7 + i, Coin = i % 2 == 0, Note = i == 0 ? "Не хватило ответа на последнюю угрозу." : null
                }).ToList();
                model.HasData = true; model.FilterAll = true;
                model.PeriodFilter = 2;
                if(model.TotalGames != 3 || model.FilteredGames.Count != 3) throw new Exception("Period summary does not match the list");
                model.FilterWins = true;
                if(model.FilteredGames.Any(g => g.Result != GameResult.Win)) throw new Exception("Result filter failed");
                model.PeriodFilter = 0; model.FilterAll = true;
            } else { panel.SetDeck(null); }
            surface.Measure(new Size(width, height)); surface.Arrange(new Rect(0, 0, width, height)); surface.UpdateLayout();
            panel.UpdateValues();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            var frame = new DispatcherFrame(); timer.Tick += (s,e) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame);
            surface.UpdateLayout();
            if(args.Length > 4 && args[4] == "expanded") {
                var match = Helper.FindVisualChildren<Expander>(history).FirstOrDefault();
                if(match != null) match.IsExpanded = true;
                surface.UpdateLayout();
            }
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(surface);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
            using(var output = File.Create(args[1])) png.Save(output);
            Console.WriteLine("Rendered " + args[1]); return 0;
        } catch(Exception e) { Console.WriteLine(e); return 1; }
    }
}
'@
$framework = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$sourcePath = Join-Path $binPath 'StandardTracker.DesignRender.cs'
$exePath = Join-Path $binPath 'StandardTracker.DesignRender.exe'
Set-Content -LiteralPath $sourcePath -Value $source -Encoding UTF8
$netstandardPath = (Get-ChildItem (Join-Path $repoPath 'packages/microsoft.netframework.referenceassemblies.net472') -Filter netstandard.dll -Recurse | Select-Object -First 1).FullName
$references = @((Join-Path $framework 'WPF/PresentationFramework.dll'), $netstandardPath,
    (Join-Path $framework 'WPF/PresentationCore.dll'), (Join-Path $framework 'WPF/WindowsBase.dll'),
    (Join-Path $framework 'System.Xaml.dll'), (Join-Path $framework 'System.Core.dll'),
    (Join-Path $binPath 'HearthstoneDeckTracker.exe'), (Join-Path $binPath 'HearthDb.dll'),
    (Join-Path $binPath 'MahApps.Metro.dll')) | ForEach-Object { '/reference:' + $_ }
& (Join-Path $framework 'csc.exe') /nologo /target:exe /platform:x64 "/out:$exePath" @references $sourcePath
if($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$profilePath = Join-Path ([IO.Path]::GetTempPath()) ('standard-design-' + [Guid]::NewGuid())
if($Empty) { & $exePath $profilePath $outputPath $Width $Height empty } elseif($Expanded) { & $exePath $profilePath $outputPath $Width $Height expanded } else { & $exePath $profilePath $outputPath $Width $Height }
exit $LASTEXITCODE
