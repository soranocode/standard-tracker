using System;
using System.Text.RegularExpressions;
using HearthDb.Deckstrings;

namespace Hearthstone_Deck_Tracker.Hearthstone
{
	internal static class DeckCodeUtility
	{
		private static readonly Regex CodePattern = new(@"(?<![A-Za-z0-9+/])[A-Za-z0-9+/]{30,}={0,2}(?![A-Za-z0-9+/=])", RegexOptions.Compiled);
		private static readonly Regex TitlePattern = new(@"###\s*(?<title>[^#\r\n]*)", RegexOptions.Compiled);

		public static Deck Import(string input)
		{
			var code = CodePattern.Match(input);
			if(!code.Success)
				throw new FormatException("No deck code found");
			var deck = HearthDbConverter.FromHearthDbDeck(DeckSerializer.Deserialize(code.Value));
			var titles = TitlePattern.Matches(input.Substring(0, code.Index));
			var title = titles.Count == 0 ? "" : titles[titles.Count - 1].Groups["title"].Value.Trim();
			deck.Name = string.IsNullOrWhiteSpace(title) ? ClassName(deck.Class) : title;
			return deck;
		}

		public static string Code(Deck deck)
		{
			var converted = HearthDbConverter.ToHearthDbDeck(deck.GetSelectedDeckVersion());
			return converted == null ? "" : DeckSerializer.Serialize(converted, false);
		}

		public static string CopyText(Deck deck)
		{
			var code = Code(deck);
			return string.IsNullOrEmpty(code) ? "" : $"### {deck.Name}\r\n{code}";
		}

		private static string ClassName(string? className) => className switch
		{
			"DeathKnight" or "Deathknight" => "Рыцарь смерти",
			"DemonHunter" or "Demonhunter" => "Охотник на демонов",
			"Druid" => "Друид",
			"Hunter" => "Охотник",
			"Mage" => "Маг",
			"Paladin" => "Паладин",
			"Priest" => "Жрец",
			"Rogue" => "Разбойник",
			"Shaman" => "Шаман",
			"Warlock" => "Чернокнижник",
			"Warrior" => "Воин",
			_ => className ?? "Колода"
		};
	}
}
