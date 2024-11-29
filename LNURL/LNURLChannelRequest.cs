using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Lightning;
using LNURL.JsonConverters;
using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LNURL;

/// <summary>
///     https://github.com/fiatjaf/lnurl-rfc/blob/luds/02.md
/// </summary>
public class LNURLChannelRequest
{
    [JsonProperty("uri")]
    [JsonConverter(typeof(NodeUriJsonConverter))]
    public NodeInfo Uri { get; set; }

    [JsonProperty("callback")]
    [JsonConverter(typeof(UriJsonConverter))]
    public Uri Callback { get; set; }

    [JsonProperty("k1")] public string K1 { get; set; }

    [JsonProperty("tag")] public string Tag { get; set; }


    public async Task SendRequest(PubKey ourId, bool privateChannel, HttpClient httpClient,
        CancellationToken cancellationToken = default)
    {
        var url = Callback;
        var uriBuilder = new UriBuilder(url);
        LNURL.AppendPayloadToQuery(uriBuilder, "k1", K1);
        LNURL.AppendPayloadToQuery(uriBuilder, "remoteid", ourId.ToString());
        LNURL.AppendPayloadToQuery(uriBuilder, "private",privateChannel? "1":"0");
    
        url = new Uri(uriBuilder.ToString());
        var response = await httpClient.GetAsync(url, cancellationToken);
        var json = JObject.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (LNUrlStatusResponse.IsErrorResponse(json, out var error)) throw new LNUrlException(error.Reason);

    }

    public async Task CancelRequest(PubKey ourId, HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        var url = Callback;
        var uriBuilder = new UriBuilder(url);
        LNURL.AppendPayloadToQuery(uriBuilder, "k1", K1);
        LNURL.AppendPayloadToQuery(uriBuilder, "remoteid", ourId.ToString());
        LNURL.AppendPayloadToQuery(uriBuilder, "cancel", "1");
        
        url = new Uri(uriBuilder.ToString());
        var response = await httpClient.GetAsync(url, cancellationToken);
        var json = JObject.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (LNUrlStatusResponse.IsErrorResponse(json, out var error)) throw new LNUrlException(error.Reason);
    }
}