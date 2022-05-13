using BTCPayServer.Lightning;
using LNURL.JsonConverters;
using Newtonsoft.Json;

namespace LNURL;

/// <summary>
///     https://github.com/fiatjaf/lnurl-rfc/blob/luds/07.md
/// </summary>
public class LNURLHostedChannelRequest
{
    [JsonProperty("uri")]
    [JsonConverter(typeof(NodeUriJsonConverter))]
    public NodeInfo Uri { get; set; }

    [JsonProperty("alias")] public string Alias { get; set; }

    [JsonProperty("k1")] public string K1 { get; set; }

    [JsonProperty("tag")] public string Tag { get; set; }
}