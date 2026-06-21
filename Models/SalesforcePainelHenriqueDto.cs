using System.Text.Json.Serialization;

namespace WebhookService.Models;

public class SalesforcePainelHenriqueDto
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("ID_da_Empresa__c")]
    public long IdDaEmpresa { get; set; }

    [JsonPropertyName("CNPJ__c")]
    public string Cnpj { get; set; } = string.Empty;

    [JsonPropertyName("Nome_Fantasia__c")]
    public string NomeFantasia { get; set; } = string.Empty;

    [JsonPropertyName("Razao_Social__c")]
    public string RazaoSocial { get; set; } = string.Empty;

    [JsonPropertyName("Plano_Atual__c")]
    public string PlanoAtual { get; set; } = string.Empty;

    [JsonPropertyName("Valor_Atual__c")]
    public decimal ValorAtual { get; set; }

    [JsonPropertyName("Plano_Novo__c")]
    public string PlanoNovo { get; set; } = string.Empty;

    [JsonPropertyName("Novo_MRR__c")]
    public decimal NovoMrr { get; set; }
}
