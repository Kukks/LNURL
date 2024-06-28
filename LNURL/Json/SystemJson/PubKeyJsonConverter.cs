using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using NBitcoin;

namespace LNURL.Json.SystemJson;

public class PubKeyJsonConverter : JsonConverter<PubKey>
{
    public override PubKey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if(reader.TokenType == JsonTokenType.Null)
            return null;
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Unexpected token type for PubKey");
        var val = reader.GetString();
        return val is null ? null : new PubKey(val);
        
    }

    public override void Write(Utf8JsonWriter writer, PubKey value, JsonSerializerOptions options)
    {
        if (value is { })
            writer.WriteStringValue(value.ToString());
        else
        {
            writer.WriteNullValue();
        }
    }
}