using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace The_Long_Dark_Save_Editor_2.Serialization
{
    [JsonConverter(typeof(PreservedNumberJsonConverter))]
    public struct PreservedNumber
    {
        public decimal Value { get; set; }
        public bool PreferIntegerFormat { get; set; }

        public static PreservedNumber FromToken(JToken token)
        {
            return new PreservedNumber
            {
                Value = token.Value<decimal>(),
                PreferIntegerFormat = token.Type == JTokenType.Integer,
            };
        }

        public static PreservedNumber FromDecimal(decimal value, bool preferIntegerFormat = false)
        {
            return new PreservedNumber
            {
                Value = value,
                PreferIntegerFormat = preferIntegerFormat,
            };
        }

        public object ToJsonValue()
        {
            if (PreferIntegerFormat && decimal.Truncate(Value) == Value && Value >= long.MinValue && Value <= long.MaxValue)
                return (long)Value;

            return Value;
        }

        public double ToDouble()
        {
            return (double)Value;
        }
    }
}
