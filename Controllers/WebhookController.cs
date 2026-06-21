using Microsoft.AspNetCore.Mvc;
using WebhookService.Models;
using WebhookService.Services;

namespace WebhookService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WebhookController : ControllerBase
{
    private readonly IWebhookQueue _queue;

    public WebhookController(IWebhookQueue queue)
    {
        _queue = queue;
    }

    [HttpPost]
    public async Task<IActionResult> ReceiveWebhookAsync([FromBody] WebhookEvent webhookEvent)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(webhookEvent.EventId))
            {
                return BadRequest(new { Error = "EventId is required." });
            }

            FileLogger.LogInfo($"Webhook event received. ID: {webhookEvent.EventId}. Adding to queue...");

            await _queue.EnqueueNotificationAsync(webhookEvent);

            return Accepted(new { Message = "Webhook received and enqueued successfully." });
        }
        catch (Exception ex)
        {
            FileLogger.LogError("Error processing webhook in controller.", ex);
            return StatusCode(500, new { Error = "Internal server error processing webhook." });
        }
    }
}
