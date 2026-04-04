using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace The_Long_Dark_Save_Editor_2.Serialization
{
    public class PreservedNumberJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(PreservedNumber);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return default(PreservedNumber);

            return PreservedNumber.FromToken(JToken.Load(reader));
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var number = (PreservedNumber)value;
            var jsonValue = number.ToJsonValue();

            if (jsonValue is long longValue)
                writer.WriteValue(longValue);
            else if (jsonValue is decimal decimalValue)
                writer.WriteValue(decimalValue);
            else
                writer.WriteNull();
        }
    }
}
