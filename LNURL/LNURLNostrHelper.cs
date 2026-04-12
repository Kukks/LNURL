using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using NBitcoin.Secp256k1;
using NNostr.Client;
using NNostr.Client.Protocols;

namespace LNURL;

/// <summary>
/// Server-side helper for merchants to expose LNURL endpoints over Nostr relays.
/// Handles incoming NIP-17 (Gift Wrap / NIP-59) LNURL requests and produces wrapped responses.
/// </summary>
public class LNURLNostrHelper
{
    private readonly ECPrivKey _key;
    private ECXOnlyPubKey PubKey => _key.CreateXOnlyPubKey();
    private readonly Uri[] _relays;
    private readonly Func<NameValueCollection, Task<string>> _handleData;
    private readonly IEnumerable<KeyValuePair<string, string>> _queryParams;

    /// <summary>
    /// Gets the <c>nostr:</c> endpoint URI (nprofile-based) for this helper.
    /// </summary>
    public Uri Endpoint
    {
        get
        {
            var pubKey = PubKey;
            string nip19;
            if (_relays?.Any() is not true)
            {
                nip19 = pubKey.ToNIP19();
            }
            else
            {
                nip19 = new NIP19.NosteProfileNote
                {
                    PubKey = pubKey.ToHex(),
                    Relays = _relays.Select(r => r.ToString()).ToArray()
                }.ToNIP19();
            }

            var uriBuilder = new UriBuilder("nostr", nip19);
            if (_queryParams?.Any() is true)
            {
                foreach (var param in _queryParams)
                {
                    LNURL.AppendPayloadToQuery(uriBuilder, param.Key, param.Value);
                }
            }

            return uriBuilder.Uri;
        }
    }

    /// <summary>
    /// Gets the Nostr subscription filter for listening to incoming LNURL requests
    /// addressed to this helper. Listens for Kind 1059 (Gift Wrap) events.
    /// </summary>
    public NostrSubscriptionFilter Filter => new()
    {
        ReferencedPublicKeys = new[] { PubKey.ToHex() },
        Kinds = new[] { 1059 }
    };

    /// <summary>
    /// Gets an LNURL-encoded URI for this helper's Nostr endpoint.
    /// </summary>
    /// <param name="tag">The LNURL tag (e.g. <c>"payRequest"</c>, <c>"withdrawRequest"</c>).</param>
    /// <param name="bech32">If <c>true</c>, returns a bech32-encoded LNURL; otherwise a LUD-17 scheme URI.</param>
    public Uri GetLNURL(string tag, bool bech32 = false) => LNURL.EncodeUri(Endpoint, tag, bech32);

    /// <summary>
    /// Initializes a new LNURL Nostr helper for a merchant.
    /// </summary>
    /// <param name="key">The merchant's Nostr private key.</param>
    /// <param name="relays">Nostr relay URIs where the merchant listens for requests.</param>
    /// <param name="handleData">
    /// Callback that processes incoming LNURL query parameters and returns a raw JSON response string.
    /// </param>
    /// <param name="queryParams">Optional additional query parameters to include in the endpoint URI.</param>
    public LNURLNostrHelper(ECPrivKey key, Uri[] relays,
        Func<NameValueCollection, Task<string>> handleData,
        IEnumerable<KeyValuePair<string, string>> queryParams = null)
    {
        _key = key;
        _relays = relays;
        _handleData = handleData;
        _queryParams = queryParams;
    }

    /// <summary>
    /// Creates a Kind 31120 parameterized replaceable event containing LNURL parameters.
    /// Publish this event to your relays so wallets using <c>naddr</c> can fetch it directly.
    /// </summary>
    /// <param name="parametersJson">The LNURL parameters JSON (same format as the HTTP response).</param>
    /// <param name="tag">The LNURL tag (e.g. <c>"payRequest"</c>) — used as the <c>d</c> tag value.</param>
    /// <returns>A signed Kind 31120 event ready to publish.</returns>
    public async Task<NostrEvent> CreateParameterEvent(string parametersJson, string tag)
    {
        var evt = new NostrEvent
        {
            Kind = NostrLNURLCommunicator.LnurlParameterEventKind,
            PublicKey = PubKey.ToHex(),
            Content = parametersJson,
            CreatedAt = DateTimeOffset.UtcNow,
            Tags = new List<NostrEventTag>
            {
                new() { TagIdentifier = "d", Data = new List<string> { tag } }
            }
        };
        await evt.ComputeIdAndSignAsync(_key);
        return evt;
    }

    /// <summary>
    /// Gets an <c>naddr</c>-based <c>nostr:</c> URI for this helper, pointing to a Kind 31120
    /// parameterized replaceable event with the given tag as the <c>d</c> value.
    /// </summary>
    /// <param name="tag">The LNURL tag (e.g. <c>"payRequest"</c>).</param>
    public Uri GetNaddrEndpoint(string tag)
    {
        var naddr = new NIP19.NostrAddressNote
        {
            Kind = (uint)NostrLNURLCommunicator.LnurlParameterEventKind,
            Author = PubKey.ToHex(),
            Identifier = Convert.ToHexString(Encoding.UTF8.GetBytes(tag)),
            Relays = _relays?.Select(r => r.ToString()).ToArray() ?? Array.Empty<string>()
        };
        return new UriBuilder("nostr", naddr.ToNIP19()).Uri;
    }

    /// <summary>
    /// Gets an LNURL-encoded URI using <c>naddr</c> addressing.
    /// </summary>
    /// <param name="tag">The LNURL tag (e.g. <c>"payRequest"</c>).</param>
    /// <param name="bech32">If <c>true</c>, returns a bech32-encoded LNURL; otherwise a LUD-17 scheme URI.</param>
    public Uri GetNaddrLNURL(string tag, bool bech32 = false) => LNURL.EncodeUri(GetNaddrEndpoint(tag), tag, bech32);

    /// <summary>
    /// Handles an incoming NIP-17 Gift Wrap event containing an LNURL request.
    /// Unwraps the NIP-59 layers, processes the request via the callback,
    /// and returns a Gift Wrapped response event to publish.
    /// </summary>
    /// <param name="nostrEvent">The incoming Kind 1059 (Gift Wrap) Nostr event.</param>
    /// <returns>A Gift Wrapped response event to publish, or <c>null</c> if the event cannot be processed.</returns>
    public async Task<NostrEvent> HandleIncomingRequest(NostrEvent nostrEvent)
    {
        NostrEvent innerEvent;
        try
        {
            innerEvent = await NIP17.Open(nostrEvent, _key);
        }
        catch
        {
            return null;
        }

        var senderPubKey = NostrExtensions.ParsePubKey(innerEvent.PublicKey);
        var content = innerEvent.Content;

        var values = HttpUtility.ParseQueryString(content ?? string.Empty);
        var response = await _handleData(values);

        // Create a Kind 14 DM response
        var responseDm = new NostrEvent
        {
            Content = response,
            PublicKey = PubKey.ToHex(),
            Kind = 14,
            CreatedAt = DateTimeOffset.Now,
            Tags = new List<NostrEventTag>
            {
                new() { TagIdentifier = "p", Data = new List<string> { innerEvent.PublicKey } }
            }
        };

        // Wrap using NIP-17 (Seal + Gift Wrap)
        return await NIP17.Create(responseDm, _key, senderPubKey, responseDm.Tags.ToArray());
    }
}
