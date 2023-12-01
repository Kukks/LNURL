using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using NBitcoin.Secp256k1;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NNostr.Client;
using NNostr.Client.Protocols;

namespace LNURL;

public class LNURLNostrHelper
{
    private readonly ECPrivKey _key;
    private ECXOnlyPubKey _pubKey => _key.CreateXOnlyPubKey();
    private readonly Uri[] _relays;
    private readonly Func<NameValueCollection, Task<JObject>> _handleData;
    private readonly IEnumerable<KeyValuePair<string, string>> _queryParams;

    public Uri Endpoint
    {
        get
        {
            var pubKey = _pubKey;
            var nip19 = string.Empty;
            if (_relays?.Any() is not true)
            {
                nip19 = pubKey.ToNIP19();
            }
            else
            {
                
                nip19 = new NIP19.NosteProfileNote()
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

    public Uri GetLNURL(string tag, bool bech32 = false) => LNURL.EncodeUri(Endpoint, tag, bech32);
    public LNURLNostrHelper(ECPrivKey key, Uri[] relays,Func<NameValueCollection, Task<JObject>> handleData, IEnumerable<KeyValuePair<string, string>> queryParams = null)
    {
        _key = key;
        _relays = relays;
        _handleData = handleData;
        _queryParams = queryParams;
    }

    public NostrSubscriptionFilter Filter => new NostrSubscriptionFilter()
    {
        ReferencedPublicKeys = new[] {_pubKey.ToHex()}
    };
    
    public async Task<NostrEvent?> HandleIncomingRequest(NostrEvent nostrEvent)
    {
        var ourPubKey = _pubKey.ToHex();
        if (nostrEvent.GetTaggedData("p").First() != _pubKey.ToHex())
        {
            return null;
        }

        var content = string.IsNullOrEmpty(nostrEvent.Content)? null: await nostrEvent.DecryptNip04EventAsync(_key);
        var uriB = new UriBuilder("https://dummy.com");
        uriB.Query = content;
        var values = uriB.Uri.ParseQueryString();
        var response = await _handleData(values);
        var contentResponse = response.ToString(Formatting.None);
        var evt = new NostrEvent()
        {
            Content =  contentResponse,
            PublicKey = ourPubKey,
            Kind = 4,
            CreatedAt = DateTimeOffset.Now,
            Tags = new List<NostrEventTag>
            {
                new()
                {
                    TagIdentifier = "p",
                    Data = new List<string>(){nostrEvent.PublicKey}
                }
            }
        };
        await evt.ComputeIdAndSignAsync(_key);
        return evt;

    }
}