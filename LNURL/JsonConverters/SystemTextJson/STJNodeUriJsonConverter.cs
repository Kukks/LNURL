using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using BTCPayServer.Lightning;

namespace LNURL.JsonConverters.SystemTextJson;

/// <summary>
/// System.Text.Json converter for <see cref="NodeInfo"/>, serializing and deserializing
/// Lightning node URIs (e.g. <c>pubkey@host:port</c>) as JSON strings.
/// </summary>
public class STJNodeUriJsonConverter : JsonConverter<NodeInfo>
{
    /// <inheritdoc />
    public override NodeInfo Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Unexpected token type for NodeUri");
        if (NodeInfo.TryParse(reader.GetString(), out var info))
            return info;
        throw new JsonException("Invalid NodeUri");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, NodeInfo value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
