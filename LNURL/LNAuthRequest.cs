using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace LNURL
{
    /// <summary>
    /// https://github.com/fiatjaf/lnurl-rfc/blob/luds/04.md
    /// </summary>
    public class LNAuthRequest
    {
        public Uri LNUrl { get; set; }
        public enum LNAUthRequestAction
        {
            Register,
            Login,
            Link,
            Auth
        }
            
        public string Tag => "login";
        public string K1 { get; set; }
        public LNAUthRequestAction? Action { get; set; }

        public async Task<LNUrlStatusResponse> SendChallenge(string sig, string key, HttpClient httpClient)
        {
                
            var url = LNUrl;
            var uriBuilder = new UriBuilder(url);
            LNURL.AppendPayloadToQuery(uriBuilder, "sig", sig);
            LNURL.AppendPayloadToQuery(uriBuilder, "key", key);
            url = new Uri(uriBuilder.ToString());
            var response = JObject.Parse(await httpClient.GetStringAsync(url));
            return response.ToObject<LNUrlStatusResponse>();
        }
    }
}