using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using HearthDb.Enums;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Stats;
using Newtonsoft.Json.Linq;
using StandardTracker.Library;
using Card = Hearthstone_Deck_Tracker.Hearthstone.Card;

namespace Hearthstone_Deck_Tracker.Library;

// Compatibility boundary for the inherited WPF/game scaffold. Only the native
// StandardTracker.Library contract is serialized in the application profile.
internal static class LibraryRuntimeAdapter
{
	private static readonly string[] AdditionalMatchFields =
	{
		"BattlegroundsRating", "BattlegroundsRatingAfter", "BattlegroundsRaces", "BattlegroundsDetails",
		"MercenariesRating", "MercenariesRatingAfter", "MercenariesBountyRunId", "MercenariesBountyRunTurnsTaken",
		"MercenariesBountyRunCompletedNodes", "MercenariesBountyRunRewards", "PlayerCardbackId", "OpponentCardbackId",
		"FriendlyPlayerId", "OpponentPlayerId", "ScenarioId", "ServerInfo", "BrawlSeasonId", "ArenaSeasonId",
		"ArenaWins", "ArenaLosses", "ArenaRating", "BrawlWins", "BrawlLosses"
	};

	public static LibraryDocument Capture(DeckList decks, DeckStatsList stats, DefaultDeckStats defaults, Guid libraryId)
	{
		var library = new LibraryDocument
		{
			LibraryId = libraryId,
			Tags = decks.AllTags.ToList(),
			RecentDecks = decks.LastDeckClass.Select(d => new RecentDeck { DeckId = d.Id, HeroClass = d.Class, Title = d.Name }).ToList()
		};
		foreach(var deck in decks.Decks)
		{
			var entry = new LibraryDeck();
			CopyDeck(deck, entry);
			entry.SelectedVersion = CaptureVersion(deck.SelectedVersion);
			entry.History = deck.Versions.Select(CaptureRevision).ToList();
			library.Decks.Add(entry);
		}
		foreach(var group in stats.DeckStats.Values)
			library.Matches.AddRange(group.Games.Select(g => CaptureMatch(g, group.DeckId, group.Name)));
		foreach(var group in defaults.DeckStats)
			library.Matches.AddRange(group.Games.Select(g => CaptureMatch(g, null, group.Name)));
		return library;
	}

	public static LibraryRuntimeState Materialize(LibraryDocument library)
	{
		var state = new LibraryRuntimeState { LibraryId = library.LibraryId };
		state.Decks.LibraryId = library.LibraryId;
		state.Decks.AllTags = library.Tags.Union(new[] { "All", "Favorite", "None" }).ToList();
		state.Decks.LastDeckClass = library.RecentDecks.Select(d => new DeckInfo { Id = d.DeckId, Class = d.HeroClass, Name = d.Title }).ToList();
		foreach(var entry in library.Decks)
		{
			var deck = MaterializeRevision(entry);
			deck.Versions = entry.History.Select(MaterializeRevision).ToList();
			deck.SelectedVersion = MaterializeVersion(entry.SelectedVersion);
			state.Decks.Decks.Add(deck);
		}
		foreach(var entry in library.Matches)
		{
			var game = MaterializeMatch(entry);
			if(entry.DeckId.HasValue)
			{
				if(!state.Stats.DeckStats.TryGetValue(entry.DeckId.Value, out var group))
				{
					group = new DeckStats { DeckId = entry.DeckId.Value, Name = entry.DeckTitle };
					state.Stats.DeckStats.TryAdd(entry.DeckId.Value, group);
				}
				group.Games.Add(game);
			}
			else
			{
				// A missing class still needs a bucket so that local records survive.
				var hero = entry.PlayerClass ?? "Unknown";
				state.Defaults.GetDeckStats(hero)!.Games.Add(game);
			}
		}
		return state;
	}

	private static LibraryDeckRevision CaptureRevision(Deck deck)
	{
		var entry = new LibraryDeckRevision();
		CopyDeck(deck, entry);
		return entry;
	}

	private static void CopyDeck(Deck deck, LibraryDeckRevision entry)
	{
		entry.Id = deck.DeckId;
		entry.Title = deck.Name;
		entry.HeroClass = deck.Class;
		entry.Note = deck.Note;
		entry.SourceUrl = deck.Url;
		entry.Archetype = deck.Archetype;
		entry.EditedAt = deck.LastEdited;
		entry.Archived = deck.Archived;
		entry.HearthstoneDeckId = deck.HsId;
		entry.Kind = deck.StoredArenaFlag == true ? "arena" : deck.StoredDungeonFlag == true ? "dungeon" : deck.StoredDuelsFlag == true ? "duels" : "constructed";
		entry.Version = CaptureVersion(deck.Version);
		entry.Tags = deck.Tags.ToList();
		entry.Cards = deck.Cards.Select(CaptureCard).ToList();
		entry.Sideboards = deck.Sideboards.Select(CaptureSideboard).ToList();
		entry.MissingCards = deck.MissingCards.Select(CaptureCard).ToList();
		if(deck.StoredArenaFlag == true && deck.ArenaReward != null) entry.AdditionalData["arenaReward"] = JToken.FromObject(deck.ArenaReward);
	}

