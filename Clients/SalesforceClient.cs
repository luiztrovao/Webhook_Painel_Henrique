using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using WebhookService.Models;
using WebhookService.Services;

namespace WebhookService.Clients;

public class SalesforceClient : ISalesforceClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private string? _accessToken;
    private string? _instanceUrl;

    public SalesforceClient(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _config = config;
    }

    private async Task AuthenticateAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_accessToken)) return;

        FileLogger.LogInfo("Authenticating to Salesforce (Client Credentials Flow)...");

        var clientId = _config["Salesforce:ClientId"];
        var clientSecret = _config["Salesforce:ClientSecret"];
        var loginUrl = _config["Salesforce:LoginUrl"] ?? "https://login.salesforce.com";

        var requestContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", clientId!),
            new KeyValuePair<string, string>("client_secret", clientSecret!)
        });

        try
        {
            var response = await _httpClient.PostAsync($"{loginUrl}/services/oauth2/token", requestContent, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new Exception($"Salesforce Error (Status {response.StatusCode}): {errorBody}");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            
            _accessToken = doc.RootElement.GetProperty("access_token").GetString();
            _instanceUrl = doc.RootElement.GetProperty("instance_url").GetString();
            
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            
            FileLogger.LogInfo("Salesforce authentication successful.");
        }
        catch (Exception ex)
        {
            FileLogger.LogError("Error in Salesforce AuthenticateAsync.", ex);
            throw;
        }
    }

    public async Task UpsertPanelAsync(SalesforcePainelHenriqueDto dto, CancellationToken cancellationToken = default)
    {
        await AuthenticateAsync(cancellationToken);

        FileLogger.LogInfo($"Sending data to Salesforce (Upsert) for CNPJ {dto.Cnpj}");

        var endpoint = $"{_instanceUrl}/services/data/v60.0/sobjects/Painel_Henrique__c/CNPJ__c/{dto.Cnpj}";

        var jsonContent = JsonSerializer.Serialize(dto);
        
        if (bool.TryParse(_config["Salesforce:DryRun"], out var isDryRun) && isDryRun)
        {
            FileLogger.LogInfo($"[DRY RUN ACTIVE] Ignored sending to Salesforce. Payload for {endpoint}:\n{jsonContent}");
            return;
        }

        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PatchAsync(endpoint, content, cancellationToken);
            
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                FileLogger.LogInfo("Token expired in Upsert. Trying to re-authenticate...");
                _accessToken = null;
                await AuthenticateAsync(cancellationToken);
                
                content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                response = await _httpClient.PatchAsync(endpoint, content, cancellationToken);
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorDetails = await response.Content.ReadAsStringAsync(cancellationToken);
                FileLogger.LogError($"Error in Salesforce UPSERT: {errorDetails}");
                response.EnsureSuccessStatusCode(); 
            }

            FileLogger.LogInfo($"UPSERT successfully completed for CNPJ {dto.Cnpj}");
        }
        catch (Exception ex)
        {
            FileLogger.LogError("Exception during UpsertPanelAsync.", ex);
            throw;
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            FileLogger.LogInfo("Starting forced Salesforce connection test...");
            _accessToken = null; 
            await AuthenticateAsync(cancellationToken);
            return !string.IsNullOrEmpty(_accessToken);
        }
        catch (Exception ex)
        {
            FileLogger.LogError("Failed Salesforce connection test. Check credentials.", ex);
            throw; 
        }
    }
}
