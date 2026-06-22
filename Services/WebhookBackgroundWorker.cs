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
                if (webhookEvent != null && !stoppingToken.IsCancellationRequested)
                {
                    webhookEvent.RetryCount++;

                    if (webhookEvent.RetryCount > 3)
                    {
                        FileLogger.LogError($"DEAD LETTER: Event {webhookEvent.EventId} failed permanently after 3 retries.", ex);
                    }
                    else
                    {
                        FileLogger.LogError($"Error processing event {webhookEvent.EventId}. Attempt {webhookEvent.RetryCount}/3. Re-queueing...", ex);
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
                else
                {
                    FileLogger.LogError("Unexpected error processing event in background.", ex);
                }
            }
        }

        FileLogger.LogInfo("Webhook Background Processor finished safely.");
    }
}
