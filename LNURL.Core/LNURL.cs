using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LNURL;

/// <summary>
/// Static facade class providing core LNURL protocol operations including encoding, decoding,
/// and fetching information from LNURL endpoints. Implements LUD-01 (bech32 encoding),
/// LUD-17 (scheme-based encoding), and LUD-16 (Lightning Address / internet identifier).
/// </summary>
public class LNURL
{
    private static readonly Dictionary<string, string> SchemeTagMapping =
        new(StringComparer.InvariantCultureIgnoreCase)
        {
            {"lnurlc", "channelRequest"},
            {"lnurlw", "withdrawRequest"},
            {"lnurlp", "payRequest"},
            {"keyauth", "login"}
        };

    private static readonly Dictionary<string, string> SchemeTagMappingReversed =
        SchemeTagMapping.ToDictionary(pair => pair.Value, pair => pair.Key,
            StringComparer.InvariantCultureIgnoreCase);

    /// <summary>
    /// Appends a URL-encoded key-value pair to the query string of a <see cref="UriBuilder"/>.
    /// </summary>
    /// <param name="uri">The <see cref="UriBuilder"/> whose query string will be modified.</param>
    /// <param name="key">The query parameter name.</param>
    /// <param name="value">The query parameter value.</param>
    public static void AppendPayloadToQuery(UriBuilder uri, string key, string value)
    {
        if (uri.Query.Length > 1)
            uri.Query += "&";

        uri.Query = uri.Query + WebUtility.UrlEncode(key) + "=" +
                    WebUtility.UrlEncode(value);
    }

    /// <summary>
    /// Parses an LNURL string (bech32-encoded per LUD-01, or a LUD-17 scheme URI) into an absolute <see cref="Uri"/>
    /// and extracts the LNURL tag if present. The <c>lightning:</c> prefix is stripped automatically.
    /// </summary>
    /// <param name="lnurl">
    /// The LNURL string to parse. Accepted formats include bech32 (<c>lnurl1...</c>),
    /// bech32 with prefix (<c>lightning:lnurl1...</c>), or LUD-17 scheme URIs
    /// (<c>lnurlp://</c>, <c>lnurlw://</c>, <c>lnurlc://</c>, <c>keyauth://</c>).
    /// </param>
    /// <param name="tag">
    /// When this method returns, contains the LNURL tag (e.g. <c>"payRequest"</c>, <c>"withdrawRequest"</c>,
    /// <c>"channelRequest"</c>, <c>"login"</c>) if it could be determined; otherwise <c>null</c>.
    /// </param>
    /// <returns>The decoded service <see cref="Uri"/>.</returns>
    /// <exception cref="FormatException">
    /// Thrown when the decoded URL is not secure (must be HTTPS, .onion, or local network),
    /// or the string is not a valid bech32 LNURL or LUD-17 URI.
    /// </exception>
    public static Uri Parse(string lnurl, out string tag)
    {
        lnurl = lnurl.Replace("lightning:", "", StringComparison.InvariantCultureIgnoreCase);
        if (lnurl.StartsWith("lnurl1", StringComparison.InvariantCultureIgnoreCase))
        {
            Bech32Engine.Decode(lnurl, out _, out var data);
            var result = new Uri(Encoding.UTF8.GetString(data));

            if (!result.IsOnion() && !result.Scheme.Equals("https") && !result.IsLocalNetwork() &&
                result.Scheme != "nostr")
                throw new FormatException("LNURL provided is not secure.");

            var query = result.ParseQueryString();
            tag = query.Get("tag");
            return result;
        }

        if (Uri.TryCreate(lnurl, UriKind.Absolute, out var lud17Uri) &&
            SchemeTagMapping.TryGetValue(lud17Uri.Scheme.ToLowerInvariant(), out tag))
            return new Uri(lud17Uri.ToString()
                .Replace(lud17Uri.Scheme + ":",
                    (lud17Uri.Host.StartsWith("nprofile1") || lud17Uri.Host.StartsWith("naddr1")) ? "nostr:" :
                    lud17Uri.IsOnion() ? "http:" : "https:"));

        throw new FormatException("LNURL uses bech32 and 'lnurl' as the hrp (LUD1) or an lnurl LUD17 scheme. ");
    }

    /// <summary>
    /// Encodes a service URL into a bech32-formatted LNURL string as specified by LUD-01.
    /// </summary>
    /// <param name="serviceUrl">The HTTPS, .onion, or local-network service URL to encode.</param>
    /// <returns>A bech32-encoded LNURL string.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="serviceUrl"/> is not HTTPS, an onion service, or on the local network.
    /// </exception>
    public static string EncodeBech32(Uri serviceUrl)
    {
        if (serviceUrl.Scheme != "https" && !serviceUrl.IsOnion() && !serviceUrl.IsLocalNetwork() &&
            serviceUrl.Scheme != "nostr")
            throw new ArgumentException(
                "serviceUrl must be an onion service OR https based OR on the local network OR a Nostr NIP-21 URI",
                nameof(serviceUrl));

        return Bech32Engine.Encode("lnurl", Encoding.UTF8.GetBytes(serviceUrl.ToString()));
    }

