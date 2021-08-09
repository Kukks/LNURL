using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BTCPayServer.Client.JsonConverters;
using BTCPayServer.JsonConverters;
using BTCPayServer.Lightning;
using ExchangeSharp;
using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using LightMoneyJsonConverter = BTCPayServer.Lightning.JsonConverters.LightMoneyJsonConverter;

namespace BTCPayServer.LNUrl
{
    public class LNURL
    {
        private static void AppendPayloadToQuery(UriBuilder uri, string key, string value)
        {
            if (uri.Query.Length > 1)
                uri.Query += "&";

            uri.Query = uri.Query + WebUtility.UrlEncode(key.UrlEncode()) + "=" +
                        WebUtility.UrlEncode(value.UrlEncode());
        }

        /// <summary>
        /// https://github.com/fiatjaf/lnurl-rfc/blob/luds/02.md
        /// </summary>
        public class LNURLChannelRequest
        {
            [JsonProperty("uri")]
            [JsonConverter(typeof(NodeUriJsonConverter))]
            public NodeInfo Uri { get; set; }

            [JsonProperty("uri")]
            [JsonConverter(typeof(UriJsonConverter))]
            public Uri Callback { get; set; }

            [JsonProperty("k1")] public string K1 { get; set; }

            [JsonProperty("tag")] public string Tag { get; set; }
        }

        /// <summary>
        /// https://github.com/fiatjaf/lnurl-rfc/blob/luds/06.md
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
            /// https://github.com/fiatjaf/lnurl-rfc/blob/luds/12.md
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
                AppendPayloadToQuery(uriBuilder, "amount", amount.MilliSatoshi.ToString());
                if (!string.IsNullOrEmpty(comment))
                {
                    AppendPayloadToQuery(uriBuilder, "comment", comment);
                }

                url = new Uri(uriBuilder.ToString());
                var response = (JObject.Parse(await httpClient.GetStringAsync(url)));
                if (LNUrlErrorResponse.IsErrorResponse(response, out var error))
                {
                    throw new LNUrlException(error.Reason);
                }

                var result = response.ToObject<LNURLPayRequestCallbackResponse>();
                if (result.Verify(this, amount, network))
                {
                    return result;
                }

                throw new LNUrlException(
                    "LNURL payRequest returned an invoice but its amount or hash did not match the request");
            }

            public class LNURLPayRequestCallbackResponse
            {
                [JsonProperty("pr")] public string Pr { get; set; }

                [JsonProperty("routes")] public Array Routes { get; set; }

                /// <summary>
                /// https://github.com/fiatjaf/lnurl-rfc/blob/luds/11.md
                /// </summary>
                [JsonProperty("disposable")]
                public bool? Disposable { get; set; }

                /// <summary>
                /// https://github.com/fiatjaf/lnurl-rfc/blob/luds/09.md
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
                /// https://github.com/fiatjaf/lnurl-rfc/blob/luds/09.md
                /// </summary>
                public class LNURLPayRequestSuccessActionMessage : ILNURLPayRequestSuccessAction
                {
                    [JsonProperty("message")] public string Message { get; set; }

                    [JsonProperty("tag")] public string Tag { get; set; }
                }

