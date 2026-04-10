using System.Text.Json;
using System.Text.Json.Serialization;
using LNURL.JsonConverters.SystemTextJson;

namespace LNURL;

public static class LNURLJsonOptions
{
    private static JsonSerializerOptions _default;

    public static JsonSerializerOptions Default => _default ??= CreateOptions();

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
