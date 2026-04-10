using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using BTCPayServer.Lightning;
using BTCPayServer.Lightning.JsonConverters;
using LNURL.JsonConverters;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using STJ = System.Text.Json.Serialization;

namespace LNURL;

/// <summary>
/// Represents an LNURL-pay request as defined in LUD-06.
/// Contains the service parameters for initiating a Lightning payment, including
/// sendable amount range, metadata, and optional payer data fields (LUD-18).
/// </summary>
/// <remarks>
/// After obtaining this object (via <see cref="LNURL.FetchInformation(Uri, HttpClient)"/> or
/// <see cref="LNURL.FetchPayRequestViaInternetIdentifier(string, HttpClient)"/>),
/// call <see cref="SendRequest"/> to complete the payment flow and receive a BOLT11 invoice.
/// </remarks>
public class LNURLPayRequest
{
    /// <summary>
    /// Gets or sets the callback URL to which the wallet sends the payment amount to receive a BOLT11 invoice.
    /// </summary>
    [JsonProperty("callback")]
    [JsonConverter(typeof(UriJsonConverter))]
    [STJ.JsonPropertyName("callback")]
    public Uri Callback { get; set; }

    /// <summary>
    /// Gets or sets the raw JSON-encoded metadata string. This is a JSON array of <c>[mime-type, content]</c> pairs
    /// whose SHA-256 hash must match the payment request description hash (LUD-06).
    /// </summary>
    /// <seealso cref="ParsedMetadata"/>
    [JsonProperty("metadata")]
    [STJ.JsonPropertyName("metadata")]
    public string Metadata { get; set; }

    /// <summary>
    /// Gets the parsed metadata as a list of key-value pairs where the key is the MIME type
    /// and the value is the content (e.g. <c>text/plain</c>, <c>image/png;base64</c>).
    /// </summary>
    [JsonIgnore]
    [STJ.JsonIgnore]
    public List<KeyValuePair<string, string>> ParsedMetadata => JsonConvert
        .DeserializeObject<string[][]>(Metadata ?? string.Empty)
        .Select(strings => new KeyValuePair<string, string>(strings[0], strings[1])).ToList();

    /// <summary>
    /// Gets or sets the LNURL tag. For pay requests this is always <c>"payRequest"</c>.
    /// </summary>
    [JsonProperty("tag")]
    [STJ.JsonPropertyName("tag")]
    public string Tag { get; set; }

    /// <summary>
    /// Gets or sets the minimum amount the service accepts, in millisatoshis.
    /// </summary>
    [JsonProperty("minSendable")]
    [JsonConverter(typeof(LightMoneyJsonConverter))]
    [STJ.JsonPropertyName("minSendable")]
    public LightMoney MinSendable { get; set; }

