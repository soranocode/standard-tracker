using System;
using System.Threading.Tasks;
using HearthDb;

namespace Hearthstone_Deck_Tracker.Utility.Assets;

public static class CardDefsManager
{
	public static Action? CardsChanged;
	public static Action? InitialDefsLoaded;
	public static bool HasLoadedInitialBaseDefs { get; private set; }
	private static bool _loading;
	public static async void EnsureLatestCardDefs()
	{
		if(_loading || HasLoadedInitialBaseDefs) return;
		_loading = true;
		try
		{
			await Task.Run(() => Cards.LoadBaseData(Cards.GetBundledBaseData()));
			HasLoadedInitialBaseDefs = true;
			InitialDefsLoaded?.Invoke();
			CardsChanged?.Invoke();
		}
		finally { _loading = false; }
	}
	public static Task LoadLocale(string langCode, bool allowCache = true) => Task.CompletedTask;
}
