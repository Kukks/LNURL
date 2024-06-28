using System;
using System.Text.Json;
using JsonException = System.Text.Json.JsonException;

namespace LNURL.Json.SystemJson;

public class UriJsonConverter : System.Text.Json.Serialization.JsonConverter<Uri>
{
  
    public override Uri Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Unexpected token type for uri");
        if(Uri.TryCreate(reader.GetString(), UriKind.Absolute, out var result))
            return result;
        throw new JsonException("Invalid uri");
    }

    public override void Write(Utf8JsonWriter writer, Uri value, JsonSerializerOptions options)
    {
        if (value is not null)
            writer.WriteStringValue(value.ToString());
        else
        {
            writer.WriteNullValue();
        }
    }
}