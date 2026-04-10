using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using STJ = System.Text.Json.Serialization;

namespace LNURL;

/// <summary>
/// Represents an LNURL status response, used across all LNURL flows to indicate success or error.
/// When the <see cref="Status"/> is <c>"ERROR"</c>, the <see cref="Reason"/> property contains
/// a human-readable error message.
/// </summary>
public class LNUrlStatusResponse
{
    /// <summary>
    /// Gets or sets the status string. A value of <c>"ERROR"</c> (case-insensitive) indicates an error;
    /// <c>"OK"</c> indicates success.
    /// </summary>
    [JsonProperty("status")]
    [STJ.JsonPropertyName("status")]
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the human-readable error message. Only meaningful when <see cref="Status"/> is <c>"ERROR"</c>.
    /// </summary>
    [JsonProperty("reason")]
    [STJ.JsonPropertyName("reason")]
    public string Reason { get; set; }

    /// <summary>
    /// Determines whether the given JSON response represents an LNURL error response.
    /// </summary>
    /// <param name="response">The JSON object to inspect.</param>
    /// <param name="status">
    /// When this method returns <c>true</c>, contains the deserialized <see cref="LNUrlStatusResponse"/>;
    /// otherwise <c>null</c>.
    /// </param>
    /// <returns><c>true</c> if the response contains a <c>status</c> field equal to <c>"ERROR"</c>; otherwise <c>false</c>.</returns>
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
