# n8n Workflows

## Contenuto

Workflow n8n per automation, notifiche e integrazioni del sistema MeepleAI.

## Struttura

```
n8n/
├── templates/            13 workflow templates (reference)
└── workflows/             1 workflow di produzione
```

## Accesso n8n

- **URL**: http://localhost:5678
- **Autenticazione**: Basic Auth (configurato via env vars)
- **Editor**: Web UI per creare/modificare workflow visualmente

## Templates (templates/)

Workflow template di riferimento, pronti per import e personalizzazione.

### Notifiche (5 templates)

1. **email-notification.json**
   - Invio email con template personalizzabili
   - Supporto SMTP (Gmail, SendGrid, etc.)
   - Variabili: subject, body, to, cc, bcc

2. **slack-notification.json**
   - Invio messaggi a Slack channel
   - Webhook integration
   - Supporto attachments e rich formatting

3. **discord-webhook.json**
   - Invio messaggi a Discord channel
   - Webhook integration
   - Supporto embed messages

4. **integration-slack-notifications.json**
   - Slack integration avanzata con threading
   - Notifiche structured con severity colors

5. **error-alerting.json**
   - Alert su errori critici
   - Multi-channel (email + Slack + PagerDuty)
   - Severity-based routing

### Monitoring & Health (2 templates)

6. **health-monitor.json**
   - Health check periodico dei servizi
   - Controllo: API, PostgreSQL, Qdrant, Redis
   - Trigger alert se servizio down

7. **daily-reports.json**
   - Report giornalieri via email
   - Statistiche: Utenti, PDF processati, domande RAG
   - Scheduled: Ogni giorno alle 09:00

### Data Processing (3 templates)

8. **pdf-processing-pipeline.json**
   - Orchestrazione PDF processing
   - Trigger: PDF upload completato
   - Passi: Extract → Validate → Index → Notify

9. **data-export.json**
   - Export dati periodico
   - Formati: CSV, JSON, Excel
   - Storage: S3, local filesystem, email attachment

10. **bgg-game-sync.json**
    - Sincronizzazione dati BoardGameGeek
    - Scheduled: Settimanale
    - Aggiorna metadati giochi

### Automation (3 templates)

11. **backup-automation.json**
    - Backup automatico database e file
    - Scheduled: Giornaliero (02:00)
    - Storage: S3, local, NFS

12. **cache-warming.json**
    - Riscaldamento cache all'avvio
    - Pre-load: Giochi popolari, configurazioni
    - Trigger: API startup webhook

13. **user-onboarding.json**
    - Onboarding automatico nuovi utenti
    - Trigger: UserRegistered event
    - Passi: Welcome email → Tutorial → Survey

## Workflows (workflows/)

Workflow in produzione, attivamente utilizzati.

### agent-explain-orchestrator.json

**Scopo**: Orchestrazione spiegazioni AI per regole di gioco complesse

**Trigger**: HTTP webhook POST `/webhook/agent-explain`

**Input**:
```json
{
  "gameId": "guid",
  "question": "Come si gioca a Catan?",
  "userId": "guid"
}
```

**Workflow**:
1. Ricevi richiesta spiegazione
2. Interroga RAG pipeline (API `/api/v1/chat`)
3. Se confidence <0.70:
   - Trigger SmolAgent per analisi visuale
   - Merge risultati RAG + Vision
4. Validate risposta (hallucination check)
5. Ritorna spiegazione o error

**Output**:
```json
{
  "answer": "Risposta strutturata",
  "confidence": 0.85,
  "sources": ["doc1.pdf", "doc2.pdf"],
  "visualAnalysis": { ... }
}
```

**Status**: ✅ Production-ready

## Utilizzo

### 1. Import Template

```bash
# Via UI
1. Apri http://localhost:5678
2. Click "+ Add workflow"
3. Click "Import from file"
4. Seleziona template da templates/
5. Customize e salva

# Via CLI (se n8n CLI configurato)
n8n import:workflow --input=templates/email-notification.json
```

### 2. Configurare Credentials

n8n richiede credentials per servizi esterni:

**Email (SMTP)**:
- Settings → Credentials → Add Credential → Email (SMTP)
- Host: smtp.gmail.com
- Port: 587
- User: your-email@gmail.com
- Password: App Password (non la password account!)

**Slack**:
- Settings → Credentials → Add Credential → Slack
- Webhook URL: https://hooks.slack.com/services/YOUR/WEBHOOK/URL

**PostgreSQL** (già configurato):
- Host: postgres
- Port: 5432
- Database: meepleai
- User: meepleai
- Password: (da env var)

### 3. Attivare Workflow

1. Apri workflow in editor
2. Toggle "Active" switch in alto a destra
3. Workflow ora risponde ai trigger

### 4. Testare Workflow

```bash
# Trigger manuale via webhook
curl -X POST http://localhost:5678/webhook/test \
  -H "Content-Type: application/json" \
  -d '{"test": "data"}'

# Oppure usa "Execute Workflow" button in UI
```

