#region

using System;
using System.Collections.Generic;
using System.Linq;
using Hearthstone_Deck_Tracker.Library;

#endregion

namespace Hearthstone_Deck_Tracker.Stats
{
	public class DefaultDeckStats
	{
		private static Lazy<DefaultDeckStats> _instance = new Lazy<DefaultDeckStats>(Load);
		public List<DeckStats> DeckStats;

		internal DefaultDeckStats()
		{
			DeckStats = new List<DeckStats>();
		}

		static DefaultDeckStats()
		{
		}

		public static DefaultDeckStats Instance => _instance.Value;

		public DeckStats? GetDeckStats(string? hero)
		{
			if(string.IsNullOrEmpty(hero))
				return null;
			var ds = DeckStats.FirstOrDefault(d => d.Name == hero);
			if(ds != null)
				return ds;
			ds = new DeckStats {Name = hero};
			DeckStats.Add(ds);
			return ds;
		}

		internal static DefaultDeckStats? LoadedInstance => _instance.IsValueCreated ? _instance.Value : null;

		private static DefaultDeckStats Load() => StandardLibrarySession.Current.Defaults;

#if(!SQUIRREL)
		internal static void SetupDefaultDeckStatsFile()
		{
			// Profiles are isolated. Legacy file migration is intentionally unavailable.
		}
#endif

		public static void Save() => StandardLibrarySession.Save(defaults: Instance);

		internal static void Reload()
		{
			StandardLibrarySession.Reset();
			_instance = new Lazy<DefaultDeckStats>(Load);
		}
	}
}
