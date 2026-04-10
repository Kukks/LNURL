using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using static LNURL.LNURLPayRequest.LNURLPayRequestCallbackResponse;

namespace LNURL.JsonConverters.SystemTextJson;

/// <summary>
/// System.Text.Json converter for polymorphic deserialization of <see cref="ILNURLPayRequestSuccessAction"/>
/// based on the <c>tag</c> property (LUD-09). Supports <c>"message"</c>, <c>"url"</c>, and <c>"aes"</c> tags.
/// </summary>
public class STJSuccessActionJsonConverter : JsonConverter<ILNURLPayRequestSuccessAction>
{
    /// <inheritdoc />
    public override ILNURLPayRequestSuccessAction Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        if (!root.TryGetProperty("tag", out var tagElement))
            throw new JsonException("Missing 'tag' property in success action");

        var tag = tagElement.GetString();
        var raw = root.GetRawText();
        return tag switch
        {
            "message" => JsonSerializer.Deserialize<LNURLPayRequestSuccessActionMessage>(raw, options),
            "url" => JsonSerializer.Deserialize<LNURLPayRequestSuccessActionUrl>(raw, options),
            "aes" => JsonSerializer.Deserialize<LNURLPayRequestSuccessActionAES>(raw, options),
            _ => throw new JsonException($"Unknown success action tag: {tag}")
        };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, ILNURLPayRequestSuccessAction value,
        JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}
