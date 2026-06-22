using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace WebhookService.Models;

public class WebhookEvent
{
    [Required]
    [JsonPropertyName("eventId")]
    public string EventId { get; set; } = string.Empty;

    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = string.Empty;

    [JsonPropertyName("payloadUrl")]
    public string? PayloadUrl { get; set; }

    [JsonIgnore]
    public int RetryCount { get; set; } = 0;
}

