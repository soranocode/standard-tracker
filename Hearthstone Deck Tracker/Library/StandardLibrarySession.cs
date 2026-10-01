using System;
using System.IO;
using Hearthstone_Deck_Tracker.Importing;
using Hearthstone_Deck_Tracker.Stats;
using Hearthstone_Deck_Tracker.Utility.Logging;
using StandardTracker.Library;

namespace Hearthstone_Deck_Tracker.Library;

internal static class StandardLibrarySession
{
	private static readonly object Gate = new();
	private static string? _directory;
	private static LibraryRuntimeState? _state;

	public static LibraryRuntimeState Current
	{
		get
		{
			lock(Gate)
			{
				var directory = Path.GetFullPath(Config.Instance.DataDir);
				if(_state != null && string.Equals(_directory, directory, StringComparison.OrdinalIgnoreCase)) return _state;
				var store = new LibraryStore(directory);
				LibraryDocument library;
				if(!File.Exists(store.FilePath) && !File.Exists(store.FilePath + ".bak") && HdtProfileReader.ProfileExists(directory))
				{
					// Upgrade only our own earlier development profile. Neither the HDT
					// AppData directory nor portable files in the working directory are used.
					var previous = HdtProfileReader.ReadProfile(directory);
					library = LibraryRuntimeAdapter.Capture(previous.Decks, previous.Stats, previous.Defaults, Guid.NewGuid());
					var backup = store.SaveImport(library);
					foreach(var name in HdtProfileReader.FileNames)
						if(File.Exists(Path.Combine(directory, name))) File.Copy(Path.Combine(directory, name), Path.Combine(backup, name));
					Log.Info("Converted the earlier Standard Tracker profile to its native library. Backup: " + backup);
				}
				else library = store.Load();
				// Give even an empty profile a persistent identity and a complete first
				// daily backup before any deck or match has been recorded.
				if(!File.Exists(store.FilePath)) store.Save(library);
				var state = LibraryRuntimeAdapter.Materialize(library);
				_directory = directory;
				_state = state;
				return state;
			}
		}
	}

	public static void Save(DeckList? decks = null, DeckStatsList? stats = null, DefaultDeckStats? defaults = null)
	{
		lock(Gate)
		{
			var state = Current;
			decks ??= DeckList.LoadedInstance ?? state.Decks;
			stats ??= DeckStatsList.LoadedInstance ?? state.Stats;
			defaults ??= DefaultDeckStats.LoadedInstance ?? state.Defaults;
			var document = LibraryRuntimeAdapter.Capture(decks, stats, defaults, state.LibraryId);
			new LibraryStore(_directory!).Save(document);
			state.Decks = decks; state.Stats = stats; state.Defaults = defaults;
		}
	}

	public static void Reset()
	{
		lock(Gate) { _state = null; _directory = null; }
	}
}
