using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using BTCPayServer.Lightning;
using LNURL.Json.Newtonsoft;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using JsonSerializer = System.Text.Json.JsonSerializer;
using LightMoneyJsonConverter = BTCPayServer.Lightning.JsonConverters.LightMoneyJsonConverter;
using LNURLPayRequestSuccessActionJsonConverter = LNURL.Json.SystemJson.LNURLPayRequestSuccessActionJsonConverter;
using PubKeyJsonConverter = LNURL.Json.SystemJson.PubKeyJsonConverter;
using SigJsonConverter = LNURL.Json.SystemJson.SigJsonConverter;

namespace LNURL.Requests;

/// <summary>
///     https://github.com/fiatjaf/lnurl-rfc/blob/luds/06.md
/// </summary>
public class LNURLPayRequest : ILNURLRequest
{
    [JsonProperty("callback")]
    [JsonPropertyName("callback")]
    [Newtonsoft.Json.JsonConverter(typeof(UriJsonConverter))]
    [System.Text.Json.Serialization.JsonConverter(typeof(Json.SystemJson.UriJsonConverter))]
    public Uri Callback { get; set; }

    [JsonProperty("metadata")]
    [JsonPropertyName("metadata")]
    public string Metadata { get; set; }


    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public List<KeyValuePair<string, string>> ParsedMetadata => JsonSerializer
        .Deserialize<string[][]>(Metadata ?? string.Empty)
        .Select(strings => new KeyValuePair<string, string>(strings[0], strings[1])).ToList();

    [JsonProperty("tag")]
    [JsonPropertyName("tag")]
    public string Tag { get; set; }

    [JsonProperty("minSendable")]
    [JsonPropertyName("minSendable")]
    [Newtonsoft.Json.JsonConverter(typeof(LightMoneyJsonConverter))]
    [System.Text.Json.Serialization.JsonConverter(typeof(Json.SystemJson.LightMoneyJsonConverter))]
    public LightMoney MinSendable { get; set; }

    [JsonProperty("maxSendable")]
    [JsonPropertyName("maxSendable")]
    [Newtonsoft.Json.JsonConverter(typeof(LightMoneyJsonConverter))]
    [System.Text.Json.Serialization.JsonConverter(typeof(Json.SystemJson.LightMoneyJsonConverter))]
    public LightMoney MaxSendable { get; set; }

