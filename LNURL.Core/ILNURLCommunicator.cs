using System;
using System.Threading;
using System.Threading.Tasks;

namespace LNURL;

/// <summary>
/// Abstracts the transport layer for LNURL protocol communication,
/// enabling LNURL flows over HTTP, Nostr, or other transports.
/// </summary>
public interface ILNURLCommunicator
{
    /// <summary>
    /// Sends a request to the given LNURL endpoint and returns the raw JSON response.
    /// </summary>
    /// <param name="lnurl">The endpoint URI (may be an HTTP URL, <c>nostr:</c> URI, etc.).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The raw JSON response string from the endpoint.</returns>
    Task<string> SendRequest(Uri lnurl, CancellationToken cancellationToken = default);
}
