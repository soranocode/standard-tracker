using System;
using System.Collections.Generic;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Stats;

namespace Hearthstone_Deck_Tracker.Importing;

internal sealed class TrackerImportData
{
	public List<Deck> Decks { get; } = new();
	public List<TrackerImportGame> Games { get; } = new();
	public int InvalidGames { get; set; }
	public List<string> Warnings { get; } = new();
}

internal sealed class TrackerImportGame
{
	public GameStats Game { get; set; } = new();
	public Guid? SourceDeckId { get; set; }
}

internal sealed class TrackerImportPreview
{
	public DeckList Decks { get; set; } = new();
	public DeckStatsList Stats { get; set; } = new();
	public DefaultDeckStats DefaultStats { get; set; } = null!;
	public int NewDecks { get; set; }
	public int MergedDecks { get; set; }
	public int UpdatedDecks { get; set; }
	public int SkippedDecks { get; set; }
	public int NewGames { get; set; }
	public int DuplicateGames { get; set; }
	public int UnsupportedGames { get; set; }
	public int InvalidGames { get; set; }
	public int UnassignedGames { get; set; }
	public List<string> Warnings { get; } = new();
	public bool HasChanges => NewDecks > 0 || UpdatedDecks > 0 || NewGames > 0;

	public string Summary => $"Новых колод: {NewDecks}\nКолоды для объединения: {MergedDecks}\nПропущенных колод: {SkippedDecks}\n"
		+ $"Новых матчей: {NewGames}\nУже перенесено: {DuplicateGames}\n"
		+ $"Неподдерживаемые режимы: {UnsupportedGames}\nНекорректные записи: {InvalidGames}\n"
		+ $"Матчи без колоды: {UnassignedGames} (сохраняются в общей статистике)";
}
