using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using HearthDb.Enums;
using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Stats;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HDTTests
{
	[TestClass]
	public class StandardTrackerIsolationTests
	{
		[TestMethod]
		public void OnlyConfirmedStandardQueuesAreSupported()
		{
			foreach(FormatType format in Enum.GetValues(typeof(FormatType)))
			foreach(GameMode mode in Enum.GetValues(typeof(GameMode)))
			{
				var expected = format == FormatType.FT_STANDARD
					&& (mode == GameMode.Ranked || mode == GameMode.Casual || mode == GameMode.Friendly);
				Assert.AreEqual(expected, StandardMode.IsSupported(format, mode), format + "/" + mode);
			}
		}

		[TestMethod]
		public async Task SharedHttpClientCannotContactAnyServer()
		{
			// A localhost port has no server. A real transport would throw; the offline handler answers locally.
			using(var result = await Core.HttpClient.GetAsync("http://127.0.0.1:1/test"))
				Assert.AreEqual(HttpStatusCode.ServiceUnavailable, result.StatusCode);
		}

		[TestMethod]
		public void FreshProfileIgnoresLegacyFilesAndSurvivesRestart()
		{
			var temp = Path.Combine(Path.GetTempPath(), "standard-tracker-test-" + Guid.NewGuid());
			var legacy = Path.Combine(temp, "legacy");
			var isolated = Path.Combine(temp, "isolated");
			Directory.CreateDirectory(legacy);
			Directory.CreateDirectory(isolated);
			var profileField = typeof(Config).GetField("AppDataPath", BindingFlags.Public | BindingFlags.Static);
			var configField = typeof(Config).GetField("_config", BindingFlags.NonPublic | BindingFlags.Static);
			var originalProfile = (string)profileField.GetValue(null);
			var originalConfig = configField.GetValue(null);
			var originalDirectory = Environment.CurrentDirectory;
			try
			{
				Assert.AreEqual("StandardTracker", Path.GetFileName(originalProfile));
				File.WriteAllText(Path.Combine(legacy, "config.xml"), "legacy sentinel");
				foreach(var name in new[] { "PlayerDecks.xml", "DeckStats.xml", "DefaultDeckStats.xml" })
					File.WriteAllText(Path.Combine(legacy, name), "legacy sentinel");
				Environment.CurrentDirectory = legacy;
				profileField.SetValue(null, isolated);
				configField.SetValue(null, null);
				Config.Load();
				Assert.AreEqual(isolated + "\\", Config.Instance.ConfigDir);
				Assert.AreEqual(isolated + "\\", Config.Instance.DataDir);
				Config.Instance.SaveDataInAppData = false;
				Config.Instance.SaveConfigInAppData = false;
				Config.Instance.DataDirPath = legacy;
				Config.Instance.GoogleAnalytics = true;
				Config.Instance.CheckForUpdates = true;
				Config.Instance.HsReplayAutoUpload = true;
				Config.Instance.SyncCollection = true;
				Config.Instance.ConstructedAutoImportNew = true;
				Config.Instance.RunBobsBuddy = true;
				Config.Instance.WindowWidth = 987;
				Config.Instance.CheckConfigWarnings();
				Config.Save();
				configField.SetValue(null, null);
				Config.Load();
				Assert.AreEqual(isolated + "\\", Config.Instance.DataDir);
				Assert.AreEqual(isolated + "\\", Config.Instance.ConfigDir);
				Assert.IsFalse(Config.Instance.GoogleAnalytics || Config.Instance.CheckForUpdates
					|| Config.Instance.HsReplayAutoUpload || Config.Instance.SyncCollection
					|| Config.Instance.ConstructedAutoImportNew || Config.Instance.RunBobsBuddy);
				foreach(var typeAndMethod in new[] {
					Tuple.Create(typeof(DeckList), "SetupDeckListFile"),
					Tuple.Create(typeof(DeckStatsList), "SetupDeckStatsFile"),
					Tuple.Create(typeof(DefaultDeckStats), "SetupDefaultDeckStatsFile") })
					typeAndMethod.Item1.GetMethod(typeAndMethod.Item2, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
				Assert.AreEqual(987, Config.Instance.WindowWidth);
				Assert.IsFalse(File.Exists(Path.Combine(isolated, "PlayerDecks.xml")));
				var loaded = (DeckList)typeof(DeckList).GetMethod("Load", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
				Assert.AreEqual(0, loaded.Decks.Count);
				var stats = (DeckStatsList)typeof(DeckStatsList).GetMethod("Load", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
				Assert.AreEqual(0, stats.DeckStats.Count);
				foreach(var path in Directory.GetFiles(legacy))
					Assert.AreEqual("legacy sentinel", File.ReadAllText(path));
			}
			finally
			{
				profileField.SetValue(null, originalProfile);
				configField.SetValue(null, originalConfig);
				Environment.CurrentDirectory = originalDirectory;
				Directory.Delete(temp, true);
			}
		}
	}
}
