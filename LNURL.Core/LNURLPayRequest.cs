using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
public class LNURLPayRequest
{
    /// <summary>
    /// Gets or sets the callback URL to which the wallet sends the payment amount to receive a BOLT11 invoice.
    /// </summary>
    [JsonProperty("callback", NullValueHandling = NullValueHandling.Ignore)]
    [JsonConverter(typeof(UriJsonConverter))]
    [STJ.JsonPropertyName("callback")]
    public Uri Callback { get; set; }

    /// <summary>
    /// Gets or sets the raw JSON-encoded metadata string.
    /// </summary>
    [JsonProperty("metadata")]
    [STJ.JsonPropertyName("metadata")]
    public string Metadata { get; set; }

    /// <summary>
    /// Gets the parsed metadata as a list of key-value pairs where the key is the MIME type
    /// and the value is the content.
    /// </summary>
    [JsonIgnore]
    [STJ.JsonIgnore]
    public List<KeyValuePair<string, string>> ParsedMetadata =>
        (string.IsNullOrEmpty(Metadata)
            ? Array.Empty<string[]>()
            : System.Text.Json.JsonSerializer.Deserialize<string[][]>(Metadata))
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
    /// Gets or sets the maximum number of characters allowed in a payment comment (LUD-12).
    /// </summary>
    [JsonProperty("commentAllowed", NullValueHandling = NullValueHandling.Ignore)]
    [STJ.JsonPropertyName("commentAllowed")]
    public int? CommentAllowed { get; set; }

    /// <summary>
    /// Gets or sets an optional LNURL-withdraw link associated with this pay request (LUD-19).
    /// </summary>
    [JsonProperty("withdrawLink", NullValueHandling = NullValueHandling.Ignore)]
    [JsonConverter(typeof(UriJsonConverter))]
    [STJ.JsonPropertyName("withdrawLink")]
    public Uri WithdrawLink { get; set; }

    /// <summary>
    /// Gets or sets the payer data fields that the service accepts or requires (LUD-18).
    /// </summary>
    [JsonProperty("payerData", NullValueHandling = NullValueHandling.Ignore)]
    [STJ.JsonPropertyName("payerData")]
    public LUD18PayerData PayerData { get; set; }

    /// <summary>
    /// Gets or sets the Nostr public key of the service, used for zap receipts (NIP-57).
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
    public bool VerifyPayerData(LUD18PayerDataResponse response)
    {
        return VerifyPayerData(PayerData, response);
    }

    /// <summary>
    /// Verifies that a payer data response satisfies the given payer data field requirements (LUD-18).
    /// </summary>
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
    public Task<LNURLPayRequestCallbackResponse> SendRequest(LightMoney amount, Network network,
        HttpClient httpClient, string comment = null, LUD18PayerDataResponse payerData = null,
        CancellationToken cancellationToken = default)
    {
        return SendRequest(amount, network, new HttpLNURLCommunicator(httpClient), comment, payerData,
            cancellationToken);
    }

    /// <summary>
    /// Sends the second step of the LNURL-pay flow (LUD-06) using a custom <see cref="ILNURLCommunicator"/> transport.
    /// </summary>
    public async Task<LNURLPayRequestCallbackResponse> SendRequest(LightMoney amount, Network network,
        ILNURLCommunicator communicator, string comment = null, LUD18PayerDataResponse payerData = null,
        CancellationToken cancellationToken = default)
    {
        var url = Callback;
        var uriBuilder = new UriBuilder(url);
        LNURL.AppendPayloadToQuery(uriBuilder, "amount", amount.MilliSatoshi.ToString());
        if (!string.IsNullOrEmpty(comment)) LNURL.AppendPayloadToQuery(uriBuilder, "comment", comment);

        if (payerData is not null)
            LNURL.AppendPayloadToQuery(uriBuilder, "payerdata",
                HttpUtility.UrlEncode(System.Text.Json.JsonSerializer.Serialize(payerData, LNURLJsonOptions.Default)));

        url = new Uri(uriBuilder.ToString());
        var content = await communicator.SendRequest(url, cancellationToken);
        if (LNUrlStatusResponse.IsErrorResponse(content, out var error)) throw new LNUrlException(error.Reason);

        var result = System.Text.Json.JsonSerializer.Deserialize<LNURLPayRequestCallbackResponse>(content, LNURLJsonOptions.Default);
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
        /// Gets or sets whether this payer data field is mandatory.
        /// </summary>
        [JsonProperty("mandatory", DefaultValueHandling = DefaultValueHandling.Populate)]
        [STJ.JsonPropertyName("mandatory")]
        public bool Mandatory { get; set; }
    }

    /// <summary>
    /// Describes the payer data fields that a service accepts or requires (LUD-18).
    /// </summary>
    public class LUD18PayerData
    {
        [JsonProperty("name", DefaultValueHandling = DefaultValueHandling.Ignore)]
        [STJ.JsonPropertyName("name")]
        public PayerDataField Name { get; set; }

        [JsonProperty("pubkey", DefaultValueHandling = DefaultValueHandling.Ignore)]
        [STJ.JsonPropertyName("pubkey")]
        public PayerDataField Pubkey { get; set; }

        [JsonProperty("email", DefaultValueHandling = DefaultValueHandling.Ignore)]
        [STJ.JsonPropertyName("email")]
        public PayerDataField Email { get; set; }

        [JsonProperty("auth", DefaultValueHandling = DefaultValueHandling.Ignore)]
        [STJ.JsonPropertyName("auth")]
        public AuthPayerDataField Auth { get; set; }
    }

