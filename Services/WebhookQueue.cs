using System.Threading.Channels;
using WebhookService.Models;

namespace WebhookService.Services;

public class WebhookQueue : IWebhookQueue
{
    private readonly Channel<WebhookEvent> _queue;

    public WebhookQueue()
    {
        var options = new BoundedChannelOptions(10000)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        
        _queue = Channel.CreateBounded<WebhookEvent>(options);
    }

    public async ValueTask EnqueueNotificationAsync(WebhookEvent notification, CancellationToken cancellationToken = default)
    {
        await _queue.Writer.WriteAsync(notification, cancellationToken);
    }

    public async ValueTask<WebhookEvent> DequeueNotificationAsync(CancellationToken cancellationToken = default)
    {
        return await _queue.Reader.ReadAsync(cancellationToken);
    }
}
