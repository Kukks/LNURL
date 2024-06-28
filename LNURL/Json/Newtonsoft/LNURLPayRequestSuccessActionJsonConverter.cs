using System;
using LNURL.Requests;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LNURL.Json.Newtonsoft;

public class  LNURLPayRequestSuccessActionJsonConverter : JsonConverter<LNURLPayRequest.LNURLPayRequestCallbackResponse.ILNURLPayRequestSuccessAction>
{
    public override void WriteJson(JsonWriter writer, LNURLPayRequest.LNURLPayRequestCallbackResponse.ILNURLPayRequestSuccessAction value,
        JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        JObject.FromObject(value).WriteTo(writer);
    }

    public override LNURLPayRequest.LNURLPayRequestCallbackResponse.ILNURLPayRequestSuccessAction ReadJson(JsonReader reader, Type objectType,
        LNURLPayRequest.LNURLPayRequestCallbackResponse.ILNURLPayRequestSuccessAction existingValue,
        bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType is JsonToken.Null) return null;
        var jobj = JObject.Load(reader);
        switch (jobj.GetValue("tag").Value<string>())
        {
            case "message":
                return jobj.ToObject<LNURLPayRequest.LNURLPayRequestCallbackResponse.LNURLPayRequestSuccessActionMessage>();
            case "url":
                return jobj.ToObject<LNURLPayRequest.LNURLPayRequestCallbackResponse.LNURLPayRequestSuccessActionUrl>();
            case "aes":
                return jobj.ToObject<LNURLPayRequest.LNURLPayRequestCallbackResponse.LNURLPayRequestSuccessActionAES>();
        }

        throw new FormatException();
    }
}