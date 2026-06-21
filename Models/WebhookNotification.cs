namespace WebhookService.Models;

public class WebhookNotification
{
    public string IdDaEmpresa { get; set; } = string.Empty;
    public string Cnpj { get; set; } = string.Empty;
    public string NomeFantasia { get; set; } = string.Empty;
    public string RazaoSocial { get; set; } = string.Empty;
    public string PlanoAtual { get; set; } = string.Empty;
    public decimal ValorAtual { get; set; }
    public string PlanoNovo { get; set; } = string.Empty;
    public decimal MrrNovo { get; set; }
}
