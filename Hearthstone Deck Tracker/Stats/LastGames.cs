#region

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Hearthstone_Deck_Tracker.Annotations;
using Hearthstone_Deck_Tracker.Hearthstone;

#endregion

namespace Hearthstone_Deck_Tracker.Stats
{
	public class LastGames : INotifyPropertyChanged
	{
		private const int MaxGamesCount = 10;
		private List<GameInfo>? _gameInfos;
		private bool _hasGames;

		static LastGames()
		{
		}

		private LastGames()
		{
		}

		public static LastGames Instance { get; } = new LastGames();

		public List<GameInfo> GameInfos => _gameInfos ??= Load();

		public List<GameStats> Games => GetGames();

		public bool HasGames
		{
			get { return _hasGames; }
			private set
			{
				_hasGames = value;
				OnPropertyChanged();
			}
		}

		private List<GameStats> GetGames()
		{
			var remove = new List<GameInfo>();
			var games = new List<GameStats>();
			foreach(var gi in GameInfos)
			{
				DeckStats? stats;
				if(gi.DeckId != Guid.Empty)
					DeckStatsList.Instance.DeckStats.TryGetValue(gi.DeckId, out stats);
				else if(!string.IsNullOrEmpty(gi.Hero))
					stats = DefaultDeckStats.Instance.GetDeckStats(gi.Hero);
				else
					stats = DefaultDeckStats.Instance.DeckStats.FirstOrDefault(x => x.Games.Any(g => g.GameId == gi.GameId));
				if(stats == null)
					remove.Add(gi);
				else
				{
					var game = stats.Games.FirstOrDefault(x => x.GameId == gi.GameId);
					if(game?.HasReplayFile ?? false)
						games.Add(game);
					else
						remove.Add(gi);
				}
				
			}
			foreach(var game in remove)
				_gameInfos?.Remove(game);
			HasGames = games.Any();
			return games;
		} 

		public void Add(GameStats game) => Add(game.DeckId, game.GameId, game.PlayerHero);

		public void Add(Guid deckId, Guid gameId, string? hero)
		{
			GameInfos.Insert(0, new GameInfo(deckId, gameId, hero));
			if(GameInfos.Count > MaxGamesCount)
				GameInfos.RemoveAt(MaxGamesCount);
			OnPropertyChanged(nameof(Games));
		}

		public void Remove(GameStats game) => Remove(game.GameId);

		public void Remove(Guid gameId)
		{
			var gameInfo = GameInfos.FirstOrDefault(x => x.GameId == gameId);
			if(gameInfo != null)
				GameInfos.Remove(gameInfo);
			OnPropertyChanged(nameof(Games));
		}

		public void RemoveDeck(Deck deck) => RemoveDeck(deck.DeckId);

		public void RemoveDeck(DeckStats deck) => RemoveDeck(deck.DeckId);

		public void RemoveDeck(Guid deckId)
		{
			if(deckId == Guid.Empty)
				return;
			var games = GameInfos.Where(x => x.DeckId == deckId).ToList();
			foreach(var game in games)
				GameInfos.Remove(game);
			OnPropertyChanged(nameof(Games));
		}

		// Recent replays are a view of the native match library, not a second store.
		public static void Save() => Refresh(DeckStatsList.Instance, DefaultDeckStats.Instance);

		internal static void Refresh(DeckStatsList stats, DefaultDeckStats defaults)
		{
			Instance._gameInfos = FromStatistics(stats, defaults);
			Instance.OnPropertyChanged(nameof(Games));
		}

		private static List<GameInfo> Load() => FromStatistics(DeckStatsList.Instance, DefaultDeckStats.Instance);
		private static List<GameInfo> FromStatistics(DeckStatsList stats, DefaultDeckStats defaults) => stats.DeckStats.Values
			.Concat(defaults.DeckStats).SelectMany(s => s.Games)
			.Where(g => g.HasReplayFile).OrderByDescending(g => g.StartTime).Take(MaxGamesCount)
			.Select(g => new GameInfo(g.DeckId, g.GameId, g.PlayerHero)).ToList();

		public event PropertyChangedEventHandler? PropertyChanged;

		[NotifyPropertyChangedInvocator]
		protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	public class GameInfo
	{
		public GameInfo()
		{
		}

		public GameInfo(Guid deckId, Guid gameId, string? hero = null)
		{
			DeckId = deckId;
			GameId = gameId;
			if(DeckId == Guid.Empty)
				Hero = hero;
		}

		public Guid DeckId { get; set; }

		public Guid GameId { get; set; }

		public string? Hero { get; set; }
	}
}
