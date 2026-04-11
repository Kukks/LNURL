using System.Text.Json;
using System.Text.Json.Serialization;
using LNURL.JsonConverters.SystemTextJson;

namespace LNURL;

/// <summary>
/// Provides pre-configured <see cref="JsonSerializerOptions"/> for System.Text.Json serialization
/// of LNURL types. Includes all custom converters needed for LNURL protocol types such as
/// <see cref="System.Uri"/>, <see cref="BTCPayServer.Lightning.LightMoney"/>,
/// <see cref="BTCPayServer.Lightning.NodeInfo"/>, <see cref="NBitcoin.PubKey"/>,
/// and <see cref="NBitcoin.Crypto.ECDSASignature"/>.
/// </summary>
public static class LNURLJsonOptions
{
    private static JsonSerializerOptions _default;

    /// <summary>
    /// Gets the default <see cref="JsonSerializerOptions"/> instance, lazily initialized via <see cref="CreateOptions"/>.
    /// This instance is reused across calls for optimal performance.
    /// </summary>
    public static JsonSerializerOptions Default => _default ??= CreateOptions();

    /// <summary>
    /// Creates a new <see cref="JsonSerializerOptions"/> instance configured with camelCase naming,
    /// null-value ignoring, case-insensitive property matching, and all LNURL-specific converters.
    /// </summary>
    /// <returns>A new configured <see cref="JsonSerializerOptions"/> instance.</returns>
    public static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };
        AddConverters(options);
        return options;
    }

    /// <summary>
    /// Registers all LNURL-specific System.Text.Json converters on the given <paramref name="options"/> instance.
    /// Call this method when you need to add LNURL converter support to your own <see cref="JsonSerializerOptions"/>.
    /// </summary>
    /// <param name="options">The <see cref="JsonSerializerOptions"/> to add converters to.</param>
    public static void AddConverters(JsonSerializerOptions options)
    {
        options.Converters.Add(new STJUriJsonConverter());
        options.Converters.Add(new STJLightMoneyJsonConverter());
        options.Converters.Add(new STJNodeUriJsonConverter());
        options.Converters.Add(new STJPubKeyJsonConverter());
        options.Converters.Add(new STJSigJsonConverter());
        options.Converters.Add(new STJSuccessActionJsonConverter());
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }
}
