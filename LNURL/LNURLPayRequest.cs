using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BTCPayServer.Lightning;
using BTCPayServer.Lightning.JsonConverters;
using LNURL.JsonConverters;
using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LNURL
{
    /// <summary>
    ///     https://github.com/fiatjaf/lnurl-rfc/blob/luds/06.md
    /// </summary>
    public class LNURLPayRequest
    {
        [JsonProperty("callback")]
        [JsonConverter(typeof(UriJsonConverter))]
        public Uri Callback { get; set; }

        [JsonProperty("metadata")] public string Metadata { get; set; }

        [JsonIgnore]
        public List<KeyValuePair<string, string>> ParsedMetadata => JsonConvert
            .DeserializeObject<string[][]>(Metadata ?? string.Empty)
            .Select(strings => new KeyValuePair<string, string>(strings[0], strings[1])).ToList();

        [JsonProperty("tag")] public string Tag { get; set; }

        [JsonProperty("minSendable")]
        [JsonConverter(typeof(LightMoneyJsonConverter))]
        public LightMoney MinSendable { get; set; }

        [JsonProperty("maxSendable")]
        [JsonConverter(typeof(LightMoneyJsonConverter))]
        public LightMoney MaxSendable { get; set; }

        /// <summary>
        ///     https://github.com/fiatjaf/lnurl-rfc/blob/luds/12.md
        /// </summary>
        [JsonProperty("commentAllowed")]
        public int? CommentAllowed { get; set; }

        //https://github.com/fiatjaf/lnurl-rfc/blob/luds/19.md
        [JsonProperty("withdrawLink")]
        [JsonConverter(typeof(UriJsonConverter))]
        public Uri WithdrawLink { get; set; }

        public async Task<LNURLPayRequestCallbackResponse> SendRequest(LightMoney amount, Network network,
            HttpClient httpClient, string comment = null)
        {
            var url = Callback;
            var uriBuilder = new UriBuilder(url);
            LNURL.AppendPayloadToQuery(uriBuilder, "amount", amount.MilliSatoshi.ToString());
            if (!string.IsNullOrEmpty(comment)) LNURL.AppendPayloadToQuery(uriBuilder, "comment", comment);

            url = new Uri(uriBuilder.ToString());
            var response = JObject.Parse(await httpClient.GetStringAsync(url));
            if (LNUrlStatusResponse.IsErrorResponse(response, out var error)) throw new LNUrlException(error.Reason);

            var result = response.ToObject<LNURLPayRequestCallbackResponse>();
            if (result.Verify(this, amount, network)) return result;

            throw new LNUrlException(
                "LNURL payRequest returned an invoice but its amount or hash did not match the request");
        }

        public class LNURLPayRequestCallbackResponse
        {
            [JsonProperty("pr")] public string Pr { get; set; }

            [JsonProperty("routes")] public Array Routes { get; set; }

            /// <summary>
            ///     https://github.com/fiatjaf/lnurl-rfc/blob/luds/11.md
            /// </summary>
            [JsonProperty("disposable")]
            public bool? Disposable { get; set; }

            /// <summary>
            ///     https://github.com/fiatjaf/lnurl-rfc/blob/luds/09.md
            /// </summary>
            [JsonProperty("successAction")]
            [JsonConverter(typeof(LNURLPayRequestSuccessActionJsonConverter))]
            public ILNURLPayRequestSuccessAction SuccessAction { get; set; }

            public bool Verify(LNURLPayRequest request, LightMoney expectedAmount, Network network)
            {
                return BOLT11PaymentRequest.TryParse(Pr, out var inv, network) &&
                       inv.MinimumAmount == expectedAmount &&
                       inv.VerifyDescriptionHash(request.Metadata);
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
                [JsonProperty("message")] public string Message { get; set; }

                [JsonProperty("tag")] public string Tag { get; set; }
            }

            /// <summary>
            ///     https://github.com/fiatjaf/lnurl-rfc/blob/luds/09.md
            /// </summary>
            public class LNURLPayRequestSuccessActionUrl : ILNURLPayRequestSuccessAction
            {
                [JsonProperty("description")] public string Description { get; set; }

                [JsonProperty("url")]
                [JsonConverter(typeof(UriJsonConverter))]
                public string Url { get; set; }

                [JsonProperty("tag")] public string Tag { get; set; }
            }

            /// <summary>
            ///     https://github.com/fiatjaf/lnurl-rfc/blob/luds/09.md
            ///     https://github.com/fiatjaf/lnurl-rfc/blob/luds/10.md
            /// </summary>
            public class LNURLPayRequestSuccessActionAES : ILNURLPayRequestSuccessAction
            {
                [JsonProperty("description")] public string Description { get; set; }
                [JsonProperty("ciphertext")] public string CipherText { get; set; }
                [JsonProperty("iv")] public string IV { get; set; }
                [JsonProperty("tag")] public string Tag { get; set; }

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


            public class LNURLPayRequestSuccessActionJsonConverter : JsonConverter<ILNURLPayRequestSuccessAction>
            {
                public override void WriteJson(JsonWriter writer, ILNURLPayRequestSuccessAction value,
                    JsonSerializer serializer)
                {
                    JObject.FromObject(value).WriteTo(writer);
                }

                public override ILNURLPayRequestSuccessAction ReadJson(JsonReader reader, Type objectType,
                    ILNURLPayRequestSuccessAction existingValue,
                    bool hasExistingValue, JsonSerializer serializer)
                {
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
}