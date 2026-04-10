using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LNURL.JsonConverters.SystemTextJson;

public class STJUriJsonConverter : JsonConverter<Uri>
{
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

    public override void Write(Utf8JsonWriter writer, Uri value, JsonSerializerOptions options)
    {
        if (value is null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value.IsAbsoluteUri ? value.AbsoluteUri : value.ToString());
    }
}
