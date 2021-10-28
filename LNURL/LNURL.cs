using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace LNURL
{
    public class LNURL
    {
        private static readonly Dictionary<string, string> SchemeTagMapping =
            new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
            {
                { "lnurlc", "channelRequest" },
                { "lnurlw", "withdrawRequest" },
                { "lnurlp", "payRequest" },
                { "keyauth", "login" }
            };

        private static readonly Dictionary<string, string> SchemeTagMappingReversed =
            SchemeTagMapping.ToDictionary(pair => pair.Value, pair => pair.Key,
                StringComparer.InvariantCultureIgnoreCase);

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
                Bech32Engine.Decode(lnurl, out _, out var data);
                var result = new Uri(Encoding.UTF8.GetString(data));

                if (!result.IsOnion() && !result.Scheme.Equals("https") && !result.IsLocalNetwork())
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

        public static string EncodeBech32(Uri serviceUrl)
        {
            if (serviceUrl.Scheme != "https" && !serviceUrl.IsOnion() && !serviceUrl.IsLocalNetwork())
            {
                throw new ArgumentException("serviceUrl must be an onion service OR https based OR on the local network", nameof(serviceUrl));
            }

            return Bech32Engine.Encode("lnurl", Encoding.UTF8.GetBytes(serviceUrl.ToString()));
        }

        public static Uri EncodeUri(Uri serviceUrl, string tag, bool bech32)
        {
            if (serviceUrl.Scheme != "https" && !serviceUrl.IsOnion() && !serviceUrl.IsLocalNetwork())
            {
                throw new ArgumentException("serviceUrl must be an onion service OR https based OR on the local network", nameof(serviceUrl));
            }

            if (bech32)
            {
                return new Uri($"lightning:{EncodeBech32(serviceUrl)}");
            }

            if (string.IsNullOrEmpty(tag))
            {
                tag = serviceUrl.ParseQueryString().Get("tag");
            }

            if (string.IsNullOrEmpty(tag))
            {
                throw new ArgumentNullException("tag must be provided", nameof(tag));
            }

            if (!SchemeTagMappingReversed.TryGetValue(tag.ToLowerInvariant(), out var scheme))
            {
                throw new ArgumentOutOfRangeException(
                    $"tag must be either {string.Join(',', SchemeTagMappingReversed.Select(pair => pair.Key))}",
                    nameof(tag));
            }

            return new UriBuilder(serviceUrl)
            {
                Scheme = scheme
            }.Uri;
        }

        //https://github.com/fiatjaf/lnurl-rfc/blob/luds/16.md
        public static async Task<LNURLPayRequest> FetchPayRequestViaInternetIdentifier(string identifier,
            HttpClient httpClient)
        {
            return (LNURLPayRequest)await FetchInformation(ExtractUriFromInternetIdentifier(identifier), "payRequest",
                httpClient);
        }

        public static Uri ExtractUriFromInternetIdentifier(string identifier)
        {
            var s = identifier.Split("@");
            var s2 = s[1].Split(":");
            UriBuilder uriBuilder;
            if (s2.Length > 1)
            {
                uriBuilder = new UriBuilder(s[1].EndsWith(".onion", StringComparison.InvariantCultureIgnoreCase) ? "http" : "https",
                    s2[0], int.Parse(s2[1]))
                {
                    Path = $"/.well-known/lnurlp/{s[0]}"
                };
            }
            else
            {
                uriBuilder =
                    new UriBuilder(s[1].EndsWith(".onion", StringComparison.InvariantCultureIgnoreCase) ? "http" : "https",
                        s2[0])
                    {
                        Path = $"/.well-known/lnurlp/{s[0]}"
                    };
            }
            
            return uriBuilder.Uri;
        }

        public static async Task<object> FetchInformation(Uri lnUrl, HttpClient httpClient)
        {
            return await FetchInformation(lnUrl, null, httpClient);
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
                        return FetchInformation(response, tag);
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
                        return FetchInformation(response, tag);
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
                            ? (LNAuthRequest.LNAUthRequestAction?)null
                            : Enum.Parse<LNAuthRequest.LNAUthRequestAction>(action, true)
                    };

                default:
                    response = JObject.Parse(await httpClient.GetStringAsync(lnUrl));
                    return FetchInformation(response, tag);
            }
        }

        private static object FetchInformation(JObject response, string tag)
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