using System;
using System.Collections.Specialized;
using System.Net;
using System.Web;
using NBitcoin;

namespace LNURL;

/// <summary>
/// Provides extension methods for <see cref="Uri"/> used by the LNURL library to determine
/// network characteristics of service endpoints.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Determines whether the URI points to a Tor hidden service (.onion address).
    /// LNURL allows HTTP (instead of HTTPS) for .onion addresses.
    /// </summary>
    /// <param name="uri">The URI to check.</param>
    /// <returns><c>true</c> if the URI host ends with <c>.onion</c>; otherwise <c>false</c>.</returns>
    public static bool IsOnion(this Uri uri)
    {
        if (uri == null || !uri.IsAbsoluteUri)
            return false;
        return uri.DnsSafeHost.EndsWith(".onion", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether the URI points to a local network address.
    /// This includes DNS names ending in <c>.internal</c>, <c>.local</c>, <c>.lan</c>,
    /// single-label hostnames (no dots), and RFC 1918 / loopback IP addresses.
    /// LNURL allows HTTP (instead of HTTPS) for local network addresses.
    /// </summary>
    /// <param name="server">The URI to check.</param>
    /// <returns><c>true</c> if the URI host is on a local network; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="server"/> is <c>null</c>.</exception>
    public static bool IsLocalNetwork(this Uri server)
    {
        if (server == null)
            throw new ArgumentNullException(nameof(server));

        if (server.HostNameType == UriHostNameType.Dns)
            return server.Host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase) ||
                   server.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
                   server.Host.EndsWith(".lan", StringComparison.OrdinalIgnoreCase) ||
                   server.Host.IndexOf('.', StringComparison.OrdinalIgnoreCase) == -1;

        if (IPAddress.TryParse(server.Host, out var ip)) return ip.IsLocal() || ip.IsRFC1918();

        return false;
    }

    internal static NameValueCollection ParseQueryString(this Uri uri)
    {
        return HttpUtility.ParseQueryString(uri.Query);
    }
}
