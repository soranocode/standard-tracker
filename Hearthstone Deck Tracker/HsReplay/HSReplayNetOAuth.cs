using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Hearthstone_Deck_Tracker.Controls.Error;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.HsReplay.Data;
using Hearthstone_Deck_Tracker.Live.Data;
using Hearthstone_Deck_Tracker.Utility;
using Hearthstone_Deck_Tracker.Utility.Logging;
using Hearthstone_Deck_Tracker.Utility.Twitch;
using HSReplay.OAuth;
using HSReplay.OAuth.Data;
using HSReplay.Responses;
using Newtonsoft.Json;

namespace Hearthstone_Deck_Tracker.HsReplay
{
	// ReSharper disable InconsistentNaming
	internal sealed class HSReplayNetOAuth
	{
		public static bool AccountUpdateInProgress;
		public static event Action? Authenticated;
		public static event Action? LoggedOut;
		public static event Action? TwitchUsersUpdated;
		public static event Action? AccountDataUpdated;
		public static event Action? CollectionUpdated;
		public static event Action? UploadTokenClaimed;
		public static bool IsFullyAuthenticated => false;
		public static bool IsAuthenticatedFor(params Scope[] scopes) => false;
		public static bool IsAuthenticatedForAnything() => false;
		public static List<TwitchAccount>? TwitchUsers => null;
		public static User? AccountData => null;
		public static void Save() { }
		public static void SaveTokenData(TokenData data) { }
		public enum ClaimBlizzardAccountResponse { Success, Error, TokenAlreadyClaimed }
		public static Task<bool> Authenticate(string? successUrl = null, string? errorUrl = null) => Task.FromResult(false);
		public static Task Logout() => Task.CompletedTask;
		public static Task<bool> UpdateToken() => Task.FromResult(false);
		public static Task<bool> UpdateTwitchUsers() => Task.FromResult(false);
		public static Task<bool> UpdateAccountData() => Task.FromResult(false);
		public static Task SendTwitchPayload(Payload payload) => Task.CompletedTask;
		public static Task<UserCurrentVideo?> GetCurrentVideo(int user_id) => Task.FromResult<UserCurrentVideo?>(null);
		public static Task<bool> IsStreaming(int user_id) => Task.FromResult(false);
		public static Task<bool> UpdateCollection(Collection collection) => Task.FromResult(false);
		public static Task<bool> UpdateMercenariesCollection(MercenariesCollection collection) => Task.FromResult(false);
		public static Task<bool> ClaimUploadToken(string token) => Task.FromResult(false);
		public static Task<ClaimBlizzardAccountResponse> ClaimBlizzardAccount(ulong accountHi, ulong accountLo, string battleTag) => Task.FromResult(ClaimBlizzardAccountResponse.Error);
		public static Task<string?> IdentifyClientAnalyticsToken(string token = "") => Task.FromResult<string?>(null);
		public static Task<T?> MakeRequest<T>(Func<OAuthClient, Task<T>> request) where T : class => Task.FromResult<T?>(null);
		public static Task<HttpResponseMessage?> SendAsyncWithAuth(HttpRequestMessage req) => Task.FromResult<HttpResponseMessage?>(null);
	}
}
