using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NNostr.Client;

namespace LNURL;

/// <summary>
/// An <see cref="ILNURLCommunicator"/> that routes requests by URI scheme:
/// <c>nostr:</c> URIs are handled via Nostr relays, all others via HTTP.
/// </summary>
public class LNURLCompositeCommunicator : ILNURLCommunicator
{
    private readonly HttpLNURLCommunicator _httpCommunicator;
    private readonly NostrLNURLCommunicator _nostrCommunicator;

    /// <summary>
    /// Initializes a new composite communicator.
    /// </summary>
    /// <param name="httpClient">The HTTP client for HTTP-based LNURL requests. If <c>null</c>, a new instance is created.</param>
    /// <param name="nostrClient">An optional Nostr client for relay-based LNURL requests.</param>
    public LNURLCompositeCommunicator(HttpClient httpClient = null, NostrClient nostrClient = null)
    {
        _httpCommunicator = new HttpLNURLCommunicator(httpClient);
        _nostrCommunicator = new NostrLNURLCommunicator(nostrClient);
    }

    /// <inheritdoc />
    public Task<string> SendRequest(Uri lnurl, CancellationToken cancellationToken = default)
    {
        return lnurl.Scheme == "nostr"
            ? _nostrCommunicator.SendRequest(lnurl, cancellationToken)
            : _httpCommunicator.SendRequest(lnurl, cancellationToken);
    }
}
