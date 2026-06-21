using WebhookService.Clients;
using WebhookService.Models;

namespace WebhookService.Services;

public class WebhookProcessorService : IWebhookProcessorService
{
    private readonly ISalesforceClient _salesforceClient;
    private readonly IConsoleClient _consoleClient;

    public WebhookProcessorService(ISalesforceClient salesforceClient, IConsoleClient consoleClient)
    {
        _salesforceClient = salesforceClient;
        _consoleClient = consoleClient;
    }

    public async Task ProcessNotificationAsync(WebhookEvent webhookEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            var notification = await _consoleClient.GetPayloadAsync(webhookEvent.EventId, cancellationToken);
            if (notification == null)
            {
                FileLogger.LogInfo($"Could not get payload for EventId {webhookEvent.EventId}. Ignoring event.");
                return;
            }

            var cleanCnpj = new string(notification.Cnpj?.Where(char.IsDigit).ToArray() ?? Array.Empty<char>());
            if (string.IsNullOrWhiteSpace(cleanCnpj))
            {
                FileLogger.LogInfo($"CNPJ {notification.Cnpj} is empty or invalid. Ignoring notification.");
                return;
            }

            var dto = new SalesforcePainelHenriqueDto
            {
                Name = string.IsNullOrWhiteSpace(notification.NomeFantasia) ? "Empresa" : notification.NomeFantasia,
                IdDaEmpresa = long.TryParse(notification.IdDaEmpresa, out var idEmpresa) ? idEmpresa : 0,
                Cnpj = cleanCnpj,
                NomeFantasia = notification.NomeFantasia,
                RazaoSocial = notification.RazaoSocial,
                PlanoAtual = notification.PlanoNovo ?? string.Empty,
                ValorAtual = notification.ValorAtual,
                PlanoNovo = notification.PlanoNovo ?? string.Empty,
                NovoMrr = notification.MrrNovo
            };

            FileLogger.LogInfo($"Updating panel for CNPJ {cleanCnpj}. Plan after change: {dto.PlanoAtual}.");

            await _salesforceClient.UpsertPanelAsync(dto, cancellationToken);
        }
        catch (Exception ex)
        {
            FileLogger.LogError($"Error processing notification for EventId: {webhookEvent.EventId}", ex);
            throw; 
        }
    }
}
