using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using HearthDb;
using HearthDb.Enums;
using Hearthstone_Deck_Tracker.Utility.Logging;

namespace Hearthstone_Deck_Tracker.Utility.Assets;

public static class CardDefsManager
{
    public static Action? CardsChanged;
    public static Action? InitialDefsLoaded;
    public static bool HasLoadedInitialBaseDefs { get; private set; }
    private static bool _loading;
    private static readonly object LocaleSync = new();
    private static Task<bool>? _russianLocaleLoad;

    public static async void EnsureLatestCardDefs()
    {
        if(_loading || HasLoadedInitialBaseDefs) return;
        _loading = true;
        try
        {
            await Task.Run(() => Cards.LoadBaseData(Cards.GetBundledBaseData()));
            HasLoadedInitialBaseDefs = true;
            if(string.Equals(Helper.GetCardLanguage(), "ruRU", StringComparison.OrdinalIgnoreCase))
                await EnsureRussianLocaleLoaded();
            InitialDefsLoaded?.Invoke();
            CardsChanged?.Invoke();
        }
        finally { _loading = false; }
    }

    public static async Task LoadLocale(string langCode, bool allowCache = true)
    {
        if(!HasLoadedInitialBaseDefs || !string.Equals(langCode, "ruRU", StringComparison.OrdinalIgnoreCase))
            return;
        if(await EnsureRussianLocaleLoaded())
            CardsChanged?.Invoke();
    }

    private static Task<bool> EnsureRussianLocaleLoaded()
    {
        lock(LocaleSync)
            return _russianLocaleLoad ??= Task.Run(LoadRussianLocale);
    }

    private static bool LoadRussianLocale()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Utility", "Assets", "CardDefs.ruRU.xml.gz");
        if(!File.Exists(path))
        {
            Log.Warn($"Bundled Russian card definitions not found: {path}");
            return false;
        }
        try
        {
            using var file = File.OpenRead(path);
            using var stream = new GZipStream(file, CompressionMode.Decompress);
            Cards.LoadLocaleData(stream, Locale.ruRU);
            return true;
        }
        catch(Exception ex)
        {
            Log.Warn($"Could not load bundled Russian card definitions: {ex.Message}");
            return false;
        }
    }
}
