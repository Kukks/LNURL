using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Lightning;
using BTCPayServer.Lightning.JsonConverters;
using LNURL.JsonConverters;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using STJ = System.Text.Json.Serialization;

namespace LNURL;

/// <summary>
/// Represents an LNURL-withdraw request as defined in LUD-03.
/// Allows a wallet to withdraw funds by providing a BOLT11 invoice to the service.
/// Also supports LUD-14 (balance check/notify), LUD-15 (balance notify parameter),
/// and LUD-19 (pay link).
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
    /// Gets or sets the unique identifier for this withdraw request. Must be included in the callback.
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
    /// This is a convenience overload without PIN support.
    /// </summary>
    /// <param name="bolt11">The BOLT11 payment request (invoice) string for receiving the withdrawn funds.</param>
    /// <param name="httpClient">The <see cref="HttpClient"/> used to perform the HTTP request.</param>
    /// <param name="balanceNotify">An optional URL the service should call when the wallet's balance changes (LUD-15).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="LNUrlStatusResponse"/> indicating success or failure.</returns>
    public Task<LNUrlStatusResponse> SendRequest(string bolt11, HttpClient httpClient,
        Uri balanceNotify = null, CancellationToken cancellationToken = default)
    {
        return SendRequest(bolt11, httpClient, null, balanceNotify, cancellationToken);
    }

    /// <summary>
    /// Sends a withdrawal request to the service callback with the specified BOLT11 invoice,
    /// optional PIN, and optional balance notification URL.
    /// </summary>
    /// <param name="bolt11">The BOLT11 payment request (invoice) string for receiving the withdrawn funds.</param>
    /// <param name="httpClient">The <see cref="HttpClient"/> used to perform the HTTP request.</param>
    /// <param name="pin">An optional PIN for BoltCard withdraw-pin support.</param>
    /// <param name="balanceNotify">An optional URL the service should call when the wallet's balance changes (LUD-15).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="LNUrlStatusResponse"/> indicating success or failure.</returns>
    public async Task<LNUrlStatusResponse> SendRequest(string bolt11, HttpClient httpClient, string pin = null,
        Uri balanceNotify = null, CancellationToken cancellationToken = default)
    {
        var url = Callback;
        var uriBuilder = new UriBuilder(url);
        LNURL.AppendPayloadToQuery(uriBuilder, "pr", bolt11);
        LNURL.AppendPayloadToQuery(uriBuilder, "k1", K1);
        if (balanceNotify != null) LNURL.AppendPayloadToQuery(uriBuilder, "balanceNotify", balanceNotify.ToString());
        if (pin != null) LNURL.AppendPayloadToQuery(uriBuilder, "pin", pin);

        url = new Uri(uriBuilder.ToString());
        var response = await httpClient.GetAsync(url, cancellationToken);
        var json = JObject.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        return json.ToObject<LNUrlStatusResponse>();
    }
}