    /// <summary>
    /// Represents the payer data provided by the wallet in response to a <see cref="LUD18PayerData"/> request.
    /// </summary>
    public class LUD18PayerDataResponse
    {
        [JsonProperty("name", DefaultValueHandling = DefaultValueHandling.Ignore)]
        [STJ.JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonProperty("pubkey", DefaultValueHandling = DefaultValueHandling.Ignore)]
        [JsonConverter(typeof(PubKeyJsonConverter))]
        [STJ.JsonPropertyName("pubkey")]
        public PubKey Pubkey { get; set; }

        [JsonProperty("email", DefaultValueHandling = DefaultValueHandling.Ignore)]
        [STJ.JsonPropertyName("email")]
        public string Email { get; set; }

        [JsonProperty("auth", DefaultValueHandling = DefaultValueHandling.Ignore)]
        [STJ.JsonPropertyName("auth")]
        public LUD18AuthPayerDataResponse Auth { get; set; }
    }

    /// <summary>
    /// Represents the payer's authentication response for LUD-18 auth.
    /// </summary>
    public class LUD18AuthPayerDataResponse
    {
        [JsonProperty("key")]
        [JsonConverter(typeof(PubKeyJsonConverter))]
        [STJ.JsonPropertyName("key")]
        public PubKey Key { get; set; }

        [JsonProperty("k1")]
        [STJ.JsonPropertyName("k1")]
        public string K1 { get; set; }

        [JsonProperty("sig")]
        [JsonConverter(typeof(SigJsonConverter))]
        [STJ.JsonPropertyName("sig")]
        public ECDSASignature Sig { get; set; }
    }

    /// <summary>
    /// Extends <see cref="PayerDataField"/> with a challenge field for LUD-18 authentication.
    /// </summary>
    public class AuthPayerDataField : PayerDataField
    {
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
        /// Gets or sets an array of route hints for the payment.
        /// </summary>
        [JsonProperty("routes")]
        [STJ.JsonPropertyName("routes")]
        public string[] Routes { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the URL for verifying payment settlement status (LUD-21).
        /// </summary>
        [JsonProperty("verify", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(UriJsonConverter))]
        [STJ.JsonPropertyName("verify")]
        public Uri VerifyUrl { get; set; }

        /// <summary>
        /// Gets or sets whether this payment request is disposable (LUD-11).
        /// </summary>
        [JsonProperty("disposable")]
        [STJ.JsonPropertyName("disposable")]
        public bool? Disposable { get; set; }

        /// <summary>
        /// Gets or sets the success action to display after payment (LUD-09).
        /// </summary>
        [JsonProperty("successAction")]
        [JsonConverter(typeof(LNURLPayRequestSuccessActionJsonConverter))]
        [STJ.JsonPropertyName("successAction")]
        public ILNURLPayRequestSuccessAction SuccessAction { get; set; }

        /// <summary>
        /// Verifies that the BOLT11 invoice matches the expected amount.
        /// </summary>
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
        /// </summary>
        public BOLT11PaymentRequest GetPaymentRequest(Network network)
        {
            _paymentRequest ??= BOLT11PaymentRequest.Parse(Pr, network);
            return _paymentRequest;
        }

        /// <summary>
        /// Fetches the payment verification status from the <see cref="VerifyUrl"/> endpoint (LUD-21).
        /// </summary>
        public async Task<LNURLVerifyResponse> FetchVerifyResponse(HttpClient httpClient,
            CancellationToken cancellationToken = default)
        {
            if (VerifyUrl is null)
                throw new InvalidOperationException("This callback response does not contain a verify URL.");

            return await LNURLVerifyResponse.FetchStatus(VerifyUrl, httpClient, cancellationToken);
        }

        /// <summary>
        /// Marker interface for LNURL-pay success actions (LUD-09).
        /// </summary>
        public interface ILNURLPayRequestSuccessAction
        {
            public string Tag { get; set; }
        }

        public class LNURLPayRequestSuccessActionMessage : ILNURLPayRequestSuccessAction
        {
            [JsonProperty("message")]
            [STJ.JsonPropertyName("message")]
            public string Message { get; set; }

            [JsonProperty("tag")]
            [STJ.JsonPropertyName("tag")]
            public string Tag { get; set; }
        }

        public class LNURLPayRequestSuccessActionUrl : ILNURLPayRequestSuccessAction
        {
            [JsonProperty("description")]
            [STJ.JsonPropertyName("description")]
            public string Description { get; set; }

            [JsonProperty("url")]
            [JsonConverter(typeof(UriJsonConverter))]
            [STJ.JsonPropertyName("url")]
            public string Url { get; set; }

            [JsonProperty("tag")]
            [STJ.JsonPropertyName("tag")]
            public string Tag { get; set; }
        }

        public class LNURLPayRequestSuccessActionAES : ILNURLPayRequestSuccessAction
        {
            [JsonProperty("description")]
            [STJ.JsonPropertyName("description")]
            public string Description { get; set; }

            [JsonProperty("ciphertext")]
            [STJ.JsonPropertyName("ciphertext")]
            public string CipherText { get; set; }

            [JsonProperty("iv")]
            [STJ.JsonPropertyName("iv")]
            public string IV { get; set; }

            [JsonProperty("tag")]
            [STJ.JsonPropertyName("tag")]
            public string Tag { get; set; }

            /// <summary>
            /// Decrypts the <see cref="CipherText"/> using the payment preimage as the AES key (LUD-10).
            /// </summary>
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
                Newtonsoft.Json.JsonSerializer serializer)
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
                bool hasExistingValue, Newtonsoft.Json.JsonSerializer serializer)
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
