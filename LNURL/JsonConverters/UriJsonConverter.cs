using System;
using System.Reflection;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;

namespace LNURL.JsonConverters;

public class UriJsonConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return typeof(Uri).GetTypeInfo().IsAssignableFrom(objectType.GetTypeInfo());
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
        JsonSerializer serializer)
    {
        try
        {
            var res = reader.TokenType == JsonToken.Null ? null :
                Uri.TryCreate((string) reader.Value, UriKind.Absolute, out var result) ? result :
                throw new JsonObjectException("Invalid Uri value", reader);
            if (objectType == typeof(string))
            {
                return res?.ToString();
            }

            return res;
        }
        catch (InvalidCastException)
        {
            throw new JsonObjectException("Invalid Uri value", reader);
        }
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        switch (value)
        {
            case null:
                return;
            case string s:
                writer.WriteValue(s);
                break;
            case Uri uri:
                writer.WriteValue(uri.IsAbsoluteUri? uri.AbsoluteUri : uri.ToString());
                break;
        }
    }
}