    /// <summary>
    /// Encodes a service URL into either a bech32 LNURL (LUD-01) or a LUD-17 scheme-based URI.
    /// </summary>
    /// <param name="serviceUrl">The HTTPS, .onion, or local-network service URL to encode.</param>
    /// <param name="tag">The LNURL tag. If <c>null</c>, extracted from the URL query string.</param>
    /// <param name="bech32">If <c>true</c>, returns a <c>lightning:lnurl1...</c> URI; otherwise a LUD-17 scheme URI.</param>
    /// <returns>A <see cref="Uri"/> in the chosen encoding format.</returns>
    public static Uri EncodeUri(Uri serviceUrl, string tag, bool bech32)
    {
        if (serviceUrl.Scheme != "https" && !serviceUrl.IsOnion() && !serviceUrl.IsLocalNetwork() &&
            serviceUrl.Scheme != "nostr")
            throw new ArgumentException(
                "serviceUrl must be an onion service OR https based OR on the local network OR a Nostr NIP-21 URI",
                nameof(serviceUrl));
        if (string.IsNullOrEmpty(tag)) tag = serviceUrl.ParseQueryString().Get("tag");
        if (tag == "login") LNAuthRequest.EnsureValidUrl(serviceUrl);
        if (bech32) return new Uri($"lightning:{EncodeBech32(serviceUrl)}");

        if (string.IsNullOrEmpty(tag)) tag = serviceUrl.ParseQueryString().Get("tag");

        if (string.IsNullOrEmpty(tag)) throw new ArgumentNullException("tag must be provided", nameof(tag));

        if (!SchemeTagMappingReversed.TryGetValue(tag.ToLowerInvariant(), out var scheme))
            throw new ArgumentOutOfRangeException(
                $"tag must be either {string.Join(',', SchemeTagMappingReversed.Select(pair => pair.Key))}",
                nameof(tag));


        return new UriBuilder(serviceUrl)
        {
            Scheme = scheme
        }.Uri;
    }

    /// <summary>
    /// Fetches a <see cref="LNURLPayRequest"/> for the given Lightning Address (internet identifier)
    /// as specified by LUD-16.
    /// </summary>
    /// <param name="identifier">A Lightning Address in the form <c>user@domain</c>.</param>
    /// <param name="httpClient">The <see cref="HttpClient"/> used to perform the HTTP request.</param>
    /// <returns>The <see cref="LNURLPayRequest"/> fetched from the well-known LNURL-pay endpoint.</returns>
    public static Task<LNURLPayRequest> FetchPayRequestViaInternetIdentifier(string identifier,
        HttpClient httpClient)
    {
        return FetchPayRequestViaInternetIdentifier(identifier, httpClient, default);
    }

    /// <summary>
    /// Fetches a <see cref="LNURLPayRequest"/> for the given Lightning Address (internet identifier)
    /// as specified by LUD-16.
    /// </summary>
    /// <param name="identifier">A Lightning Address in the form <c>user@domain</c>.</param>
    /// <param name="httpClient">The <see cref="HttpClient"/> used to perform the HTTP request.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="LNURLPayRequest"/> fetched from the well-known LNURL-pay endpoint.</returns>
    public static async Task<LNURLPayRequest> FetchPayRequestViaInternetIdentifier(string identifier,
        HttpClient httpClient, CancellationToken cancellationToken)
    {
        return (LNURLPayRequest) await FetchInformation(ExtractUriFromInternetIdentifier(identifier), "payRequest",
            httpClient, cancellationToken);
    }

    /// <summary>
    /// Converts a Lightning Address (internet identifier per LUD-16) to its well-known LNURL-pay URL.
    /// </summary>
    /// <param name="identifier">A Lightning Address in the form <c>user@domain</c> or <c>user@domain:port</c>.</param>
    /// <returns>The well-known LNURL-pay <see cref="Uri"/> for the given identifier.</returns>
    public static Uri ExtractUriFromInternetIdentifier(string identifier)
    {
        var s = identifier.Split("@");
        var s2 = s[1].Split(":");
        UriBuilder uriBuilder;
        if (s2.Length > 1)
            uriBuilder = new UriBuilder(
                s2[0].EndsWith(".onion", StringComparison.InvariantCultureIgnoreCase) ? "http" : "https",
                s2[0], int.Parse(s2[1]))
            {
                Path = $"/.well-known/lnurlp/{s[0]}"
            };
        else
            uriBuilder =
                new UriBuilder(s[1].EndsWith(".onion", StringComparison.InvariantCultureIgnoreCase) ? "http" : "https",
                    s2[0])
                {
                    Path = $"/.well-known/lnurlp/{s[0]}"
                };

        return uriBuilder.Uri;
    }


