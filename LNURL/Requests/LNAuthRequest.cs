using System;
using System.Collections.Specialized;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;

namespace LNURL.Requests;

/// <summary>
///     https://github.com/fiatjaf/lnurl-rfc/blob/luds/04.md
/// </summary>
public class LNAuthRequest : ILNURLRequest
{
    public enum LNAuthRequestAction
    {
        Register,
        Login,
        Link,
        Auth
    }

    public Uri LNUrl { get; set; }


    [JsonProperty("tag")]
    [JsonPropertyName("tag")]
    public string Tag => "login";

    [JsonProperty("k1")]
    [JsonPropertyName("k1")]
    public string K1 { get; set; }

    [JsonProperty("action")]
    [JsonPropertyName("action")]
    [Newtonsoft.Json.JsonConverter(typeof(StringEnumerator))]
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonStringEnumConverter))]
    public LNAuthRequestAction? Action { get; set; }

    public async Task<LNUrlStatusResponse> SendChallenge(ECDSASignature sig, PubKey key, HttpClient httpClient,
        CancellationToken cancellationToken = default)
    {
        var url = LNUrl;
        var uriBuilder = new UriBuilder(url);
        LNURL.AppendPayloadToQuery(uriBuilder, "sig", Encoders.Hex.EncodeData(sig.ToDER()));
        LNURL.AppendPayloadToQuery(uriBuilder, "key", key.ToHex());
        url = new Uri(uriBuilder.ToString());
        return await httpClient.GetFromJsonAsync<LNUrlStatusResponse>(url, cancellationToken);
    }

    public Task<LNUrlStatusResponse> SendChallenge(Key key, HttpClient httpClient,
        CancellationToken cancellationToken = default)
    {
        var sig = SignChallenge(key);
        return SendChallenge(sig, key.PubKey, httpClient, cancellationToken);
    }

    public ECDSASignature SignChallenge(Key key)
    {
        return SignChallenge(key, K1);
    }

    public static ECDSASignature SignChallenge(Key key, string k1)
    {
        var messageBytes = Encoders.Hex.DecodeData(k1);
        var messageHash = new uint256(messageBytes);
        return key.Sign(messageHash);
    }

    public static void EnsureValidUrl(Uri serviceUrl)
    {
        var tag = serviceUrl.ParseQueryString().Get("tag");
        if (tag != "login")
            throw new ArgumentException(nameof(serviceUrl),
                "LNURL-Auth(LUD04) requires tag to be provided straight away");
        var k1 = serviceUrl.ParseQueryString().Get("k1");
        if (k1 is null) throw new ArgumentException(nameof(serviceUrl), "LNURL-Auth(LUD04) requires k1 to be provided");

        byte[] k1Bytes;
        try
        {
            k1Bytes = Encoders.Hex.DecodeData(k1);
        }
        catch (Exception)
        {
            throw new ArgumentException(nameof(serviceUrl), "LNURL-Auth(LUD04) requires k1 to be hex encoded");
        }

        if (k1Bytes.Length != 32)
            throw new ArgumentException(nameof(serviceUrl), "LNURL-Auth(LUD04) requires k1 to be 32bytes");

        var action = serviceUrl.ParseQueryString().Get("action");
        if (action != null && !Enum.TryParse(typeof(LNAuthRequestAction), action, true, out _))
            throw new ArgumentException(nameof(serviceUrl), "LNURL-Auth(LUD04) action value was invalid");
    }

    public static bool VerifyChallenge(ECDSASignature sig, PubKey expectedPubKey, byte[] expectedMessage)
    {
        var messageHash = new uint256(expectedMessage);
        return expectedPubKey.Verify(messageHash, sig);
    }
}