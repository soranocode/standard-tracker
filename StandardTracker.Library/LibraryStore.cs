using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace StandardTracker.Library;

public sealed class LibraryStore
{
	public const string FileName = "library.json";
	private static readonly JsonSerializerSettings Settings = new()
	{
		ContractResolver = new CamelCasePropertyNamesContractResolver(),
		Formatting = Formatting.Indented,
		NullValueHandling = NullValueHandling.Ignore,
		TypeNameHandling = TypeNameHandling.None,
		MaxDepth = 64
	};
	private static readonly ConcurrentDictionary<string, object> Gates = new(StringComparer.OrdinalIgnoreCase);
	private readonly object _gate;
	public string DirectoryPath { get; }
	public string FilePath => Path.Combine(DirectoryPath, FileName);

	public LibraryStore(string directory)
	{
		DirectoryPath = Path.GetFullPath(directory);
		_gate = Gates.GetOrAdd(FilePath, _ => new object());
	}

	// Missing profiles are empty. Invalid or newer profiles must never be silently
	// replaced with an empty library. A valid last successful write can be recovered.
	public LibraryDocument Load()
	{
		lock(_gate)
		{
			if(!File.Exists(FilePath))
			{
				if(!File.Exists(FilePath + ".bak")) return new LibraryDocument();
				var previous = ReadFile(FilePath + ".bak");
				WriteAtomic(previous, false);
				return previous;
			}
			try { return ReadFile(FilePath); }
			catch(LibraryVersionException) { throw; }
			catch(Exception ex) when(ex is JsonException || ex is InvalidDataException)
			{
				if(!File.Exists(FilePath + ".bak")) throw;
				var recovered = ReadFile(FilePath + ".bak");
				File.Copy(FilePath, FilePath + ".corrupted-" + Guid.NewGuid().ToString("N"));
				WriteAtomic(recovered, false);
				return recovered;
			}
		}
	}

	public static LibraryDocument ReadFile(string path)
	{
		ValidateHeader(path);
		using(var stream = File.OpenText(path))
		using(var reader = new JsonTextReader(stream) { MaxDepth = 64 })
		{
			var library = JsonSerializer.Create(Settings).Deserialize<LibraryDocument>(reader)
				?? throw new InvalidDataException("Файл библиотеки пуст.");
			if(reader.Read()) throw new InvalidDataException("После библиотеки обнаружены лишние данные.");
			Validate(library);
			return library;
		}
	}

	private static void ValidateHeader(string path)
	{
		// Check the version before deserializing deck/match shapes. A newer schema
		// with different shapes must not be mistaken for corruption and rolled back.
		using(var stream = File.OpenText(path))
		using(var reader = new JsonTextReader(stream) { MaxDepth = 64 })
		{
			if(!reader.Read() || reader.TokenType != JsonToken.StartObject)
				throw new InvalidDataException("Это не библиотека Standard Tracker.");
			var formatFound = false;
			var versionFound = false;
			while(reader.Read() && reader.Depth > 0)
			{
				if(reader.Depth != 1 || reader.TokenType != JsonToken.PropertyName) continue;
				var name = (string)reader.Value!;
				if(!reader.Read()) break;
				if(name == "format")
				{
					if(reader.TokenType != JsonToken.String || (string?)reader.Value != LibraryDocument.FormatName)
						throw new InvalidDataException("Это не библиотека Standard Tracker.");
					formatFound = true;
				}
				else if(name == "schemaVersion")
				{
					if(reader.TokenType != JsonToken.Integer || Convert.ToString(reader.Value) != LibraryDocument.CurrentSchemaVersion.ToString())
						throw new LibraryVersionException("Версия библиотеки не поддерживается. Нужна совместимая версия Standard Tracker.");
					versionFound = true;
				}
				else reader.Skip();
				if(formatFound && versionFound) return;
			}
			throw new InvalidDataException("В библиотеке отсутствуют формат и версия схемы.");
		}
	}

