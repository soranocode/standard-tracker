using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;
using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Library;
using Hearthstone_Deck_Tracker.Importing;
using Hearthstone_Deck_Tracker.Stats;
using Hearthstone_Deck_Tracker.Utility;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using StandardTracker.Library;

namespace HDTTests
{
	[TestClass]
	public class StandardLibraryTests
	{
		private string _directory;
		private string _previousPath;
		private object _previousConfig;
		private readonly Dictionary<Type, object> _previousInstances = new Dictionary<Type, object>();

		[TestInitialize]
		public void Setup()
		{
			_directory = Path.Combine(Path.GetTempPath(), "standard-library-test-" + Guid.NewGuid());
			Directory.CreateDirectory(_directory);
			_previousPath = Config.AppDataPath;
			_previousConfig = typeof(Config).GetField("_config", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
			foreach(var type in new[] { typeof(DeckList), typeof(DeckStatsList), typeof(DefaultDeckStats) })
				_previousInstances[type] = type.GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
			typeof(Config).GetField("AppDataPath", BindingFlags.Public | BindingFlags.Static).SetValue(null, _directory);
			typeof(Config).GetField("_config", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
			Config.Load();
			Reload();
		}

		[TestCleanup]
		public void Cleanup()
		{
			typeof(Config).GetField("AppDataPath", BindingFlags.Public | BindingFlags.Static).SetValue(null, _previousPath);
			typeof(Config).GetField("_config", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, _previousConfig);
			foreach(var pair in _previousInstances)
				pair.Key.GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, pair.Value);
			StandardLibrarySession.Reset();
			Directory.Delete(_directory, true);
		}

		private static void Reload()
		{
			DeckList.Reload(); DeckStatsList.Reload(); DefaultDeckStats.Reload();
		}

		private static Deck Deck(string title = "Своя колода")
		{
			var deck = new Deck
			{
				Name = title, Class = "Druid", Note = "Заметка о колоде", HsId = 42,
				Version = new SerializableVersion(2, 1) { Revision = 3, Build = 4 },
				IsArenaDeck = false, IsDungeonDeck = false, IsDuelsDeck = false
			};
			deck.Cards.Add(new Card("CORE_EX1_011") { Count = 2 });
			deck.Sideboards.Add(new Sideboard("ETC_080", new List<Card> { new Card("TLC_100") { Count = 1 } }));
			deck.Tags.Add("Favorite");
			return deck;
		}

		private static GameStats Match(Deck deck) => new GameStats
		{
			GameId = Guid.NewGuid(), DeckId = deck.DeckId, DeckName = deck.Name,
			PlayerDeckVersion = deck.Version, PlayerHero = "Druid", OpponentHero = "Mage", PlayerName = "Игрок", OpponentName = "Противник",
			GameMode = GameMode.Ranked, Format = Format.Wild, Result = GameResult.Win, Coin = true, Turns = 8,
			StartTime = new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc), EndTime = new DateTime(2026, 9, 30, 12, 6, 0, DateTimeKind.Utc),
			Note = "Заметка о матче", StarLevel = 48, StarLevelAfter = 49, Stars = 1, StarsAfter = 2, LeagueId = 5,
			LegendRank = 123, LegendRankAfter = 122, OpponentLegendRank = 91, Region = Region.EU,
			ImportSource = "Firestone", ImportId = "review-123", ReplayFile = "local-replay.xml",
			PlayerCards = new List<TrackedCard> { new TrackedCard("CORE_EX1_011", 2, 1) }
		};

		[TestMethod]
		public void LibraryAssemblyHasNoHdtOrGameOrWpfDependencies()
		{
			var names = typeof(LibraryDocument).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
			foreach(var name in new[] { "HearthstoneDeckTracker", "HearthDb", "HearthMirror", "PresentationFramework", "PresentationCore" })
				Assert.IsFalse(names.Contains(name), name);
		}

		[TestMethod]
		public void EmptyProfileHasNativeIdentityBeforeItsFirstBackup()
		{
			Assert.AreEqual(0, DeckList.Instance.Decks.Count);
			var store = new LibraryStore(_directory);
			Assert.IsTrue(File.Exists(store.FilePath));
			var libraryId = store.Load().LibraryId;
			Directory.CreateDirectory(Config.Instance.BackupDir);
			BackupManager.CreateBackup("Backup_empty.zip");
			using(var archive = ZipFile.OpenRead(Path.Combine(Config.Instance.BackupDir, "Backup_empty.zip")))
				Assert.IsNotNull(archive.GetEntry(LibraryStore.FileName));
			Reload();
			Assert.AreEqual(0, DeckList.Instance.Decks.Count);
			Assert.AreEqual(libraryId, store.Load().LibraryId);
		}

