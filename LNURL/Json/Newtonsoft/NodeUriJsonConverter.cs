using System;
using System.Diagnostics.CodeAnalysis;
using BTCPayServer.Lightning;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;

namespace LNURL.Json.Newtonsoft;

public class NodeUriJsonConverter : global::Newtonsoft.Json.JsonConverter<NodeInfo>
{
    public override NodeInfo ReadJson(JsonReader reader, Type objectType, [AllowNull] NodeInfo existingValue,
        bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType != JsonToken.String)
            throw new JsonObjectException("Unexpected token type for NodeUri", reader.Path);
        if (NodeInfo.TryParse((string) reader.Value, out var info))
            return info;
        throw new JsonObjectException("Invalid NodeUri", reader.Path);
    }

    public override void WriteJson(JsonWriter writer, [AllowNull] NodeInfo value, JsonSerializer serializer)
    {
        if (value is NodeInfo)
            writer.WriteValue(value.ToString());
    }
}