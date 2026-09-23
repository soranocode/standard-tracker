using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
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
		private int _periodFilter;

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
				OnPropertyChanged(nameof(HasFilteredGames));
				OnPropertyChanged(nameof(DayGroups));
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
		public string WinrateLabel => TotalGames == 0 ? "—" : WinrateTotal.ToString("0.#", CultureInfo.GetCultureInfo("ru-RU")) + "%";
		public IEnumerable<MatchHistoryDay> DayGroups => FilteredGames.GroupBy(g => g.StartTime.Date)
			.Select(g => new MatchHistoryDay(g.Key, g.ToList()));

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
				OnPropertyChanged(nameof(WinrateLabel));
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
			? $"Показано {FilteredGames.Count} из {TotalGames}"
			: "";

		public int PeriodFilter
		{
			get => _periodFilter;
			set
			{
				if(_periodFilter == value)
					return;
				_periodFilter = value;
				ApplyFilter();
				OnPropertyChanged();
			}
		}

		public bool HasFilteredGames => FilteredGames.Count > 0;

		public static Brush GetResultAccent(GameResult result)
			=> result == GameResult.Win ? WinSegment : LossSegment;

		public static Brush GetResultIconBackground(GameResult result)
			=> result == GameResult.Win ? WinIconBg : LossIconBg;

		public static string GetResultGlyph(GameResult result)
			=> result == GameResult.Win ? "✓" : "−";

		public void Update()
		{
			Games = _deck?.GetRelevantGames()
				.Where(x => x.Result == GameResult.Win || x.Result == GameResult.Loss)
				.OrderByDescending(x => x.StartTime).ToList() ?? new List<GameStats>();
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
			if(_periodFilter == 1)
				query = query.Where(g => g.StartTime.Date >= DateTime.Today.AddDays(-6));
			else if(_periodFilter == 2)
				query = query.Where(g => g.StartTime.Date == DateTime.Today);
			var periodGames = query.ToList();
			Wins = periodGames.Count(g => g.Result == GameResult.Win);
			Losses = periodGames.Count(g => g.Result == GameResult.Loss);
			WinrateTotal = TotalGames == 0 ? 0 : Math.Round(100.0 * Wins / TotalGames, 1);
			RecentResults.Clear();
			foreach(var game in periodGames.Take(24).Reverse())
				RecentResults.Add(GetResultAccent(game.Result));
			if(_resultFilter == "win")
				query = query.Where(g => g.Result == GameResult.Win);
			else if(_resultFilter == "loss")
				query = query.Where(g => g.Result == GameResult.Loss);
			FilteredGames = query.ToList();
		}
	}

	public class MatchHistoryDay
	{
		public MatchHistoryDay(DateTime date, List<GameStats> games)
		{
			Label = date == DateTime.Today ? "Сегодня" : date == DateTime.Today.AddDays(-1) ? "Вчера"
				: date.ToString("d MMMM", CultureInfo.GetCultureInfo("ru-RU"));
			Games = games;
		}
		public string Label { get; }
		public List<GameStats> Games { get; }
		public string CountLabel => $"Матчей: {Games.Count}";
	}
}
