using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using BTCPayServer.Lightning;

namespace LNURL.JsonConverters.SystemTextJson;

public class STJLightMoneyJsonConverter : JsonConverter<LightMoney>
{
    public override LightMoney Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;
        if (reader.TokenType == JsonTokenType.Number)
            return new LightMoney(reader.GetInt64());
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            if (long.TryParse(str, out var msats))
                return new LightMoney(msats);
        }

        throw new JsonException("Invalid LightMoney value");
    }

    public override void Write(Utf8JsonWriter writer, LightMoney value, JsonSerializerOptions options)
    {
        if (value is null)
            writer.WriteNullValue();
        else
            writer.WriteNumberValue(value.MilliSatoshi);
    }
}
