#region

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using HtmlDocument = HtmlAgilityPack.HtmlDocument;

#endregion

namespace Hearthstone_Deck_Tracker.Importing
{
	public static class ImportingHelper
	{
		public static async Task<HtmlDocument> GetHtmlDoc(string url) => await GetHtmlDoc(url, null, null);

		public static async Task<HtmlDocument> GetHtmlDocGzip(string url)
		{
			return await Task.FromResult(new HtmlDocument());
		}

		public static async Task<HtmlDocument> GetHtmlDoc(string url, string? header, string? headerValue)
		{
			return await Task.FromResult(new HtmlDocument());
		}

		public static async Task<string> PostJson(string url, string data)
		{
			return await JsonRequest(url, data);
		}

		public static async Task<string> JsonRequest(string url, string? data = null)
		{
			return await Task.FromResult(string.Empty);
		}

		public static async Task<HtmlDocument> GetHtmlDocJs(string url)
		{
			return await Task.FromResult(new HtmlDocument());
		}

		// To handle GZipped Web Content
		// http://stackoverflow.com/a/4567408/2762059
		private class GzipWebClient : WebClient
		{
			protected override WebRequest? GetWebRequest(Uri address)
			{
				var request = (HttpWebRequest)base.GetWebRequest(address);
				if(request != null)
					request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
				return request;
			}
		}
	}
}
