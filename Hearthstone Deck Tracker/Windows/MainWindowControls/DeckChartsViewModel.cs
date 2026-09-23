using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Stats;
using Hearthstone_Deck_Tracker.Utility.MVVM;

namespace Hearthstone_Deck_Tracker.Windows.MainWindowControls
{
	public class DeckChartsViewModel : ViewModel
	{
		private Deck? _deck;
		private double _winrateTotal;
		private bool _hasData;
		private List<GameStats> _games = new List<GameStats>();
		private List<GameStats> _filteredGames = new List<GameStats>();
		private int _wins;
		private int _losses;
		private bool _hasDeck;
		private string _resultFilter = "all";

		private static readonly Brush WinSegment = new SolidColorBrush(Color.FromRgb(0x80, 0xD8, 0xB0));
		private static readonly Brush LossSegment = new SolidColorBrush(Color.FromRgb(0xED, 0x93, 0x9E));
		private static readonly Brush WinIconBg = new SolidColorBrush(Color.FromRgb(0x21, 0x3A, 0x35));
		private static readonly Brush LossIconBg = new SolidColorBrush(Color.FromRgb(0x3A, 0x29, 0x33));

		static DeckChartsViewModel()
		{
			WinSegment.Freeze();
			LossSegment.Freeze();
			WinIconBg.Freeze();
			LossIconBg.Freeze();
		}

		public Deck? Deck
		{
			get => _deck;
			set
			{
				if(_deck != null)
					_deck.OnStatsUpdated -= Update;
				_deck = value;
				if(_deck != null)
					_deck.OnStatsUpdated += Update;
				HasDeck = _deck != null;
				Update();
				OnPropertyChanged();
			}
		}

		public List<GameStats> Games
		{
			get => _games;
			set
			{
				_games = value;
				OnPropertyChanged();
			}
		}

		public List<GameStats> FilteredGames
		{
			get => _filteredGames;
			set
			{
				_filteredGames = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(ShownCountLabel));
			}
		}

		public ObservableCollection<Brush> RecentResults { get; } = new();

		public int Wins
		{
			get => _wins;
			set
			{
				_wins = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(TotalGames));
			}
		}

		public int Losses
		{
			get => _losses;
			set
			{
				_losses = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(TotalGames));
			}
		}

		public int TotalGames => Wins + Losses;

		public bool HasData
		{
			get => _hasData;
			set
			{
				_hasData = value;
				OnPropertyChanged();
			}
		}

		public double WinrateTotal
		{
			get => _winrateTotal;
			set
			{
				_winrateTotal = value;
				OnPropertyChanged();
			}
		}

		public bool HasDeck
		{
			get => _hasDeck;
			set
			{
				_hasDeck = value;
				OnPropertyChanged();
			}
		}

		public bool FilterAll
		{
			get => _resultFilter == "all";
			set
			{
				if(value)
					SetResultFilter("all");
			}
		}

		public bool FilterWins
		{
			get => _resultFilter == "win";
			set
			{
				if(value)
					SetResultFilter("win");
			}
		}

		public bool FilterLosses
		{
			get => _resultFilter == "loss";
			set
			{
				if(value)
					SetResultFilter("loss");
			}
		}

		public string ShownCountLabel => HasData
			? $"Показано {FilteredGames.Count} из {Games.Count}"
			: "";

		public static Brush GetResultAccent(GameResult result)
			=> result == GameResult.Win ? WinSegment : LossSegment;

		public static Brush GetResultIconBackground(GameResult result)
			=> result == GameResult.Win ? WinIconBg : LossIconBg;

		public static string GetResultGlyph(GameResult result)
			=> result == GameResult.Win ? "✓" : "−";

		public void Update()
		{
			Games = _deck?.GetRelevantGames().OrderByDescending(x => x.StartTime).ToList() ?? new List<GameStats>();
			HasData = Games.Any();

			if(!HasData)
			{
				Wins = 0;
				Losses = 0;
				WinrateTotal = 0;
				FilteredGames = new List<GameStats>();
				RecentResults.Clear();
				return;
			}

			Wins = Games.Count(g => g.Result == GameResult.Win);
			Losses = Games.Count(g => g.Result == GameResult.Loss);
			var total = Wins + Losses;
			WinrateTotal = total > 0 ? Math.Round(100.0 * Wins / total, 1) : 0;

			RecentResults.Clear();
			foreach(var game in Games.Take(24).Reverse())
				RecentResults.Add(game.Result == GameResult.Loss ? LossSegment : WinSegment);

			ApplyFilter();
			OnPropertyChanged(nameof(FilterAll));
			OnPropertyChanged(nameof(FilterWins));
			OnPropertyChanged(nameof(FilterLosses));
		}

		private void SetResultFilter(string filter)
		{
			_resultFilter = filter;
			ApplyFilter();
			OnPropertyChanged(nameof(FilterAll));
			OnPropertyChanged(nameof(FilterWins));
			OnPropertyChanged(nameof(FilterLosses));
		}

		private void ApplyFilter()
		{
			IEnumerable<GameStats> query = Games;
			if(_resultFilter == "win")
				query = query.Where(g => g.Result == GameResult.Win);
			else if(_resultFilter == "loss")
				query = query.Where(g => g.Result == GameResult.Loss);
			FilteredGames = query.ToList();
		}
	}
}
