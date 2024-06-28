using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using LNURL.Requests;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace LNURL;

public class LNUrlStatusResponse : ILNURLRequest
{
    [JsonProperty("status")]
    [JsonPropertyName("status")]
    public string Status { get; set; }

    [JsonPropertyName("reason")]
    [JsonProperty("reason")]
    public string Reason { get; set; }

    public static bool IsErrorResponse(JObject response, out LNUrlStatusResponse status)
    {
        return IsErrorResponse(JsonSerializer.Deserialize<JsonElement>(response.ToString()), out status);
    }

    public static bool IsErrorResponse(JsonElement response, out LNUrlStatusResponse status)

    {
        if (response.TryGetProperty("status", out var statusElement) && statusElement.GetString() is { } statusStr &&
            statusStr
                .Equals("Error", StringComparison.InvariantCultureIgnoreCase))
        {
            status = response.Deserialize<LNUrlStatusResponse>();
            return true;
        }

        status = null;
        return false;
    }

    public string Tag => "error";
}