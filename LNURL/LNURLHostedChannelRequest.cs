using BTCPayServer.Lightning;
using LNURL.JsonConverters;
using Newtonsoft.Json;
using STJ = System.Text.Json.Serialization;

namespace LNURL;

/// <summary>
///     https://github.com/fiatjaf/lnurl-rfc/blob/luds/07.md
/// </summary>
public class LNURLHostedChannelRequest
{
    [JsonProperty("uri")]
    [JsonConverter(typeof(NodeUriJsonConverter))]
    [STJ.JsonPropertyName("uri")]
    public NodeInfo Uri { get; set; }

    [JsonProperty("alias")]
    [STJ.JsonPropertyName("alias")]
    public string Alias { get; set; }

    [JsonProperty("k1")]
    [STJ.JsonPropertyName("k1")]
    public string K1 { get; set; }

    [JsonProperty("tag")]
    [STJ.JsonPropertyName("tag")]
    public string Tag { get; set; }
}