## Integrazioni MeepleAI

### Domain Events → n8n

L'API pubblica domain events che possono triggerare workflow:

**Eventi Disponibili**:
- `DocumentProcessing.PdfProcessed` → `pdf-processing-pipeline`
- `Authentication.UserRegistered` → `user-onboarding`
- `Administration.AlertTriggered` → `error-alerting`
- `KnowledgeBase.LowConfidenceAnswer` → Quality review workflow

**Configurazione**:
```csharp
// API: WorkflowIntegration context
services.Configure<N8nOptions>(options =>
{
    options.BaseUrl = "http://n8n:5678";
    options.Webhooks = new Dictionary<string, string>
    {
        ["PdfProcessed"] = "/webhook/pdf-processed",
        ["UserRegistered"] = "/webhook/user-registered",
        ["AlertTriggered"] = "/webhook/alert-triggered"
    };
});
```

### n8n → API Callback

Workflow possono chiamare API endpoints:

**HTTP Request Node**:
- Method: POST
- URL: `http://api:8080/api/v1/webhooks/n8n`
- Authentication: API Key header
- Body: JSON con risultati workflow

**Esempio**:
```json
{
  "workflowId": "agent-explain-orchestrator",
  "executionId": "abc-123",
  "status": "success",
  "result": { ... }
}
```

## Monitoring Workflows

### n8n UI

- **Executions**: http://localhost:5678/executions
  - Vedi tutte le esecuzioni (success/error)
  - Inspection: input/output di ogni node
  - Retry failed executions

- **Logs**: Console output in Docker logs
  ```bash
  docker compose logs -f n8n
  ```

### Metrics

n8n può esporre metrics per Prometheus:

**Configurazione** (experimental):
```yaml
N8N_METRICS: true
N8N_METRICS_PREFIX: n8n_
```

**Metriche**:
- `n8n_workflow_executions_total{workflow,status}`
- `n8n_workflow_execution_duration_seconds`
- `n8n_workflow_errors_total`

## Best Practices

### 1. Error Handling

Sempre aggiungere error handling nodes:
- Try-Catch pattern con "On Error" workflow
- Retry con exponential backoff (3 tentativi)
- Log errori in database o notification service

### 2. Secrets Management

**Mai hardcodare secrets** nei workflow:
- Usa n8n Credentials
- Oppure fetch da API `/api/v1/configuration/{key}`
- Environment variables (per Docker secrets)

### 3. Idempotency

Workflow dovrebbero essere idempotenti:
- Check se operazione già eseguita
- Usa transaction IDs
- Database deduplication

### 4. Performance

- **Batching**: Processa items in batch (es. 100 per volta)
- **Async**: Usa webhook per long-running tasks
- **Pagination**: Non caricare tutti i record, usa limit/offset

### 5. Versioning

- Esporta workflow dopo modifiche importanti
- Commit in git: `workflows/my-workflow-v2.json`
- Changelog in commit message

## Troubleshooting

### Workflow Non Triggera

**Check**:
1. Workflow è "Active"?
2. Webhook URL corretto? Vedi workflow settings → Webhook URL
3. Credentials configurate? Settings → Credentials
4. Logs: `docker compose logs n8n`

### Execution Failed

**Diagnosis**:
1. Apri execution in UI: Executions → Click execution
2. Vedi node che ha fallito (rosso)
3. Click node → Vedi error message e stack trace
4. Fix e "Retry execution"

### Slow Performance

**Fix**:
1. Riduci batch size
2. Aggiungi "Wait" nodes per rate limiting
3. Usa "Split in Batches" node
4. Aumenta n8n memory limit (docker-compose.yml)

### Webhook Not Reachable

**Check**:
1. n8n container running: `docker compose ps`
2. Network corretto: n8n e api nello stesso network
3. Firewall: Porta 5678 aperta
4. DNS: Usa hostname container (`http://n8n:5678` non `localhost`)

## Security

### Authentication

n8n supporta:
- **Basic Auth**: Username/password (development)
- **OAuth2**: Google, GitHub, etc. (production recommended)
- **API Key**: Per webhook calls

**Configurazione**:
```yaml
N8N_BASIC_AUTH_ACTIVE: true
N8N_BASIC_AUTH_USER: admin
N8N_BASIC_AUTH_PASSWORD: ${N8N_PASSWORD}  # Da Docker secret
```

### Network Isolation

n8n dovrebbe:
- ❌ Non essere esposto pubblicamente (reverse proxy con auth)
- ✅ Essere nella stessa Docker network di api/postgres
- ✅ Usare TLS per comunicazioni esterne

### Webhook Validation

Webhook dovrebbero validare:
- HMAC signature (se supportato)
- API key header
- IP whitelist (opzionale)

## Related Documentation

- `../init/n8n/README.md` - Inizializzazione workflow
- `docs/02-development/n8n-integration.md`
- n8n official docs: https://docs.n8n.io
- API WorkflowIntegration context: `apps/api/src/Api/BoundedContexts/WorkflowIntegration/`
