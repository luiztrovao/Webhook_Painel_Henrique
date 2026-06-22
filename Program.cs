using WebhookService.Clients;
using WebhookService.Services;
using Polly;
using Polly.Extensions.Http;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<WebhookService.Filters.ApiKeyAuthAttribute>();

builder.Services.AddSingleton<IWebhookQueue, WebhookQueue>();

builder.Services.AddHttpClient<ISalesforceClient, SalesforceClient>();

builder.Services.AddHttpClient<IConsoleClient, ConsoleClient>()
    .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientHttpError() 
        .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests) 
        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

builder.Services.AddScoped<IWebhookProcessorService, WebhookProcessorService>();

builder.Services.AddHostedService<WebhookBackgroundWorker>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
