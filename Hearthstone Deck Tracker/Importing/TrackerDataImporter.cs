using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Stats;
using Hearthstone_Deck_Tracker.Library;
using StandardTracker.Library;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Hearthstone_Deck_Tracker.Importing;

internal static class TrackerDataImporter
{
	private static readonly string[] Classes = { "Deathknight", "DemonHunter", "Druid", "Hunter", "Mage", "Paladin", "Priest", "Rogue", "Shaman", "Warlock", "Warrior" };

	public static TrackerImportData ReadHdt(string directory) => HdtProfileReader.Read(directory);

	public static TrackerImportData ReadFirestone(string path)
	{
		if(new[] { ".sqlite", ".db", ".sqlite3" }.Contains(Path.GetExtension(path).ToLowerInvariant()))
			return ReadFirestoneJson(FirestoneSqliteReader.ReadHistory(path));
		using(var reader = File.OpenText(path))
		using(var json = new JsonTextReader(reader) { DateParseHandling = DateParseHandling.None, MaxDepth = 64 })
		{
			var root = JToken.Load(json);
			if(json.Read()) throw new InvalidDataException("После JSON-истории обнаружены лишние данные.");
			return ReadFirestoneJson(root);
		}
	}

	internal static TrackerImportData ReadFirestoneJson(JToken root)
	{
		var container = root as JObject;
		var rows = root as JArray ?? container?["stats"] as JArray ?? container?["matchHistory"] as JArray;
		// Also accept a standard Dexie export of FirestoneDB.
		if(rows == null && container?["data"] is JObject export && export["data"] is JArray tables)
			rows = tables.OfType<JObject>().FirstOrDefault(t => (string?)t["tableName"] == "matchHistory")?["rows"] as JArray;
		if(rows == null)
			throw new InvalidDataException("Не найдена история Firestone. Нужен JSON с массивом матчей, stats или таблицей matchHistory.");
		var data = new TrackerImportData();
		var decks = new Dictionary<string, Deck>();
		for(var index = 0; index < rows.Count; index++)
		{
			try
			{
				if(rows[index] is not JObject row)
					throw new InvalidDataException("Ожидалась запись матча.");
				var mode = Text(row, "gameMode").ToLowerInvariant();
				var format = Text(row, "gameFormat").ToLowerInvariant();
				// Keep unsupported rows for the preview's skipped count, without decoding their deck strings.
				var supportedFormat = new[] { "standard", "wild" }.Contains(format) || (mode == "friendly" && string.IsNullOrEmpty(format));
				if(!new[] { "ranked", "casual", "friendly" }.Contains(mode) || !supportedFormat)
				{
					data.Games.Add(new TrackerImportGame { Game = new GameStats { GameMode = GameMode.None } });
					continue;
				}
				var result = Text(row, "result") switch
				{
					"won" => GameResult.Win,
					"lost" => GameResult.Loss,
					"tied" => GameResult.Draw,
					_ => throw new InvalidDataException("Неизвестный результат матча.")
				};
				var timestamp = (long?)row["creationTimestamp"] ?? throw new InvalidDataException("Отсутствует дата матча.");
				var duration = (int?)row["gameDurationSeconds"] ?? 0;
				var turns = (int?)row["gameDurationTurns"] ?? 0;
				if(duration < 0 || turns < 0 || timestamp <= 0)
					throw new InvalidDataException("Неверная дата, длительность или число ходов.");
				// Firestone writes creationTimestamp when the replay finishes.
				var end = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).LocalDateTime;
				var game = new GameStats
				{
					DeckId = Guid.Empty, ImportSource = "Firestone", ImportId = Text(row, "reviewId"),
					GameMode = mode == "ranked" ? GameMode.Ranked : mode == "casual" ? GameMode.Casual : GameMode.Friendly,
					Format = format == "wild" ? Format.Wild : Format.Standard, Result = result,
					StartTime = end.AddSeconds(-duration), EndTime = end, Turns = turns,
					PlayerHero = NormalizeClass(Text(row, "playerClass")), OpponentHero = NormalizeClass(Text(row, "opponentClass")),
					PlayerName = Text(row, "playerName"), OpponentName = Text(row, "opponentName"),
					PlayerHeroCardId = Text(row, "playerCardId"), OpponentHeroCardId = Text(row, "opponentCardId"),
					Coin = Text(row, "coinPlay") == "coin", HearthstoneBuild = (int?)row["buildNumber"],
					Region = ParseRegion(row["region"]), DeckName = Text(row, "playerDeckName")
				};
				if(string.IsNullOrEmpty(game.PlayerHero) && !string.IsNullOrEmpty(game.PlayerHeroCardId))
					game.PlayerHero = NormalizeClass(new Card(game.PlayerHeroCardId!).PlayerClass);
				SetRank(game, Text(row, "playerRank"));
				if(string.IsNullOrEmpty(game.ImportId))
					game.ImportId = Fingerprint(game);
				game.GameId = StableId("firestone:" + game.ImportId);
				var code = Text(row, "playerDecklist");
				Deck? deck = null;
				if(!string.IsNullOrWhiteSpace(code))
				{
					if(!decks.TryGetValue(code, out deck))
					{
						try
						{
							deck = DeckCodeUtility.Import(code).CloneForImport();
							game.PlayerHero ??= NormalizeClass(deck.Class);
							if(!SameClass(deck.Class, game.PlayerHero) || !ValidDeck(deck))
								throw new InvalidDataException("Код колоды не соответствует классу матча.");
							deck.DeckId = StableId("firestone-deck:" + Contents(deck));
							deck.LastEdited = end;
							decks.Add(code, deck);
							data.Decks.Add(deck);
						}
						catch(Exception ex) when(ex is not OutOfMemoryException)
						{
							deck = null;
							Warn(data.Warnings, $"Запись {index + 1}: код колоды не прочитан, матч сохранится в общей статистике.");
						}
					}
					game.PlayerHero ??= NormalizeClass(deck?.Class);
					if(deck != null && !SameClass(deck.Class, game.PlayerHero))
						throw new InvalidDataException("Класс колоды не соответствует матчу.");
					if(deck != null && end >= deck.LastEdited)
					{
						if(!string.IsNullOrWhiteSpace(game.DeckName)) deck.Name = game.DeckName!;
						deck.LastEdited = end;
					}
				}
				if(string.IsNullOrEmpty(game.PlayerHero))
					throw new InvalidDataException("Неизвестный класс игрока.");
				if(deck != null)
				{
					game.DeckId = deck.DeckId;
					game.PlayerDeckVersion = deck.Version;
				}
				data.Games.Add(new TrackerImportGame { Game = game, SourceDeckId = deck?.DeckId });
			}
			catch(Exception ex) when(ex is ArgumentException || ex is FormatException || ex is InvalidDataException || ex is InvalidCastException || ex is OverflowException)
			{
				data.InvalidGames++;
				Warn(data.Warnings, $"Запись {index + 1}: {ex.Message}");
			}
		}
		return data;
	}

	public static TrackerImportPreview Prepare(TrackerImportData data, DeckList localDecks, DeckStatsList localStats, DefaultDeckStats localDefault)
	{
		var preview = new TrackerImportPreview
		{
			Decks = new DeckList
			{
				LibraryId = localDecks.LibraryId,
				Decks = new System.Collections.ObjectModel.ObservableCollection<Deck>(localDecks.Decks.Select(d => d.CloneForImport())),
				AllTags = new List<string>(localDecks.AllTags), LastDeckClass = new List<DeckInfo>(localDecks.LastDeckClass)
			},
			Stats = new DeckStatsList(), DefaultStats = CloneDefaults(localDefault), InvalidGames = data.InvalidGames
		};
		// ConcurrentDictionary is the live source of truth after the profile has loaded.
		preview.Stats.SerializableDeckStats = localStats.DeckStats.Values.Select(CloneStats).ToList();
		preview.Warnings.AddRange(data.Warnings);
		var existingGames = preview.Stats.DeckStats.Values.Concat(preview.DefaultStats.DeckStats).SelectMany(s => s.Games).ToList();
		var ids = new HashSet<Guid>(existingGames.Where(g => g.GameId != Guid.Empty).Select(g => g.GameId));
		var externalIds = new HashSet<string>(existingGames.Where(g => !string.IsNullOrEmpty(g.ImportId)).Select(g => g.ImportSource + ":" + g.ImportId));
		var map = new Dictionary<Guid, Deck>();
		var versions = new Dictionary<string, SerializableVersion>();
		foreach(var source in data.Decks)
		{
			var detachedSource = source.CloneForImport();
			if(!ValidDeck(source) || detachedSource.IsArenaDeck || detachedSource.IsDungeonDeck || detachedSource.IsDuelsDeck || detachedSource.IsBrawlDeck)
			{
				preview.SkippedDecks++;
				continue;
			}
			if(map.ContainsKey(source.DeckId))
				throw new InvalidDataException("В исходном файле повторяется идентификатор колоды.");
			var candidates = preview.Decks.Decks.Where(d => SameClass(d.Class, source.Class) && d.IsConstructedDeck).ToList();
			var target = candidates.FirstOrDefault(d => d.DeckId == source.DeckId)
				?? candidates.OrderBy(d => d.Archived).FirstOrDefault(d => AllVersions(d).Any(v => AllVersions(source).Any(s => SameContents(v, s))));
			if(target == null)
			{
				target = source.CloneForImport();
				if(preview.Decks.Decks.Any(d => d.DeckId == target.DeckId))
					target.DeckId = Guid.NewGuid();
				preview.Decks.Decks.Add(target);
				preview.NewDecks++;
			}
			else
				preview.MergedDecks++;
			var updated = false;
			map.Add(source.DeckId, target);
			foreach(var version in AllVersions(source))
			{
				if(!ValidDeck(version))
					continue;
				var mapped = AllVersions(target).FirstOrDefault(v => SameContents(v, version));
				if(mapped == null)
				{
					mapped = version.CloneForImport();
					mapped.Versions.Clear();
					var max = target.GetMaxVersion();
					mapped.Version = new SerializableVersion(max.Major, max.Minor + 1);
					target.Versions.Add(mapped);
					updated = true;
				}
				versions[source.DeckId + ":" + version.Version] = mapped.Version;
			}
			foreach(var tag in source.Tags.Where(t => !target.Tags.Contains(t)))
			{
				target.Tags.Add(tag);
				updated = true;
			}
			foreach(var tag in target.Tags.Where(t => !preview.Decks.AllTags.Contains(t)))
				preview.Decks.AllTags.Add(tag);
			if(updated) preview.UpdatedDecks++;
		}
		foreach(var record in data.Games)
		{
			var source = record.Game;
			if(!Supported(source)) { preview.UnsupportedGames++; continue; }
			if(source.Result == GameResult.None || source.StartTime == DateTime.MinValue || source.EndTime < source.StartTime
				|| source.Turns < 0 || string.IsNullOrEmpty(NormalizeClass(source.PlayerHero)))
			{
				preview.InvalidGames++;
				continue;
			}
			var game = CloneGame(source);
			game.PlayerHero = NormalizeClass(source.PlayerHero);
			game.OpponentHero = NormalizeClass(source.OpponentHero);
			if(game.GameId == Guid.Empty)
				game.GameId = StableId("hdt:" + Fingerprint(game));
			var key = game.ImportSource + ":" + game.ImportId;
			if(ids.Contains(game.GameId) || (!string.IsNullOrEmpty(game.ImportId) && externalIds.Contains(key)))
			{
				preview.DuplicateGames++;
				continue;
			}
			DeckStats stats;
			if(record.SourceDeckId.HasValue && map.TryGetValue(record.SourceDeckId.Value, out var deck) && SameClass(deck.Class, game.PlayerHero))
			{
				game.DeckId = deck.DeckId;
				game.DeckName = deck.Name;
				var sourceVersion = game.PlayerDeckVersion ?? SerializableVersion.Default;
				game.PlayerDeckVersion = versions.TryGetValue(record.SourceDeckId.Value + ":" + sourceVersion, out var version) ? version : null;
				stats = preview.Stats.DeckStats.GetOrAdd(deck.DeckId, _ => new DeckStats(deck));
			}
			else
			{
				game.DeckId = Guid.Empty;
				game.PlayerDeckVersion = null;
				stats = preview.DefaultStats.GetDeckStats(game.PlayerHero)!;
				preview.UnassignedGames++;
			}
			// External replay paths belong to another profile; never present them as our own replay files.
			game.ReplayFile = null;
			stats.Games.Add(game);
			ids.Add(game.GameId);
			if(!string.IsNullOrEmpty(game.ImportId)) externalIds.Add(key);
			preview.NewGames++;
		}
		return preview;
	}

	public static string Save(TrackerImportPreview preview, string directory)
	{
		var store = new LibraryStore(directory);
		var libraryId = File.Exists(store.FilePath) ? store.Load().LibraryId
			: preview.Decks.LibraryId == Guid.Empty ? Guid.NewGuid() : preview.Decks.LibraryId;
		var library = LibraryRuntimeAdapter.Capture(preview.Decks, preview.Stats, preview.DefaultStats, libraryId);
		return store.SaveImport(library);
	}

	public static void Apply(TrackerImportPreview preview, DeckList decks, DeckStatsList stats, DefaultDeckStats defaults)
	{
		foreach(var imported in preview.Decks.Decks)
		{
			var existing = decks.Decks.FirstOrDefault(d => d.DeckId == imported.DeckId);
			if(existing == null) decks.Decks.Add(imported);
			else
			{
				existing.Versions = imported.Versions;
				existing.Tags = imported.Tags;
			}
		}
		decks.AllTags = preview.Decks.AllTags;
		stats.DeckStats.Clear();
		foreach(var pair in preview.Stats.DeckStats) stats.DeckStats.TryAdd(pair.Key, pair.Value);
		defaults.DeckStats = preview.DefaultStats.DeckStats;
		LastGames.Refresh(stats, defaults);
		foreach(var deck in decks.Decks) deck.StatsUpdated();
	}

	private static bool Supported(GameStats game) => game.GameMode == GameMode.Friendly
		|| ((game.GameMode == GameMode.Ranked || game.GameMode == GameMode.Casual) && (game.Format == Format.Standard || game.Format == Format.Wild));
	private static bool ValidDeck(Deck deck) => !string.IsNullOrEmpty(NormalizeClass(deck.Class)) && deck.Cards.Count > 0 && deck.Cards.All(c => !string.IsNullOrEmpty(c.Id) && c.Count > 0);
	private static IEnumerable<Deck> AllVersions(Deck deck) => new[] { deck }.Concat(deck.Versions);
	private static bool SameClass(string? left, string? right) => NormalizeClass(left) == NormalizeClass(right);
	private static string? NormalizeClass(string? value) => Classes.FirstOrDefault(c => string.Equals(c, (value ?? "").Replace("-", "").Replace(" ", ""), StringComparison.OrdinalIgnoreCase));
	private static bool SameContents(Deck left, Deck right) => SameClass(left.Class, right.Class) && Contents(left) == Contents(right);
	private static string Contents(Deck deck) => NormalizeClass(deck.Class) + ":" + CardsKey(deck.Cards)
		+ "/" + string.Join("/", deck.Sideboards.OrderBy(s => s.OwnerCardId).Select(s => s.OwnerCardId + ":" + CardsKey(s.Cards)));
	private static string CardsKey(IEnumerable<Card> cards) => string.Join(",", cards.GroupBy(c => c.Id).OrderBy(g => g.Key).Select(g => g.Key + "=" + g.Sum(c => c.Count)));
	private static string Fingerprint(GameStats game) => string.Join("|", game.StartTime.ToUniversalTime().Ticks, game.EndTime.ToUniversalTime().Ticks,
		game.GameMode, game.Format, game.Result, game.PlayerHero, game.OpponentHero, game.PlayerName, game.OpponentName, game.Coin, game.Turns, game.Region);
	private static Guid StableId(string value)
	{
		using(var sha = SHA256.Create()) return new Guid(sha.ComputeHash(Encoding.UTF8.GetBytes(value)).Take(16).ToArray());
	}
	private static string Text(JObject row, string key) => (string?)row[key] ?? "";
	private static void Warn(List<string> warnings, string message) { if(warnings.Count < 20) warnings.Add(message); }
	private static Region ParseRegion(JToken? token)
	{
		if(token?.Type == JTokenType.Integer && Enum.IsDefined(typeof(Region), (int)token)) return (Region)(int)token;
		return Enum.TryParse((string?)token, true, out Region region) ? region : Region.UNKNOWN;
	}
	private static void SetRank(GameStats game, string rank)
	{
		var parts = rank.Split('-');
		if(parts.Length == 2 && int.TryParse(parts[1], out var number))
		{
			if(parts[0] == "legend") game.LegendRank = number;
			else if(int.TryParse(parts[0], out var league) && league >= 1 && league <= 5 && number >= 1 && number <= 10)
			{
				// HDT uses league ID 5 for the entire modern ladder; StarLevel identifies its division.
				game.LeagueId = 5;
				game.StarLevel = (5 - league) * 10 + 11 - number;
			}
		}
		else if(int.TryParse(rank, out var legacy)) game.Rank = legacy;
	}
	private static GameStats CloneGame(GameStats game) => LibraryRuntimeAdapter.MaterializeMatch(
		LibraryRuntimeAdapter.CaptureMatch(game, game.StoredDeckId, game.StoredDeckName));

	private static DeckStats CloneStats(DeckStats stats)
	{
		var clone = new DeckStats { DeckId = stats.DeckId, Name = stats.Name, Games = stats.Games.Select(CloneGame).ToList() };
		foreach(var game in clone.Games) game.DeckId = stats.DeckId;
		return clone;
	}

	private static DefaultDeckStats CloneDefaults(DefaultDeckStats stats) => new()
	{
		DeckStats = stats.DeckStats.Select(CloneStats).ToList()
	};
}
