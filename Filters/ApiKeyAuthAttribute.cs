using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using WebhookService.Services;

namespace WebhookService.Filters;

public class ApiKeyAuthAttribute : IAsyncActionFilter
{
    private const string ApiKeyHeaderName = "X-Api-Key";
    private readonly IConfiguration _configuration;

    public ApiKeyAuthAttribute(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue(ApiKeyHeaderName, out var extractedApiKey))
        {
            FileLogger.LogInfo("Access blocked: X-Api-Key header not provided.");
            context.Result = new UnauthorizedObjectResult(new { Error = "API Key is missing." });
            return;
        }

        var configuredApiKey = _configuration["WebhookApiKey"];

        if (string.IsNullOrEmpty(configuredApiKey))
        {
            FileLogger.LogInfo("Access blocked: Server API Key not configured.");
            context.Result = new UnauthorizedObjectResult(new { Error = "Server Configuration Error." });
            return;
        }

        var configuredBytes = Encoding.UTF8.GetBytes(configuredApiKey);
        var extractedBytes = Encoding.UTF8.GetBytes(extractedApiKey.ToString());

        if (configuredBytes.Length != extractedBytes.Length || !CryptographicOperations.FixedTimeEquals(configuredBytes, extractedBytes))
        {
            FileLogger.LogInfo("Access blocked: Invalid API Key provided.");
            context.Result = new UnauthorizedObjectResult(new { Error = "Unauthorized client." });
            return;
        }

        await next();
    }
}
