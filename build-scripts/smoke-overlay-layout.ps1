# Run after an x64 build, with Windows PowerShell (-STA).
# Exercises the actual WPF player pane in an offscreen host and a fresh test profile.
param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
$binPath = [IO.Path]::GetFullPath((Join-Path $repoPath "Hearthstone Deck Tracker/bin/x64/$Configuration"))
if (!(Test-Path -LiteralPath (Join-Path $binPath 'HearthstoneDeckTracker.exe'))) {
    throw "Build the x64 $Configuration tracker before running this smoke test."
}
$profilePath = Join-Path ([IO.Path]::GetTempPath()) ('standard-overlay-layout-' + [Guid]::NewGuid())
$sourcePath = Join-Path $binPath 'StandardTracker.OverlayLayoutSmoke.cs'
$outputPath = Join-Path $binPath 'StandardTracker.OverlayLayoutSmoke.exe'
$source = @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.Controls;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Utility.Extensions;
using Hearthstone_Deck_Tracker.Utility.Themes;
using HCard = Hearthstone_Deck_Tracker.Hearthstone.Card;

class OverlayLayoutSmoke
{
    // The Framework compiler cannot read Card.Id's init-only accessor directly.
    static string Id(HCard card) { return (string)typeof(HCard).GetProperty("Id").GetValue(card, null); }
    static HCard Snapshot(HCard card)
    {
        var copy = (HCard)card.Clone();
        copy.IsCreated = card.IsCreated;
        copy.HighlightInHand = card.HighlightInHand;
        return copy;
    }
    static void Pump(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (sender, args) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }
    static void Complete(Task task)
    {
        var timeout = Stopwatch.StartNew();
        while(!task.IsCompleted)
        {
            if(timeout.Elapsed > TimeSpan.FromSeconds(10))
                throw new TimeoutException("AnimatedCardList.Update did not finish.");
            Pump(20);
        }
        task.GetAwaiter().GetResult();
        Pump(200);
    }
    static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for(var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if(child is T) yield return (T)child;
            foreach(var result in Descendants<T>(child)) yield return result;
        }
    }
    static void Check(AnimatedCardList list, StackPanel pane, Canvas counter, Border bounds,
        List<HCard> cards, string stage, bool reset = false)
    {
        var ordered = cards.ToSortedCardList();
        // Keep expected objects separate: Update mutates its displayed Card instances.
        Complete(list.Update(ordered.Select(Snapshot).ToList(), reset));
        bounds.UpdateLayout();
        if(list.AnimatedCards.Count != ordered.Count)
            throw new Exception(stage + ": row count mismatch");
        for(var index = 0; index < ordered.Count; index++)
        {
            var actual = list.AnimatedCards[index].Card;
            var expected = ordered[index];
            if(actual == null || Id(actual) != Id(expected) || actual.Count != expected.Count
                || actual.IsCreated != expected.IsCreated)
                throw new Exception(stage + ": card/count/created/order mismatch at " + index);
        }
        var scroll = Descendants<PassiveScrollViewer>(list).Single();
        var counterBottom = counter.TransformToAncestor(bounds).Transform(new Point(0, counter.ActualHeight)).Y;
        if(pane.ActualHeight > bounds.ActualHeight + 0.1 || counterBottom > bounds.ActualHeight + 0.1)
            throw new Exception(stage + ": panel/counter overflow");
        if(list.ViewModel.MaxHeightCard < 24 || list.ViewModel.MaxHeightCard > 34)
            throw new Exception(stage + ": invalid row size");
        Console.WriteLine(string.Format(
            "{0}: rows={1}; rowHeight={2:F2}; panelHeight={3:F2}/{4:F2}; viewport={5:F2}; extent={6:F2}; scrollable={7:F2}; counterBottom={8:F2}; order/count/created PASS",
            stage, ordered.Count, list.ViewModel.MaxHeightCard, pane.ActualHeight, bounds.ActualHeight,
            scroll.ViewportHeight, scroll.ExtentHeight, scroll.ScrollableHeight, counterBottom));
        if(stage == "50-rows")
        {
            if(scroll.ScrollableHeight <= 0) throw new Exception("Expected overflow scrolling.");
            scroll.ScrollToBottom();
            Pump(100);
            if(Math.Abs(scroll.VerticalOffset - scroll.ScrollableHeight) > 0.1)
                throw new Exception("Cannot reach last row.");
            Console.WriteLine("Scroll to last row PASS");
            scroll.ScrollToTop();
        }
    }
    [STAThread] static int Main(string[] args)
    {
        try
        {
            typeof(Application).GetField("_resourceAssembly", BindingFlags.NonPublic | BindingFlags.Static)
                .SetValue(null, typeof(App).Assembly);
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            Directory.CreateDirectory(args[0]);
            typeof(Config).GetField("AppDataPath").SetValue(null, args[0]);
            Config.Load();
            Config.Instance.LastSeenHearthstoneLang = "enUS";
            Config.Instance.OverlayCardAnimations = false;
            HearthDb.Config.AutoLoadCardDefs = false;
            HearthDb.Cards.LoadBaseData(HearthDb.Cards.GetBundledBaseData());
            var app = new App();
            app.InitializeComponent();
            // Pumping the host must not start the real tracker, log readers, or real overlay.
            app.Startup -= (StartupEventHandler)Delegate.CreateDelegate(typeof(StartupEventHandler), app,
                typeof(App).GetMethod("App_OnStartup", BindingFlags.NonPublic | BindingFlags.Instance));
            System.Threading.SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            ThemeManager.Run();
            var overlay = Core.Overlay;
            var pane = (StackPanel)overlay.FindName("StackPanelPlayer");
            ((Border)pane.Parent).Child = null;
            pane.RenderTransform = Transform.Identity;
            pane.Opacity = 1;
            pane.Visibility = Visibility.Visible;
            pane.VerticalAlignment = VerticalAlignment.Top;
            foreach(UIElement child in pane.Children) child.Visibility = Visibility.Collapsed;
            var list = (AnimatedCardList)overlay.FindName("ListViewPlayer");
            list.Visibility = Visibility.Visible;
            var title = (HearthstoneTextBlock)overlay.FindName("LblDeckTitle");
            title.Visibility = Visibility.Visible;
            title.Text = "Overlay layout smoke";
            var counter = (Canvas)overlay.FindName("CanvasPlayerCount");
            counter.Visibility = Visibility.Visible;
            var bounds = new Border { Height = 600, Width = 218, Child = pane,
                Background = new SolidColorBrush(Color.FromRgb(24, 29, 37)) };
            var host = new Window { Content = bounds, Width = 250, Height = 650, ShowActivated = false,
                ShowInTaskbar = false, Left = -10000, Top = -10000, WindowStyle = WindowStyle.None };
            host.Show();
            Pump(100);

            var utility = typeof(Deck).Assembly.GetType("Hearthstone_Deck_Tracker.Hearthstone.DeckCodeUtility");
            var deck = (Deck)utility.GetMethod("Import", BindingFlags.Public | BindingFlags.Static).Invoke(null,
                new object[] { "AAECAQcC69YHstgHDuPmBqr8Bqv8BqWFB+iHB9KXB7etB+yyB7XAB5XCB5vCB5zCB6ngB/vgBwAA" });
            if(deck == null || deck.Cards.Count != 16) throw new Exception("Expected 16-row sample deck.");
            var cards = deck.Cards.ToList();
            var baseCards = cards.ToList();
            Check(list, pane, counter, bounds, cards, "initial-16-rows", true);
            var additions = HearthDb.Cards.Collectible.Values
                .Where(card => !baseCards.Any(existing => Id(existing) == card.Id))
                .Take(40).Select(card => new HCard(card) { Count = 1, IsCreated = true }).ToList();
            if(additions.Count != 40) throw new Exception("Insufficient distinct fixture cards.");
            cards.Add(additions[0]);
            Check(list, pane, counter, bounds, cards, "new-card-17-rows");
            additions[0].Count = 4;
            Check(list, pane, counter, bounds, cards, "same-card-4-copies");
            var createdCopy = Snapshot(baseCards[0]);
            createdCopy.IsCreated = true;
            createdCopy.Count = 1;
            cards.Add(createdCopy);
            Check(list, pane, counter, bounds, cards, "created-copy-of-original");
            cards.AddRange(additions.Skip(1).Take(12));
            Check(list, pane, counter, bounds, cards, "30-rows");
            cards.AddRange(additions.Skip(13).Take(20));
            Check(list, pane, counter, bounds, cards, "50-rows");
            Check(list, pane, counter, bounds, baseCards, "return-to-16-rows");
            Check(list, pane, counter, bounds, new List<HCard> { createdCopy, baseCards[0] }, "reset-created-before-original", true);
            Check(list, pane, counter, bounds, new List<HCard> { baseCards[0], createdCopy }, "reset-original-before-created", true);
            Check(list, pane, counter, bounds, new List<HCard> { createdCopy }, "reset-only-created", true);
            Check(list, pane, counter, bounds, new List<HCard> { createdCopy, baseCards[0] }, "insert-original-after-created");
            createdCopy.Count = 3;
            Check(list, pane, counter, bounds, new List<HCard> { createdCopy, baseCards[0] }, "update-created-count-only");
            Check(list, pane, counter, bounds, new List<HCard> { baseCards[0] }, "remove-created-only");
            host.Close();
            Console.WriteLine("All dynamic overlay layout checks PASS");
            return 0;
        }
        catch(Exception error) { Console.WriteLine(error); return 1; }
    }
}
'@
Set-Content -LiteralPath $sourcePath -Value $source -Encoding UTF8
$frameworkPath = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$netstandardPath = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies/Microsoft/Framework/.NETFramework/v4.7.2/Facades/netstandard.dll'
if (!(Test-Path -LiteralPath $netstandardPath)) {
    $netstandardPath = (Get-ChildItem (Join-Path $repoPath 'packages/microsoft.netframework.referenceassemblies.net472') -Filter netstandard.dll -Recurse | Select-Object -First 1).FullName
}
$references = @((Join-Path $frameworkPath 'WPF/PresentationFramework.dll'), $netstandardPath,
    (Join-Path $frameworkPath 'WPF/PresentationCore.dll'), (Join-Path $frameworkPath 'WPF/WindowsBase.dll'),
    (Join-Path $frameworkPath 'System.Xaml.dll'), (Join-Path $frameworkPath 'System.Core.dll'),
    (Join-Path $binPath 'HearthstoneDeckTracker.exe'), (Join-Path $binPath 'HearthDb.dll'),
    (Join-Path $binPath 'HearthMirror.dll')) | ForEach-Object { '/reference:' + $_ }
& (Join-Path $frameworkPath 'csc.exe') /nologo /target:exe /platform:x64 "/out:$outputPath" @references $sourcePath
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $outputPath $profilePath
exit $LASTEXITCODE
