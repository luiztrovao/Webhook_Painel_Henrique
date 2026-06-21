# Documentação Técnica — WebhookService

> **Projeto:** Integração CRM Interno → Salesforce  
> **Framework:** ASP.NET Core 8.0 (Web API)  
> **Última atualização:** 18/06/2026  
> **Responsável:** Luiz Victor

---

## Sumário

1. [Visão Geral](#visão-geral)
2. [Arquitetura do Sistema](#arquitetura-do-sistema)
3. [Estrutura de Pastas e Arquivos](#estrutura-de-pastas-e-arquivos)
4. [Fluxo Completo de Dados](#fluxo-completo-de-dados)
5. [Detalhamento dos Componentes](#detalhamento-dos-componentes)
6. [Configuração e Variáveis de Ambiente](#configuração-e-variáveis-de-ambiente)
7. [Endpoints da API](#endpoints-da-api)
8. [Regras de Negócio](#regras-de-negócio)
9. [Objeto Salesforce (Painel_Henrique__c)](#objeto-salesforce-painel_henrique__c)
10. [Como Rodar Localmente](#como-rodar-localmente)
11. [Deploy em Produção](#deploy-em-produção)
12. [Troubleshooting (Resolução de Problemas)](#troubleshooting)
13. [Decisões Técnicas e Justificativas](#decisões-técnicas-e-justificativas)

---

## Visão Geral

Este sistema é uma **API de Webhook** que atua como ponte entre um **CRM interno (SIEG)** e o **Salesforce**. Quando o CRM dispara um evento (ex: empresa mudou de plano, atualizou faturamento), ele envia um payload HTTP para esta API, que processa os dados e os sincroniza com o Salesforce.

**Principais características:**
- Processamento **assíncrono** via fila em memória (não bloqueia o emissor do webhook)
- Autenticação no Salesforce via **Client Credentials Flow** (OAuth 2.0 máquina-para-máquina)
- **Sem autenticação no endpoint do webhook** (uso interno, rede privada)
- Arquitetura **modular** com separação clara de responsabilidades

---

## Arquitetura do Sistema

```
┌─────────────────┐     POST /api/Webhook     ┌──────────────────────┐
│                 │ ─────────────────────────► │  WebhookController   │
│   CRM Interno   │     (payload JSON)         │  (Recebe + Valida)   │
│     (SIEG)      │ ◄───────────────────────── │  Retorna 202         │
└─────────────────┘     Resposta imediata      └──────────┬───────────┘
                                                          │
                                                          │ Enfileira
                                                          ▼
                                               ┌──────────────────────┐
                                               │    WebhookQueue      │
                                               │  (Fila em Memória)   │
                                               │  Channel<T> - 10k   │
                                               └──────────┬───────────┘
                                                          │
                                                          │ Consome (loop infinito)
                                                          ▼
                                               ┌──────────────────────┐
                                               │ WebhookSegundoPlano  │
                                               │  (Background Worker) │
                                               └──────────┬───────────┘
                                                          │
                                                          │ Delega
                                                          ▼
                                               ┌──────────────────────┐
                                               │ WebhookProcessor     │
                                               │     Service          │
                                               │  (Regras de Negócio) │
                                               └──────────┬───────────┘
                                                          │
                                                          │ Chama API REST
                                                          ▼
                                               ┌──────────────────────┐
                                               │   SalesforceClient   │
                                               │  (OAuth + SOQL +     │
                                               │   Upsert via PATCH)  │
                                               └──────────┬───────────┘
                                                          │
                                                          │ HTTPS
                                                          ▼
                                               ┌──────────────────────┐
                                               │     Salesforce       │
                                               │  Painel_Henrique__c  │
                                               └──────────────────────┘
```

---

## Estrutura de Pastas e Arquivos

```
Webhook/
├── Controllers/
│   ├── WebhookController.cs        # Endpoint principal de recebimento de webhooks
│   └── SystemController.cs         # Endpoints de diagnóstico e health check
│
├── Services/
│   ├── IWebhookQueue.cs            # Interface da fila
│   ├── WebhookQueue.cs             # Implementação da fila (Channel<T>)
│   ├── IWebhookProcessorService.cs # Interface do processador de regras
│   ├── WebhookProcessorService.cs  # Regras de negócio (limpeza CNPJ, plano legado, etc.)
│   └── WebhookSegundoPlano.cs      # Background Worker (consumidor da fila)
│
├── Clients/
│   ├── ISalesforceClient.cs        # Interface do cliente Salesforce
│   └── SalesforceClient.cs         # Implementação: OAuth, SOQL query e Upsert
│
├── Models/
│   ├── WebhookNotification.cs      # Modelo do payload recebido do CRM
│   └── SalesforcePainelHenriqueDto.cs  # DTO mapeado para o objeto Salesforce
│
├── Program.cs                      # Ponto de entrada e configuração do DI Container
├── appsettings.json                # Configurações gerais (LoginUrl do Salesforce)
├── appsettings.Development.json    # Configurações do ambiente de desenvolvimento
└── DOCUMENTACAO_TECNICA.md         # Este documento
```

---

## Fluxo Completo de Dados

### Passo a Passo

| Etapa | O que acontece | Arquivo responsável |
|-------|---------------|---------------------|
| 1 | CRM envia um `POST` com JSON para `/api/Webhook` | `WebhookController.cs` |
| 2 | Controller valida se o CNPJ está presente | `WebhookController.cs` |
| 3 | Notificação é adicionada na fila em memória | `WebhookQueue.cs` |
| 4 | Controller retorna `202 Accepted` imediatamente | `WebhookController.cs` |
| 5 | Worker (loop em background) retira o item da fila | `WebhookSegundoPlano.cs` |
| 6 | Worker delega processamento para o serviço de regras | `WebhookProcessorService.cs` |
| 7 | Serviço limpa o CNPJ (remove pontuação) | `WebhookProcessorService.cs` |
| 8 | Serviço consulta o plano atual no Salesforce via SOQL | `SalesforceClient.cs` |
| 9 | Serviço aplica a regra do "Plano Legado" | `WebhookProcessorService.cs` |
| 10 | Serviço monta o DTO com os campos do Salesforce | `WebhookProcessorService.cs` |
| 11 | DTO é enviado via PATCH (Upsert) para o Salesforce | `SalesforceClient.cs` |

---

## Detalhamento dos Componentes

### WebhookController

**Arquivo:** `Controllers/WebhookController.cs`  
**Rota:** `POST /api/Webhook`  
**Responsabilidade:** Receber o payload e enfileirar. Não contém lógica de negócio.

**Comportamento:**
- Recebe um JSON no corpo da requisição e o desserializa em `WebhookNotification`
- Se o campo `Cnpj` estiver vazio → retorna `400 Bad Request`
- Se válido → enfileira na `IWebhookQueue` e retorna `202 Accepted`

---

### SystemController

**Arquivo:** `Controllers/SystemController.cs`  
**Rotas:** `GET /api/System/salesforce-status` e `GET /api/System/config-check`  
**Responsabilidade:** Diagnóstico e monitoramento do sistema.

**Endpoints:**
- `salesforce-status` — Tenta autenticar no Salesforce e retorna se a conexão está Online ou Offline, incluindo a mensagem de erro exata em caso de falha.
- `config-check` — Exibe de forma segura (mascarada) se as variáveis de ambiente `ClientId` e `ClientSecret` foram carregadas, mostrando apenas os 4 primeiros caracteres.

---

### WebhookQueue

**Arquivo:** `Services/WebhookQueue.cs`  
**Ciclo de vida:** `Singleton` (uma única instância compartilhada)  
**Responsabilidade:** Fila thread-safe em memória usando `System.Threading.Channels`.

**Configuração:**
- Capacidade máxima: **10.000 itens**
- Comportamento quando cheia: `Wait` (novas inserções aguardam até liberar espaço)
- Não persiste dados em disco (se a aplicação reiniciar, a fila é perdida)

---

### WebhookSegundoPlano (Background Worker)

**Arquivo:** `Services/WebhookSegundoPlano.cs`  
**Ciclo de vida:** `HostedService` (iniciado automaticamente com a aplicação)  
**Responsabilidade:** Consumir itens da fila em um loop infinito e delegar ao processador.

**Comportamento de resiliência:**
- Se ocorrer erro no processamento, o item é **devolvido para a fila** para nova tentativa
- Aguarda **5 segundos** antes de reprocessar (evita loop de erro infinito)
- Exceção de cancelamento (`OperationCanceledException`) é tratada para shutdown gracioso

---

### WebhookProcessorService

**Arquivo:** `Services/WebhookProcessorService.cs`  
**Ciclo de vida:** `Scoped` (uma instância por requisição/processamento)  
**Responsabilidade:** Contém toda a lógica de negócio, isolada para facilitar testes unitários.

**Processamento:**
1. Limpa o CNPJ removendo caracteres não numéricos
2. Consulta o plano atual no Salesforce
3. Aplica a regra do "Plano Legado"
4. Monta o DTO `SalesforcePainelHenriqueDto`
5. Executa o Upsert no Salesforce

---

### SalesforceClient

**Arquivo:** `Clients/SalesforceClient.cs`  
**Ciclo de vida:** Gerenciado pelo `IHttpClientFactory` (evita Socket Exhaustion)  
**Responsabilidade:** Comunicação HTTP com a API REST do Salesforce.

**Métodos públicos:**

| Método | Descrição |
|--------|-----------|
| `GetPlanoAtualAsync(cnpj)` | Executa uma query SOQL buscando o campo `Plano_Atual__c` pelo CNPJ |
| `UpsertPainelHenriqueAsync(dto)` | Faz um PATCH (Upsert) no objeto `Painel_Henrique__c` usando `CNPJ__c` como External ID |
| `TestConnectionAsync()` | Força uma nova autenticação para validar se as credenciais estão corretas |

**Autenticação:**
- Fluxo: OAuth 2.0 Client Credentials
- O token é armazenado em memória (`_accessToken`) e reutilizado até expirar
- Se receber `401 Unauthorized`, invalida o token e tenta re-autenticar automaticamente

**Decisão técnica importante:**  
A `_instanceUrl` (URL da instância Salesforce retornada pelo OAuth) é armazenada em variável separada e usada diretamente nas URLs das requisições. **Não** usamos `HttpClient.BaseAddress` porque o .NET proíbe alterá-lo após a primeira requisição enviada.

---

## Configuração e Variáveis de Ambiente

### appsettings.json (valores não sensíveis)

```json
{
  "Salesforce": {
    "LoginUrl": "https://sieg--dados2.sandbox.my.salesforce.com"
  }
}
```

> **Importante:** O `LoginUrl` deve ser o **My Domain** da organização Salesforce, não as URLs genéricas (`login.salesforce.com` ou `test.salesforce.com`).

### Variáveis de Ambiente do Windows (valores sensíveis)

| Variável | Descrição | Exemplo |
|----------|-----------|---------|
| `Salesforce__ClientId` | Consumer Key do Connected App | `3MVG9bjNVlqB8y...` |
| `Salesforce__ClientSecret` | Consumer Secret do Connected App | `69CDC674D8A35D...` |

**Como configurar no Windows (PowerShell como Administrador):**

```powershell
[System.Environment]::SetEnvironmentVariable("Salesforce__ClientId", "SEU_CLIENT_ID", "Machine")
[System.Environment]::SetEnvironmentVariable("Salesforce__ClientSecret", "SEU_CLIENT_SECRET", "Machine")
```

> **Atenção:** Após criar variáveis de ambiente no Windows, é necessário **fechar e reabrir o terminal** para que elas sejam carregadas pelo processo.

### Hierarquia de configuração do .NET

O ASP.NET Core lê configurações nesta ordem de prioridade (do menor para o maior):

1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. **Variáveis de ambiente** ← sobrescrevem os arquivos
4. Argumentos da linha de comando

O separador `__` (duplo underscore) nas variáveis de ambiente é equivalente a `:` nos arquivos JSON. Exemplo: `Salesforce__ClientId` = `Salesforce:ClientId`.

---

## Endpoints da API

### POST /api/Webhook
**Descrição:** Recebe o payload do CRM e enfileira para processamento.

**Request Body:**
```json
{
  "IdDaEmpresa": "12345",
  "Cnpj": "12.345.678/0001-99",
  "NomeFantasia": "Empresa Exemplo",
  "RazaoSocial": "Empresa Exemplo LTDA",
  "PlanoAtual": "Control",
  "ValorAtual": 500.00,
  "PlanoNovo": "Flow",
  "MrrNovo": 750.00
}
```

**Responses:**
| Código | Significado |
|--------|------------|
| `202 Accepted` | Webhook recebido e enfileirado com sucesso |
| `400 Bad Request` | Campo `Cnpj` ausente ou vazio |

---

### GET /api/System/salesforce-status
**Descrição:** Testa a conexão com o Salesforce em tempo real.

**Response (sucesso):**
```json
{
  "status": "Online",
  "message": "Conexão com o Salesforce estabelecida com sucesso..."
}
```

**Response (falha):**
```json
{
  "status": "Offline",
  "message": "O Salesforce recusou a conexão. Veja o erro exato abaixo:",
  "salesforceError": "Erro da Salesforce (Status BadRequest): {\"error\":\"invalid_grant\"...}"
}
```

---

### GET /api/System/config-check
**Descrição:** Verifica se as variáveis de ambiente foram carregadas (sem expor os valores).

**Response:**
```json
{
  "status": "Diagnóstico de Configuração",
  "clientId": {
    "preenchido": true,
    "tamanho": 85,
    "inicio": "3MVG..."
  },
  "clientSecret": {
    "preenchido": true,
    "tamanho": 64,
    "inicio": "69CD..."
  }
}
```

---

## Regras de Negócio

### Regra do Plano Legado

Localização: `WebhookProcessorService.cs`

Quando o sistema recebe uma notificação, ele consulta o plano atual da empresa no Salesforce. A regra aplicada é:

| Condição | Resultado |
|----------|-----------|
| Plano atual é `"Control"`, `"Flow"` ou `"Essential"` | Mantém o plano atual |
| Plano atual é qualquer outro valor (ou vazio) | Define como `"Legado"` |

### Limpeza do CNPJ

O CNPJ recebido do CRM pode vir formatado (ex: `12.345.678/0001-99`). Antes de enviar ao Salesforce, todos os caracteres não numéricos são removidos, resultando em `12345678000199`.

---

## Objeto Salesforce (Painel_Henrique__c)

O objeto customizado no Salesforce e seus campos com os respectivos API Names:

| Campo no DTO | API Name no Salesforce | Tipo | Descrição |
|--------------|----------------------|------|-----------|
| `Name` | `Name` | Texto | Nome do registro (obrigatório) |
| `IdDaEmpresa` | `ID_da_Empresa__c` | Número | ID da empresa no console SIEG |
| `Cnpj` | `CNPJ__c` | Texto | CNPJ (External ID para Upsert) |
| `NomeFantasia` | `Nome_Fantasia__c` | Texto | Nome fantasia da empresa |
| `RazaoSocial` | `Razao_Social__c` | Texto | Razão social |
| `PlanoAtual` | `Plano_Atual__c` | Texto | Plano atual (pode virar "Legado") |
| `ValorAtual` | `Valor_Atual__c` | Decimal | Valor do plano atual |
| `PlanoNovo` | `Plano_Novo__c` | Texto | Novo plano contratado |
| `NovoMrr` | `Novo_MRR__c` | Decimal | Novo MRR (receita recorrente mensal) |

> O campo `CNPJ__c` é usado como **External ID** para operações de Upsert, o que significa que se já existir um registro com aquele CNPJ, ele será atualizado. Caso contrário, um novo registro será criado.

---

## Como Rodar Localmente

### Pré-requisitos
- .NET SDK 8.0 instalado
- Variáveis de ambiente configuradas (`Salesforce__ClientId` e `Salesforce__ClientSecret`)
- Aplicativo Conectado configurado no Salesforce (ver seção de Troubleshooting)

### Comandos

```bash
# Restaurar dependências e compilar
dotnet build

# Rodar a aplicação
dotnet run

# A API estará disponível em http://localhost:5200 (verifique a porta no console)
```

### Validação rápida

1. Acesse `http://localhost:5200/api/System/config-check` — verifica variáveis de ambiente
2. Acesse `http://localhost:5200/api/System/salesforce-status` — verifica conexão com Salesforce
3. Envie um POST para `http://localhost:5200/api/Webhook` com um payload JSON de teste

---

## Deploy em Produção

### Gerar o pacote de publicação

```bash
dotnet publish -c Release -o ./publish
```

### Opções de hospedagem

| Opção | Descrição |
|-------|-----------|
| **IIS (Windows Server)** | Copiar pasta `publish/` para o servidor, configurar site no IIS com o módulo ASP.NET Core |
| **Azure App Service** | Deploy via Visual Studio, CLI (`az webapp deploy`) ou CI/CD |
| **Docker** | Criar `Dockerfile`, gerar imagem e rodar em qualquer orquestrador |

### Checklist de produção

- [ ] Configurar variáveis de ambiente no servidor de produção
- [ ] Alterar `LoginUrl` para o domínio de **produção** (se aplicável)
- [ ] Configurar certificado SSL (HTTPS)
- [ ] Entregar a URL pública (`https://seu-servidor/api/Webhook`) para o CRM
- [ ] Verificar permissões de rede/firewall entre o servidor e o Salesforce

---

## Troubleshooting

### Erros comuns e soluções

| Erro | Causa | Solução |
|------|-------|---------|
| `invalid_grant: request not supported on this domain` | Usando URL genérica (`login.salesforce.com`) ao invés do My Domain | Alterar `LoginUrl` para o My Domain da org (ex: `https://sieg--dados2.sandbox.my.salesforce.com`) |
| `invalid_grant: no client credentials user enabled` | Falta configurar o usuário "Executar como" (Run As) no Connected App | Ir em Setup > App Manager > Gerenciar o app > Editar Políticas > Preencher o campo "Executar como" |
| `invalid_client_id` | ClientId incorreto ou expirado | Verificar o Consumer Key no painel do Connected App no Salesforce |
| `This instance has already started one or more requests` | Bug do HttpClient com BaseAddress | Já corrigido no código atual (usa `_instanceUrl` diretamente) |
| `config-check` retorna `preenchido: false` | Variáveis de ambiente não carregadas | Fechar e reabrir o terminal após criar as variáveis |
| Token expira durante processamento | Token OAuth expirou | O código já trata automaticamente (re-autenticação em caso de 401) |

### Configuração do Connected App no Salesforce

Para que a integração funcione, o aplicativo conectado deve ter:

1. **Fluxo de credenciais do cliente habilitado** (Enable Client Credentials Flow)
2. **Usuário de execução (Run As)** definido — em Sandbox, usar o username com sufixo (ex: `usuario@empresa.com.sandbox`)
3. **Atenuação de IP** como "Relaxar restrições de IP" (para testes locais)
4. **Escopos OAuth:** `api` (Gerenciar dados do usuário por meio de APIs) e `refresh_token, offline_access`

---

## Decisões Técnicas e Justificativas

### Por que fila em memória ao invés de RabbitMQ/Redis?

A fila usa `System.Threading.Channels`, que é nativa do .NET e não requer infraestrutura externa. Para o volume atual de webhooks (estimado em centenas por dia), essa abordagem é suficiente e muito mais simples de manter. **Limitação:** se a aplicação reiniciar, os itens na fila são perdidos. Se isso se tornar um problema, considerar migrar para uma fila persistente (RabbitMQ, Azure Service Bus).

### Por que não há autenticação no endpoint do Webhook?

O webhook é **interno** — trafega dentro da rede privada da empresa (CRM → API). Adicionar JWT ou API Key criaria complexidade desnecessária. Se futuramente a API for exposta à internet, considerar adicionar ao menos uma API Key simples.

### Por que usar IHttpClientFactory?

O .NET tem um problema conhecido chamado **Socket Exhaustion**: criar instâncias de `HttpClient` manualmente pode esgotar as portas de rede do servidor. O `IHttpClientFactory` gerencia o ciclo de vida das conexões de forma segura e eficiente.

### Por que separar WebhookProcessorService do Worker?

O Worker (`WebhookSegundoPlano`) é acoplado ao ciclo de vida do `BackgroundService` e à fila. Extrair as regras de negócio para um serviço separado permite:
- Testar as regras de negócio com testes unitários (sem precisar da fila)
- Reutilizar a lógica em outros contextos (ex: reprocessamento manual)
- Respeitar o princípio de responsabilidade única (SRP)
