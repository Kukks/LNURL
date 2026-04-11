using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LNURL.JsonConverters.SystemTextJson;

/// <summary>
/// System.Text.Json converter for <see cref="Uri"/>, handling null and empty string values
/// and ensuring only absolute URIs are accepted during deserialization.
/// </summary>
public class STJUriJsonConverter : JsonConverter<Uri>
{
    /// <inheritdoc />
    public override Uri Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;
        var str = reader.GetString();
        if (string.IsNullOrEmpty(str))
            return null;
        if (Uri.TryCreate(str, UriKind.Absolute, out var result))
            return result;
        throw new JsonException("Invalid Uri value");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Uri value, JsonSerializerOptions options)
    {
        if (value is null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value.IsAbsoluteUri ? value.AbsoluteUri : value.ToString());
    }
}