	public void Save(LibraryDocument library)
	{
		lock(_gate) WriteAtomic(library, true);
	}

	public string SaveImport(LibraryDocument library)
	{
		lock(_gate)
		{
			Validate(library);
			var backup = Path.Combine(DirectoryPath, "ImportBackups", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(backup);
			var existed = File.Exists(FilePath);
			if(existed) File.Copy(FilePath, Path.Combine(backup, FileName));
			File.WriteAllText(Path.Combine(backup, "files-before.txt"), existed ? FileName : "", Encoding.UTF8);
			WriteAtomic(library, true);
			return backup;
		}
	}

	private void WriteAtomic(LibraryDocument library, bool keepPrevious)
	{
		Validate(library);
		Directory.CreateDirectory(DirectoryPath);
		var staged = FilePath + ".tmp-" + Guid.NewGuid().ToString("N");
		try
		{
			using(var file = new FileStream(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None))
			{
				using(var writer = new StreamWriter(file, new UTF8Encoding(false), 4096, true))
				using(var json = new JsonTextWriter(writer))
				{
					JsonSerializer.Create(Settings).Serialize(json, library);
					json.Flush();
				}
				file.Flush(true);
			}
			if(File.Exists(FilePath)) File.Replace(staged, FilePath, keepPrevious ? FilePath + ".bak" : null);
			else File.Move(staged, FilePath);
		}
		finally { if(File.Exists(staged)) File.Delete(staged); }
	}

	private static void Validate(LibraryDocument library)
	{
		if(library.Format != LibraryDocument.FormatName)
			throw new InvalidDataException("Это не библиотека Standard Tracker.");
		if(library.SchemaVersion != LibraryDocument.CurrentSchemaVersion)
			throw new LibraryVersionException("Версия библиотеки не поддерживается. Нужна совместимая версия Standard Tracker.");
		if(library.LibraryId == Guid.Empty || library.Decks == null || library.Matches == null || library.Tags == null || library.RecentDecks == null
			|| library.Tags.Any(t => t == null) || library.RecentDecks.Any(d => d == null))
			throw new InvalidDataException("В библиотеке отсутствуют обязательные данные.");
		var ids = new HashSet<Guid>();
		foreach(var deck in library.Decks)
		{
			if(deck == null || deck.Id == Guid.Empty || !ids.Add(deck.Id) || deck.History == null || deck.SelectedVersion == null)
				throw new InvalidDataException("Некорректная колода в библиотеке.");
			ValidateRevision(deck);
			foreach(var revision in deck.History) ValidateRevision(revision);
		}
		foreach(var game in library.Matches)
			if(game == null || game.Rank == null || game.OpponentRank == null || game.PlayerCards == null || game.OpponentCards == null || game.PlayerSideboards == null
				|| game.PlayerCards.Concat(game.OpponentCards).Any(c => c == null || c.CardId == null) || InvalidSideboards(game.PlayerSideboards))
				throw new InvalidDataException("Некорректный матч в библиотеке.");
	}

	private static void ValidateRevision(LibraryDeckRevision deck)
	{
		if(deck == null || deck.Title == null || deck.Version == null || deck.Cards == null || deck.Sideboards == null || deck.Tags == null || deck.MissingCards == null
			|| deck.Tags.Any(t => t == null) || InvalidCards(deck.Cards.Concat(deck.MissingCards)) || InvalidSideboards(deck.Sideboards))
			throw new InvalidDataException("Некорректный состав колоды в библиотеке.");
	}

	private static bool InvalidCards(IEnumerable<LibraryCard> cards) => cards.Any(c => c == null || string.IsNullOrEmpty(c.CardId));
	private static bool InvalidSideboards(IEnumerable<LibrarySideboard> sideboards) => sideboards.Any(s => s == null
		|| s.Cards == null || string.IsNullOrEmpty(s.OwnerCardId) || InvalidCards(s.Cards));
}

public sealed class LibraryVersionException : IOException
{
	public LibraryVersionException(string message) : base(message) { }
}
