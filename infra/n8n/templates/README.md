# n8n Workflow Templates

## Contenuto

13 workflow templates pronti all'uso per automation MeepleAI.

## Template Disponibili

### Notifiche

#### 1. email-notification.json
**Scopo**: Template generico per invio email

**Trigger**: HTTP Webhook

**Features**:
- Template personalizzabili (Handlebars)
- Supporto HTML + Plain text
- Attachments
- CC, BCC, Reply-To

**Configurazione Richiesta**:
- SMTP Credentials (Gmail, SendGrid, etc.)

**Uso**:
```json
POST /webhook/send-email
{
  "to": "user@example.com",
  "subject": "Welcome to MeepleAI",
  "body": "Hello {{name}}, welcome!",
  "variables": {
    "name": "John"
  }
}
```

#### 2. slack-notification.json
**Scopo**: Invio messaggi a Slack

**Trigger**: HTTP Webhook

**Features**:
- Rich formatting (markdown)
- Mentions (@user, @channel)
- Attachments e blocks
- Thread replies

**Configurazione Richiesta**:
- Slack Webhook URL

**Uso**:
```json
POST /webhook/slack-notify
{
  "channel": "#alerts",
  "message": "🚨 API error rate high!",
  "severity": "critical"
}
```

#### 3. discord-webhook.json
**Scopo**: Invio messaggi a Discord

**Trigger**: HTTP Webhook

**Features**:
- Embed messages
- Rich colors
- Avatars custom
- Multiple embeds

**Configurazione Richiesta**:
- Discord Webhook URL

#### 4. integration-slack-notifications.json
**Scopo**: Slack integration avanzata con threading e severity colors

**Features**:
- Thread management
- Color coding by severity (red=critical, yellow=warning, green=info)
- Automatic retry on failure

#### 5. error-alerting.json
**Scopo**: Multi-channel error alerting

**Trigger**: API error event

**Flow**:
1. Ricevi error details
2. Determina severity
3. Route notification:
   - Critical → Email + Slack + PagerDuty
   - Warning → Email + Slack
   - Info → Email only
4. Log in database

**Configurazione Richiesta**:
- SMTP, Slack Webhook, PagerDuty API Key

### Monitoring & Health

#### 6. health-monitor.json
**Scopo**: Health check periodico servizi

**Trigger**: Cron (ogni 5 minuti)

**Controlli**:
- API: `GET /health`
- PostgreSQL: Connection test
- Qdrant: `GET /healthz`
- Redis: PING

**Output**:
- Tutti UP → No action
- Servizio DOWN → Trigger alert

**Notifiche**: Slack + Email

#### 7. daily-reports.json
**Scopo**: Report giornalieri automatici

**Trigger**: Cron (ogni giorno ore 09:00)

**Metriche**:
- Utenti attivi (last 24h)
- PDF processati
- Domande RAG
- Error rate
- Cache hit rate

**Output**: Email HTML con grafici

**Destinatari**: Admin team

### Data Processing

#### 8. pdf-processing-pipeline.json
**Scopo**: Orchestrazione completa PDF processing

**Trigger**: Webhook `DocumentProcessing.PdfProcessed`

**Flow**:
1. Ricevi PDF uploaded event
2. Trigger extraction (3-stage pipeline)
3. Poll status ogni 10s (max 5min)
4. Se success:
   - Validate quality score
   - Index in Qdrant
   - Notify user (email)
5. Se failure:
   - Log error
   - Notify admin
   - Retry (max 3 volte)

**Integration**: API `/api/v1/documents/pdf/{id}/process`

#### 9. data-export.json
**Scopo**: Export dati periodico

**Trigger**: Cron (settimanale, domenica 02:00)

**Export**:
- Games catalog → CSV
- Users → JSON
- Audit logs → Excel

**Storage Options**:
- S3 bucket
- Local filesystem
- Email attachment

**Retention**: 90 giorni

#### 10. bgg-game-sync.json
**Scopo**: Sincronizzazione BoardGameGeek

**Trigger**: Cron (settimanale, lunedì 03:00)

**Flow**:
1. Fetch games da BGG API
2. Per ogni game:
   - Check se esiste in MeepleAI
   - Update metadati (players, duration, complexity)
   - Download image (se mancante)
3. Log sync statistics
4. Send summary email

**Rate Limiting**: 1 req/sec (BGG policy)

### Automation

#### 11. backup-automation.json
**Scopo**: Backup automatico database e file

**Trigger**: Cron (giornaliero, ore 02:00)

**Backup**:
- PostgreSQL dump
- Qdrant collections
- PDF files
- n8n workflows

