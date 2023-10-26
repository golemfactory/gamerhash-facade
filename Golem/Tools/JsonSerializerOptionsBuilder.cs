using System.Text.Json;
using System.Text.Json.Serialization;

namespace Golem.Tools
{
    public class JsonSerializerOptionsBuilder
    {
        private JsonNamingPolicy JsonNamingPolicy { get; set; } = JsonNamingPolicy.CamelCase;
        private bool PropertyNameCaseInsensitive { get; set; } = true;
        private bool WriteIndented { get; set; } = true;
        private List<JsonConverter> JsonConverters { get; set; } = new List<JsonConverter> { new DateTimeJsonConverter() };

        public JsonSerializerOptions Build() { 
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = this.PropertyNameCaseInsensitive,
                PropertyNamingPolicy = this.JsonNamingPolicy,
                WriteIndented = this.WriteIndented,
            };
            this.JsonConverters.ForEach(options.Converters.Add);
            
            return options;
        }

        public JsonSerializerOptionsBuilder WithJsonNamingPolicy(JsonNamingPolicy policy)
        {
            this.JsonNamingPolicy = policy;
            return this;
        }
    }
}
