using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LNURL;

/// <summary>
/// An <see cref="ILNURLCommunicator"/> that performs LNURL requests over HTTP(S)
/// using an <see cref="HttpClient"/>.
/// </summary>
public class HttpLNURLCommunicator : ILNURLCommunicator
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance with the specified <see cref="HttpClient"/>.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for requests. If <c>null</c>, a new instance is created.</param>
    public HttpLNURLCommunicator(HttpClient httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    /// <inheritdoc />
    public async Task<string> SendRequest(Uri lnurl, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(lnurl, cancellationToken);
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
