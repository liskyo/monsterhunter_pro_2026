using System;
using System.Globalization;
using Newtonsoft.Json;

namespace MonsterHunter.DataModels
{
    /// <summary>設計資料有時寫成整數、有時寫成 10030.0；可安全讀成 int。</summary>
    public sealed class JsonIntFromNumberConverter : JsonConverter<int>
    {
        public override void WriteJson(JsonWriter writer, int value, JsonSerializer serializer) =>
            writer.WriteValue(value);

        public override int ReadJson(
            JsonReader reader,
            Type objectType,
            int existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            switch (reader.TokenType)
            {
                case JsonToken.Null:
                    return 0;
                case JsonToken.Integer:
                    return Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture);
                case JsonToken.Float:
                    return (int)Math.Round(Convert.ToDouble(reader.Value, CultureInfo.InvariantCulture));
                case JsonToken.String when reader.Value is string s &&
                                          int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var i):
                    return i;
                default:
                    throw new JsonSerializationException(
                        $"Cannot convert token {reader.TokenType} to int (path: {reader.Path})");
            }
        }
    }
}
