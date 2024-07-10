using System;
using System.Text.Json;
using LNURL.Requests;

namespace LNURL.Json.SystemJson;

public class LNURLPayRequestSuccessActionJsonConverter : System.Text.Json.Serialization.JsonConverter<
    LNURLPayRequest.LNURLPayRequestCallbackResponse.ILNURLPayRequestSuccessAction>
{
    public override LNURLPayRequest.LNURLPayRequestCallbackResponse.ILNURLPayRequestSuccessAction Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.Null)
            return null;
        if (reader.TokenType is not JsonTokenType.StartObject)
            throw new JsonException("Unexpected token type for LNURLPayRequestSuccessAction");
        JsonElement element = JsonDocument.ParseValue(ref reader).RootElement;
        switch (element.GetProperty("tag").GetString())
        {
            case "message":
                return element
                    .Deserialize<LNURLPayRequest.LNURLPayRequestCallbackResponse.LNURLPayRequestSuccessActionMessage>(
                        options);
            case "url":
                return element
                    .Deserialize<LNURLPayRequest.LNURLPayRequestCallbackResponse.LNURLPayRequestSuccessActionUrl>(
                        options);
            case "aes":
                return element
                    .Deserialize<LNURLPayRequest.LNURLPayRequestCallbackResponse.LNURLPayRequestSuccessActionAES>(
                        options);
            default: throw new JsonException("Invalid LNURLPayRequestSuccessAction");
        }
    }

    public override void Write(Utf8JsonWriter writer,
        LNURLPayRequest.LNURLPayRequestCallbackResponse.ILNURLPayRequestSuccessAction value,
        JsonSerializerOptions options)
    {
        if (value is null)
            writer.WriteNullValue();
        else
        {
            JsonSerializer.Serialize(writer, value);
        }
    }
}