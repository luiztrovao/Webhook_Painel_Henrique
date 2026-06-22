using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using WebhookService.Clients;
using WebhookService.Services;
using Polly;
using Polly.Extensions.Http;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("WebhookPolicy", opt =>
    {
        opt.PermitLimit = 50;
        opt.Window = TimeSpan.FromSeconds(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 10;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<WebhookService.Filters.ApiKeyAuthAttribute>();

builder.Services.AddSingleton<IWebhookQueue, WebhookQueue>();

var retryPolicy = HttpPolicyExtensions
    .HandleTransientHttpError() 
    .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests) 
    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

builder.Services.AddHttpClient<ISalesforceClient, SalesforceClient>()
    .AddPolicyHandler(retryPolicy);

builder.Services.AddHttpClient<IConsoleClient, ConsoleClient>()
    .AddPolicyHandler(retryPolicy);

builder.Services.AddScoped<IWebhookProcessorService, WebhookProcessorService>();

builder.Services.AddHostedService<WebhookBackgroundWorker>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseRateLimiter();

app.MapControllers();

app.Run();
