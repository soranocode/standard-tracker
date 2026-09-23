#region

using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Hearthstone_Deck_Tracker.Utility.Logging;
using Newtonsoft.Json;

#endregion

namespace Hearthstone_Deck_Tracker
{
	public static class Imgur
	{
		public static async Task<string?> Upload(string clientId, MemoryStream image, string? name = null)
		{
			return await Task.FromResult<string?>(null);
		}

		private class ImgurResponse
		{
			public ImgurDataImage? data { get; set; }
			public bool success { get; set; }
			public int status { get; set; }

			public class ImgurDataImage
			{
				public string? id { get; set; }
				public string? title { get; set; }
				public string? name { get; set; }
				public string? deletehash { get; set; }
				public string? link { get; set; }
			}
		}
	}
}
