using System;
using System.Diagnostics.CodeAnalysis;
using NBitcoin;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;

namespace LNURL.JsonConverters;

public class PubKeyJsonConverter : JsonConverter<PubKey>
{
    public override PubKey ReadJson(JsonReader reader, Type objectType, [AllowNull] PubKey existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType != JsonToken.String)
            throw new JsonObjectException( "Unexpected token type for PubKey", reader.Path);
        try
        {
            return new PubKey((string) reader.Value);
        }
        catch (Exception e)
        {
            throw new JsonObjectException(e.Message,reader.Path);
        }
    }

    public override void WriteJson(JsonWriter writer, [AllowNull] PubKey value, JsonSerializer serializer)
    {
        if (value is {})
            writer.WriteValue(value.ToString());
    }
}