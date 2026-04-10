using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using STJ = System.Text.Json.Serialization;

namespace LNURL;

/// <summary>
/// Represents the response from an LNURL payment verification endpoint as defined in LUD-21.
/// Allows a wallet to check whether a Lightning payment has been settled by the service.
/// </summary>
public class LNURLVerifyResponse
{
    /// <summary>
    /// Gets or sets the status string (e.g. <c>"OK"</c>).
    /// </summary>
    [JsonProperty("status")]
    [STJ.JsonPropertyName("status")]
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets whether the payment has been settled (received) by the service.
    /// </summary>
    [JsonProperty("settled")]
    [STJ.JsonPropertyName("settled")]
    public bool Settled { get; set; }

    /// <summary>
    /// Gets or sets the hex-encoded payment preimage, available once the payment is settled.
    /// </summary>
    [JsonProperty("preimage")]
    [STJ.JsonPropertyName("preimage")]
    public string Preimage { get; set; }

    /// <summary>
    /// Gets or sets the BOLT11 payment request string associated with this verification.
    /// </summary>
    [JsonProperty("pr")]
    [STJ.JsonPropertyName("pr")]
    public string Pr { get; set; }

    /// <summary>
    /// Fetches the payment verification status from a LUD-21 verify endpoint.
    /// </summary>
    /// <param name="verifyUrl">The verification URL (obtained from <see cref="LNURLPayRequest.LNURLPayRequestCallbackResponse.VerifyUrl"/>).</param>
    /// <param name="httpClient">The <see cref="HttpClient"/> used to perform the HTTP request.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="LNURLVerifyResponse"/> containing the settlement status.</returns>
    /// <exception cref="LNUrlException">Thrown when the verify endpoint returns an error response.</exception>
    public static async Task<LNURLVerifyResponse> FetchStatus(Uri verifyUrl, HttpClient httpClient,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(verifyUrl, cancellationToken);
        var json = JObject.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (LNUrlStatusResponse.IsErrorResponse(json, out var error))
            throw new LNUrlException(error.Reason);

        return json.ToObject<LNURLVerifyResponse>();
    }
}
