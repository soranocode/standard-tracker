using System.Collections.Generic;
using System.Collections.ObjectModel;
using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.Hearthstone;
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
