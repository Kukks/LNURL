using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using BTCPayServer.LNUrl;
using NBitcoin.DataEncoders;
using Newtonsoft.Json.Linq;

namespace LNURL
{
    public class LNURL
    {
        public static readonly Dictionary<string, string> SchemeTagMapping = new Dictionary<string, string>()
        {
            { "lnurlc", "channelRequest" },
            { "lnurlw", "withdrawRequest" },
            { "lnurlp", "payRequest" },
            { "keyauth", "login" }
        };

        internal static void AppendPayloadToQuery(UriBuilder uri, string key, string value)
        {
            if (uri.Query.Length > 1)
                uri.Query += "&";

            uri.Query = uri.Query + WebUtility.UrlEncode(key) + "=" +
                        WebUtility.UrlEncode(value);
        }

        public static Uri Parse(string lnurl, out string tag)
        {
            lnurl = lnurl.Replace("lightning:", "", StringComparison.InvariantCultureIgnoreCase);
            if (lnurl.StartsWith("lnurl1", StringComparison.InvariantCultureIgnoreCase))
            {
                var encoder = Bech32Encoder.ExtractEncoderFromString(lnurl);
                var data = encoder.DecodeDataRaw(lnurl, out _);
                var result = new Uri(Encoding.UTF8.GetString(data));
                if (!result.IsOnion() && !result.Scheme.Equals("https"))
                    throw new FormatException("LNURL provided is not secure.");

                var query = result.ParseQueryString();
                tag = query.Get("tag");
                return result;
            }

            if (Uri.TryCreate(lnurl, UriKind.Absolute, out var lud17Uri) &&
                SchemeTagMapping.TryGetValue(lud17Uri.Scheme.ToLowerInvariant(), out tag))
                return new Uri(lud17Uri.ToString()
                    .Replace(lud17Uri.Scheme + ":", lud17Uri.IsOnion() ? "http:" : "https:"));

            throw new FormatException("LNURL uses bech32 and 'lnurl' as the hrp (LUD1) or an lnurl LUD17 scheme. ");
        }

        //https://github.com/fiatjaf/lnurl-rfc/blob/luds/16.md
        public static Task<object> FetchPayRequestViaInternetIdentifier(string identifier, HttpClient httpClient)
        {
            var s = identifier.Split("@");
            var name = s[0];
            var host = new Uri(s[1]);
            var uriBuilder = new UriBuilder(host.IsOnion() ? "http" : "https", identifier);
            uriBuilder.Path = $"/.wellknown/lnurlp/{name}";
            return FetchInformation(uriBuilder.Uri, "payRequest", httpClient);
        }

        public static async Task<object> FetchInformation(Uri lnUrl, string tag, HttpClient httpClient)
        {
            JObject response;
            NameValueCollection queryString;
            string k1;
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
                    queryString = lnUrl.ParseQueryString();
                    k1 = queryString.Get("k1");
                    var minWithdrawable = queryString.Get("minWithdrawable");
                    var maxWithdrawable = queryString.Get("maxWithdrawable");
                    var defaultDescription = queryString.Get("defaultDescription");
                    var callback = queryString.Get("callback");
                    if (k1 is null || minWithdrawable is null || maxWithdrawable is null || callback is null)
                    {
                        response = JObject.Parse(await httpClient.GetStringAsync(lnUrl));
                        return await FetchInformation(response, tag, httpClient);
                    }

                    return new LNURLWithdrawRequest
                    {
                        Callback = new Uri(callback),
                        K1 = k1,
                        Tag = tag,
                        DefaultDescription = defaultDescription,
                        MaxWithdrawable = maxWithdrawable,
                        MinWithdrawable = minWithdrawable
                    };
                case "login":

                    queryString = lnUrl.ParseQueryString();
                    k1 = queryString.Get("k1");
                    var action = queryString.Get("action");

                    return new LNAuthRequest()
                    {
                        K1 = k1,
                        LNUrl = lnUrl,
                        Action = string.IsNullOrEmpty(action)
                            ? (LNAuthRequest.LNAUthRequestAction?) null
                            : Enum.Parse<LNAuthRequest.LNAUthRequestAction>(action, true)
                    };

                default:
                    response = JObject.Parse(await httpClient.GetStringAsync(lnUrl));
                    return await FetchInformation(response, tag, httpClient);
            }
        }


        private static async Task<object> FetchInformation(JObject response, string tag, HttpClient httpClient)
        {
            if (LNUrlStatusResponse.IsErrorResponse(response, out var errorResponse)) return errorResponse;

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
    }
}