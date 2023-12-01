using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace LNURL;

public class HttpLNURLCommunicator : ILNURLCommunicator
{
    private readonly HttpClient _httpClient;

    public HttpLNURLCommunicator(HttpClient httpClient = null)
    {
        _httpClient = httpClient?? new HttpClient();
    }

    public async Task<JObject> SendRequest(Uri lnurl, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(lnurl, cancellationToken);
        return JObject.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
    }
    
}