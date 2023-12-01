using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Lightning;
using BTCPayServer.Lightning.JsonConverters;
using LNURL.JsonConverters;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LNURL;

/// <summary>
///     https://github.com/fiatjaf/lnurl-rfc/blob/luds/02.md
/// </summary>
public class LNURLWithdrawRequest
{
    [JsonProperty("callback")]
    [JsonConverter(typeof(UriJsonConverter))]
    public Uri Callback { get; set; }

    [JsonProperty("k1")] public string K1 { get; set; }

    [JsonProperty("tag")] public string Tag { get; set; }

    [JsonProperty("defaultDescription")] public string DefaultDescription { get; set; }

    [JsonProperty("minWithdrawable")]
    [JsonConverter(typeof(LightMoneyJsonConverter))]
    public LightMoney MinWithdrawable { get; set; }

    [JsonProperty("maxWithdrawable")]
    [JsonConverter(typeof(LightMoneyJsonConverter))]
    public LightMoney MaxWithdrawable { get; set; }

    //https://github.com/fiatjaf/lnurl-rfc/blob/luds/14.md
    [JsonProperty("currentBalance")]
    [JsonConverter(typeof(LightMoneyJsonConverter))]
    public LightMoney CurrentBalance { get; set; }

    //https://github.com/fiatjaf/lnurl-rfc/blob/luds/14.md
    [JsonProperty("balanceCheck")]
    [JsonConverter(typeof(UriJsonConverter))]
    public Uri BalanceCheck { get; set; }

    //https://github.com/fiatjaf/lnurl-rfc/blob/luds/19.md
    [JsonProperty("payLink", NullValueHandling = NullValueHandling.Ignore)]
    [JsonConverter(typeof(UriJsonConverter))]
    public Uri PayLink { get; set; }
    
    //https://github.com/bitcoin-ring/luds/blob/withdraw-pin/21.md
    [JsonProperty("pinLimit", NullValueHandling = NullValueHandling.Ignore)]
    [JsonConverter(typeof(LightMoneyJsonConverter))]
    public LightMoney PinLimit { get; set; }

    //https://github.com/fiatjaf/lnurl-rfc/blob/luds/15.md
    public Task<LNUrlStatusResponse> SendRequest(string bolt11, HttpClient httpClient, string pin = null,
        Uri balanceNotify = null, CancellationToken cancellationToken = default)
    {
        return SendRequest(bolt11, httpClient, null, balanceNotify, cancellationToken);
    }    
    public async Task<LNUrlStatusResponse> SendRequest(string bolt11, ILNURLCommunicator communicator, string pin = null,
        Uri balanceNotify = null, CancellationToken cancellationToken = default)
    {
        var url = Callback;
        var uriBuilder = new UriBuilder(url);
        LNURL.AppendPayloadToQuery(uriBuilder, "pr", bolt11);
        LNURL.AppendPayloadToQuery(uriBuilder, "k1", K1);
        if (balanceNotify != null) LNURL.AppendPayloadToQuery(uriBuilder, "balanceNotify", balanceNotify.ToString());
        if (pin != null) LNURL.AppendPayloadToQuery(uriBuilder, "pin", pin);

        url = new Uri(uriBuilder.ToString());

        var response = await communicator.SendRequest(url, cancellationToken);
        return response.ToObject<LNUrlStatusResponse>();
    }
}
