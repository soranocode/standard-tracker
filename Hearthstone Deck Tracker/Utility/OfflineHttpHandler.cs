using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Hearthstone_Deck_Tracker.Utility;

internal sealed class OfflineHttpHandler : HttpMessageHandler
{
	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		=> Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
		{
			RequestMessage = request,
			Content = new StringContent("Standard Tracker uses local data only.")
		});
}