	private static Deck MaterializeRevision(LibraryDeckRevision entry)
	{
		var deck = new Deck
		{
			DeckId = entry.Id, Name = entry.Title, Class = entry.HeroClass, Note = entry.Note, Url = entry.SourceUrl,
			Archetype = entry.Archetype, LastEdited = entry.EditedAt, Archived = entry.Archived, HsId = entry.HearthstoneDeckId,
			Version = MaterializeVersion(entry.Version), Tags = entry.Tags.ToList(),
			Cards = new ObservableCollection<Card>(entry.Cards.Select(MaterializeCard)),
			Sideboards = entry.Sideboards.Select(MaterializeSideboard).ToList(), MissingCards = entry.MissingCards.Select(MaterializeCard).ToList(),
			IsArenaDeck = entry.Kind == "arena", IsDungeonDeck = entry.Kind == "dungeon", IsDuelsDeck = entry.Kind == "duels"
		};
		if(entry.AdditionalData?["arenaReward"] != null)
			deck.ArenaReward = entry.AdditionalData["arenaReward"]!.ToObject<Controls.Stats.ArenaReward>();
		return deck;
	}

	public static LibraryMatch CaptureMatch(GameStats game, Guid? deckId, string? deckTitle)
	{
		var entry = new LibraryMatch
		{
			Id = game.GameId, DeckId = deckId, DeckTitle = game.StoredDeckName ?? deckTitle,
			DeckVersion = game.PlayerDeckVersion == null ? null : CaptureVersion(game.PlayerDeckVersion),
			StartedAt = game.StartTime, EndedAt = game.EndTime, Mode = game.GameMode.ToString(), Format = game.Format?.ToString(),
			Result = game.Result.ToString(), Region = game.Region.ToString(), PlayerClass = game.PlayerHero, OpponentClass = game.OpponentHero,
			PlayerName = game.PlayerName, OpponentName = game.OpponentName, PlayerHeroCardId = game.PlayerHeroCardId,
			OpponentHeroCardId = game.OpponentHeroCardId, PlayerHeroClasses = game.PlayerHeroClasses, OpponentHeroClasses = game.OpponentHeroClasses,
			HadCoin = game.Coin, Conceded = game.WasConceded, Turns = game.Turns, Note = game.Note, IsClone = game.IsClone,
			ReplayFile = game.ReplayFile, HearthstoneDeckId = game.HsDeckId, HearthstoneBuild = game.HearthstoneBuild,
			HearthstoneGameType = (int)game.GameType, RankedSeasonId = game.RankedSeasonId, Reconnected = game.IsReconnect,
			Rank = new LibraryRank
			{
				LegacyRank = game.Rank, StarLevel = game.StarLevel, StarLevelAfter = game.StarLevelAfter, Stars = game.Stars,
				StarsAfter = game.StarsAfter, Legend = game.LegendRank, LegendAfter = game.LegendRankAfter, League = game.LeagueId, Multiplier = game.StarMultiplier
			},
			OpponentRank = new LibraryRank { LegacyRank = game.OpponentRank, StarLevel = game.OpponentStarLevel, Legend = game.OpponentLegendRank },
			ImportedFrom = game.ImportSource == null && game.ImportId == null ? null : new ImportIdentity { Tracker = game.ImportSource, MatchId = game.ImportId },
			PlayerCards = game.PlayerCards.Select(CaptureTrackedCard).ToList(), OpponentCards = game.OpponentCards.Select(CaptureTrackedCard).ToList(),
			PlayerSideboards = game.PlayerSideboards.Select(CaptureSideboard).ToList()
		};
		foreach(var name in AdditionalMatchFields)
		{
			var value = typeof(GameStats).GetProperty(name)!.GetValue(game);
			if(value != null) entry.AdditionalData[name] = JToken.FromObject(value);
		}
		entry.AdditionalData["replayUpload"] = new JObject
		{
			["id"] = game.HsReplay.UploadId, ["tries"] = game.HsReplay.UploadTries,
			["unsupported"] = game.HsReplay.Unsupported, ["url"] = game.HsReplay.ReplayUrl
		};
		return entry;
	}

