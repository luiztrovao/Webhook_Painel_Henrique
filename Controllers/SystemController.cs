using Microsoft.AspNetCore.Mvc;
using WebhookService.Clients;

namespace WebhookService.Controllers;

/// <summary>
/// Controlador de diagnóstico para verificar a saúde do sistema e das integrações.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SystemController : ControllerBase
{
    private readonly ISalesforceClient _salesforceClient;

    public SystemController(ISalesforceClient salesforceClient)
    {
        _salesforceClient = salesforceClient;
    }

    /// <summary>
    /// Testa se a aplicação consegue se conectar ao Salesforce usando as Variáveis de Ambiente.
    /// Não exige payload de webhook, serve apenas para validação técnica.
    /// </summary>
    [HttpGet("salesforce-status")]
    public async Task<IActionResult> CheckSalesforceConnection()
    {
        try
        {
            var isConnected = await _salesforceClient.TestConnectionAsync();

            if (isConnected)
            {
                return Ok(new { 
                    Status = "Online", 
                    Message = "Conexão com o Salesforce estabelecida com sucesso usando as Variáveis de Ambiente configuradas!" 
                });
            }

            return StatusCode(500, new { Status = "Offline", Message = "Falha desconhecida" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { 
                Status = "Offline", 
                Message = "O Salesforce recusou a conexão. Veja o erro exato abaixo:",
                SalesforceError = ex.Message
            });
        }
    }

    /// <summary>
    /// Valida se as Variáveis de Ambiente foram carregadas corretamente pelo .NET.
    /// Retorna de forma segura (parcialmente oculta) o tamanho e início das chaves.
    /// </summary>
    [HttpGet("config-check")]
    public IActionResult CheckConfig([FromServices] IConfiguration config)
    {
        var clientId = config["Salesforce:ClientId"];
        var clientSecret = config["Salesforce:ClientSecret"];
        
        return Ok(new {
            Status = "Diagnóstico de Configuração",
            ClientId = new {
                Preenchido = !string.IsNullOrEmpty(clientId),
                Tamanho = clientId?.Length ?? 0,
                Inicio = !string.IsNullOrEmpty(clientId) && clientId.Length >= 4 ? clientId.Substring(0, 4) + "..." : "vazio"
            },
            ClientSecret = new {
                Preenchido = !string.IsNullOrEmpty(clientSecret),
                Tamanho = clientSecret?.Length ?? 0,
                Inicio = !string.IsNullOrEmpty(clientSecret) && clientSecret.Length >= 4 ? clientSecret.Substring(0, 4) + "..." : "vazio"
            }
        });
    }
}