    /// <summary>
    /// Gets or sets the maximum amount the service accepts, in millisatoshis.
    /// </summary>
    [JsonProperty("maxSendable")]
    [JsonConverter(typeof(LightMoneyJsonConverter))]
    [STJ.JsonPropertyName("maxSendable")]
    public LightMoney MaxSendable { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of characters allowed in a payment comment, as defined in LUD-12.
    /// A value of <c>null</c> or <c>0</c> means comments are not supported.
    /// </summary>
    [JsonProperty("commentAllowed", NullValueHandling = NullValueHandling.Ignore)]
    [STJ.JsonPropertyName("commentAllowed")]
    public int? CommentAllowed { get; set; }

    /// <summary>
    /// Gets or sets an optional LNURL-withdraw link associated with this pay request, as defined in LUD-19.
    /// Enables a combined pay-and-withdraw flow.
    /// </summary>
    [JsonProperty("withdrawLink", NullValueHandling = NullValueHandling.Ignore)]
    [JsonConverter(typeof(UriJsonConverter))]
    [STJ.JsonPropertyName("withdrawLink")]
    public Uri WithdrawLink { get; set; }

    /// <summary>
    /// Gets or sets the payer data fields that the service accepts or requires, as defined in LUD-18.
    /// </summary>
    [JsonProperty("payerData", NullValueHandling = NullValueHandling.Ignore)]
    [STJ.JsonPropertyName("payerData")]
    public LUD18PayerData PayerData { get; set; }

    /// <summary>
    /// Gets or sets additional JSON properties not mapped to strongly-typed members.
    /// </summary>
    [JsonExtensionData] public IDictionary<string, JToken> AdditionalData { get; set; }

    /// <summary>
    /// Gets or sets the Nostr public key of the service, used for zap receipts as defined in NIP-57.
    /// </summary>
    [JsonProperty("nostrPubkey", NullValueHandling = NullValueHandling.Ignore)]
    [STJ.JsonPropertyName("nostrPubkey")]
    public string? NostrPubkey { get; set; }

    /// <summary>
    /// Gets or sets whether this service supports Nostr zap receipts (NIP-57).
    /// </summary>
    [JsonProperty("allowsNostr", NullValueHandling = NullValueHandling.Ignore)]
    [STJ.JsonPropertyName("allowsNostr")]
    public bool? AllowsNostr { get; set; }

    /// <summary>
    /// Verifies that the given payer data response satisfies the requirements defined in this pay request's
    /// <see cref="PayerData"/> fields (LUD-18).
    /// </summary>
    /// <param name="response">The payer data response to validate.</param>
    /// <returns><c>true</c> if the payer data is valid; otherwise <c>false</c>.</returns>
    public bool VerifyPayerData(LUD18PayerDataResponse response)
    {
        return VerifyPayerData(PayerData, response);
    }

    /// <summary>
    /// Verifies that a payer data response satisfies the given payer data field requirements (LUD-18),
    /// including cryptographic verification of the auth challenge signature.
    /// </summary>
    /// <param name="payerFields">The payer data fields describing what is required or accepted.</param>
    /// <param name="payerData">The payer data response to validate.</param>
    /// <returns><c>true</c> if the payer data is valid; otherwise <c>false</c>.</returns>
    public static bool VerifyPayerData(LUD18PayerData payerFields, LUD18PayerDataResponse payerData)
    {
        if ((payerFields.Name is null && !string.IsNullOrEmpty(payerData.Name)) ||
            (payerFields.Name?.Mandatory is true && string.IsNullOrEmpty(payerData.Name)) ||
            (payerFields.Pubkey is null && payerData.Pubkey is not null) ||
            (payerFields.Pubkey?.Mandatory is true && payerData.Pubkey is null) ||
            (payerFields.Email is null && !string.IsNullOrEmpty(payerData.Email)) ||
            (payerFields.Email?.Mandatory is true && string.IsNullOrEmpty(payerData.Email)) ||
            (payerFields.Auth is null && payerData.Auth is not null) ||
            (payerFields.Auth?.Mandatory is true && payerData.Auth is null) ||
            payerFields.Auth?.K1 != payerData.Auth?.K1 ||
            !LNAuthRequest.VerifyChallenge(payerData.Auth.Sig, payerData.Auth.Key,
                Encoders.Hex.DecodeData(payerData.Auth?.K1)))
            return false;

        return true;
    }

    /// <summary>
    /// Sends the second step of the LNURL-pay flow (LUD-06) by calling the service callback with the
    /// chosen amount, optional comment (LUD-12), and optional payer data (LUD-18).
    /// </summary>
    /// <param name="amount">The amount to pay, in millisatoshis. Must be between <see cref="MinSendable"/> and <see cref="MaxSendable"/>.</param>
    /// <param name="network">The Bitcoin network used to parse and verify the returned BOLT11 invoice.</param>
    /// <param name="httpClient">The <see cref="HttpClient"/> used to perform the HTTP request.</param>
    /// <param name="comment">An optional payment comment (LUD-12). Length must not exceed <see cref="CommentAllowed"/>.</param>
    /// <param name="payerData">Optional payer data to include in the request (LUD-18).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="LNURLPayRequestCallbackResponse"/> containing the BOLT11 invoice and optional success action.</returns>
    /// <exception cref="LNUrlException">Thrown when the service returns an error or the invoice fails verification.</exception>
    public async Task<LNURLPayRequestCallbackResponse> SendRequest(LightMoney amount, Network network,
        HttpClient httpClient, string comment = null, LUD18PayerDataResponse payerData = null, CancellationToken cancellationToken = default)
    {
        var url = Callback;
        var uriBuilder = new UriBuilder(url);
        LNURL.AppendPayloadToQuery(uriBuilder, "amount", amount.MilliSatoshi.ToString());
        if (!string.IsNullOrEmpty(comment)) LNURL.AppendPayloadToQuery(uriBuilder, "comment", comment);

        if (payerData is not null)
            LNURL.AppendPayloadToQuery(uriBuilder, "payerdata",
                HttpUtility.UrlEncode(JsonConvert.SerializeObject(payerData)));

        url = new Uri(uriBuilder.ToString());
        var response = await httpClient.GetAsync(url, cancellationToken);
        var json = JObject.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (LNUrlStatusResponse.IsErrorResponse(json, out var error)) throw new LNUrlException(error.Reason);

        var result = json.ToObject<LNURLPayRequestCallbackResponse>();
        if (result.Verify(this, amount, network, out var invoice)) return result;

        throw new LNUrlException(
            "LNURL payRequest returned an invoice but its amount or hash did not match the request");
    }

    /// <summary>
    /// Represents a payer data field descriptor indicating whether the field is mandatory (LUD-18).
    /// </summary>
    public class PayerDataField
    {
        /// <summary>
        /// Gets or sets whether this payer data field is mandatory. If <c>true</c>, the payer must provide this field.
        /// </summary>
        [JsonProperty("mandatory", DefaultValueHandling = DefaultValueHandling.Populate)]
        [STJ.JsonPropertyName("mandatory")]
        public bool Mandatory { get; set; }
    }

    /// <summary>
    /// Describes the payer data fields that a service accepts or requires, as defined in LUD-18.
    /// Each field is optional; if present, its <see cref="PayerDataField.Mandatory"/> flag indicates
    /// whether the payer must provide that data.
    /// </summary>
    public class LUD18PayerData
    {
        /// <summary>
        /// Gets or sets the name field descriptor. When present, the service accepts or requires the payer's name.
        /// </summary>
        [JsonProperty("name", DefaultValueHandling = DefaultValueHandling.Ignore)]
        [STJ.JsonPropertyName("name")]
        public PayerDataField Name { get; set; }

        /// <summary>
        /// Gets or sets the public key field descriptor. When present, the service accepts or requires the payer's public key.
        /// </summary>
        [JsonProperty("pubkey", DefaultValueHandling = DefaultValueHandling.Ignore)]
        [STJ.JsonPropertyName("pubkey")]
        public PayerDataField Pubkey { get; set; }

        /// <summary>
        /// Gets or sets the email field descriptor. When present, the service accepts or requires the payer's email.
        /// </summary>
        [JsonProperty("email", DefaultValueHandling = DefaultValueHandling.Ignore)]
        [STJ.JsonPropertyName("email")]
        public PayerDataField Email { get; set; }

        /// <summary>
        /// Gets or sets the auth field descriptor with a challenge. When present, the service requires
        /// the payer to sign a challenge for authentication (LUD-18 auth).
        /// </summary>
        [JsonProperty("auth", DefaultValueHandling = DefaultValueHandling.Ignore)]
        [STJ.JsonPropertyName("auth")]
        public AuthPayerDataField Auth { get; set; }
    }

    /// <summary>
    /// Represents the payer data provided by the wallet in response to a <see cref="LUD18PayerData"/> request (LUD-18).
    /// </summary>
    public class LUD18PayerDataResponse
    {
        /// <summary>
        /// Gets or sets the payer's name.
        /// </summary>
        [JsonProperty("name", DefaultValueHandling = DefaultValueHandling.Ignore)]
        [STJ.JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the payer's public key.
        /// </summary>
        [JsonProperty("pubkey", DefaultValueHandling = DefaultValueHandling.Ignore)]
        [JsonConverter(typeof(PubKeyJsonConverter))]
        [STJ.JsonPropertyName("pubkey")]
        public PubKey Pubkey { get; set; }

        /// <summary>
        /// Gets or sets the payer's email address.
        /// </summary>
        [JsonProperty("email", DefaultValueHandling = DefaultValueHandling.Ignore)]
        [STJ.JsonPropertyName("email")]
        public string Email { get; set; }

        /// <summary>
        /// Gets or sets the payer's auth response containing a signed challenge (LUD-18 auth).
        /// </summary>
        [JsonProperty("auth", DefaultValueHandling = DefaultValueHandling.Ignore)]
        [STJ.JsonPropertyName("auth")]
        public LUD18AuthPayerDataResponse Auth { get; set; }
    }

    /// <summary>
    /// Represents the payer's authentication response for LUD-18 auth, containing the signed challenge.
    /// </summary>
    public class LUD18AuthPayerDataResponse
    {
        /// <summary>
        /// Gets or sets the payer's public key used to sign the challenge.
        /// </summary>
        [JsonProperty("key")]
        [JsonConverter(typeof(PubKeyJsonConverter))]
        [STJ.JsonPropertyName("key")]
        public PubKey Key { get; set; }

        /// <summary>
        /// Gets or sets the hex-encoded 32-byte challenge that was signed.
        /// </summary>
        [JsonProperty("k1")]
        [STJ.JsonPropertyName("k1")]
        public string K1 { get; set; }

        /// <summary>
        /// Gets or sets the ECDSA signature over the challenge.
        /// </summary>
        [JsonProperty("sig")]
        [JsonConverter(typeof(SigJsonConverter))]
        [STJ.JsonPropertyName("sig")]
        public ECDSASignature Sig { get; set; }
    }

    /// <summary>
    /// Extends <see cref="PayerDataField"/> with an additional challenge field for LUD-18 authentication.
    /// </summary>
    public class AuthPayerDataField : PayerDataField
    {
        /// <summary>
        /// Gets or sets the hex-encoded 32-byte challenge that the payer must sign.
        /// </summary>
        [JsonProperty("k1")]
        [STJ.JsonPropertyName("k1")]
        public string K1 { get; set; }
    }

    /// <summary>
    /// Represents the callback response from an LNURL-pay service (LUD-06), containing
    /// the BOLT11 payment request and optional success action (LUD-09).
    /// </summary>
    public class LNURLPayRequestCallbackResponse
    {
        [JsonIgnore]
        [STJ.JsonIgnore]
        private BOLT11PaymentRequest _paymentRequest;

        /// <summary>
        /// Gets or sets the BOLT11 payment request (invoice) string.
        /// </summary>
        [JsonProperty("pr")]
        [STJ.JsonPropertyName("pr")]
        public string Pr { get; set; }

        /// <summary>
        /// Gets or sets an array of route hints for the payment. Typically empty.
        /// </summary>
        [JsonProperty("routes")]
        [STJ.JsonPropertyName("routes")]
        public string[] Routes { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the URL for verifying payment settlement status, as defined in LUD-21.
        /// </summary>
        [JsonProperty("verify", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(UriJsonConverter))]
        [STJ.JsonPropertyName("verify")]
        public Uri VerifyUrl { get; set; }

        /// <summary>
        /// Gets or sets whether this payment request is disposable (single-use), as defined in LUD-11.
        /// If <c>true</c>, the wallet should not persist the payment link for reuse.
        /// </summary>
        [JsonProperty("disposable")]
        [STJ.JsonPropertyName("disposable")]
        public bool? Disposable { get; set; }

        /// <summary>
        /// Gets or sets the success action to display after payment, as defined in LUD-09.
        /// Can be a <see cref="LNURLPayRequestSuccessActionMessage"/>, <see cref="LNURLPayRequestSuccessActionUrl"/>,
        /// or <see cref="LNURLPayRequestSuccessActionAES"/>.
        /// </summary>
        [JsonProperty("successAction")]
        [JsonConverter(typeof(LNURLPayRequestSuccessActionJsonConverter))]
        [STJ.JsonPropertyName("successAction")]
        public ILNURLPayRequestSuccessAction SuccessAction { get; set; }

        /// <summary>
        /// Verifies that the BOLT11 invoice in <see cref="Pr"/> matches the expected amount and optionally
        /// verifies the description hash against the pay request metadata.
        /// </summary>
        /// <param name="request">The original <see cref="LNURLPayRequest"/> to verify against.</param>
        /// <param name="expectedAmount">The expected invoice amount in millisatoshis.</param>
        /// <param name="network">The Bitcoin network used to parse the BOLT11 invoice.</param>
        /// <param name="bolt11PaymentRequest">When this method returns, contains the parsed <see cref="BOLT11PaymentRequest"/>.</param>
        /// <param name="verifyDescriptionHash">If <c>true</c>, also verifies the description hash matches the request metadata.</param>
        /// <returns><c>true</c> if verification succeeds; otherwise <c>false</c>.</returns>
        public bool Verify(LNURLPayRequest request, LightMoney expectedAmount, Network network,
            out BOLT11PaymentRequest bolt11PaymentRequest, bool verifyDescriptionHash = false)
        {
            if (string.IsNullOrEmpty(Pr))
            {
                bolt11PaymentRequest = null;
                return false;
            }
            if (_paymentRequest != null)
                bolt11PaymentRequest = _paymentRequest;
            else if (!BOLT11PaymentRequest.TryParse(Pr, out bolt11PaymentRequest, network))
                return false;
            else
                _paymentRequest = bolt11PaymentRequest;

            return _paymentRequest.MinimumAmount == expectedAmount && (!verifyDescriptionHash ||
                   _paymentRequest.VerifyDescriptionHash(request.Metadata));
        }

        /// <summary>
        /// Parses and returns the BOLT11 payment request from the <see cref="Pr"/> string.
        /// The result is cached for subsequent calls.
        /// </summary>
        /// <param name="network">The Bitcoin network used to parse the BOLT11 invoice.</param>
        /// <returns>The parsed <see cref="BOLT11PaymentRequest"/>.</returns>
        public BOLT11PaymentRequest GetPaymentRequest(Network network)
        {
            _paymentRequest ??= BOLT11PaymentRequest.Parse(Pr, network);
            return _paymentRequest;
        }

        /// <summary>
        /// Fetches the payment verification status from the <see cref="VerifyUrl"/> endpoint (LUD-21).
        /// </summary>
        /// <param name="httpClient">The <see cref="HttpClient"/> used to perform the HTTP request.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>An <see cref="LNURLVerifyResponse"/> indicating whether the payment has settled.</returns>
        /// <exception cref="InvalidOperationException">Thrown when <see cref="VerifyUrl"/> is <c>null</c>.</exception>
        /// <exception cref="LNUrlException">Thrown when the verify endpoint returns an error.</exception>
        public async Task<LNURLVerifyResponse> FetchVerifyResponse(HttpClient httpClient,
            CancellationToken cancellationToken = default)
        {
            if (VerifyUrl is null)
                throw new InvalidOperationException("This callback response does not contain a verify URL.");

            return await LNURLVerifyResponse.FetchStatus(VerifyUrl, httpClient, cancellationToken);
        }

        /// <summary>
        /// Marker interface for LNURL-pay success actions (LUD-09). Implementations include
        /// <see cref="LNURLPayRequestSuccessActionMessage"/>, <see cref="LNURLPayRequestSuccessActionUrl"/>,
        /// and <see cref="LNURLPayRequestSuccessActionAES"/>.
        /// </summary>
        public interface ILNURLPayRequestSuccessAction
        {
            /// <summary>
            /// Gets or sets the success action type tag (<c>"message"</c>, <c>"url"</c>, or <c>"aes"</c>).
            /// </summary>
            public string Tag { get; set; }
        }

        /// <summary>
        /// A success action that displays a plain-text message to the user after payment (LUD-09, tag <c>"message"</c>).
        /// </summary>
        public class LNURLPayRequestSuccessActionMessage : ILNURLPayRequestSuccessAction
        {
            /// <summary>
            /// Gets or sets the message to display to the user.
            /// </summary>
            [JsonProperty("message")]
            [STJ.JsonPropertyName("message")]
            public string Message { get; set; }

            /// <summary>
            /// Gets or sets the success action type tag. Always <c>"message"</c>.
            /// </summary>
            [JsonProperty("tag")]
            [STJ.JsonPropertyName("tag")]
            public string Tag { get; set; }
        }

        /// <summary>
        /// A success action that directs the user to a URL after payment (LUD-09, tag <c>"url"</c>).
        /// </summary>
        public class LNURLPayRequestSuccessActionUrl : ILNURLPayRequestSuccessAction
        {
            /// <summary>
            /// Gets or sets a human-readable description of the URL.
            /// </summary>
            [JsonProperty("description")]
            [STJ.JsonPropertyName("description")]
            public string Description { get; set; }

            /// <summary>
            /// Gets or sets the URL to open after payment.
            /// </summary>
            [JsonProperty("url")]
            [JsonConverter(typeof(UriJsonConverter))]
            [STJ.JsonPropertyName("url")]
            public string Url { get; set; }

            /// <summary>
            /// Gets or sets the success action type tag. Always <c>"url"</c>.
            /// </summary>
            [JsonProperty("tag")]
            [STJ.JsonPropertyName("tag")]
            public string Tag { get; set; }
        }

        /// <summary>
        /// A success action containing AES-encrypted data that can be decrypted using the payment preimage
        /// (LUD-09 and LUD-10, tag <c>"aes"</c>).
        /// </summary>
        public class LNURLPayRequestSuccessActionAES : ILNURLPayRequestSuccessAction
        {
            /// <summary>
            /// Gets or sets a human-readable description of the encrypted content.
            /// </summary>
            [JsonProperty("description")]
            [STJ.JsonPropertyName("description")]
            public string Description { get; set; }

            /// <summary>
            /// Gets or sets the base64-encoded AES ciphertext.
            /// </summary>
            [JsonProperty("ciphertext")]
            [STJ.JsonPropertyName("ciphertext")]
            public string CipherText { get; set; }

            /// <summary>
            /// Gets or sets the base64-encoded AES initialization vector (IV).
            /// </summary>
            [JsonProperty("iv")]
            [STJ.JsonPropertyName("iv")]
            public string IV { get; set; }

            /// <summary>
            /// Gets or sets the success action type tag. Always <c>"aes"</c>.
            /// </summary>
            [JsonProperty("tag")]
            [STJ.JsonPropertyName("tag")]
            public string Tag { get; set; }

            /// <summary>
            /// Decrypts the <see cref="CipherText"/> using the payment preimage as the AES key (LUD-10).
            /// </summary>
            /// <param name="preimage">The hex-encoded payment preimage, used as the AES decryption key.</param>
            /// <returns>The decrypted plaintext string.</returns>
            public string Decrypt(string preimage)
            {
                var cipherText = Encoders.Base64.DecodeData(CipherText);
                string plaintext = null;

                using (var aesAlg = Aes.Create())
                {
                    aesAlg.Key = Encoding.UTF8.GetBytes(preimage);
                    aesAlg.IV = Encoders.Base64.DecodeData(IV);

                    var decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                    using (var msDecrypt = new MemoryStream(cipherText))
                    {
                        using (var csDecrypt =
                               new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                        {
                            using (var srDecrypt = new StreamReader(csDecrypt))
                            {
                                plaintext = srDecrypt.ReadToEnd();
                            }
                        }
                    }
                }

                return plaintext;
            }
        }


        /// <summary>
        /// Newtonsoft.Json converter for polymorphic deserialization of <see cref="ILNURLPayRequestSuccessAction"/>
        /// based on the <c>tag</c> property (LUD-09).
        /// </summary>
        public class LNURLPayRequestSuccessActionJsonConverter : JsonConverter<ILNURLPayRequestSuccessAction>
        {
            /// <inheritdoc />
            public override void WriteJson(JsonWriter writer, ILNURLPayRequestSuccessAction value,
                JsonSerializer serializer)
            {
                if (value is null)
                {
                    writer.WriteNull();
                    return;
                }

                JObject.FromObject(value).WriteTo(writer);
            }

            /// <inheritdoc />
            public override ILNURLPayRequestSuccessAction ReadJson(JsonReader reader, Type objectType,
                ILNURLPayRequestSuccessAction existingValue,
                bool hasExistingValue, JsonSerializer serializer)
            {
                if (reader.TokenType is JsonToken.Null) return null;
                var jobj = JObject.Load(reader);
                switch (jobj.GetValue("tag").Value<string>())
                {
                    case "message":
                        return jobj.ToObject<LNURLPayRequestSuccessActionMessage>();
                    case "url":
                        return jobj.ToObject<LNURLPayRequestSuccessActionUrl>();
                    case "aes":
                        return jobj.ToObject<LNURLPayRequestSuccessActionAES>();
                }

                throw new FormatException();
            }
        }
    }
}
