using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using STJ = System.Text.Json.Serialization;

namespace LNURL;

/// <summary>
/// Represents an LNURL-auth request as defined in LUD-04.
/// Provides passwordless authentication by signing a challenge (<c>k1</c>) with a Lightning node key.
/// </summary>
public class LNAuthRequest
{
    /// <summary>
    /// Defines the action types for LNURL-auth requests (LUD-04).
    /// </summary>
    public enum LNAuthRequestAction
    {
        /// <summary>Register a new account.</summary>
        Register,
        /// <summary>Log in to an existing account.</summary>
        Login,
        /// <summary>Link an additional authentication method to an existing account.</summary>
        Link,
        /// <summary>Authorize a specific action or scope.</summary>
        Auth
    }

    /// <summary>
    /// Gets or sets the full LNURL-auth URL.
    /// </summary>
    [JsonIgnore]
    [STJ.JsonIgnore]
    public Uri LNUrl { get; set; }

    /// <summary>
    /// Gets the LNURL tag. For auth requests this is always <c>"login"</c>.
    /// </summary>
    [JsonProperty("tag")]
    [STJ.JsonPropertyName("tag")]
    public string Tag => "login";

    /// <summary>
    /// Gets or sets the hex-encoded 32-byte challenge that the wallet must sign.
    /// </summary>
    [JsonProperty("k1")]
    [STJ.JsonPropertyName("k1")]
    public string K1 { get; set; }

    /// <summary>
    /// Gets or sets the optional action hint indicating the purpose of this authentication request.
    /// </summary>
    [JsonProperty("action")]
    [JsonConverter(typeof(StringEnumConverter))]
    [STJ.JsonPropertyName("action")]
    public LNAuthRequestAction? Action { get; set; }

    /// <summary>
    /// Sends the signed challenge and public key to the LNURL-auth service to complete authentication.
    /// </summary>
    public Task<LNUrlStatusResponse> SendChallenge(ECDSASignature sig, PubKey key, HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        return SendChallenge(sig, key, new HttpLNURLCommunicator(httpClient), cancellationToken);
    }

    /// <summary>
    /// Sends the signed challenge using a custom <see cref="ILNURLCommunicator"/> transport.
    /// </summary>
    public async Task<LNUrlStatusResponse> SendChallenge(ECDSASignature sig, PubKey key, ILNURLCommunicator communicator, CancellationToken cancellationToken = default)
    {
        var url = LNUrl;
        var uriBuilder = new UriBuilder(url);
        LNURL.AppendPayloadToQuery(uriBuilder, "sig", Encoders.Hex.EncodeData(sig.ToDER()));
        LNURL.AppendPayloadToQuery(uriBuilder, "key", key.ToHex());
        url = new Uri(uriBuilder.ToString());
        var content = await communicator.SendRequest(url, cancellationToken);

        return System.Text.Json.JsonSerializer.Deserialize<LNUrlStatusResponse>(content, LNURLJsonOptions.Default);
    }

    /// <summary>
    /// Signs the <see cref="K1"/> challenge with the given key and sends the result.
    /// </summary>
    public Task<LNUrlStatusResponse> SendChallenge(Key key, HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        var sig = SignChallenge(key);
        return SendChallenge(sig, key.PubKey, httpClient, cancellationToken);
    }

    /// <summary>
    /// Signs the <see cref="K1"/> challenge with the given key and sends the result using a custom transport.
    /// </summary>
    public Task<LNUrlStatusResponse> SendChallenge(Key key, ILNURLCommunicator communicator, CancellationToken cancellationToken = default)
    {
        var sig = SignChallenge(key);
        return SendChallenge(sig, key.PubKey, communicator, cancellationToken);
    }

    /// <summary>
    /// Signs this request's <see cref="K1"/> challenge with the given private key.
    /// </summary>
    public ECDSASignature SignChallenge(Key key)
    {
        return SignChallenge(key, K1);
    }

    /// <summary>
    /// Signs an arbitrary hex-encoded challenge with the given private key.
    /// </summary>
    public static ECDSASignature SignChallenge(Key key, string k1)
    {
        var messageBytes = Encoders.Hex.DecodeData(k1);
        var messageHash = new uint256(messageBytes);
        return key.Sign(messageHash);
    }

    /// <summary>
    /// Validates that a service URL conforms to the LNURL-auth requirements (LUD-04).
    /// </summary>
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

    /// <summary>
    /// Verifies that a given ECDSA signature is valid for the expected public key and message.
    /// </summary>
    public static bool VerifyChallenge(ECDSASignature sig, PubKey expectedPubKey, byte[] expectedMessage)
    {
        var messageHash = new uint256(expectedMessage);
        return expectedPubKey.Verify(messageHash, sig);
    }
}
