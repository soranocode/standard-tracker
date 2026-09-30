using System;
using System.Collections.Generic;
using System.Linq;
using HearthMirror;
using Hearthstone_Deck_Tracker.Importing;
using MirrorDeck = HearthMirror.Objects.Deck;

namespace Hearthstone_Deck_Tracker.Hearthstone;

internal class ConstructedDeckReader
{
	private readonly Func<long?> _readSelectedId;
	private readonly Func<List<MirrorDeck>?> _readDecks;

	internal ConstructedDeckReader(Func<long?>? readSelectedId = null, Func<List<MirrorDeck>?>? readDecks = null)
	{
		_readSelectedId = readSelectedId ?? ReadSelectedId;
		_readDecks = readDecks ?? (() => Reflection.Client.GetDecks());
	}

	private static long? ReadSelectedId()
	{
		var pickerId = Reflection.Client.GetDeckPickerState()?.SelectedDeck;
		return pickerId > 0 ? pickerId : Reflection.Client.GetSelectedDeckInMenu();
	}

	internal MirrorDeck? Read(MirrorDeck? captured, string? heroClass = null, IEnumerable<Card>? revealedCards = null)
	{
		if(IsComplete(captured))
			return captured;

		// Retry an incomplete snapshot instead of permanently caching a partial memory read.
		var selectedId = captured?.Id > 0 ? captured.Id : _readSelectedId();
		var decks = _readDecks();
		if(decks == null)
			return null;
		if(selectedId > 0)
			return decks.FirstOrDefault(d => d.Id == selectedId && IsComplete(d));

		// At late startup the picker may be gone. Only a unique match to real player
		// cards can recover the full original deck; never import just the drawn cards.
		var known = revealedCards?.Where(c => c.Collectible && !c.IsCreated && c.Count > 0)
			.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.Sum(c => c.Count));
		if(string.IsNullOrEmpty(heroClass) || known == null || known.Count == 0)
			return null;
		var matches = decks.Where(d => IsComplete(d)
			&& string.Equals(new Card(d.Hero).PlayerClass, heroClass, StringComparison.OrdinalIgnoreCase)
			&& known.All(c => d.Cards.Where(x => x.Id == c.Key).Sum(x => x.Count) >= c.Value)).Take(2).ToList();
		return matches.Count == 1 ? matches[0] : null;
	}

	private static bool IsComplete(MirrorDeck? deck) => deck != null && deck.Id > 0
		&& !string.IsNullOrEmpty(deck.Hero) && new Card(deck.Hero).IsKnownCard
		&& deck.Cards != null && deck.Cards.All(c => c != null && c.Count > 0
			&& !string.IsNullOrEmpty(c.Id) && new Card(c.Id).IsKnownCard)
		&& DeckImporter.IsValidDeck(deck) && !DeckImporter.IsExactlyWhizbang(deck);
}
