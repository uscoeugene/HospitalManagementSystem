using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HMS.API.Json
{
    public class DateOnlyJsonConverter : JsonConverter<DateOnly>
    {
        private const string Format = "yyyy-MM-dd";

        public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var s = reader.GetString();
                if (DateOnly.TryParse(s, out var d)) return d;
                throw new JsonException($"Invalid DateOnly value: {s}");
            }

            if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out var l))
            {
                // treat as days since 0001-01-01 (not commonly used) fallback
                return DateOnly.FromDateTime(DateTime.FromOADate(l));
            }

            throw new JsonException();
        }

        public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString(Format));
        }
    }

    public class NullableDateOnlyJsonConverter : JsonConverter<DateOnly?>
    {
        private readonly DateOnlyJsonConverter _inner = new DateOnlyJsonConverter();

        public override DateOnly? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null) return null;
            return _inner.Read(ref reader, typeof(DateOnly), options);
        }

        public override void Write(Utf8JsonWriter writer, DateOnly? value, JsonSerializerOptions options)
        {
            if (value.HasValue) _inner.Write(writer, value.Value, options);
            else writer.WriteNullValue();
        }
    }
}
