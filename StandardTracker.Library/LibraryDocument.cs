using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace StandardTracker.Library;

// The persisted contract belongs to Standard Tracker. It has no dependency on
// the desktop scaffold, HDT XML, HearthDb, HearthMirror, or a running game.
public sealed class LibraryDocument
{
	public const string FormatName = "standard-tracker-library";
	public const int CurrentSchemaVersion = 1;
	[JsonProperty(Required = Required.Always)] public string Format { get; set; } = FormatName;
	[JsonProperty(Required = Required.Always)] public int SchemaVersion { get; set; } = CurrentSchemaVersion;
	[JsonProperty(Required = Required.Always)] public Guid LibraryId { get; set; } = Guid.NewGuid();
	[JsonProperty(Required = Required.Always)] public List<LibraryDeck> Decks { get; set; } = new();
	[JsonProperty(Required = Required.Always)] public List<LibraryMatch> Matches { get; set; } = new();
	public List<string> Tags { get; set; } = new() { "All", "Favorite", "None" };
	public List<RecentDeck> RecentDecks { get; set; } = new();
}

public sealed class LibraryDeck : LibraryDeckRevision
{
	public LibraryVersion SelectedVersion { get; set; } = new();
	public List<LibraryDeckRevision> History { get; set; } = new();
}

public class LibraryDeckRevision
{
	public Guid Id { get; set; }
	public string Title { get; set; } = "";
	public string? HeroClass { get; set; }
	public string? Note { get; set; }
	public string? SourceUrl { get; set; }
	public string Archetype { get; set; } = "";
	public DateTime EditedAt { get; set; }
	public bool Archived { get; set; }
	public long HearthstoneDeckId { get; set; }
	public string Kind { get; set; } = "constructed";
	public LibraryVersion Version { get; set; } = new();
	public List<string> Tags { get; set; } = new();
	public List<LibraryCard> Cards { get; set; } = new();
	public List<LibrarySideboard> Sideboards { get; set; } = new();
	public List<LibraryCard> MissingCards { get; set; } = new();
	public JObject AdditionalData { get; set; } = new();
}

public sealed class LibraryVersion
{
	public int Major { get; set; } = 1;
	public int Minor { get; set; }
	public int Patch { get; set; }
	public int Build { get; set; }
}

public sealed class LibraryCard
{
	public string CardId { get; set; } = "";
	public int Quantity { get; set; }
	public int Unconfirmed { get; set; }
}

public sealed class LibrarySideboard
{
	public string OwnerCardId { get; set; } = "";
	public List<LibraryCard> Cards { get; set; } = new();
}

public sealed class RecentDeck
{
	public Guid DeckId { get; set; }
	public string? HeroClass { get; set; }
	public string? Title { get; set; }
}

public sealed class LibraryMatch
{
	public Guid Id { get; set; }
	public Guid? DeckId { get; set; }
	public string? DeckTitle { get; set; }
	public LibraryVersion? DeckVersion { get; set; }
	public DateTime StartedAt { get; set; }
	public DateTime EndedAt { get; set; }
	public string Mode { get; set; } = "None";
	public string? Format { get; set; }
	public string Result { get; set; } = "None";
	public string Region { get; set; } = "UNKNOWN";
	public string? PlayerClass { get; set; }
	public string? OpponentClass { get; set; }
	public string? PlayerName { get; set; }
	public string? OpponentName { get; set; }
	public string? PlayerHeroCardId { get; set; }
	public string? OpponentHeroCardId { get; set; }
	public string[]? PlayerHeroClasses { get; set; }
	public string[]? OpponentHeroClasses { get; set; }
	public bool HadCoin { get; set; }
	public bool Conceded { get; set; }
	public int Turns { get; set; }
	public string? Note { get; set; }
	public bool IsClone { get; set; }
	public string? ReplayFile { get; set; }
	public long HearthstoneDeckId { get; set; }
	public int? HearthstoneBuild { get; set; }
	public int HearthstoneGameType { get; set; }
	public int RankedSeasonId { get; set; }
	public bool Reconnected { get; set; }
	public LibraryRank Rank { get; set; } = new();
	public LibraryRank OpponentRank { get; set; } = new();
	public ImportIdentity? ImportedFrom { get; set; }
	public List<LibraryCard> PlayerCards { get; set; } = new();
	public List<LibraryCard> OpponentCards { get; set; } = new();
	public List<LibrarySideboard> PlayerSideboards { get; set; } = new();
	// Optional opaque data from desktop integrations; the library never requires
	// those integrations to read, store or import its decks and match progress.
	public JObject AdditionalData { get; set; } = new();
}

public sealed class LibraryRank
{
	public int LegacyRank { get; set; }
	public int StarLevel { get; set; }
	public int StarLevelAfter { get; set; }
	public int Stars { get; set; }
	public int StarsAfter { get; set; }
	public int Legend { get; set; }
	public int LegendAfter { get; set; }
	public int League { get; set; }
	public int Multiplier { get; set; }
}

public sealed class ImportIdentity
{
	public string? Tracker { get; set; }
	public string? MatchId { get; set; }
}