	public static GameStats MaterializeMatch(LibraryMatch entry)
	{
		var game = new GameStats
		{
			GameId = entry.Id, DeckId = entry.DeckId ?? Guid.Empty, DeckName = entry.DeckTitle,
			PlayerDeckVersion = entry.DeckVersion == null ? null : MaterializeVersion(entry.DeckVersion),
			StartTime = entry.StartedAt, EndTime = entry.EndedAt, GameMode = Parse<GameMode>(entry.Mode),
			Format = entry.Format == null ? null : Parse<Enums.Format>(entry.Format), Result = Parse<GameResult>(entry.Result), Region = Parse<Region>(entry.Region),
			PlayerHero = entry.PlayerClass, OpponentHero = entry.OpponentClass, PlayerName = entry.PlayerName, OpponentName = entry.OpponentName,
			PlayerHeroCardId = entry.PlayerHeroCardId, OpponentHeroCardId = entry.OpponentHeroCardId,
			PlayerHeroClasses = entry.PlayerHeroClasses, OpponentHeroClasses = entry.OpponentHeroClasses,
			Coin = entry.HadCoin, WasConceded = entry.Conceded, Turns = entry.Turns, Note = entry.Note, IsClone = entry.IsClone,
			ReplayFile = entry.ReplayFile, HsDeckId = entry.HearthstoneDeckId, HearthstoneBuild = entry.HearthstoneBuild,
			GameType = (GameType)entry.HearthstoneGameType, RankedSeasonId = entry.RankedSeasonId, IsReconnect = entry.Reconnected,
			Rank = entry.Rank.LegacyRank, StarLevel = entry.Rank.StarLevel, StarLevelAfter = entry.Rank.StarLevelAfter,
			Stars = entry.Rank.Stars, StarsAfter = entry.Rank.StarsAfter, LegendRank = entry.Rank.Legend, LegendRankAfter = entry.Rank.LegendAfter,
			LeagueId = entry.Rank.League, StarMultiplier = entry.Rank.Multiplier, OpponentRank = entry.OpponentRank.LegacyRank,
			OpponentStarLevel = entry.OpponentRank.StarLevel, OpponentLegendRank = entry.OpponentRank.Legend,
			ImportSource = entry.ImportedFrom?.Tracker, ImportId = entry.ImportedFrom?.MatchId,
			PlayerCards = entry.PlayerCards.Select(MaterializeTrackedCard).ToList(), OpponentCards = entry.OpponentCards.Select(MaterializeTrackedCard).ToList(),
			PlayerSideboards = entry.PlayerSideboards.Select(MaterializeSideboard).ToList()
		};
		foreach(var name in AdditionalMatchFields)
			if(entry.AdditionalData?[name] is { } value)
			{
				var property = typeof(GameStats).GetProperty(name)!;
				property.SetValue(game, value.ToObject(property.PropertyType));
			}
		if(entry.AdditionalData?["replayUpload"] is JObject replay)
		{
			game.HsReplay.UploadId = (string?)replay["id"]; game.HsReplay.UploadTries = (int?)replay["tries"] ?? 0;
			game.HsReplay.Unsupported = (bool?)replay["unsupported"] ?? false; game.HsReplay.ReplayUrl = (string?)replay["url"];
		}
		return game;
	}

	private static T Parse<T>(string value) where T : struct => Enum.TryParse<T>(value, out var parsed) ? parsed : default;
	private static LibraryVersion CaptureVersion(SerializableVersion version) => new() { Major = version.Major, Minor = version.Minor, Patch = version.Revision, Build = version.Build };
	private static SerializableVersion MaterializeVersion(LibraryVersion version) => new(version.Major, version.Minor) { Revision = version.Patch, Build = version.Build };
	private static LibraryCard CaptureCard(Card card) => new() { CardId = card.Id, Quantity = card.Count };
	private static Card MaterializeCard(LibraryCard card) => new(card.CardId) { Count = card.Quantity };
	private static LibraryCard CaptureTrackedCard(TrackedCard card) => new() { CardId = card.Id ?? "", Quantity = card.Count, Unconfirmed = card.Unconfirmed };
	private static TrackedCard MaterializeTrackedCard(LibraryCard card) => new(card.CardId, card.Quantity, card.Unconfirmed);
	private static LibrarySideboard CaptureSideboard(Sideboard sideboard) => new() { OwnerCardId = sideboard.OwnerCardId, Cards = sideboard.Cards.Select(CaptureCard).ToList() };
	private static Sideboard MaterializeSideboard(LibrarySideboard sideboard) => new(sideboard.OwnerCardId, sideboard.Cards.Select(MaterializeCard).ToList());
}

internal sealed class LibraryRuntimeState
{
	public Guid LibraryId { get; set; }
	public DeckList Decks { get; set; } = new();
	public DeckStatsList Stats { get; set; } = new();
	public DefaultDeckStats Defaults { get; set; } = new();
}