                /// <summary>
                /// https://github.com/fiatjaf/lnurl-rfc/blob/luds/09.md
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
                /// https://github.com/fiatjaf/lnurl-rfc/blob/luds/09.md
                /// https://github.com/fiatjaf/lnurl-rfc/blob/luds/10.md
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
                        using (Aes aesAlg = Aes.Create())
                        {
                            aesAlg.Key = Encoding.UTF8.GetBytes(preimage);
                            aesAlg.IV = Encoders.Base64.DecodeData(IV);

                            // Create a decryptor to perform the stream transform.
                            ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                            // Create the streams used for decryption.
                            using (MemoryStream msDecrypt = new MemoryStream(cipherText))
                            {
                                using (CryptoStream csDecrypt =
                                    new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                                {
                                    using (StreamReader srDecrypt = new StreamReader(csDecrypt))
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


        /// <summary>
        /// https://github.com/fiatjaf/lnurl-rfc/blob/luds/02.md
        /// </summary>
        public class LNURLWithdrawRequest
        {
            [JsonProperty("uri")]
            [JsonConverter(typeof(UriJsonConverter))]
            public Uri Callback { get; set; }

            [JsonProperty("k1")] public string K1 { get; set; }

            [JsonProperty("tag")] public string Tag { get; set; }

            [JsonProperty("defaultDescription")] public string DefaultDescription { get; set; }

            [JsonProperty("minWithdrawable")]
            [JsonConverter(typeof(LightMoneyJsonConverter))]
            public LightMoney MinWithdrawable { get; set; }

            [JsonProperty("maxWithdrawable")]
            [JsonConverter(typeof(LightMoneyJsonConverter))]
            public LightMoney MaxWithdrawable { get; set; }

            //https://github.com/fiatjaf/lnurl-rfc/blob/luds/14.md
            [JsonProperty("maxWithdrawable")]
            [JsonConverter(typeof(LightMoneyJsonConverter))]
            public LightMoney CurrentBalance { get; set; }

            //https://github.com/fiatjaf/lnurl-rfc/blob/luds/14.md
            [JsonProperty("balanceCheck")]
            [JsonConverter(typeof(UriJsonConverter))]
            public Uri BalanceCheck { get; set; }

            //https://github.com/fiatjaf/lnurl-rfc/blob/luds/19.md
            [JsonProperty("payLink")]
            [JsonConverter(typeof(UriJsonConverter))]
            public Uri PayLink { get; set; }

            //https://github.com/fiatjaf/lnurl-rfc/blob/luds/15.md
            public async Task<LNUrlErrorResponse> SendRequest(string bolt11, HttpClient httpClient,
                Uri balanceNotify = null)
            {
                var url = Callback;
                var uriBuilder = new UriBuilder(url);
                AppendPayloadToQuery(uriBuilder, "pr", bolt11);
                if (balanceNotify != null)
                {
                    AppendPayloadToQuery(uriBuilder, "balanceNotify", balanceNotify.ToString());
                }

                url = new Uri(uriBuilder.ToString());
                var response = (JObject.Parse(await httpClient.GetStringAsync(url)));

                return response.ToObject<LNUrlErrorResponse>();
            }
        }

        public static readonly Dictionary<string, string> SchemeTagMapping = new Dictionary<string, string>
        {
            { "lnurlc", "channelRequest" },
            { "lnurlw", "withdrawRequest" },
            { "lnurlp", "payRequest" },
            { "keyauth", "login" }
        };

        public static Uri Parse(string lnurl, out string tag)
        {
            lnurl = lnurl.Replace("lightning:", "", StringComparison.InvariantCultureIgnoreCase);
            if (lnurl.StartsWith("lnurl1", StringComparison.InvariantCultureIgnoreCase))
            {
                var encoder = Bech32Encoder.ExtractEncoderFromString(lnurl);
                var data = encoder.DecodeDataRaw(lnurl, out _);
                var result = new Uri(Encoding.UTF8.GetString(data));
                if (!result.IsOnion() && !result.Scheme.Equals("https"))
                {
                    throw new FormatException("LNURL provided is not secure.");
                }

                var query = result.ParseQueryString();
                tag = query.Get("tag");
                return result;
            }

            if (Uri.TryCreate(lnurl, UriKind.Absolute, out var lud17Uri) &&
                SchemeTagMapping.TryGetValue(lud17Uri.Scheme.ToLowerInvariant(), out tag))
            {
                return new Uri(lud17Uri.ToString()
                    .Replace(lud17Uri.Scheme + ":", lud17Uri.IsOnion() ? "http:" : "https:"));
            }

            throw new FormatException("LNURL uses bech32 and 'lnurl' as the hrp (LUD1) or an lnurl LUD17 scheme. ");
        }

        public static Task<object> FetchPayRequestViaInternetIdentifier(string identifier, HttpClient httpClient)
        {
            var s = identifier.Split("@");
            var name = s[0];
            var host = new Uri(s[1]);
            UriBuilder uriBuilder = new UriBuilder(host.IsOnion() ? "http" : "https", identifier);
            uriBuilder.Path = $"/.wellknown/lnurlp/{name}";
            return FetchInformation(uriBuilder.Uri, "payRequest", httpClient);
        }

        public static async Task<object> FetchInformation(Uri lnUrl, string tag, HttpClient httpClient)
        {
            JObject response;
            switch (tag)
            {
                case null:
                    response = JObject.Parse(await httpClient.GetStringAsync(lnUrl));
                    if (response.TryGetValue("tag", out var tagToken))
                    {
                        tag = tagToken.ToString();
                        return await FetchInformation(response, tag, httpClient);
                    }

                    throw new LNUrlException("A tag identifying the LNURL endpoint was not received.");
                case "withdrawRequest":
                    //fast withdraw request supported:
                    var queryString = lnUrl.ParseQueryString();
                    var k1 = queryString.Get("k1");
                    var minWithdrawable = queryString.Get("minWithdrawable");
                    var maxWithdrawable = queryString.Get("maxWithdrawable");
                    var defaultDescription = queryString.Get("defaultDescription");
                    var callback = queryString.Get("callback");
                    if (k1 is null || minWithdrawable is null || maxWithdrawable is null || callback is null)
                    {
                        response = JObject.Parse(await httpClient.GetStringAsync(lnUrl));
                        return await FetchInformation(response, tag, httpClient);
                    }

                    return new LNURLWithdrawRequest()
                    {
                        Callback = new Uri(callback),
                        K1 = k1,
                        Tag = tag,
                        DefaultDescription = defaultDescription,
                        MaxWithdrawable = maxWithdrawable,
                        MinWithdrawable = minWithdrawable
                    };
                default:
                    response = JObject.Parse(await httpClient.GetStringAsync(lnUrl));
                    return await FetchInformation(response, tag, httpClient);
            }
        }

        private static async Task<object> FetchInformation(JObject response, string tag, HttpClient httpClient)
        {
            if (LNUrlErrorResponse.IsErrorResponse(response, out var errorResponse))
            {
                return errorResponse;
            }

            switch (tag)
            {
                case "channelRequest":
                    return response.ToObject<LNURLChannelRequest>();
                case "withdrawRequest":
                    return response.ToObject<LNURLWithdrawRequest>();
                case "payRequest":
                    return response.ToObject<LNURLPayRequest>();
                default:
                    return response;
            }
        }

        public class LNUrlException : Exception
        {
            public LNUrlException(string message) : base(message)
            {
            }
        }


        public class LNUrlErrorResponse
        {
            [JsonProperty("status")] public string Status { get; set; }
            [JsonProperty("reason")] public string Reason { get; set; }

            public static bool IsErrorResponse(JObject response, out LNUrlErrorResponse error)
            {
                if (response.ContainsKey("status") && response["status"].Value<string>()
                                                       .Equals("Error", StringComparison.InvariantCultureIgnoreCase)
                                                   && response.ContainsKey("reason"))
                {
                    error = response.ToObject<LNUrlErrorResponse>();
                    return true;
                }

                error = null;
                return false;
            }
        }
    }
}