**Storage**:
- S3 bucket (primary)
- Local NFS (secondary)

**Retention**: 30 giorni

**Encryption**: AES-256

#### 12. cache-warming.json
**Scopo**: Pre-caricamento cache all'avvio

**Trigger**: API startup webhook

**Cache**:
- Top 100 games
- System configuration
- Feature flags
- Popular RAG queries

**Target**: 90%+ cache hit rate entro 5min dall'avvio

#### 13. user-onboarding.json
**Scopo**: Onboarding automatico nuovi utenti

**Trigger**: Webhook `Authentication.UserRegistered`

**Flow**:
1. Send welcome email (template personalizzato)
2. Wait 2 ore
3. Send tutorial email (come usare MeepleAI)
4. Wait 3 giorni
5. Send feedback survey
6. Track engagement metrics

**Personalizzazione**: Nome utente, lingua preferita

## Utilizzo

### Import Template

#### Via UI
1. Apri http://localhost:5678
2. Click "Workflows" → "Add workflow"
3. Click menu (⋮) → "Import from file"
4. Seleziona template file
5. Review e customize
6. Save

#### Via API (se n8n API abilitato)
```bash
curl -X POST http://localhost:5678/api/v1/workflows/import \
  -H "X-N8N-API-KEY: your-api-key" \
  -F "file=@email-notification.json"
```

### Personalizzazione

1. **Credentials**: Configura in Settings → Credentials
2. **Variables**: Modifica default values nei nodes
3. **Trigger URL**: Copia webhook URL da Webhook node
4. **Schedule**: Modifica Cron expression se necessario
5. **Error Handling**: Aggiungi custom error nodes

### Attivazione

1. Toggle "Active" switch (in alto a destra)
2. Workflow inizia ad ascoltare trigger
3. Test con "Execute Workflow" o trigger manuale

## Customizzazione Comuni

### Cambiare Email Template

```javascript
// Nel node "Compose Email"
const subject = "{{$json.subject}}";
const body = `
  <h1>Ciao {{$json.userName}}</h1>
  <p>{{$json.message}}</p>
`;
```

### Aggiungere Retry Logic

```javascript
// Node "Error Trigger"
const maxRetries = 3;
const currentRetry = $json.retryCount || 0;

if (currentRetry < maxRetries) {
  // Retry
  return [{
    json: {
      ...originalData,
      retryCount: currentRetry + 1
    }
  }];
} else {
  // Give up, send alert
  throw new Error(`Failed after ${maxRetries} retries`);
}
```

### Cambiare Slack Channel

```javascript
// Nel node "Slack Message"
const channel = severity === 'critical' ? '#incidents' : '#alerts';
```

## Testing Template

### 1. Manual Trigger
1. Apri workflow in editor
2. Click "Execute Workflow" button
3. Fornisci test data (se richiesto)
4. Verifica execution results

### 2. Webhook Test
```bash
curl -X POST http://localhost:5678/webhook/test \
  -H "Content-Type: application/json" \
  -d '{
    "testData": "value"
  }'
```

### 3. Cron Test
Cambia cron expression a "ogni minuto" per test rapido:
```
*/1 * * * *  # Ogni minuto
```

Poi ripristina schedule originale.

## Best Practices

1. **Naming**: Usa nomi descrittivi per nodes e workflows
2. **Documentation**: Aggiungi "Sticky Note" nodes per spiegare logica
3. **Error Handling**: Sempre aggiungere "On Error" workflow
4. **Credentials**: Mai hardcodare, usa n8n Credentials
5. **Testing**: Test in development prima di attivare in production
6. **Versioning**: Esporta e commit in git dopo modifiche

## Troubleshooting

### Template Non Importa

**Errori Comuni**:
- File corrotto: Re-download da repository
- Versione n8n incompatibile: Upgrade n8n
- Missing credentials schema: Update n8n

### Workflow Fallisce Immediatamente

**Check**:
1. Credentials configurate?
2. Tutti i required fields popolati?
3. Services esterni raggiungibili?
4. Logs: Vedi error message in execution details

## Contribuire

Per aggiungere nuovo template:

1. Crea workflow in n8n UI
2. Test completamente
3. Export workflow
4. Rimuovi credentials sensibili
5. Aggiungi a `templates/`
6. Documenta in questo README
7. PR con `[N8N]` prefix

## Related Documentation

- `../README.md` - n8n overview
- `../workflows/README.md` - Production workflows
- `../../init/n8n/README.md` - Workflow initialization
- n8n docs: https://docs.n8n.io
