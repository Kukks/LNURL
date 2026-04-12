using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Lightning;
using LNURL.JsonConverters;
using NBitcoin;
using Newtonsoft.Json;
using STJ = System.Text.Json.Serialization;

namespace LNURL;

/// <summary>
/// Represents an LNURL-channel request as defined in LUD-02.
/// Allows a wallet to request a channel to be opened from the service node.
/// </summary>
public class LNURLChannelRequest
{
    /// <summary>
    /// Gets or sets the node URI of the service that will open the channel.
    /// </summary>
    [JsonProperty("uri")]
    [JsonConverter(typeof(NodeUriJsonConverter))]
    [STJ.JsonPropertyName("uri")]
    public NodeInfo Uri { get; set; }

    /// <summary>
    /// Gets or sets the callback URL to which the wallet sends its node ID to request a channel.
    /// </summary>
    [JsonProperty("callback")]
    [JsonConverter(typeof(UriJsonConverter))]
    [STJ.JsonPropertyName("callback")]
    public Uri Callback { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier for this channel request.
    /// </summary>
    [JsonProperty("k1")]
    [STJ.JsonPropertyName("k1")]
    public string K1 { get; set; }

    /// <summary>
    /// Gets or sets the LNURL tag. For channel requests this is always <c>"channelRequest"</c>.
    /// </summary>
    [JsonProperty("tag")]
    [STJ.JsonPropertyName("tag")]
    public string Tag { get; set; }

    /// <summary>
    /// Sends a channel open request to the service callback.
    /// </summary>
    public Task SendRequest(PubKey ourId, bool privateChannel, HttpClient httpClient,
        CancellationToken cancellationToken = default)
    {
        return SendRequest(ourId, privateChannel, new HttpLNURLCommunicator(httpClient), cancellationToken);
    }

    /// <summary>
    /// Sends a channel open request using a custom <see cref="ILNURLCommunicator"/> transport.
    /// </summary>
    public async Task SendRequest(PubKey ourId, bool privateChannel, ILNURLCommunicator communicator,
        CancellationToken cancellationToken = default)
    {
        var url = Callback;
        var uriBuilder = new UriBuilder(url);
        LNURL.AppendPayloadToQuery(uriBuilder, "k1", K1);
        LNURL.AppendPayloadToQuery(uriBuilder, "remoteid", ourId.ToString());
        LNURL.AppendPayloadToQuery(uriBuilder, "private", privateChannel ? "1" : "0");

        url = new Uri(uriBuilder.ToString());
        var content = await communicator.SendRequest(url, cancellationToken);
        if (LNUrlStatusResponse.IsErrorResponse(content, out var error)) throw new LNUrlException(error.Reason);
    }

    /// <summary>
    /// Sends a cancellation request for this channel request to the service callback.
    /// </summary>
    public Task CancelRequest(PubKey ourId, HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        return CancelRequest(ourId, new HttpLNURLCommunicator(httpClient), cancellationToken);
    }

    /// <summary>
    /// Sends a cancellation request using a custom <see cref="ILNURLCommunicator"/> transport.
    /// </summary>
    public async Task CancelRequest(PubKey ourId, ILNURLCommunicator communicator, CancellationToken cancellationToken = default)
    {
        var url = Callback;
        var uriBuilder = new UriBuilder(url);
        LNURL.AppendPayloadToQuery(uriBuilder, "k1", K1);
        LNURL.AppendPayloadToQuery(uriBuilder, "remoteid", ourId.ToString());
        LNURL.AppendPayloadToQuery(uriBuilder, "cancel", "1");

        url = new Uri(uriBuilder.ToString());
        var content = await communicator.SendRequest(url, cancellationToken);
        if (LNUrlStatusResponse.IsErrorResponse(content, out var error)) throw new LNUrlException(error.Reason);
    }
}
