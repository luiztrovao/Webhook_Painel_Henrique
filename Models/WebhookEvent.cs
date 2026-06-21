using System.Text.Json.Serialization;

namespace WebhookService.Models;

public class WebhookEvent
{
    [JsonPropertyName("eventId")]
    public string EventId { get; set; } = string.Empty;

    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = string.Empty;

    [JsonPropertyName("payloadUrl")]
    public string? PayloadUrl { get; set; }
}
