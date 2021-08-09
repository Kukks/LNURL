using System;
using System.Net.Http;
using System.Threading.Tasks;
using BTCPayServer.Lightning;
using BTCPayServer.Lightning.JsonConverters;
using LNURL.JsonConverters;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LNURL
{
    /// <summary>
    ///     https://github.com/fiatjaf/lnurl-rfc/blob/luds/02.md
    /// </summary>
    public class LNURLWithdrawRequest
    {
        [JsonProperty("uri")]
        [JsonConverter(typeof(UriJsonConverter))]
        public Uri Callback { get; set; }

        [JsonProperty("k1")] public string K1 { get; set; }

        [JsonProperty("tag")] public string Tag { get; set; }

        [JsonProperty("defaultDescription")] public string DefaultDescription { get; set; }

        [JsonProperty("minWithdrawable")]
        [JsonConverter(typeof(LightMoneyJsonConverter))]
        public LightMoney MinWithdrawable { get; set; }

        [JsonProperty("maxWithdrawable")]
        [JsonConverter(typeof(LightMoneyJsonConverter))]
        public LightMoney MaxWithdrawable { get; set; }

        //https://github.com/fiatjaf/lnurl-rfc/blob/luds/14.md
        [JsonProperty("maxWithdrawable")]
        [JsonConverter(typeof(LightMoneyJsonConverter))]
        public LightMoney CurrentBalance { get; set; }

        //https://github.com/fiatjaf/lnurl-rfc/blob/luds/14.md
        [JsonProperty("balanceCheck")]
        [JsonConverter(typeof(UriJsonConverter))]
        public Uri BalanceCheck { get; set; }

        //https://github.com/fiatjaf/lnurl-rfc/blob/luds/19.md
        [JsonProperty("payLink")]
        [JsonConverter(typeof(UriJsonConverter))]
        public Uri PayLink { get; set; }

        //https://github.com/fiatjaf/lnurl-rfc/blob/luds/15.md
        public async Task<LNUrlStatusResponse> SendRequest(string bolt11, HttpClient httpClient,
            Uri balanceNotify = null)
        {
            var url = Callback;
            var uriBuilder = new UriBuilder(url);
            LNURL.AppendPayloadToQuery(uriBuilder, "pr", bolt11);
            if (balanceNotify != null) LNURL.AppendPayloadToQuery(uriBuilder, "balanceNotify", balanceNotify.ToString());

            url = new Uri(uriBuilder.ToString());
            var response = JObject.Parse(await httpClient.GetStringAsync(url));

            return response.ToObject<LNUrlStatusResponse>();
        }
    }
}