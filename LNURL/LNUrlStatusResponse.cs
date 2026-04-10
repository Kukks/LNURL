using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using STJ = System.Text.Json.Serialization;

namespace LNURL;

public class LNUrlStatusResponse
{
    [JsonProperty("status")]
    [STJ.JsonPropertyName("status")]
    public string Status { get; set; }

    [JsonProperty("reason")]
    [STJ.JsonPropertyName("reason")]
    public string Reason { get; set; }

    public static bool IsErrorResponse(JObject response, out LNUrlStatusResponse status)
    {
        if (response.ContainsKey("status") && response["status"].Value<string>()
                .Equals("Error", StringComparison.InvariantCultureIgnoreCase))
        {
            status = response.ToObject<LNUrlStatusResponse>();
            return true;
        }

        status = null;
        return false;
    }
}
