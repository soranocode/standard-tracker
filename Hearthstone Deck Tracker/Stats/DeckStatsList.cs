#region

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Library;

#endregion

namespace Hearthstone_Deck_Tracker.Stats
{
	public class DeckStatsList
	{
		private static Lazy<DeckStatsList> _instance = new Lazy<DeckStatsList>(Load);

		[XmlArray(ElementName = "DeckStats")]
		[XmlArrayItem(ElementName = "Deck")]
		public List<DeckStats> SerializableDeckStats = new List<DeckStats>();

		private ConcurrentDictionary<Guid, DeckStats>? _deckStats;
		[XmlIgnore]
		public ConcurrentDictionary<Guid, DeckStats> DeckStats => _deckStats ??=
																	new ConcurrentDictionary<Guid, DeckStats>(
																		SerializableDeckStats.Where(x => x != null).GroupBy(x => x.DeckId).Select(x => new KeyValuePair<Guid, DeckStats>(x.First().DeckId, x.First())));

		public static DeckStatsList Instance => _instance.Value;

		internal static DeckStatsList? LoadedInstance => _instance.IsValueCreated ? _instance.Value : null;

		private static DeckStatsList Load() => StandardLibrarySession.Current.Stats;

#if(!SQUIRREL)
		internal static void SetupDeckStatsFile()
		{
			// Profiles are isolated. Legacy file migration is intentionally unavailable.
		}
#endif


		public static void Save() => StandardLibrarySession.Save(stats: Instance);

		internal static void Reload()
		{
			StandardLibrarySession.Reset();
			_instance = new Lazy<DeckStatsList>(Load);
		}

		internal DeckStats Add(Deck deck)
		{
			var ds = new DeckStats(deck);
			Instance.DeckStats.TryAdd(deck.DeckId, ds);
			return ds;
		}
	}
}
