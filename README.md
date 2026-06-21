# Webhook_Painel_Henrique

Um microsserviço de integração desenvolvido em **.NET 8** para receber eventos de Webhook do Console SIEG e sincronizar as atualizações de planos de clientes com o Salesforce CRM.

## 📌 O que o projeto faz?

Quando um cliente altera seu plano ou assinatura, a plataforma dispara um evento. Este serviço atua como uma ponte:
1. **Recebe o Webhook**: Capta instantaneamente o identificador da mudança (`EventId`).
2. **Processamento em Fila**: Coloca o evento em uma fila de processamento em segundo plano (`BackgroundWorker`), retornando uma resposta imediata para quem enviou, evitando lentidão.
3. **Busca Dados Completos**: Consulta a API do Console para obter os detalhes completos da mudança (novo MRR, qual o novo plano, CNPJ, etc.).
4. **Atualiza o Salesforce**: Faz um *Upsert* (cria ou atualiza) no painel de contratos dentro do Salesforce usando o CNPJ como chave, garantindo que o time comercial e financeiro veja a mudança em tempo real.

## 🏗️ Arquitetura

O projeto é dividido em camadas simples e claras:
- **`Controllers`**: Ponto de entrada (`WebhookController`). Apenas recebe e joga na fila.
- **`Models`**: Estruturas de dados (JSON) transacionadas entre os sistemas.
- **`Services`**: A regra de negócio. Contém a fila em memória (`WebhookQueue`), o Worker rodando em segundo plano (`WebhookBackgroundWorker`) e a lógica principal (`WebhookProcessorService`). Possui também o `FileLogger` que grava logs de erro e execução em arquivos locais.
- **`Clients`**: Comunicação HTTP com serviços externos (`ConsoleClient` e `SalesforceClient`), usando políticas de resiliência e retentativas para evitar falhas de comunicação.

## 🚀 Como Executar

### 1. Configurar Variáveis
As senhas e URLs não ficam no código por segurança. Você precisará configurar o arquivo `appsettings.json` com suas credenciais:

```json
"Salesforce": {
  "LoginUrl": "https://sua-url-do-salesforce.com",
  "ClientId": "SEU_CLIENT_ID",
  "ClientSecret": "SEU_CLIENT_SECRET",
  "DryRun": false
},
"ConsoleApi": {
  "BaseUrl": "https://api.console.com",
  "ApiKey": "SUA_API_KEY"
}
```

> **Dica**: Ativar `"DryRun": true` permite testar o fluxo sem enviar dados reais ao Salesforce.

### 2. Rodar a Aplicação
Com o SDK do .NET 8 instalado:
```bash
dotnet restore
dotnet run
```
A API ficará disponível e ouvindo requisições na porta local, com o Swagger habilitado no ambiente de desenvolvimento para facilitar testes manuais.

## 🛡️ Tratamento de Erros e Logs
Toda a aplicação é coberta por blocos `try-catch`. Quando um erro inesperado ocorre, ele não é exibido no console do sistema, mas sim registrado silenciosamente em um arquivo gerado automaticamente na pasta `/logs/webhook-log.txt`. Eventos que falham no processamento de rede são devolvidos para a fila para retentativas.
