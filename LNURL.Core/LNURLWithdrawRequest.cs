using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Lightning;
using BTCPayServer.Lightning.JsonConverters;
using LNURL.JsonConverters;
using Newtonsoft.Json;
using STJ = System.Text.Json.Serialization;

namespace LNURL;

/// <summary>
/// Represents an LNURL-withdraw request as defined in LUD-03.
/// Allows a wallet to withdraw funds by providing a BOLT11 invoice to the service.
/// </summary>
public class LNURLWithdrawRequest
{
    /// <summary>
    /// Gets or sets the callback URL to which the wallet submits its BOLT11 invoice for withdrawal.
    /// </summary>
    [JsonProperty("callback")]
    [JsonConverter(typeof(UriJsonConverter))]
    [STJ.JsonPropertyName("callback")]
    public Uri Callback { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier for this withdraw request.
    /// </summary>
    [JsonProperty("k1")]
    [STJ.JsonPropertyName("k1")]
    public string K1 { get; set; }

    /// <summary>
    /// Gets or sets the LNURL tag. For withdraw requests this is always <c>"withdrawRequest"</c>.
    /// </summary>
    [JsonProperty("tag")]
    [STJ.JsonPropertyName("tag")]
    public string Tag { get; set; }

    /// <summary>
    /// Gets or sets the default description for the BOLT11 invoice.
    /// </summary>
    [JsonProperty("defaultDescription")]
    [STJ.JsonPropertyName("defaultDescription")]
    public string DefaultDescription { get; set; }

    /// <summary>
    /// Gets or sets the minimum withdrawable amount in millisatoshis.
    /// </summary>
    [JsonProperty("minWithdrawable")]
    [JsonConverter(typeof(LightMoneyJsonConverter))]
    [STJ.JsonPropertyName("minWithdrawable")]
    public LightMoney MinWithdrawable { get; set; }

    /// <summary>
    /// Gets or sets the maximum withdrawable amount in millisatoshis.
    /// </summary>
    [JsonProperty("maxWithdrawable")]
    [JsonConverter(typeof(LightMoneyJsonConverter))]
    [STJ.JsonPropertyName("maxWithdrawable")]
    public LightMoney MaxWithdrawable { get; set; }

    /// <summary>
    /// Gets or sets the current balance available for withdrawal, as defined in LUD-14.
    /// </summary>
    [JsonProperty("currentBalance")]
    [JsonConverter(typeof(LightMoneyJsonConverter))]
    [STJ.JsonPropertyName("currentBalance")]
    public LightMoney CurrentBalance { get; set; }

    /// <summary>
    /// Gets or sets the URL the wallet can poll to check balance updates, as defined in LUD-14.
    /// </summary>
    [JsonProperty("balanceCheck")]
    [JsonConverter(typeof(UriJsonConverter))]
    [STJ.JsonPropertyName("balanceCheck")]
    public Uri BalanceCheck { get; set; }

    /// <summary>
    /// Gets or sets an optional LNURL-pay link associated with this withdraw request, as defined in LUD-19.
    /// </summary>
    [JsonProperty("payLink", NullValueHandling = NullValueHandling.Ignore)]
    [JsonConverter(typeof(UriJsonConverter))]
    [STJ.JsonPropertyName("payLink")]
    public Uri PayLink { get; set; }

    /// <summary>
    /// Gets or sets the maximum amount that can be withdrawn using a PIN, for BoltCard withdraw-pin support.
    /// </summary>
    [JsonProperty("pinLimit", NullValueHandling = NullValueHandling.Ignore)]
    [JsonConverter(typeof(LightMoneyJsonConverter))]
    [STJ.JsonPropertyName("pinLimit")]
    public LightMoney PinLimit { get; set; }

    /// <summary>
    /// Sends a withdrawal request to the service callback with the specified BOLT11 invoice.
    /// </summary>
    public Task<LNUrlStatusResponse> SendRequest(string bolt11, HttpClient httpClient,
        Uri balanceNotify = null, CancellationToken cancellationToken = default)
    {
        return SendRequest(bolt11, httpClient, null, balanceNotify, cancellationToken);
    }

    /// <summary>
    /// Sends a withdrawal request to the service callback with the specified BOLT11 invoice,
    /// optional PIN, and optional balance notification URL.
    /// </summary>
    public Task<LNUrlStatusResponse> SendRequest(string bolt11, HttpClient httpClient, string pin = null,
        Uri balanceNotify = null, CancellationToken cancellationToken = default)
    {
        return SendRequest(bolt11, new HttpLNURLCommunicator(httpClient), pin, balanceNotify, cancellationToken);
    }

    /// <summary>
    /// Sends a withdrawal request using a custom <see cref="ILNURLCommunicator"/> transport.
    /// </summary>
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
        var content = await communicator.SendRequest(url, cancellationToken);

        return System.Text.Json.JsonSerializer.Deserialize<LNUrlStatusResponse>(content, LNURLJsonOptions.Default);
    }
}
