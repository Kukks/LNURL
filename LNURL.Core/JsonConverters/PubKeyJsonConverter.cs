using System;
using System.Diagnostics.CodeAnalysis;
using NBitcoin;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;

namespace LNURL.JsonConverters;

/// <summary>
/// Newtonsoft.Json converter for <see cref="PubKey"/>, serializing and deserializing
/// secp256k1 public keys as hex-encoded JSON strings.
/// </summary>
public class PubKeyJsonConverter : JsonConverter<PubKey>
{
    /// <inheritdoc />
    public override PubKey ReadJson(JsonReader reader, Type objectType, [AllowNull] PubKey existingValue,
        bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType != JsonToken.String)
            throw new JsonObjectException("Unexpected token type for PubKey", reader.Path);
        try
        {
            return new PubKey((string) reader.Value);
        }
        catch (Exception e)
        {
            throw new JsonObjectException(e.Message, reader.Path);
        }
    }

    /// <inheritdoc />
    public override void WriteJson(JsonWriter writer, [AllowNull] PubKey value, JsonSerializer serializer)
    {
        if (value is { })
            writer.WriteValue(value.ToString());
    }
}