		[TestMethod]
		public void AllSaveEntryPointsPersistOneNativeLibraryAndReloadProgress()
		{
			var deck = Deck(); var game = Match(deck);
			var historical = Deck("Старая версия"); historical.Version = new SerializableVersion(1, 0);
			deck.Versions.Add(historical); deck.SelectedVersion = historical.Version;
			DeckList.Instance.Decks.Add(deck);
			DeckStatsList.Instance.DeckStats.TryAdd(deck.DeckId, new DeckStats(deck) { Games = new List<GameStats> { game } });
			DeckList.Save();
			var store = new LibraryStore(_directory);
			var libraryId = store.Load().LibraryId;
			Assert.AreEqual(1, store.Load().Matches.Count, "Saving a deck must also retain live statistics.");
			var unassigned = Match(deck); unassigned.GameId = Guid.NewGuid(); unassigned.DeckId = Guid.Empty;
			DefaultDeckStats.Instance.GetDeckStats("Druid").Games.Add(unassigned);
			DefaultDeckStats.Save();
			Assert.AreEqual(2, store.Load().Matches.Count);
			deck.Note = "Изменено перед сохранением статистики";
			DeckStatsList.Save();
			Assert.AreEqual(deck.Note, store.Load().Decks.Single().Note);
			Assert.AreEqual(libraryId, store.Load().LibraryId);
			foreach(var name in new[] { "PlayerDecks.xml", "DeckStats.xml", "DefaultDeckStats.xml" })
				Assert.IsFalse(File.Exists(Path.Combine(_directory, name)), name);
			var json = JObject.Parse(File.ReadAllText(store.FilePath));
			Assert.AreEqual(LibraryDocument.FormatName, (string)json["format"]);
			Assert.AreEqual(1, (int)json["schemaVersion"]);
			Assert.IsNull(json["SerializableDeckStats"]);
			Reload();
			var restoredDeck = DeckList.Instance.Decks.Single();
			var restoredGame = DeckStatsList.Instance.DeckStats[deck.DeckId].Games.Single();
			Assert.AreEqual(deck.Name, restoredDeck.Name);
			Assert.AreEqual(deck.Note, restoredDeck.Note);
			Assert.AreEqual(42L, restoredDeck.HsId);
			Assert.AreEqual(deck.Version, restoredDeck.Version);
			Assert.AreEqual(historical.Version, restoredDeck.SelectedVersion);
			Assert.AreEqual("Старая версия", restoredDeck.Versions.Single().Name);
			Assert.AreEqual("TLC_100", restoredDeck.Sideboards.Single().Cards.Single().Id);
			Assert.IsTrue(restoredDeck.Tags.Contains("Favorite"));
			Assert.AreEqual(game.GameId, restoredGame.GameId);
			Assert.AreEqual(deck.DeckId, restoredGame.DeckId);
			Assert.AreEqual(game.StartTime, restoredGame.StartTime);
			Assert.AreEqual(DateTimeKind.Utc, restoredGame.StartTime.Kind);
			Assert.AreEqual(Format.Wild, restoredGame.Format);
			Assert.AreEqual(49, restoredGame.StarLevelAfter);
			Assert.AreEqual(122, restoredGame.LegendRankAfter);
			Assert.AreEqual("review-123", restoredGame.ImportId);
			Assert.AreEqual(game.Note, restoredGame.Note);
			Assert.AreEqual("local-replay.xml", restoredGame.ReplayFile);
			Assert.AreEqual(1, restoredGame.PlayerCards.Single().Unconfirmed);
			Assert.AreEqual(unassigned.GameId, DefaultDeckStats.Instance.DeckStats.Single().Games.Single().GameId);
		}

		[TestMethod]
		public void EarlierStandardProfileUpgradesOnceAndLeavesItsXmlUntouched()
		{
			var deck = Deck(); var game = Match(deck);
			var decks = new DeckList(); decks.Decks.Add(deck);
			var stats = new DeckStatsList(); stats.SerializableDeckStats.Add(new DeckStats(deck) { Games = new List<GameStats> { game } });
			WriteXml(Path.Combine(_directory, "PlayerDecks.xml"), decks);
			WriteXml(Path.Combine(_directory, "DeckStats.xml"), stats);
			WriteXml(Path.Combine(_directory, "DefaultDeckStats.xml"), new DefaultDeckStats());
			var before = Directory.GetFiles(_directory, "*.xml").ToDictionary(p => p, File.ReadAllText);
			Assert.AreEqual(deck.DeckId, DeckList.Instance.Decks.Single().DeckId);
			Assert.AreEqual(game.GameId, DeckStatsList.Instance.DeckStats[deck.DeckId].Games.Single().GameId);
			DeckList.Instance.Decks.Single().Name = "Теперь своё название";
			DeckList.Save();
			foreach(var file in before) Assert.AreEqual(file.Value, File.ReadAllText(file.Key));
			Reload();
			Assert.AreEqual("Теперь своё название", DeckList.Instance.Decks.Single().Name);
			Assert.AreEqual(1, Directory.GetDirectories(Path.Combine(_directory, "ImportBackups")).Length);
		}

