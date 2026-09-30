using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Stats;
using HDTTests.Hearthstone.Secrets;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HmCard = HearthMirror.Objects.Card;
using HmDeck = HearthMirror.Objects.Deck;

namespace HDTTests
{
	[TestClass]
	public class ConstructedDeckSyncTests
	{
		private const string CardId = "CORE_EX1_011";
		private const string OtherCardId = "TLC_100";
		private static HmDeck GameDeck(int count = 30, long id = 42) => new HmDeck
		{
			Id = id,
			Name = "Game deck",
			Hero = HearthDb.CardIds.Collectible.Druid.MalfurionStormrageHeroHeroSkins,
			Cards = new List<HmCard> { new HmCard(CardId, count, 0) },
			Sideboards = new Dictionary<string, List<HmCard>>()
		};

		private static Deck LocalDeck(int count = 30) => new Deck
		{
			Name = "Saved title",
			Class = "Druid",
			Cards = new ObservableCollection<Card> { new Card(CardId) { Count = count } }
		};

		[TestMethod]
		public void RetriesWhenQueueDeckListIsNotReady()
		{
			var fullDeck = GameDeck();
			var reads = 0;
			var reader = new ConstructedDeckReader(() => 42,
				() => ++reads == 1 ? null : new List<HmDeck> { fullDeck });
			Assert.IsNull(reader.Read(null));
			Assert.AreSame(fullDeck, reader.Read(null));
			Assert.AreEqual(2, reads);
		}

		[TestMethod]
		public void ReplacesIncompleteCapturedDeckWithFullGameDeck()
		{
			var fullDeck = GameDeck();
			var reader = new ConstructedDeckReader(() => null, () => new List<HmDeck> { fullDeck });
			Assert.AreSame(fullDeck, reader.Read(GameDeck(4)));
		}

		[TestMethod]
		public void CompleteQueueSnapshotSurvivesMissingPickerAndDeckListInGame()
		{
			var fullDeck = GameDeck();
			var reads = 0;
			var reader = new ConstructedDeckReader(() => { reads++; return null; },
				() => { reads++; return null; });
			Assert.AreSame(fullDeck, reader.Read(fullDeck));
			Assert.AreEqual(0, reads);
		}

		[TestMethod]
		public void LateStartupFindsUniqueFullDeckFromOriginalCards()
		{
			var fullDeck = GameDeck();
			var other = GameDeck(id: 43);
			other.Cards = new List<HmCard> { new HmCard(OtherCardId, 30, 0) };
			var reader = new ConstructedDeckReader(() => null, () => new List<HmDeck> { other, fullDeck });
			var known = new[] { new Card(CardId) { Count = 1 } };
			Assert.AreSame(fullDeck, reader.Read(null, "Druid", known));
			Assert.AreEqual(30, fullDeck.Cards.Sum(c => c.Count));
		}

		[TestMethod]
		public void LateStartupDoesNotGuessBetweenTwoMatchingDecksOrFromCreatedCards()
		{
			var reader = new ConstructedDeckReader(() => null,
				() => new List<HmDeck> { GameDeck(), GameDeck(id: 43) });
			Assert.IsNull(reader.Read(null, "Druid", new[] { new Card(CardId) }));
			Assert.IsNull(reader.Read(null, "Druid", new[] { new Card(CardId) { IsCreated = true } }));
		}

		[TestMethod]
		public void SelectedIncompleteDeckDoesNotFallBackToAnotherDeck()
		{
			var reader = new ConstructedDeckReader(() => 42,
				() => new List<HmDeck> { GameDeck(4), GameDeck(id: 43) });
			Assert.IsNull(reader.Read(null, "Druid", new[] { new Card(CardId) }));
		}

		[TestMethod]
		public void ImportsFullDeckOnceAndReusesItsSavedTitle()
		{
			var decks = new List<Deck>();
			var selected = DeckManager.GetOrImportConstructedDeck(decks, GameDeck());
			Assert.IsNotNull(selected);
			Assert.AreEqual(30, selected.Cards.Sum(c => c.Count));
			selected.Name = "My title";
			Assert.AreSame(selected, DeckManager.GetOrImportConstructedDeck(decks, GameDeck()));
			Assert.AreEqual("My title", selected.Name);
			Assert.AreEqual(1, decks.Count);
		}

		[TestMethod]
		public void MatchesManualImportAndRestoresArchivedDeckWithoutDuplicate()
		{
			var saved = LocalDeck();
			saved.Archived = true;
			var decks = new List<Deck> { saved };
			Assert.AreSame(saved, DeckManager.GetOrImportConstructedDeck(decks, GameDeck()));
			Assert.IsFalse(saved.Archived);
			Assert.AreEqual(42L, saved.HsId);
			Assert.AreEqual("Saved title", saved.Name);
			Assert.AreEqual(1, decks.Count);
		}

		[TestMethod]
		public void MissingSideboardReadDoesNotEraseSavedSideboard()
		{
			var saved = LocalDeck();
			saved.Sideboards.Add(new Sideboard("ETC_080", new List<Card> { new Card(OtherCardId) }));
			var hsDeck = GameDeck();
			hsDeck.Sideboards = null;
			Assert.AreSame(saved, DeckManager.GetOrImportConstructedDeck(new List<Deck> { saved }, hsDeck));
			Assert.AreEqual(1, saved.Sideboards.Count);
			var game = new MockGame { CurrentGameStats = new GameStats(GameResult.None, "Mage", "Druid") };
			DeckManager.BindConstructedDeckToGame(game, saved, hsDeck);
			Assert.AreEqual(1, game.CurrentGameStats.PlayerSideboards.Count);
		}

		[TestMethod]
		public void MidGameBindingKeepsGameStateAndAssociatesFullDeckWithStatistics()
		{
			var stats = new GameStats(GameResult.None, "Mage", "Druid") { Turns = 7 };
			var game = new MockGame { IsInMenu = false, CurrentGameStats = stats };
			var saved = LocalDeck();
			var fullDeck = GameDeck();
			DeckManager.BindConstructedDeckToGame(game, saved, fullDeck);
			Assert.AreSame(stats, game.CurrentGameStats);
			Assert.AreEqual(7, stats.Turns);
			Assert.AreEqual(saved.DeckId, stats.DeckId);
			Assert.AreEqual(saved.SelectedVersion, stats.PlayerDeckVersion);
			Assert.AreEqual(42L, stats.HsDeckId);
			Assert.AreEqual(30, stats.PlayerCards.Sum(c => c.Count));
			Assert.IsTrue(game.IsUsingPremade);
			Assert.AreSame(fullDeck, game.CurrentSelectedDeck);
		}

		[TestMethod]
		public void QueueCaptureDoesNotChangePreviousMatchStatistics()
		{
			var stats = new GameStats(GameResult.Win, "Mage", "Druid");
			var game = new MockGame { IsInMenu = true, CurrentGameStats = stats };
			DeckManager.BindConstructedDeckToGame(game, LocalDeck(), GameDeck());
			Assert.AreEqual(0L, stats.HsDeckId);
			Assert.IsNull(stats.PlayerDeckVersion);
		}
	}
}
