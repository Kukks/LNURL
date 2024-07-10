using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;

namespace LNURL.Json.SystemJson;

public class SigJsonConverter : JsonConverter<ECDSASignature>
{
    public override ECDSASignature Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if(reader.TokenType == JsonTokenType.Null)
            return null;
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Unexpected token type for ECDSASignature");
        return ECDSASignature.FromDER(Encoders.Hex.DecodeData(reader.GetString()));

    }

    public override void Write(Utf8JsonWriter writer, ECDSASignature value, JsonSerializerOptions options)
    {
        if (value is not null)
            writer.WriteStringValue(Encoders.Hex.EncodeData(value.ToDER()));
        else
        {
            writer.WriteNullValue();
        }
    }
}