		[TestMethod]
		public void NativeProfileOpensWhenAllLegacyXmlIsRemovedOrUnreadable()
		{
			var store = new LibraryStore(_directory);
			var document = new LibraryDocument();
			document.Decks.Add(new LibraryDeck { Id = Guid.NewGuid(), Title = "Только наша библиотека", HeroClass = "Druid" });
			store.Save(document);
			File.WriteAllText(Path.Combine(_directory, "PlayerDecks.xml"), "broken legacy data");
			Assert.AreEqual("Только наша библиотека", DeckList.Instance.Decks.Single().Name);
			File.Delete(Path.Combine(_directory, "PlayerDecks.xml"));
			Reload();
			Assert.AreEqual("Только наша библиотека", DeckList.Instance.Decks.Single().Name);
		}

		[TestMethod]
		public void HdtMigrationKeepsProgressInNativeLibraryAfterRemovingSource()
		{
			var source = Path.Combine(_directory, "hdt-source"); Directory.CreateDirectory(source);
			var deck = Deck("Колода из HDT"); var game = Match(deck); game.ImportSource = null; game.ImportId = null;
			var decks = new DeckList(); decks.Decks.Add(deck);
			var stats = new DeckStatsList(); stats.SerializableDeckStats.Add(new DeckStats(deck) { Games = new List<GameStats> { game } });
			WriteXml(Path.Combine(source, "PlayerDecks.xml"), decks);
			WriteXml(Path.Combine(source, "DeckStats.xml"), stats);
			var before = Directory.GetFiles(source).ToDictionary(p => p, File.ReadAllText);
			var data = TrackerDataImporter.ReadHdt(source);
			var preview = TrackerDataImporter.Prepare(data, DeckList.Instance, DeckStatsList.Instance, DefaultDeckStats.Instance);
			TrackerDataImporter.Save(preview, _directory);
			TrackerDataImporter.Apply(preview, DeckList.Instance, DeckStatsList.Instance, DefaultDeckStats.Instance);
			foreach(var file in before) Assert.AreEqual(file.Value, File.ReadAllText(file.Key));
			var nativeId = new LibraryStore(_directory).Load().LibraryId;
			DeckList.Save();
			Assert.AreEqual(nativeId, new LibraryStore(_directory).Load().LibraryId);
			Directory.Delete(source, true);
			Reload();
			Assert.AreEqual("Колода из HDT", DeckList.Instance.Decks.Single().Name);
			var restored = DeckStatsList.Instance.DeckStats[deck.DeckId].Games.Single();
			Assert.AreEqual(GameResult.Win, restored.Result);
			Assert.AreEqual("HDT", restored.ImportSource);
			Assert.AreEqual(game.GameId, restored.GameId);
			Assert.AreEqual(0, Directory.GetFiles(_directory, "*Deck*.xml").Length);
			var again = TrackerDataImporter.Prepare(data, DeckList.Instance, DeckStatsList.Instance, DefaultDeckStats.Instance);
			Assert.AreEqual(0, again.NewGames);
		}

		[TestMethod]
		public void MissingNativePrimaryRecoversBackupBeforeConsideringEarlierXml()
		{
			var store = new LibraryStore(_directory); var first = new LibraryDocument();
			first.Decks.Add(new LibraryDeck { Id = Guid.NewGuid(), Title = "Из нашей резервной копии" });
			store.Save(first); store.Save(new LibraryDocument());
			File.Delete(store.FilePath);
			File.WriteAllText(Path.Combine(_directory, "PlayerDecks.xml"), "unreadable old xml");
			Assert.AreEqual("Из нашей резервной копии", DeckList.Instance.Decks.Single().Name);
			Assert.AreEqual(first.LibraryId, store.Load().LibraryId);
		}

