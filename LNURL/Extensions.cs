using System;
using System.Net;
using NBitcoin;

namespace LNURL;

public static class Extensions
{
    public static bool IsOnion(this Uri uri)
    {
        if (uri == null || !uri.IsAbsoluteUri)
            return false;
        return uri.DnsSafeHost.EndsWith(".onion", StringComparison.OrdinalIgnoreCase);
    }

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
}