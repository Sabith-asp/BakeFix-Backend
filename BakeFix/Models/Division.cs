using System.Text.Json.Serialization;
using BakeFix.Services.Json;

namespace BakeFix.Models
{
    public class Division
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public string Name { get; set; } = string.Empty;

        [JsonConverter(typeof(UtcInstantJsonConverter))]
        public DateTime CreatedAt { get; set; }
    }

    public class DivisionFormData
    {
        public string Name { get; set; } = string.Empty;
    }
}
