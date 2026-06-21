using WebhookService.Models;

namespace WebhookService.Clients;

public interface ISalesforceClient
{
    Task UpsertPanelAsync(SalesforcePainelHenriqueDto dto, CancellationToken cancellationToken = default);
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}
