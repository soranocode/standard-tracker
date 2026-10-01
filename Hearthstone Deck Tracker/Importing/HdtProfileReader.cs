using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using Hearthstone_Deck_Tracker.Library;
using Hearthstone_Deck_Tracker.Stats;

namespace Hearthstone_Deck_Tracker.Importing;

// HDT XML is an external source adapter, never the Standard Tracker store.
internal static class HdtProfileReader
{
	internal static readonly string[] FileNames = { "PlayerDecks.xml", "DeckStats.xml", "DefaultDeckStats.xml" };
	internal static bool ProfileExists(string directory) => FileNames.Any(name => File.Exists(Path.Combine(directory, name)));

	internal static LibraryRuntimeState ReadProfile(string directory) => new()
	{
		Decks = ReadOptional<DeckList>(directory, FileNames[0]) ?? new DeckList(),
		Stats = ReadOptional<DeckStatsList>(directory, FileNames[1]) ?? new DeckStatsList(),
		Defaults = ReadOptional<DefaultDeckStats>(directory, FileNames[2]) ?? new DefaultDeckStats()
	};

	public static TrackerImportData Read(string directory)
	{
		if(!ProfileExists(directory)) throw new InvalidDataException("В выбранной папке нет файлов данных Hearthstone Deck Tracker.");
		var source = ReadProfile(directory);
		var data = new TrackerImportData();
		data.Decks.AddRange(source.Decks.Decks.Select(d => d.CloneForImport()));
		if(!File.Exists(Path.Combine(directory, FileNames[0])))
			data.Warnings.Add("PlayerDecks.xml отсутствует: матчи будут сохранены без привязки к колодам.");
		if(!File.Exists(Path.Combine(directory, FileNames[1])))
			data.Warnings.Add("DeckStats.xml отсутствует: история матчей по колодам не найдена.");
		foreach(var stats in source.Stats.DeckStats.Values)
			foreach(var game in stats.Games)
			{
				game.DeckId = stats.DeckId;
				game.PlayerHero ??= data.Decks.FirstOrDefault(d => d.DeckId == stats.DeckId)?.Class;
				game.ImportSource = "HDT";
				game.ImportId = game.GameId == Guid.Empty ? null : game.GameId.ToString("N");
				data.Games.Add(new TrackerImportGame { Game = game, SourceDeckId = stats.DeckId });
			}
		foreach(var stats in source.Defaults.DeckStats)
			foreach(var game in stats.Games)
			{
				game.DeckId = Guid.Empty;
				game.PlayerHero ??= stats.Name;
				game.ImportSource = "HDT";
				game.ImportId = game.GameId == Guid.Empty ? null : game.GameId.ToString("N");
				data.Games.Add(new TrackerImportGame { Game = game });
			}
		return data;
	}

	private static T? ReadOptional<T>(string directory, string name) where T : class
	{
		var path = Path.Combine(directory, name);
		if(!File.Exists(path)) return null;
		using(var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 128 * 1024 * 1024 }))
			return (T)new XmlSerializer(typeof(T)).Deserialize(reader);
	}
}
