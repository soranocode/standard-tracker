using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using HearthMirror;
using Hearthstone_Deck_Tracker.HsReplay.Data;
using Hearthstone_Deck_Tracker.Utility.Logging;
using HSReplay;
using HSReplay.Requests;
using HSReplay.Responses;
using Newtonsoft.Json.Linq;

namespace Hearthstone_Deck_Tracker.HsReplay
{
	internal class ApiWrapper
	{
		public static Task PostBattlegroundsHeroPickFeedback(BattlegroundsHeroPickFeedbackParams parameters, bool isDuos) => Task.CompletedTask;
		public static Task PostBattlegroundsTrinketFeedback(BattlegroundsTrinketPickFeedbackParams parameters) => Task.CompletedTask;
		public static Task<T?> GetArenaHeroPickStats<T>(ArenaHeroPickParams parameters) where T : class => Task.FromResult<T?>(null);
		public static Task<T?> GetArenaCardPickStats<T>(ArenaCardPickParams parameters) where T : class => Task.FromResult<T?>(null);
		public static Task<T?> ScoreArenaDeck<T>(ArenaScoreDeckParams parameters) where T : class => Task.FromResult<T?>(null);

		// Offline compatibility surface for the retained local UI. No network client.
		public static Task UpdateUploadTokenStatus()
			=> Task.CompletedTask;

		public static Task<LogUploadRequest> CreateUploadRequest(UploadMetaData metaData)
			=> Task.FromException<LogUploadRequest>(new NotSupportedException("Cloud services are unavailable in Standard Tracker."));

		public static Task UploadLog(LogUploadRequest uploadRequest, string[] logLines)
			=> Task.CompletedTask;

		public static Task<DecksData?> GetAvailableDecks()
			=> Task.FromResult<DecksData?>(null);

		public static Task<DeckWinrateData?> GetDeckWinrates(string deckId, bool wild)
			=> Task.FromResult<DeckWinrateData?>(null);

		public static Task<BattlegroundsCompStats?> GetTier7CompStats(string token, BattlegroundsCompStatsParams parameters)
			=> Task.FromResult<BattlegroundsCompStats?>(null);

		public static Task<PlayerTrialStatus?> GetPlayerTrialStatus(string name, ulong accountHi, ulong accountLo)
			=> Task.FromResult<PlayerTrialStatus?>(null);

		public static Task<string?> ActivatePlayerTrial(string name, ulong accountHi, ulong accountLo)
			=> Task.FromResult<string?>(null);

		public static Task<BattlegroundsQuestPickStats[]?> GetTier7QuestStats(string token, BattlegroundsQuestPickParams parameters)
			=> Task.FromResult<BattlegroundsQuestPickStats[]?>(null);

		public static Task<BattlegroundsTrinketPickStats?> GetTier7TrinketPickStats(string token, BattlegroundsTrinketPickParams parameters)
			=> Task.FromResult<BattlegroundsTrinketPickStats?>(null);

		public static Task<BattlegroundsHeroPickStats?> GetTier7HeroPickStats(string token, BattlegroundsHeroPickStatsParams parameters)
			=> Task.FromResult<BattlegroundsHeroPickStats?>(null);

		public static Task<BattlegroundsHeroPickStats?> GetTier7DuosHeroPickStats(string token, BattlegroundsHeroPickStatsParams parameters)
			=> Task.FromResult<BattlegroundsHeroPickStats?>(null);

		public static Task<MulliganGuideData?> GetConstructedMulliganGuide(string token, MulliganGuideParams parameters)
			=> Task.FromResult<MulliganGuideData?>(null);

		public static Task<MulliganV2Data?> GetConstructedMulliganV2(string token, MulliganV2Params parameters)
			=> Task.FromResult<MulliganV2Data?>(null);

		public static Task<Dictionary<string, string[]>?> GetDiscoverPoolKeywords(string token)
			=> Task.FromResult<Dictionary<string, string[]>?>(null);

		public static Task<Dictionary<string, string[]>?> GetDiscoverPoolKeywords()
			=> Task.FromResult<Dictionary<string, string[]>?>(null);

		public static Task PostMulliganGuideFeedback(MulliganGuideFeedbackParams parameters)
			=> Task.CompletedTask;

		public static Task PostMulliganGuideFeedback(MulliganV2FeedbackParams parameters)
			=> Task.CompletedTask;

		public static Task<MulliganGuideStatusData?> GetMulliganGuideStatus(MulliganGuideStatusParams parameters)
			=> Task.FromResult<MulliganGuideStatusData?>(null);

		public static Task<MulliganV2StatusData?> GetMulliganGuideStatus(MulliganV2StatusParams parameters)
			=> Task.FromResult<MulliganV2StatusData?>(null);

		public static Task<BattlegroundsCompsGuidesData?> GetCompsGuides(string gameLanguage)
			=> Task.FromResult<BattlegroundsCompsGuidesData?>(null);

		public static Task<BattlegroundsTier7CompsGuidesData?> GetPremiumCompsGuides(string token, string gameLanguage, int[] availableRaces)
			=> Task.FromResult<BattlegroundsTier7CompsGuidesData?>(null);

		public static Task<ArenaTrialStatus?> GetArenaTrialStatus(ulong accountHi, ulong accountLo)
			=> Task.FromResult<ArenaTrialStatus?>(null);

		public static Task<ArenasmithStatus?> GetArenasmithStatus()
			=> Task.FromResult<ArenasmithStatus?>(null);

		public static Task<ArenaPackages?> GetArenaPackages()
			=> Task.FromResult<ArenaPackages?>(null);
	}
}
