using BTCPayServer.Lightning;
using LNURL.JsonConverters;
using Newtonsoft.Json;
using STJ = System.Text.Json.Serialization;

namespace LNURL;

/// <summary>
/// Represents an LNURL hosted channel request as defined in LUD-07.
/// A hosted channel is a trust-based channel where the host holds all funds on behalf of the client.
/// </summary>
public class LNURLHostedChannelRequest
{
    /// <summary>
    /// Gets or sets the node URI of the host that will provide the hosted channel.
    /// </summary>
    [JsonProperty("uri")]
    [JsonConverter(typeof(NodeUriJsonConverter))]
    [STJ.JsonPropertyName("uri")]
    public NodeInfo Uri { get; set; }

    /// <summary>
    /// Gets or sets the alias (human-readable name) of the host node.
    /// </summary>
    [JsonProperty("alias")]
    [STJ.JsonPropertyName("alias")]
    public string Alias { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier for this hosted channel request.
    /// </summary>
    [JsonProperty("k1")]
    [STJ.JsonPropertyName("k1")]
    public string K1 { get; set; }

    /// <summary>
    /// Gets or sets the LNURL tag. For hosted channel requests this is always <c>"hostedChannelRequest"</c>.
    /// </summary>
    [JsonProperty("tag")]
    [STJ.JsonPropertyName("tag")]
    public string Tag { get; set; }
}
