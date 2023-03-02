using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NNostr.Client;

namespace LNURL;

public class LNURLCompositeCommunicator : ILNURLCommunicator
{
    private readonly HttpClient _httpClient;
    private readonly NostrClient _nostrClient;
    private readonly HttpLNURLCommunicator _httpLNURLCommunicator;
    private readonly NostrLNURLCommunicator _nostrLnurlCommunicator;

    public LNURLCompositeCommunicator(HttpClient httpClient = null, NostrClient nostrClient = null)
    {
        _httpLNURLCommunicator = new HttpLNURLCommunicator(httpClient);
        _nostrLnurlCommunicator = new NostrLNURLCommunicator(nostrClient);
    }

    public Task<JObject> SendRequest(Uri lnurl, CancellationToken cancellationToken = default)
    {
        return lnurl.Scheme == "nostr" ? _nostrLnurlCommunicator.SendRequest(lnurl, cancellationToken) : _httpLNURLCommunicator.SendRequest(lnurl, cancellationToken);
    }
}