		[TestMethod]
		public void CorruptNativeProfileRecoversLastWriteAndKeepsCorruptBytes()
		{
			var store = new LibraryStore(_directory); var first = new LibraryDocument();
			store.Save(first);
			var second = new LibraryDocument(); store.Save(second);
			File.WriteAllText(store.FilePath, "corrupt bytes");
			Assert.AreEqual(first.LibraryId, store.Load().LibraryId);
			Assert.AreEqual("corrupt bytes", File.ReadAllText(Directory.GetFiles(_directory, "library.json.corrupted-*").Single()));
			Assert.AreEqual(first.LibraryId, LibraryStore.ReadFile(store.FilePath).LibraryId);
		}

		[TestMethod]
		public void MissingOrNewerSchemaIsRejectedWithoutReplacingNativeProfile()
		{
			var store = new LibraryStore(_directory); store.Save(new LibraryDocument()); store.Save(new LibraryDocument());
			var newer = JObject.Parse(File.ReadAllText(store.FilePath)); newer["schemaVersion"] = 999;
			newer["decks"] = new JObject { ["changedFutureShape"] = new JArray() };
			File.WriteAllText(store.FilePath, newer.ToString());
			var before = File.ReadAllText(store.FilePath);
			Assert.ThrowsException<LibraryVersionException>(() => store.Load());
			Assert.AreEqual(before, File.ReadAllText(store.FilePath));
			File.Delete(store.FilePath + ".bak");
			File.WriteAllText(store.FilePath, "{}");
			Assert.ThrowsException<InvalidDataException>(() => store.Load());
			Assert.AreEqual("{}", File.ReadAllText(store.FilePath));
		}

		[TestMethod]
		public void DailyBackupContainsAndRestoresNativeLibrary()
		{
			DeckList.Instance.Decks.Add(Deck()); DeckList.Save();
			Directory.CreateDirectory(Config.Instance.BackupDir);
			BackupManager.CreateBackup("Backup_native.zip");
			var backup = new FileInfo(Path.Combine(Config.Instance.BackupDir, "Backup_native.zip"));
			using(var archive = ZipFile.OpenRead(backup.FullName))
			{
				Assert.IsNotNull(archive.GetEntry(LibraryStore.FileName));
				Assert.IsNull(archive.GetEntry("PlayerDecks.xml"));
			}
			DeckList.Instance.Decks.Clear(); DeckList.Save();
			Assert.IsTrue(BackupManager.Restore(backup, true, LibraryStore.FileName));
			Assert.AreEqual("Своя колода", DeckList.Instance.Decks.Single().Name);
		}

		[TestMethod]
		public void RecentReplaysAreDerivedFromNativeHistoryAndIgnoreOldCache()
		{
			var deck = Deck(); var game = Match(deck);
			DeckList.Instance.Decks.Add(deck);
			DeckStatsList.Instance.DeckStats.TryAdd(deck.DeckId, new DeckStats(deck) { Games = new List<GameStats> { game } });
			Directory.CreateDirectory(Config.Instance.ReplayDir);
			File.WriteAllText(Path.Combine(Config.Instance.ReplayDir, game.ReplayFile), "local replay fixture");
			DeckStatsList.Save();
			LastGames.Save();
			Assert.AreEqual(game.GameId, LastGames.Instance.Games.Single().GameId);
			Assert.IsFalse(File.Exists(Path.Combine(_directory, "LastGames.xml")));
			File.WriteAllText(Path.Combine(_directory, "LastGames.xml"), "unreadable old cache");
			LastGames.Save();
			Assert.AreEqual(game.GameId, LastGames.Instance.Games.Single().GameId);
			Assert.AreEqual("unreadable old cache", File.ReadAllText(Path.Combine(_directory, "LastGames.xml")));
		}

		[TestMethod]
		public void OldDevelopmentBackupRestoresIntoNativeLibrary()
		{
			var source = Path.Combine(_directory, "earlier"); Directory.CreateDirectory(source);
			var decks = new DeckList(); decks.Decks.Add(Deck("Из предыдущей версии"));
			WriteXml(Path.Combine(source, "PlayerDecks.xml"), decks);
			var path = Path.Combine(_directory, "earlier.zip"); ZipFile.CreateFromDirectory(source, path);
			Assert.IsTrue(BackupManager.Restore(new FileInfo(path), true));
			Assert.AreEqual("Из предыдущей версии", DeckList.Instance.Decks.Single().Name);
			Assert.IsTrue(File.Exists(Path.Combine(_directory, LibraryStore.FileName)));
			Assert.IsFalse(File.Exists(Path.Combine(_directory, "PlayerDecks.xml")));
		}

		private static void WriteXml<T>(string path, T value)
		{
			using(var stream = File.Create(path)) new XmlSerializer(typeof(T)).Serialize(stream, value);
		}
	}
}
