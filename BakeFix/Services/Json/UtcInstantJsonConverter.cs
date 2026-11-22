using System.Text.Json;
using System.Text.Json.Serialization;

namespace BakeFix.Services.Json
{
    // Marks a property as a real moment in time (stored UTC by convention, Kind=Unspecified
    // after Dapper materializes it from a `timestamp without time zone` column). Forces the
    // wire representation to carry "Z" so the frontend can distinguish it from a naive
    // calendar-date field, without ever touching the model's actual DateTime.Kind in memory —
    // Npgsql 8 throws writing a Kind=Utc DateTime into `timestamp without time zone`, and every
    // one of these properties is also a live Dapper write parameter, so the Kind on the model
    // instance itself must stay Unspecified. This converter only affects the JSON reader/writer.
    public class UtcInstantJsonConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetDateTime();
            return value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(DateTime.SpecifyKind(value, DateTimeKind.Utc));
        }
    }

    public class NullableUtcInstantJsonConverter : JsonConverter<DateTime?>
    {
        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null) return null;
            var value = reader.GetDateTime();
            return value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (value is null) writer.WriteNullValue();
            else writer.WriteStringValue(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));
        }
    }
}
