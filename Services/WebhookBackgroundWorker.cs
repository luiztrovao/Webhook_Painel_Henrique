using WebhookService.Clients;
using WebhookService.Models;

namespace WebhookService.Services;

public class WebhookBackgroundWorker : BackgroundService
{
    private readonly IWebhookQueue _queue;
    private readonly IServiceProvider _serviceProvider;

    public WebhookBackgroundWorker(IWebhookQueue queue, IServiceProvider serviceProvider)
    {
        _queue = queue;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ProcessQueueLoopAsync(stoppingToken);
    }

    private async Task ProcessQueueLoopAsync(CancellationToken stoppingToken)
    {
        FileLogger.LogInfo("Webhook Background Processor started and waiting for items in queue...");

        while (!stoppingToken.IsCancellationRequested)
        {
            WebhookEvent? webhookEvent = null;
            try
            {
                webhookEvent = await _queue.DequeueNotificationAsync(stoppingToken);

                FileLogger.LogInfo($"Processing background event {webhookEvent.EventId}.");

                using var scope = _serviceProvider.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IWebhookProcessorService>();

                await processor.ProcessNotificationAsync(webhookEvent, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                FileLogger.LogError("Unexpected error processing event in background. Attempting to return to queue...", ex);
                
                if (webhookEvent != null && !stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        await _queue.EnqueueNotificationAsync(webhookEvent, stoppingToken);
                        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    }
                    catch (Exception retryEx)
                    {
                        FileLogger.LogError("Error trying to re-enqueue event.", retryEx);
                    }
                }
            }
        }

        FileLogger.LogInfo("Webhook Background Processor finished safely.");
    }
}
