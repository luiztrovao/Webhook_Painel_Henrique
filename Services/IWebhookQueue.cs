using WebhookService.Models;

namespace WebhookService.Services;

public interface IWebhookQueue
{
    ValueTask EnqueueNotificationAsync(WebhookEvent notification, CancellationToken cancellationToken = default);
    ValueTask<WebhookEvent> DequeueNotificationAsync(CancellationToken cancellationToken = default);
}
