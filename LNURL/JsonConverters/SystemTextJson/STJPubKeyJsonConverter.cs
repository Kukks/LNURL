using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using NBitcoin;

namespace LNURL.JsonConverters.SystemTextJson;

public class STJPubKeyJsonConverter : JsonConverter<PubKey>
{
    public override PubKey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Unexpected token type for PubKey");
        return new PubKey(reader.GetString());
    }

    public override void Write(Utf8JsonWriter writer, PubKey value, JsonSerializerOptions options)
    {
        if (value is not null)
            writer.WriteStringValue(value.ToString());
        else
            writer.WriteNullValue();
    }
}
