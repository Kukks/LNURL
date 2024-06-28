using System;
using System.Text.Json.Serialization;
using BTCPayServer.Lightning;
using LNURL.Json.Newtonsoft;
using Newtonsoft.Json;

namespace LNURL.Requests;

/// <summary>
///     https://github.com/fiatjaf/lnurl-rfc/blob/luds/02.md
/// </summary>
public class LNURLChannelRequest : ILNURLRequest
{
    [JsonProperty("uri")]
    [JsonPropertyName("uri")]
    [Newtonsoft.Json.JsonConverter(typeof(NodeUriJsonConverter))]
    [System.Text.Json.Serialization.JsonConverter(typeof(Json.SystemJson.NodeUriJsonConverter))]
    public NodeInfo Uri { get; set; }

    [JsonProperty("callback")]
    [JsonPropertyName("callback")]
    [Newtonsoft.Json.JsonConverter(typeof(UriJsonConverter))]
    [System.Text.Json.Serialization.JsonConverter(typeof(Json.SystemJson.UriJsonConverter))]
    public Uri Callback { get; set; }

    [JsonProperty("k1")]
    [JsonPropertyName("k1")]
    public string K1 { get; set; }

    [JsonProperty("tag")]
    [JsonPropertyName("tag")]
    public string Tag { get; set; }
}