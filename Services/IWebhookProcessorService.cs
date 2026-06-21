using WebhookService.Models;

namespace WebhookService.Services;

public interface IWebhookProcessorService
{
    Task ProcessNotificationAsync(WebhookEvent webhookEvent, CancellationToken cancellationToken = default);
}