    [JsonProperty("commentAllowed", NullValueHandling = NullValueHandling.Ignore)]
    [JsonPropertyName("commentAllowed")]
    [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    /// <summary>
    ///     https://github.com/fiatjaf/lnurl-rfc/blob/luds/12.md
    /// </summary>
    public int? CommentAllowed { get; set; }

    [JsonProperty("withdrawLink", NullValueHandling = NullValueHandling.Ignore)]
    [JsonPropertyName("withdrawLink")]
    [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]

    //https://github.com/fiatjaf/lnurl-rfc/blob/luds/19.md
    [Newtonsoft.Json.JsonConverter(typeof(UriJsonConverter))]
    [System.Text.Json.Serialization.JsonConverter(typeof(Json.SystemJson.UriJsonConverter))]
    public Uri WithdrawLink { get; set; }

    //https://github.com/fiatjaf/lnurl-rfc/blob/luds/18.md

    [JsonProperty("payerData", NullValueHandling = NullValueHandling.Ignore)]
    [JsonPropertyName("payerData")]
    [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LUD18PayerData PayerData { get; set; }


    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonExtensionData]
    public IDictionary<string, JToken> AdditionalData { get; set; }

    [System.Text.Json.Serialization.JsonExtensionData]
    [Newtonsoft.Json.JsonIgnore]
    public IDictionary<string, JsonElement> AdditionalDataSystemJson { get; set; }


    [JsonProperty("nostrPubkey", NullValueHandling = NullValueHandling.Ignore)]
    [JsonPropertyName("nostrPubkey")]
    [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]

    //https://github.com/nostr-protocol/nips/blob/master/57.md
    public string? NostrPubkey { get; set; }

    [JsonProperty("allowsNostr", NullValueHandling = NullValueHandling.Ignore)]
    [JsonPropertyName("allowsNostr")]
    [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]

    //https://github.com/nostr-protocol/nips/blob/master/57.md
    public bool? AllowsNostr { get; set; }

    public bool VerifyPayerData(LUD18PayerDataResponse response)
    {
        return VerifyPayerData(PayerData, response);
    }

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

    public async Task<LNURLPayRequestCallbackResponse> SendRequest(LightMoney amount, Network network,
        HttpClient httpClient, string comment = null, LUD18PayerDataResponse payerData = null,
        CancellationToken cancellationToken = default)
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

    public class PayerDataField
    {
        [JsonProperty("mandatory", DefaultValueHandling = DefaultValueHandling.Populate)]
        [JsonPropertyName("mandatory")]
        public bool Mandatory { get; set; }
    }

    public class LUD18PayerData
    {
        [JsonProperty("name", DefaultValueHandling = DefaultValueHandling.Ignore)]
        [JsonPropertyName("allowsNostr")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public PayerDataField Name { get; set; }

        [JsonProperty("pubkey", DefaultValueHandling = DefaultValueHandling.Ignore)]
        [JsonPropertyName("pubkey")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public PayerDataField Pubkey { get; set; }

        [JsonProperty("email", DefaultValueHandling = DefaultValueHandling.Ignore)]
        [JsonPropertyName("email")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public PayerDataField Email { get; set; }

        [JsonProperty("auth", DefaultValueHandling = DefaultValueHandling.Ignore)]
        [JsonPropertyName("auth")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public AuthPayerDataField Auth { get; set; }
    }

    public class LUD18PayerDataResponse
    {
        [JsonProperty("name", DefaultValueHandling = DefaultValueHandling.Ignore)]
        [JsonPropertyName("allowsNostr")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Name { get; set; }

        [JsonProperty("pubkey", DefaultValueHandling = DefaultValueHandling.Ignore)]
        [JsonPropertyName("pubkey")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [System.Text.Json.Serialization.JsonConverter(typeof(PubKeyJsonConverter))]
        [Newtonsoft.Json.JsonConverter(typeof(Json.Newtonsoft.PubKeyJsonConverter))]
        public PubKey Pubkey { get; set; }

        [JsonProperty("email", DefaultValueHandling = DefaultValueHandling.Ignore)]
        [JsonPropertyName("email")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Email { get; set; }

        [JsonProperty("auth", DefaultValueHandling = DefaultValueHandling.Ignore)]
        [JsonPropertyName("auth")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public LUD18AuthPayerDataResponse Auth { get; set; }
    }

    public class LUD18AuthPayerDataResponse
    {
        [JsonProperty("key", DefaultValueHandling = DefaultValueHandling.Ignore)]
        [JsonPropertyName("key")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [System.Text.Json.Serialization.JsonConverter(typeof(PubKeyJsonConverter))]
        [Newtonsoft.Json.JsonConverter(typeof(Json.Newtonsoft.PubKeyJsonConverter))]
        public PubKey Key { get; set; }


        [JsonPropertyName("k1")]
        [JsonProperty("k1")]
        public string K1 { get; set; }

        [JsonProperty("sig")]
        [System.Text.Json.Serialization.JsonConverter(typeof(SigJsonConverter))]
        [Newtonsoft.Json.JsonConverter(typeof(Json.Newtonsoft.SigJsonConverter))]
        public ECDSASignature Sig { get; set; }
    }

    public class AuthPayerDataField : PayerDataField
    {
        [JsonPropertyName("k1")]
        [JsonProperty("k1")]
        public string K1 { get; set; }
    }

    public class LNURLPayRequestCallbackResponse
    {
        [System.Text.Json.Serialization.JsonIgnore] [Newtonsoft.Json.JsonIgnore]
        private BOLT11PaymentRequest _paymentRequest;


        [JsonPropertyName("pr")]
        [JsonProperty("pr")]
        public string Pr { get; set; }

        [JsonPropertyName("routes")]
        [JsonProperty("routes")]
        public string[] Routes { get; set; } = Array.Empty<string>();

        /// <summary>
        ///     https://github.com/fiatjaf/lnurl-rfc/blob/luds/11.md
        /// </summary>

        [JsonPropertyName("disposable")]
        [JsonProperty("disposable")]
        public bool? Disposable { get; set; }

        /// <summary>
        ///     https://github.com/fiatjaf/lnurl-rfc/blob/luds/09.md
        /// </summary>
        [JsonProperty("successAction")]
        [JsonPropertyName("successAction")]
        [System.Text.Json.Serialization.JsonConverter(typeof(LNURLPayRequestSuccessActionJsonConverter))]
        [Newtonsoft.Json.JsonConverter(typeof(Json.Newtonsoft.LNURLPayRequestSuccessActionJsonConverter))]
        public ILNURLPayRequestSuccessAction SuccessAction { get; set; }

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
                                                                       _paymentRequest.VerifyDescriptionHash(
                                                                           request.Metadata));
        }

        public BOLT11PaymentRequest GetPaymentRequest(Network network)
        {
            _paymentRequest ??= BOLT11PaymentRequest.Parse(Pr, network);
            return _paymentRequest;
        }

        public interface ILNURLPayRequestSuccessAction
        {
            public string Tag { get; set; }
        }

        /// <summary>
        ///     https://github.com/fiatjaf/lnurl-rfc/blob/luds/09.md
        /// </summary>
        public class LNURLPayRequestSuccessActionMessage : ILNURLPayRequestSuccessAction
        {
            [JsonPropertyName("message")]
            [JsonProperty("message")]
            public string Message { get; set; }

            [JsonPropertyName("tag")]
            [JsonProperty("tag")]
            public string Tag { get; set; }
        }

        /// <summary>
        ///     https://github.com/fiatjaf/lnurl-rfc/blob/luds/09.md
        /// </summary>
        public class LNURLPayRequestSuccessActionUrl : ILNURLPayRequestSuccessAction
        {
            [JsonPropertyName("description")]
            [JsonProperty("description")]
            public string Description { get; set; }

            [JsonProperty("url")]
            [JsonPropertyName("url")]
            [Newtonsoft.Json.JsonConverter(typeof(UriJsonConverter))]
            [System.Text.Json.Serialization.JsonConverter(typeof(Json.SystemJson.UriJsonConverter))]
            public string Url { get; set; }

            [JsonPropertyName("tag")]
            [JsonProperty("tag")]
            public string Tag { get; set; }
        }

        /// <summary>
        ///     https://github.com/fiatjaf/lnurl-rfc/blob/luds/09.md
        ///     https://github.com/fiatjaf/lnurl-rfc/blob/luds/10.md
        /// </summary>
        public class LNURLPayRequestSuccessActionAES : ILNURLPayRequestSuccessAction
        {
            [JsonPropertyName("description")]
            [JsonProperty("description")]
            public string Description { get; set; }

            [JsonPropertyName("ciphertext")]
            [JsonProperty("ciphertext")]
            public string CipherText { get; set; }

            [JsonPropertyName("iv")]
            [JsonProperty("iv")]
            public string IV { get; set; }

            [JsonPropertyName("tag")]
            [JsonProperty("tag")]
            public string Tag { get; set; }

            public string Decrypt(string preimage)
            {
                var cipherText = Encoders.Base64.DecodeData(CipherText);
                // Declare the string used to hold
                // the decrypted text.
                string plaintext = null;

                // Create an Aes object
                // with the specified key and IV.
                using (var aesAlg = Aes.Create())
                {
                    aesAlg.Key = Encoding.UTF8.GetBytes(preimage);
                    aesAlg.IV = Encoders.Base64.DecodeData(IV);

                    // Create a decryptor to perform the stream transform.
                    var decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                    // Create the streams used for decryption.
                    using (var msDecrypt = new MemoryStream(cipherText))
                    {
                        using (var csDecrypt =
                               new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                        {
                            using (var srDecrypt = new StreamReader(csDecrypt))
                            {
                                // Read the decrypted bytes from the decrypting stream
                                // and place them in a string.
                                plaintext = srDecrypt.ReadToEnd();
                            }
                        }
                    }
                }

                return plaintext;
            }
        }
    }
}