    /// <summary>
    /// Fetches LNURL endpoint information from the given URL, automatically determining the response type.
    /// </summary>
    public static Task<object> FetchInformation(Uri lnUrl, HttpClient httpClient)
    {
        return FetchInformation(lnUrl, httpClient, default);
    }

    /// <inheritdoc cref="FetchInformation(Uri, HttpClient)"/>
    public static async Task<object> FetchInformation(Uri lnUrl, HttpClient httpClient, CancellationToken cancellationToken)
    {
        return await FetchInformation(lnUrl, null, httpClient, cancellationToken);
    }

    /// <summary>
    /// Fetches LNURL endpoint information from the given URL with an explicit tag hint.
    /// </summary>
    public static Task<object> FetchInformation(Uri lnUrl, string tag, HttpClient httpClient)
    {
        return FetchInformation(lnUrl, tag, httpClient, default);
    }

    /// <summary>
    /// Fetches LNURL endpoint information from the given URL with an explicit tag hint.
    /// Supports fast withdraw (LUD-03 query-string parameters) and LNURL-auth (LUD-04) inline parsing.
    /// </summary>
    public static async Task<object> FetchInformation(Uri lnUrl, string tag, HttpClient httpClient, CancellationToken cancellationToken)
    {
        return await FetchInformation(lnUrl, tag, new HttpLNURLCommunicator(httpClient), cancellationToken);
    }

    /// <summary>
    /// Fetches LNURL endpoint information using a custom <see cref="ILNURLCommunicator"/> transport.
    /// This enables LNURL flows over Nostr or other non-HTTP transports.
    /// </summary>
    public static Task<object> FetchInformation(Uri lnUrl, string tag, ILNURLCommunicator communicator)
    {
        return FetchInformation(lnUrl, tag, communicator, default);
    }

    /// <inheritdoc cref="FetchInformation(Uri, string, ILNURLCommunicator)"/>
    public static async Task<object> FetchInformation(Uri lnUrl, string tag, ILNURLCommunicator communicator, CancellationToken cancellationToken)
    {
        try
        {
            lnUrl = Parse(lnUrl.ToString(), out tag);
        }
        catch (Exception)
        {
            // ignored
        }

        tag ??= lnUrl.ParseQueryString().Get("tag");
        NameValueCollection queryString;
        string k1;
        switch (tag)
        {
            case null:
                var content = await communicator.SendRequest(lnUrl, cancellationToken);

                using (var doc = JsonDocument.Parse(content))
                {
                    if (doc.RootElement.TryGetProperty("tag", out var tagToken))
                    {
                        tag = tagToken.GetString();
                        return DeserializeByTag(content, tag, lnUrl);
                    }
                }

                throw new LNUrlException("A tag identifying the LNURL endpoint was not received.");
            case "withdrawRequest":
                queryString = lnUrl.ParseQueryString();
                k1 = queryString.Get("k1");
                var minWithdrawable = queryString.Get("minWithdrawable");
                var maxWithdrawable = queryString.Get("maxWithdrawable");
                var defaultDescription = queryString.Get("defaultDescription");
                var callback = queryString.Get("callback") ?? lnUrl.ToString();
                if (k1 is null || minWithdrawable is null || maxWithdrawable is null)
                {
                    var withdrawContent = await communicator.SendRequest(lnUrl, cancellationToken);
                    return DeserializeByTag(withdrawContent, tag, lnUrl);
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

                return new LNAuthRequest
                {
                    K1 = k1,
                    LNUrl = lnUrl,
                    Action = string.IsNullOrEmpty(action)
                        ? null
                        : Enum.Parse<LNAuthRequest.LNAuthRequestAction>(action, true)
                };

            default:
                var defaultContent = await communicator.SendRequest(lnUrl, cancellationToken);
                return DeserializeByTag(defaultContent, tag, lnUrl);
        }
    }

    private static object DeserializeByTag(string json, string tag, Uri lnUrl = null)
    {
        if (LNUrlStatusResponse.IsErrorResponse(json, out var errorResponse)) return errorResponse;

        switch (tag)
        {
            case "channelRequest":
                var channelRequest = JsonSerializer.Deserialize<LNURLChannelRequest>(json, LNURLJsonOptions.Default);
                channelRequest.Callback ??= lnUrl;
                return channelRequest;
            case "hostedChannelRequest":
                return JsonSerializer.Deserialize<LNURLHostedChannelRequest>(json, LNURLJsonOptions.Default);
            case "withdrawRequest":
                var withdrawRequest = JsonSerializer.Deserialize<LNURLWithdrawRequest>(json, LNURLJsonOptions.Default);
                withdrawRequest.Callback ??= lnUrl;
                return withdrawRequest;
            case "payRequest":
                var payRequest = JsonSerializer.Deserialize<LNURLPayRequest>(json, LNURLJsonOptions.Default);
                payRequest.Callback ??= lnUrl;
                return payRequest;
            default:
                return JsonDocument.Parse(json);
        }
    }
}
