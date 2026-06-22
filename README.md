# Webhook_Painel_Henrique

Um microsserviço de integração avançado e resiliente desenvolvido em **.NET 8** para receber eventos de Webhook do Console SIEG e sincronizar as atualizações de planos de clientes com o Salesforce CRM.

## 📌 O que o projeto faz?

Quando um cliente altera seu plano ou assinatura, a plataforma dispara um evento. Este serviço atua como uma ponte assíncrona:
1. **Segurança (API Key)**: Intercepta a requisição e valida a chave `X-Api-Key` com segurança (*Fixed Time* para prevenção de *Timing Attacks*).
2. **Rate Limiting**: Protege o servidor limitando o fluxo para no máximo 50 requisições por segundo.
3. **Recepção Rápida**: Capta o identificador da mudança (`EventId`) validado via anotações (`[Required]`).
4. **Processamento em Fila (Fire-and-Forget)**: Coloca o evento em um `Channel` (fila em memória), retornando `202 Accepted` imediato para quem enviou, evitando gargalos de I/O.
5. **Busca Dados Completos**: O `BackgroundWorker` consulta a API do Console para obter detalhes (novo MRR, CNPJ, etc.).
6. **Atualiza o Salesforce**: Faz um *Upsert* no painel de contratos dentro do Salesforce usando o CNPJ como chave, garantindo sincronia em tempo real.

## 🏗️ Arquitetura e Resiliência

O projeto segue os princípios de *Responsabilidade Única (SOLID)* e foi desenhado para tolerância a falhas:
- **`Controllers`**: Ponto de entrada (`WebhookController`), lida estritamente com roteamento HTTP, validação básica e enfileiramento.
- **`Models`**: Estruturas de dados (JSON) padronizadas.
- **`Services`**: Regra de negócio. Contém a fila assíncrona (`WebhookQueue`), o Worker em segundo plano (`WebhookBackgroundWorker`) com suporte a **Dead Letter Queue (DLQ)** (descarta eventos definitivamente quebrados após 3 tentativas) e o orquestrador `WebhookProcessorService`.
- **`Clients`**: Comunicação HTTP externa. Todos os clientes (`ConsoleClient` e `SalesforceClient`) utilizam o **Polly** (Retry Policy com Exponential Backoff) para resistir a instabilidades de rede e timeouts nas APIs de destino.
- **Log Assíncrono (`Serilog`)**: Captura de falhas espalhada por `try-catch`, escrita rotacionada diariamente em `/logs/webhook-log-yyyyMMdd.txt` utilizando `Serilog` para evitar travamento de I/O (Thread Contention) durante picos de acesso.

## 🚀 Como Executar

### 1. Configurar Variáveis
As credenciais e chaves devem ser injetadas por Variáveis de Ambiente no servidor ou configuradas localmente no `appsettings.json`:

```json
"WebhookApiKey": "SUA_CHAVE_DE_PROTECAO_DO_WEBHOOK",
"Salesforce": {
  "LoginUrl": "https://sua-url-do-salesforce.com",
  "ClientId": "SEU_CLIENT_ID",
  "ClientSecret": "SEU_CLIENT_SECRET",
  "DryRun": false
},
"ConsoleApi": {
  "BaseUrl": "https://api.console.com",
  "ApiKey": "SUA_API_KEY_AQUI"
}
```

> **Dica**: Ativar `"DryRun": true` permite rodar todo o fluxo do sistema localmente sem enviar (alterar) dados reais no Salesforce.

### 2. Rodar a Aplicação
Com o SDK do .NET 8 instalado:
```bash
dotnet restore
dotnet run
```

### 3. Como testar (Payload)
Para simular um envio para o Webhook, dispare uma requisição POST com o cabeçalho de segurança e o formato esperado:

**Headers:**
```http
Content-Type: application/json
X-Api-Key: SUA_CHAVE_DE_PROTECAO_DO_WEBHOOK
```

**Body (JSON):**
```json
{
  "eventId": "12345678-abcd-9012-efgh-345678901234",
  "eventType": "PLAN_UPGRADE",
  "payloadUrl": "https://api.sandbox.console.com/..."
}
```

## 🛡️ Segurança e Proteção de Erros
- **HTTP 401 Unauthorized**: Falta ou erro na `X-Api-Key`.
- **HTTP 429 Too Many Requests**: Limite de 50 requests/segundo ultrapassado.
- **HTTP 400 Bad Request**: Payload sem a propriedade obrigatória `eventId`.
- Todo o ciclo de vida do erro silencioso (falhas no processador de fila) fica resguardado no sistema de arquivos local com StackTraces completas para análise via Serilog.
