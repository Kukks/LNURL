using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using BTCPayServer.Lightning;

namespace LNURL.Json.SystemJson;

public class NodeUriJsonConverter : JsonConverter<NodeInfo>
{
    public override NodeInfo Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Unexpected token type for NodeUri");
        if (NodeInfo.TryParse(reader.GetString(), out var info))
            return info;
        throw new JsonException("Invalid NodeUri");
    }

    public override void Write(Utf8JsonWriter writer, NodeInfo value, JsonSerializerOptions options)
    {
        if (value is not null)
            writer.WriteStringValue(value.ToString());
        else
        {
            writer.WriteNullValue();
        }
    }
}