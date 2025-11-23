# WorkflowIntegration Bounded Context

## Responsabilità

Gestisce l'integrazione con n8n workflows per automazione, error logging e orchestrazione di processi esterni.

## Funzionalità Principali

- **Workflow Orchestration**: Integrazione con n8n per automation
- **Error Logging**: Tracciamento errori nei workflow
- **Webhook Management**: Gestione webhook per trigger n8n
- **Configuration Management**: Configurazione dinamica dei workflow
- **Execution Monitoring**: Monitoraggio esecuzioni workflow

## Struttura

### Domain/
Logica di business pura e modelli di dominio:
- **Entities/**: Aggregates principali (N8nConfig, WorkflowExecution, WorkflowError)
- **ValueObjects/**: Oggetti valore immutabili (WorkflowId, WebhookUrl, ExecutionStatus)
- **Services/**: Domain services per logica complessa
- **Events/**: Domain events (WorkflowStarted, WorkflowCompleted, WorkflowFailed, etc.)

### Application/
Orchestrazione e casi d'uso (CQRS pattern con MediatR):
- **Commands/**: Operazioni di scrittura
  - ConfigureWorkflow
  - TriggerWorkflow
  - UpdateWorkflowConfig
  - DeleteWorkflowConfig
  - LogWorkflowError
- **Queries/**: Operazioni di lettura
  - GetWorkflowConfig
  - GetAllWorkflowConfigs
  - GetWorkflowExecutions
  - GetWorkflowErrors
- **DTOs/**: Data Transfer Objects per le risposte
- **Validators/**: FluentValidation validators per validare i comandi
- **EventHandlers/**: Gestori di domain events
- **Interfaces/**: Contratti per repositories e servizi (IN8nService, IWorkflowRepository)
- **Services/**: Application services per orchestrazione

### Infrastructure/
Implementazioni concrete e adattatori:
- **Repositories/**: Implementazioni EF Core (N8nConfigRepository, WorkflowExecutionRepository)
- **Services/**: Implementazioni concrete di servizi esterni
  - N8nHttpClient: Client HTTP per n8n API
  - WebhookService: Gestione webhook endpoints
- **Adapters/**: Adattatori per servizi di terze parti

## Pattern Architetturali

- **CQRS**: Separazione tra Commands (scrittura) e Queries (lettura)
- **MediatR**: Tutti gli endpoint HTTP usano `IMediator.Send()` per invocare handlers
- **Domain-Driven Design**: Aggregates, Value Objects, Domain Events
- **Repository Pattern**: Astrazione dell'accesso ai dati

## n8n Integration

### Workflow Types
- **Document Processing**: Trigger su PDF upload completato
- **Notification**: Invio notifiche email/Slack/Discord
- **Data Sync**: Sincronizzazione dati con sistemi esterni
- **Backup**: Backup automatici database e file
- **Analytics**: Generazione report periodici

### Webhook Flow
```
External Event (PDF uploaded, User registered, etc.)
    ↓
Domain Event Published
    ↓
Event Handler (WorkflowIntegration context)
    ↓
TriggerWorkflowCommand
    ↓
N8nHttpClient (POST to webhook URL)
    ↓
n8n Workflow Execution
    ↓
Callback Webhook (optional)
    ↓
WorkflowCompletedEvent
```

## API Endpoints

```
GET    /api/v1/workflows/config            → GetAllWorkflowConfigsQuery
GET    /api/v1/workflows/config/{id}       → GetWorkflowConfigQuery
POST   /api/v1/workflows/config            → ConfigureWorkflowCommand
PUT    /api/v1/workflows/config/{id}       → UpdateWorkflowConfigCommand
DELETE /api/v1/workflows/config/{id}       → DeleteWorkflowConfigCommand
POST   /api/v1/workflows/trigger/{id}      → TriggerWorkflowCommand
GET    /api/v1/workflows/executions        → GetWorkflowExecutionsQuery
GET    /api/v1/workflows/errors            → GetWorkflowErrorsQuery
POST   /api/v1/webhooks/n8n                → HandleN8nWebhookCommand (callback)
```

## Database Entities

Vedi `Infrastructure/Entities/WorkflowIntegration/`:
- `N8nConfig`: Configurazione workflow n8n
- `WorkflowExecution`: Esecuzione workflow con status
- `WorkflowError`: Errori durante l'esecuzione

## Configuration

```json
{
  "N8n": {
    "BaseUrl": "http://n8n:5678",
    "ApiKey": "${N8N_API_KEY}",  // Stored in secrets
    "Webhooks": {
      "PdfProcessed": "http://n8n:5678/webhook/pdf-processed",
      "UserRegistered": "http://n8n:5678/webhook/user-registered",
      "AlertTriggered": "http://n8n:5678/webhook/alert-triggered"
    },
    "Timeout": {
      "Seconds": 30
    },
    "Retry": {
      "MaxAttempts": 3,
      "DelaySeconds": 2
    }
  }
}
```

## Domain Events Integration

Il context si iscrive ai seguenti domain events da altri context:

- **DocumentProcessing.PdfProcessed** → Trigger PDF processing workflow
- **Authentication.UserRegistered** → Trigger welcome email workflow
- **Administration.AlertTriggered** → Trigger alert notification workflow
- **KnowledgeBase.LowConfidenceAnswer** → Trigger quality review workflow

## Error Handling

### Retry Policy
- **Max Attempts**: 3
- **Delay**: 2s, 4s, 8s (exponential backoff)
- **Timeout**: 30s per attempt
- **Circuit Breaker**: Open dopo 5 failures consecutivi (60s cooldown)

### Error Logging
Tutti gli errori sono loggati in `WorkflowError` entity:
- Timestamp
- Workflow ID
- Error message
- Stack trace
- Request payload
- Response status

## Testing

- Unit tests per domain logic
- Integration tests con Testcontainers (PostgreSQL)
- Mock n8n service per test isolati
- E2E tests con n8n dockerized
- Test coverage: >90%

## Monitoring

### Metrics
- Workflow executions count
- Success/failure rate
- Average execution time
- Error rate by workflow type

### Observability
- **Logs**: Serilog → Seq
- **Traces**: OpenTelemetry → Jaeger (W3C trace context propagation)
- **Metrics**: Prometheus `/metrics`
- **Alerts**: AlertManager (se failure rate >10%)

## Dipendenze

- **EF Core**: Persistence
- **MediatR**: CQRS orchestration
- **FluentValidation**: Input validation
- **Polly**: Retry policies e circuit breaker
- **HttpClientFactory**: HTTP clients con connection pooling

## Security

- **API Key Storage**: Encrypted con ASP.NET Core Data Protection
- **Webhook Validation**: HMAC signature verification
- **HTTPS Only**: TLS 1.2+ required per n8n communication
- **Secrets Management**: Azure Key Vault o Docker secrets

## n8n Setup

### Docker Compose
```yaml
services:
  n8n:
    image: n8nio/n8n:latest
    ports:
      - "5678:5678"
    environment:
      - N8N_BASIC_AUTH_ACTIVE=true
      - N8N_BASIC_AUTH_USER=admin
      - N8N_BASIC_AUTH_PASSWORD=${N8N_PASSWORD}
      - N8N_WEBHOOK_URL=http://localhost:5678/
    volumes:
      - n8n_data:/home/node/.n8n
```

### Example Workflow
1. Create workflow in n8n UI
2. Add webhook trigger node
3. Copy webhook URL
4. Configure in MeepleAI:
   ```bash
   POST /api/v1/workflows/config
   {
     "name": "PDF Processing Complete",
     "webhookUrl": "http://n8n:5678/webhook/pdf-processed",
     "enabled": true,
     "triggerEvent": "DocumentProcessing.PdfProcessed"
   }
   ```

## Note di Migrazione

Questo context è stato completamente migrato alla nuova architettura DDD/CQRS. Il workflow orchestration è ora gestito tramite handlers specializzati con proper error handling e retry policies.

## Related Documentation

- `docs/02-development/n8n-integration.md`
- `docs/01-architecture/overview/system-architecture.md` (Workflow section)
- n8n official docs: https://docs.n8n.io
