using System;
using System.IO;
using System.Linq;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Serialization;
using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Importing;
using Hearthstone_Deck_Tracker.Stats;
using Hearthstone_Deck_Tracker.Library;
using StandardTracker.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace HDTTests
{
	[TestClass]
	public class TrackerDataImportTests
	{
		private static DefaultDeckStats Defaults() => XmlManager<DefaultDeckStats>.LoadFromString("<DefaultDeckStats><DeckStats /></DefaultDeckStats>");
		private static Deck Deck(string card = "CORE_EX1_011") => new Deck
		{
			Name = "Исходная колода", Class = "Druid", IsArenaDeck = false, IsDungeonDeck = false, IsDuelsDeck = false,
			Cards = new ObservableCollection<Card> { new Card(card) { Count = 30 } }
		};
		private static GameStats Game(Deck deck) => new GameStats
		{
			GameId = Guid.NewGuid(), DeckId = deck.DeckId, DeckName = deck.Name,
			PlayerDeckVersion = deck.Version, GameMode = GameMode.Ranked, Format = Format.Standard,
			Result = GameResult.Win, StartTime = new DateTime(2026, 9, 30, 12, 0, 0), EndTime = new DateTime(2026, 9, 30, 12, 5, 0),
			PlayerHero = "Druid", OpponentHero = "Mage", PlayerName = "Player#1234", OpponentName = "Opponent#1234", Turns = 8
		};
		private static TrackerImportData Data(Deck deck, GameStats game)
		{
			var data = new TrackerImportData();
			data.Decks.Add(deck);
			data.Games.Add(new TrackerImportGame { Game = game, SourceDeckId = deck.DeckId });
			return data;
		}
		private static TrackerImportPreview Prepare(TrackerImportData data, DeckList decks = null, DeckStatsList stats = null, DefaultDeckStats defaults = null)
			=> TrackerDataImporter.Prepare(data, decks ?? new DeckList(), stats ?? new DeckStatsList(), defaults ?? Defaults());
		private static JObject Firestone(string reviewId = "review-1", string code = null) => new JObject
		{
			["reviewId"] = reviewId, ["creationTimestamp"] = 1790769900000L, ["gameMode"] = "ranked", ["gameFormat"] = "wild",
			["result"] = "won", ["coinPlay"] = "coin", ["playerClass"] = "druid", ["opponentClass"] = "mage",
			["playerName"] = "Player#1234", ["opponentName"] = "Opponent#1234", ["gameDurationSeconds"] = 300,
			["gameDurationTurns"] = 8, ["region"] = 2, ["playerRank"] = "1-3", ["playerDeckName"] = "Моя колода",
			["playerDecklist"] = code
		};
		private static void Write<T>(string path, T value)
		{
			using(var stream = File.Create(path)) new XmlSerializer(typeof(T)).Serialize(stream, value);
		}
		private static string TemporaryDirectory()
		{
			var path = Path.Combine(Path.GetTempPath(), "tracker-import-tests-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(path);
			return path;
		}
		[DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_open_v2(byte[] filename, out IntPtr database, int flags, IntPtr vfs);
		[DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_exec(IntPtr database, byte[] sql, IntPtr callback, IntPtr state, IntPtr error);
		[DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_close(IntPtr database);

		[TestMethod]
		public void PreviewIsDetachedAndMergesSavedDeckWithoutChangingItsName()
		{
			var local = Deck(); local.Name = "Сохранённое имя";
			var original = Deck();
			var decks = new DeckList(); decks.Decks.Add(local);
			var stats = new DeckStatsList();
			var defaults = Defaults();
			var preview = Prepare(Data(original, Game(original)), decks, stats, defaults);
			Assert.AreEqual(1, preview.MergedDecks);
			Assert.AreEqual(0, preview.NewDecks);
			Assert.AreEqual("Сохранённое имя", preview.Decks.Decks.Single().Name);
			Assert.AreEqual(local.DeckId, preview.Stats.DeckStats.Values.Single().Games.Single().DeckId);
			Assert.AreEqual(0, stats.DeckStats.Count);
			Assert.AreEqual(0, defaults.DeckStats.Count);
			Assert.AreEqual(0, local.Versions.Count);
		}

		[TestMethod]
		public void KeepsNewAndHistoricalContentsAndRemapsVersionNumbers()
		{
			var local = Deck();
			var original = Deck("TLC_100");
			var old = Deck(); old.Version = new SerializableVersion(1, 1);
			original.Versions.Add(old);
			var decks = new DeckList(); decks.Decks.Add(local);
			var game = Game(original);
			var preview = Prepare(Data(original, game), decks);
			var merged = preview.Decks.Decks.Single();
			Assert.AreEqual(1, merged.Versions.Count);
			Assert.AreEqual("TLC_100", merged.Versions[0].Cards[0].Id);
			Assert.AreEqual(merged.Versions[0].Version, preview.Stats.DeckStats.Values.Single().Games.Single().PlayerDeckVersion);
			Assert.AreEqual(0, local.Versions.Count);
		}

		[TestMethod]
		public void ReimportAfterPersistenceDoesNotAddDecksOrMatches()
		{
			var directory = TemporaryDirectory();
			try
			{
				var deck = Deck(); var data = Data(deck, Game(deck));
				var first = Prepare(data);
				TrackerDataImporter.Save(first, directory);
				var loaded = LibraryRuntimeAdapter.Materialize(new LibraryStore(directory).Load());
				var again = Prepare(data, loaded.Decks, loaded.Stats, loaded.Defaults);
				Assert.AreEqual(0, again.NewGames);
				Assert.AreEqual(0, again.NewDecks);
				Assert.AreEqual(1, again.DuplicateGames);
				Assert.IsFalse(again.HasChanges);
			}
			finally { Directory.Delete(directory, true); }
		}

		[TestMethod]
		public void ImportsHdtFilesAndDefaultStatsWithoutTouchingSource()
		{
			var directory = TemporaryDirectory();
			try
			{
				var deck = Deck(); var game = Game(deck); game.ReplayFile = "external.hdtreplay";
				var decks = new DeckList(); decks.Decks.Add(deck);
				var stats = new DeckStatsList(); stats.SerializableDeckStats.Add(new DeckStats(deck)); stats.SerializableDeckStats[0].Games.Add(game);
				var defaults = Defaults(); var unassigned = Game(deck); unassigned.DeckId = Guid.Empty;
				defaults.GetDeckStats("Druid").Games.Add(unassigned);
				Write(Path.Combine(directory, "PlayerDecks.xml"), decks);
				Write(Path.Combine(directory, "DeckStats.xml"), stats);
				Write(Path.Combine(directory, "DefaultDeckStats.xml"), defaults);
				var before = Directory.GetFiles(directory).ToDictionary(p => p, File.ReadAllText);
				var preview = Prepare(TrackerDataImporter.ReadHdt(directory));
				Assert.AreEqual(2, preview.NewGames);
				Assert.AreEqual(1, preview.UnassignedGames);
				Assert.IsNull(preview.Stats.DeckStats.Values.Single().Games.Single().ReplayFile);
				foreach(var entry in before) Assert.AreEqual(entry.Value, File.ReadAllText(entry.Key));
			}
			finally { Directory.Delete(directory, true); }
		}

		[TestMethod]
		public void FirestoneUsesEndTimestampAndModernRankAndPreservesUnassignedMatches()
		{
			var preview = Prepare(TrackerDataImporter.ReadFirestoneJson(new JArray(Firestone())));
			var game = preview.DefaultStats.DeckStats.Single().Games.Single();
			Assert.AreEqual(Format.Wild, game.Format);
			Assert.AreEqual(GameResult.Win, game.Result);
			Assert.IsTrue(game.Coin);
			Assert.AreEqual(300, (game.EndTime - game.StartTime).TotalSeconds);
			Assert.AreEqual(1790769900000L, new DateTimeOffset(game.EndTime).ToUnixTimeMilliseconds());
			Assert.AreEqual(Region.EU, game.Region);
			Assert.AreEqual(5, game.LeagueId);
			Assert.AreEqual(48, game.StarLevel);
			Assert.AreEqual(1, preview.UnassignedGames);
		}

		[TestMethod]
		public void FirestoneDeckCodeReusesExistingDeckAndDeduplicatesReviewId()
		{
			var local = Deck();
			local.Cards = new ObservableCollection<Card>(HearthDb.Cards.Collectible.Values
				.Where(c => c.Class == HearthDb.Enums.CardClass.DRUID && c.Type == HearthDb.Enums.CardType.MINION)
				.Take(15).Select(c => new Card(c) { Count = 2 }));
			var code = HearthDb.Deckstrings.DeckSerializer.Serialize(HearthDbConverter.ToHearthDbDeck(local), false);
			var data = TrackerDataImporter.ReadFirestoneJson(new JObject { ["stats"] = new JArray(Firestone("review-1", code), Firestone("review-1", code)) });
			var decks = new DeckList(); decks.Decks.Add(local);
			var preview = Prepare(data, decks);
			Assert.AreEqual(1, preview.MergedDecks, string.Join("; ", preview.Warnings));
			Assert.AreEqual(1, preview.NewGames);
			Assert.AreEqual(1, preview.DuplicateGames);
			var again = Prepare(data, preview.Decks, preview.Stats, preview.DefaultStats);
			Assert.AreEqual(0, again.NewGames);
			Assert.AreEqual(2, again.DuplicateGames);
		}

		[TestMethod]
		public void BadFirestoneDeckCodeDoesNotLoseTheMatch()
		{
			foreach(var code in new[] { "invalid code", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" })
			{
				var preview = Prepare(TrackerDataImporter.ReadFirestoneJson(new JArray(Firestone("review-1", code))));
				Assert.AreEqual(1, preview.NewGames);
				Assert.AreEqual(1, preview.UnassignedGames);
				Assert.IsTrue(preview.Warnings.Count > 0);
			}
		}

		[TestMethod]
		public void CountsUnsupportedAndMalformedRowsSeparately()
		{
			var unsupported = Firestone("bgs"); unsupported["gameMode"] = "battlegrounds";
			var twist = Firestone("twist"); twist["gameFormat"] = "twist";
			var broken = Firestone("broken"); broken.Remove("creationTimestamp");
			var preview = Prepare(TrackerDataImporter.ReadFirestoneJson(new JArray(Firestone(), unsupported, twist, broken, JValue.CreateNull())));
			Assert.AreEqual(1, preview.NewGames);
			Assert.AreEqual(2, preview.UnsupportedGames);
			Assert.AreEqual(2, preview.InvalidGames);
		}

		[TestMethod]
		public void AcceptsDexieMatchHistoryExportAndStableIdsWithoutReviewId()
		{
			var root = new JObject { ["data"] = new JObject { ["data"] = new JArray(new JObject { ["tableName"] = "matchHistory", ["rows"] = new JArray(Firestone(null)) }) } };
			var first = Prepare(TrackerDataImporter.ReadFirestoneJson(root));
			var again = Prepare(TrackerDataImporter.ReadFirestoneJson(root), first.Decks, first.Stats, first.DefaultStats);
			Assert.AreEqual(0, again.NewGames);
			Assert.AreEqual(1, again.DuplicateGames);
		}

		[TestMethod]
		public void MissingDeckFileKeepsHdtHistoryUnassigned()
		{
			var directory = TemporaryDirectory();
			try
			{
				var deck = Deck(); var stats = new DeckStatsList();
				stats.SerializableDeckStats.Add(new DeckStats(deck)); stats.SerializableDeckStats[0].Games.Add(Game(deck));
				Write(Path.Combine(directory, "DeckStats.xml"), stats);
				var preview = Prepare(TrackerDataImporter.ReadHdt(directory));
				Assert.AreEqual(1, preview.NewGames);
				Assert.AreEqual(1, preview.UnassignedGames);
				Assert.IsTrue(preview.Warnings.Count > 0);
			}
			finally { Directory.Delete(directory, true); }
		}

		[TestMethod]
		public void ExistingMatchInDefaultStatsIsAlsoDeduplicated()
		{
			var deck = Deck(); var game = Game(deck); var defaults = Defaults();
			defaults.GetDeckStats("Druid").Games.Add(game);
			var preview = Prepare(Data(deck, game), defaults: defaults);
			Assert.AreEqual(0, preview.NewGames);
			Assert.AreEqual(1, preview.DuplicateGames);
		}

		[TestMethod]
		public void PreviewDoesNotCreateForeignStatisticsInGlobalProfile()
		{
			var field = typeof(DeckStatsList).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
			var previous = field.GetValue(null);
			var global = new DeckStatsList();
			field.SetValue(null, new Lazy<DeckStatsList>(() => global));
			try
			{
				var foreign = new Deck { Class = "Druid", Cards = new ObservableCollection<Card> { new Card("CORE_EX1_011") { Count = 30 } } };
				Prepare(Data(foreign, Game(foreign)));
				Assert.AreEqual(0, global.DeckStats.Count);
			}
			finally { field.SetValue(null, previous); }
		}

		[TestMethod]
		public void FailedLibraryCommitKeepsOriginalAndImportBackup()
		{
			var directory = TemporaryDirectory();
			try
			{
				var store = new LibraryStore(directory);
				store.Save(new LibraryDocument());
				var original = File.ReadAllText(store.FilePath);
				var deck = Deck(); var preview = Prepare(Data(deck, Game(deck)));
				using(var locked = File.Open(store.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
				{
					Assert.ThrowsException<IOException>(() => TrackerDataImporter.Save(preview, directory));
				}
				Assert.AreEqual(original, File.ReadAllText(store.FilePath));
				Assert.AreEqual(original, File.ReadAllText(Path.Combine(Directory.GetDirectories(Path.Combine(directory, "ImportBackups")).Single(), LibraryStore.FileName)));
				Assert.AreEqual(0, Directory.GetFiles(directory, "*.xml").Length);
				Assert.AreEqual(0, Directory.GetFiles(directory, "*.tmp-*").Length);
			}
			finally { Directory.Delete(directory, true); }
		}

		[TestMethod]
		public void ApplyKeepsActiveDeckObjectAndUpdatesItsStatistics()
		{
			var deck = Deck(); var decks = new DeckList(); decks.Decks.Add(deck);
			var stats = new DeckStatsList(); var defaults = Defaults();
			var preview = Prepare(Data(deck, Game(deck)), decks, stats, defaults);
			TrackerDataImporter.Apply(preview, decks, stats, defaults);
			Assert.AreSame(deck, decks.Decks.Single());
			Assert.AreEqual(1, stats.DeckStats[deck.DeckId].Games.Count);
		}

		[TestMethod]
		public void ReadsNativeFirestoneSqliteWithoutChangingSource()
		{
			var directory = TemporaryDirectory();
			var path = Path.Combine(directory, "FirestoneDB.sqlite");
			IntPtr database = IntPtr.Zero;
			try
			{
				Assert.AreEqual(0, sqlite3_open_v2(Encoding.UTF8.GetBytes(path + "\0"), out database, 6, IntPtr.Zero));
				var sql = "CREATE TABLE matchHistory (reviewId TEXT PRIMARY KEY, data TEXT); INSERT INTO matchHistory VALUES ('review-1', '"
					+ Firestone().ToString(Newtonsoft.Json.Formatting.None).Replace("'", "''") + "');";
				Assert.AreEqual(0, sqlite3_exec(database, Encoding.UTF8.GetBytes(sql + "\0"), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero));
				sqlite3_close(database); database = IntPtr.Zero;
				var before = File.ReadAllBytes(path);
				var preview = Prepare(TrackerDataImporter.ReadFirestone(path));
				Assert.AreEqual(1, preview.NewGames);
				Assert.AreEqual("Firestone", preview.DefaultStats.DeckStats.Single().Games.Single().ImportSource);
				CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
				Assert.AreEqual(1, Directory.GetFiles(directory).Length);
			}
			finally
			{
				if(database != IntPtr.Zero) sqlite3_close(database);
				Directory.Delete(directory, true);
			}
		}

		[TestMethod]
		public void RejectsUnknownJsonShapeAndExternalXmlEntities()
		{
			Assert.ThrowsException<InvalidDataException>(() => TrackerDataImporter.ReadFirestoneJson(new JObject { ["collection"] = new JArray() }));
			var directory = TemporaryDirectory();
			try
			{
				File.WriteAllText(Path.Combine(directory, "PlayerDecks.xml"), "<!DOCTYPE Decks [<!ENTITY external SYSTEM 'file:///does-not-exist'>]><Decks>&external;</Decks>");
				Assert.ThrowsException<InvalidOperationException>(() => TrackerDataImporter.ReadHdt(directory));
			}
			finally { Directory.Delete(directory, true); }
		}

		[TestMethod]
		public void DifferentSideboardsAreKeptAsDifferentDecks()
		{
			var local = Deck(); var imported = Deck();
			local.Sideboards.Add(new Sideboard("ETC_080", new[] { new Card("TLC_100") { Count = 1 } }.ToList()));
			imported.Sideboards.Add(new Sideboard("ETC_080", new[] { new Card("CORE_EX1_011") { Count = 1 } }.ToList()));
			var decks = new DeckList(); decks.Decks.Add(local);
			var preview = Prepare(Data(imported, Game(imported)), decks);
			Assert.AreEqual(1, preview.NewDecks);
			Assert.AreEqual(2, preview.Decks.Decks.Count);
			Assert.AreEqual(imported.DeckId, preview.Stats.DeckStats.Values.Single().Games.Single().DeckId);
		}

		[TestMethod]
		public void FirestoneFriendlyMatchWithoutLadderFormatIsPreserved()
		{
			var row = Firestone(); row["gameMode"] = "friendly"; row.Remove("gameFormat");
			var preview = Prepare(TrackerDataImporter.ReadFirestoneJson(new JArray(row)));
			Assert.AreEqual(1, preview.NewGames);
			Assert.AreEqual(GameMode.Friendly, preview.DefaultStats.DeckStats.Single().Games.Single().GameMode);
		}
	}
}
