using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace LNURL;

/// <summary>
///     https://github.com/fiatjaf/lnurl-rfc/blob/luds/04.md
/// </summary>
public class LNAuthRequest
{
    public enum LNAuthRequestAction
    {
        Register,
        Login,
        Link,
        Auth
    }

    public Uri LNUrl { get; set; }


    [JsonProperty("tag")] public string Tag => "login";
    [JsonProperty("k1")] public string K1 { get; set; }

    [JsonProperty("action")]
    [JsonConverter(typeof(StringEnumConverter))]
    public LNAuthRequestAction? Action { get; set; }

    public Task<LNUrlStatusResponse> SendChallenge(ECDSASignature sig, PubKey key, HttpClient httpClient)
    {
        return SendChallenge(sig, key, httpClient, default);
    }
    public async Task<LNUrlStatusResponse> SendChallenge(ECDSASignature sig, PubKey key, HttpClient httpClient, CancellationToken cancellationToken)
    {
        var url = LNUrl;
        var uriBuilder = new UriBuilder(url);
        LNURL.AppendPayloadToQuery(uriBuilder, "sig", Encoders.Hex.EncodeData(sig.ToDER()));
        LNURL.AppendPayloadToQuery(uriBuilder, "key", key.ToHex());
        url = new Uri(uriBuilder.ToString());
        var response = JObject.Parse(await httpClient.GetStringAsync(url, cancellationToken));
        return response.ToObject<LNUrlStatusResponse>();
    }

    public Task<LNUrlStatusResponse> SendChallenge(Key key, HttpClient httpClient)
    {
        return SendChallenge(key, httpClient, default);
    }
    public Task<LNUrlStatusResponse> SendChallenge(Key key, HttpClient httpClient, CancellationToken cancellationToken)
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
        var messageHash = Hashes.DoubleSHA256(messageBytes);
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

        byte[] k1Bytes = null;
        try
        {
            k1Bytes = Encoders.Hex.DecodeData(k1);
        }
        catch (Exception e)
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
        var messageHash = Hashes.DoubleSHA256(expectedMessage);
        return expectedPubKey.Verify(messageHash, sig);
    }
}