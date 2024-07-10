using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using BTCPayServer.Lightning;

namespace LNURL.Json.SystemJson;
public class LightMoneyJsonConverter:JsonConverter<LightMoney>
{
    public override LightMoney Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        try
        {
            return reader.TokenType switch
            {
                JsonTokenType.Null => null,
                JsonTokenType.Number => new LightMoney(reader.GetInt64()),
                JsonTokenType.String => new LightMoney(long.Parse(reader.GetString())),
                JsonTokenType.StartObject => reader.Read() ? LightMoney.Zero : null,
                _ => null
            };
        }
        catch (InvalidCastException)
        {
            throw new JsonException("Money amount should be in millisatoshi");
        }
    }

    public override void Write(Utf8JsonWriter writer, LightMoney value, JsonSerializerOptions options)
    {
        if(value is not null)
            writer.WriteNumberValue(value.MilliSatoshi);
        else
            writer.WriteNullValue();
    }
}