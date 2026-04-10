using System;
using System.Diagnostics.CodeAnalysis;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;

namespace LNURL.JsonConverters;

/// <summary>
/// Newtonsoft.Json converter for <see cref="ECDSASignature"/>, serializing and deserializing
/// ECDSA signatures as hex-encoded DER-format JSON strings.
/// </summary>
public class SigJsonConverter : JsonConverter<ECDSASignature>
{
    /// <inheritdoc />
    public override ECDSASignature ReadJson(JsonReader reader, Type objectType,
        [AllowNull] ECDSASignature existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType != JsonToken.String)
            throw new JsonObjectException("Unexpected token type for ECDSASignature", reader.Path);
        try
        {
            return ECDSASignature.FromDER(Encoders.Hex.DecodeData((string) reader.Value));
        }
        catch (Exception e)
        {
            throw new JsonObjectException(e.Message, reader.Path);
        }
    }

    /// <inheritdoc />
    public override void WriteJson(JsonWriter writer, [AllowNull] ECDSASignature value, JsonSerializer serializer)
    {
        if (value is { })
            writer.WriteValue(Encoders.Hex.EncodeData(value.ToDER()));
    }
}
