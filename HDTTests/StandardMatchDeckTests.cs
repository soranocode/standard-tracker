using System.Collections.Generic;
using System.Collections.ObjectModel;
using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Importing;
using Hearthstone_Deck_Tracker.Enums;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HmCard = HearthMirror.Objects.Card;
using HmDeck = HearthMirror.Objects.Deck;

namespace HDTTests
{
	[TestClass]
	public class StandardMatchDeckTests
	{
		private const string CardId = "CORE_EX1_011";
		private const string OtherCardId = "TLC_100";
		private const string DruidHeroId = HearthDb.CardIds.Collectible.Druid.MalfurionStormrageHeroHeroSkins;

		private static Deck LocalDeck(long id, string cardId) => new Deck
		{
			Name = "Local deck",
			Class = "Druid",
			HsId = id,
			Cards = new ObservableCollection<Card> { new Card(cardId) { Count = 2 } }
		};

		private static HmDeck GameDeck(int count = 2) => new HmDeck
		{
			Id = 42,
			Hero = DruidHeroId,
			Cards = new List<HmCard> { new HmCard(CardId, count, 0) }
		};

		[TestMethod]
		public void MatchesWildDeckAndItsFortyCardVersion()
		{
			const string wildCardId = "LOE_077";
			var deck = LocalDeck(0, wildCardId);
			Assert.IsFalse(deck.StandardViable);
			var gameDeck = GameDeck();
			gameDeck.Cards = new List<HmCard> { new HmCard(wildCardId, 2, 0) };
			Assert.AreSame(deck, DeckManager.FindMatchingStandardDeck(new[] { deck }, gameDeck));

			var version = LocalDeck(0, wildCardId);
			version.Cards[0].Count = 40;
			version.Version = new SerializableVersion(1, 1);
			deck.Versions.Add(version);
			gameDeck.Cards = new List<HmCard> { new HmCard(wildCardId, 40, 0) };
			Assert.AreSame(deck, DeckManager.FindMatchingStandardDeck(new[] { deck }, gameDeck));
			Assert.AreEqual(version.Version, deck.SelectedVersion);
		}

		[TestMethod]
		public void GameDeckImportAcceptsWildButRejectsIncompleteAndBrawlDecks()
		{
			var gameDeck = GameDeck();
			gameDeck.Cards = new List<HmCard> { new HmCard("LOE_077", 30, 0) };
			Assert.IsTrue(DeckImporter.IsValidDeck(gameDeck));
			gameDeck.Cards[0].Count = 40;
			Assert.IsTrue(DeckImporter.IsValidDeck(gameDeck));
			gameDeck.Cards[0].Count = 29;
			Assert.IsFalse(DeckImporter.IsValidDeck(gameDeck));
			gameDeck.Cards[0].Count = 30;
			gameDeck.Type = 6;
			Assert.IsFalse(DeckImporter.IsValidDeck(gameDeck));
		}

		[TestMethod]
		public void StandardGameDetectionDoesNotDiscardDeckBasedOnBundledLegality()
		{
			var deck = LocalDeck(0, "LOE_077");
			Assert.IsFalse(deck.StandardViable);
			var filtered = new List<Deck> { deck }.FilterByMode(GameMode.Ranked, Format.Standard);
			CollectionAssert.AreEqual(new[] { deck }, filtered);
		}

		[TestMethod]
		public void MatchesManuallyImportedDeckWithoutHearthstoneId()
		{
			var staleIdMatch = LocalDeck(42, OtherCardId);
			var matchingDeck = LocalDeck(0, CardId);

			var selected = DeckManager.FindMatchingStandardDeck(new[] { staleIdMatch, matchingDeck }, GameDeck());

			Assert.AreSame(matchingDeck, selected);
		}

		[TestMethod]
		public void DoesNotSelectWrongCardsOrArchivedDeck()
		{
			var wrongCards = LocalDeck(42, OtherCardId);
			var archivedMatch = LocalDeck(0, CardId);
			archivedMatch.Archived = true;

			Assert.IsNull(DeckManager.FindMatchingStandardDeck(new[] { wrongCards, archivedMatch }, GameDeck()));
			Assert.IsNull(DeckManager.FindMatchingStandardDeck(new[] { LocalDeck(0, CardId) }, GameDeck(1)));
		}

		[TestMethod]
		public void SelectsTheSavedVersionUsedInTheMatch()
		{
			var deck = LocalDeck(42, OtherCardId);
			var matchingVersion = LocalDeck(42, CardId);
			matchingVersion.Version = new SerializableVersion(1, 1);
			deck.Versions.Add(matchingVersion);

			Assert.AreSame(deck, DeckManager.FindMatchingStandardDeck(new[] { deck }, GameDeck()));
			Assert.AreEqual(matchingVersion.Version, deck.SelectedVersion);
		}
	}
}
