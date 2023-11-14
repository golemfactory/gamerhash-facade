using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace Golem.Tools
{
    public class DateTimeJsonConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
        {
            Debug.Assert(typeToConvert == typeof(DateTime));
            var token = reader.GetString();
            var result = DateTime.Parse(token ?? string.Empty);
            return result;
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString("yyyy-MM-ddTHH:mm:sszz"));
        }
    }
}
