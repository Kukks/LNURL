using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using STJ = System.Text.Json.Serialization;

namespace LNURL;

/// <summary>
///     https://github.com/lnurl/luds/blob/luds/21.md
/// </summary>
public class LNURLVerifyResponse
{
    [JsonProperty("status")]
    [STJ.JsonPropertyName("status")]
    public string Status { get; set; }

    [JsonProperty("settled")]
    [STJ.JsonPropertyName("settled")]
    public bool Settled { get; set; }

    [JsonProperty("preimage")]
    [STJ.JsonPropertyName("preimage")]
    public string Preimage { get; set; }

    [JsonProperty("pr")]
    [STJ.JsonPropertyName("pr")]
    public string Pr { get; set; }

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
