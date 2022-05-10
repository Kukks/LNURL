using System;
using BTCPayServer.Lightning;
using LNURL.JsonConverters;
using Newtonsoft.Json;

namespace LNURL
{
    /// <summary>
    ///     https://github.com/fiatjaf/lnurl-rfc/blob/luds/02.md
    /// </summary>
    public class LNURLChannelRequest
    {
        [JsonProperty("uri")]
        [JsonConverter(typeof(NodeUriJsonConverter))]
        public NodeInfo Uri { get; set; }

        [JsonProperty("callback")]
        [JsonConverter(typeof(UriJsonConverter))]
        public Uri Callback { get; set; }

        [JsonProperty("k1")] public string K1 { get; set; }

        [JsonProperty("tag")] public string Tag { get; set; }
    }
}