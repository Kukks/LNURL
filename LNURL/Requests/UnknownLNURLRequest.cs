using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace LNURL.Requests;

public class UnknownLNURLRequest : ILNURLRequest
{
    [JsonProperty("tag")]
    [JsonPropertyName("tag")]
    public string Tag { get; set; }


    [System.Text.Json.Serialization.JsonExtensionData]
    public IDictionary<string, JsonElement> AdditionalData { get; set; }
}