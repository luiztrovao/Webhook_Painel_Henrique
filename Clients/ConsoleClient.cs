using System.Net.Http.Headers;
using System.Text.Json;
using WebhookService.Models;
using WebhookService.Services;

namespace WebhookService.Clients;

public class ConsoleClient : IConsoleClient
{
    private readonly HttpClient _httpClient;

    public ConsoleClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        
        var baseUrl = configuration["ConsoleApi:BaseUrl"];
        if (!string.IsNullOrEmpty(baseUrl))
        {
            _httpClient.BaseAddress = new Uri(baseUrl);
        }

        var apiKey = configuration["ConsoleApi:ApiKey"];
        if (!string.IsNullOrEmpty(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }
    }

    public async Task<WebhookNotification?> GetPayloadAsync(string eventId, CancellationToken cancellationToken = default)
    {
        var route = $"/api/events/{eventId}/payload";
        FileLogger.LogInfo($"Fetching payload from Console for event: {eventId} at route {route}");

        if (_httpClient.BaseAddress?.Host == "api.sandbox.console.com")
        {
            FileLogger.LogInfo($"[MOCK ACTIVE] Returning mock payload for event {eventId}.");
            return new WebhookNotification
            {
                IdDaEmpresa = "999",
                Cnpj = "47794973000103", 
                NomeFantasia = "Empresa Mock Teste",
                RazaoSocial = "Empresa Mock Teste LTDA",
                PlanoNovo = "Essential",
                MrrNovo = 150.00m,
                ValorAtual = 100.00m
            };
        }

        try
        {
            var response = await _httpClient.GetAsync(route, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                FileLogger.LogError($"Error fetching payload from Console. Status: {response.StatusCode}, Details: {errorBody}");
                return null; 
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            
            var notification = JsonSerializer.Deserialize<WebhookNotification>(json, options);
            return notification;
        }
        catch (JsonException ex)
        {
            FileLogger.LogError("Error deserializing Console payload.", ex);
            return null;
        }
        catch (Exception ex)
        {
            FileLogger.LogError("Unexpected error in GetPayloadAsync.", ex);
            return null;
        }
    }
}
