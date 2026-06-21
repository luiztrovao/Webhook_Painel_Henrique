using WebhookService.Models;

namespace WebhookService.Clients;

public interface IConsoleClient
{
    Task<WebhookNotification?> GetPayloadAsync(string eventId, CancellationToken cancellationToken = default);
}
