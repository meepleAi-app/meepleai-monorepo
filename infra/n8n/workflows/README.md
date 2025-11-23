# n8n Production Workflows

## Contenuto

Workflow attivamente utilizzati in produzione.

## Workflow Attivi

### agent-explain-orchestrator.json

**Status**: ✅ Production-ready

**Scopo**: Orchestrazione spiegazioni AI avanzate per regole di gioco complesse, combinando RAG + Vision analysis.

**Trigger**: HTTP Webhook POST `/webhook/agent-explain`

#### Input Schema

```json
{
  "gameId": "550e8400-e29b-41d4-a716-446655440000",
  "question": "Come si posizionano le strade in Catan?",
  "userId": "660e8400-e29b-41d4-a716-446655440000",
  "includeVisualAnalysis": true,
  "language": "it"
}
```

**Parametri**:
- `gameId` (required): GUID del gioco
- `question` (required): Domanda dell'utente
- `userId` (required): GUID dell'utente
- `includeVisualAnalysis` (optional): Default `true`, trigger SmolAgent se confidence bassa
- `language` (optional): Default `it`, supporta `en`, `it`

#### Workflow Steps

```
1. Validate Input
   ├─ Check gameId exists in database
   ├─ Check userId valid
   └─ Validate question length (min 10, max 500 chars)

2. Query RAG Pipeline
   ├─ POST /api/v1/chat
   ├─ Body: { gameId, question, userId }
   └─ Response: { answer, confidence, sources }

3. Confidence Check
   ├─ If confidence ≥ 0.70 → Return RAG answer (fast path)
   └─ If confidence < 0.70 → Continue to Step 4

4. Visual Analysis (Low Confidence Path)
   ├─ Check includeVisualAnalysis = true
   ├─ Trigger SmolAgent VLM
   │   ├─ Load PDF images for game
   │   ├─ Extract relevant pages (OCR + layout analysis)
   │   └─ SmolDocling vision model analysis
   └─ Response: { visualInsights, pageReferences }

5. Merge Results
   ├─ Combine RAG text + Visual insights
   ├─ Re-score confidence (weighted average)
   └─ Format structured answer

6. Validation Layer
   ├─ Hallucination check (forbidden keywords)
   ├─ Citation verification
   └─ Factual consistency

7. Return Response
   └─ Format final JSON output
```

#### Output Schema

```json
{
  "success": true,
  "answer": "Per posizionare le strade in Catan, devi...",
  "confidence": 0.85,
  "sources": [
    {
      "documentId": "doc-guid",
      "fileName": "catan-regolamento-it.pdf",
      "pages": [5, 6]
    }
  ],
  "visualAnalysis": {
    "included": true,
    "insights": "Immagine a pagina 5 mostra posizionamento strade...",
    "pageReferences": [5]
  },
  "metadata": {
    "processingTimeMs": 2340,
    "ragConfidence": 0.65,
    "visualConfidence": 0.92,
    "fallbackUsed": true
  }
}
```

#### Error Handling

**Errori Gestiti**:
- `GAME_NOT_FOUND`: gameId non esiste
- `INVALID_QUESTION`: Domanda troppo corta/lunga
- `RAG_SERVICE_ERROR`: API /chat non risponde
- `VISUAL_ANALYSIS_FAILED`: SmolAgent errore
- `VALIDATION_FAILED`: Risposta non passa validazione

**Retry Policy**:
- RAG query: 3 tentativi, backoff 2s, 4s, 8s
- Visual analysis: 1 tentativo (slow, fallback a RAG solo)
- Max execution time: 30s (timeout)

#### Performance

**Target SLA**:
- P50 latency: <3s (RAG only)
- P95 latency: <10s (RAG + Vision)
- Success rate: >95%

**Actual Performance** (last 30 days):
- P50: 2.1s
- P95: 8.7s
- Success rate: 97.3%

#### Monitoring

**Metrics Exposed**:
- `n8n_agent_explain_executions_total{status}`
- `n8n_agent_explain_duration_seconds`
- `n8n_agent_explain_confidence_score`
- `n8n_agent_explain_visual_fallback_rate`

**Alerts**:
- `HighAgentExplainFailureRate`: Failure >5% per 1h
- `SlowAgentExplain`: P95 >15s per 30min

**Grafana Dashboard**: `ai-quality-monitoring.json`

#### Configurazione

**Credentials Richieste**:
- MeepleAI API Key (per authentication)
- PostgreSQL (per game lookup)
- SmolAgent API Key (per visual analysis)

**Environment Variables**:
```yaml
MEEPLEAI_API_URL: http://api:8080
MEEPLEAI_API_KEY: ${MEEPLEAI_API_KEY}  # Da Docker secret
SMOLDOCLING_URL: http://smoldocling-service:8002
CONFIDENCE_THRESHOLD: 0.70
VISUAL_ANALYSIS_ENABLED: true
MAX_EXECUTION_TIME_SECONDS: 30
```

#### Deployment

**Versioning**:
- Current: v1.2.0
- Last updated: 2025-11-15
- Changelog:
  - v1.2.0: Aggiunto visual analysis fallback
  - v1.1.0: Multi-language support
  - v1.0.0: Initial release

**Rollback**:
```bash
# Esporta versione corrente
n8n export:workflow --id=agent-explain-orchestrator --output=backup/

# Import versione precedente
n8n import:workflow --input=workflows/agent-explain-orchestrator-v1.1.0.json
```

## Aggiungere Nuovo Workflow di Produzione

### Checklist

Prima di promuovere template → production:

- [ ] Testato completamente in development
- [ ] Error handling implementato
- [ ] Retry logic configurato
- [ ] Monitoring metrics aggiunti
- [ ] Alert rules create (prometheus)
- [ ] Documentation completa
- [ ] Performance SLA definiti
- [ ] Runbook scritto (docs/05-operations/runbooks/)
- [ ] Code review approvato
- [ ] Staging test passed

### Processo

1. **Development**:
   ```bash
   # Crea in n8n UI (localhost)
   # Test manualmente
   # Export workflow
   ```

2. **Staging**:
   ```bash
   # Import in staging n8n
   docker compose -f docker-compose.yml -f compose.staging.yml exec n8n \
     n8n import:workflow --input=/data/workflows/my-workflow.json

   # Attiva e monitor
   # Load test con k6 (se applicabile)
   ```

3. **Production**:
   ```bash
   # Commit workflow file
   git add infra/n8n/workflows/my-workflow.json
   git commit -m "feat(n8n): Add my-workflow production workflow"

   # Deploy via CI/CD (o manuale)
   docker compose exec n8n n8n import:workflow --input=/data/workflows/my-workflow.json

   # Attiva gradualmente (canary: 10% → 50% → 100%)
   ```

4. **Monitoring**:
   ```bash
   # Monitor executions
   open http://localhost:5678/executions

   # Check metrics in Grafana
   open http://localhost:3001

   # Watch alerts in Prometheus
   open http://localhost:9090/alerts
   ```

## Manutenzione

### Health Check

```bash
# Check workflow attivi
curl http://localhost:5678/api/v1/workflows?active=true

# Check recent executions
curl http://localhost:5678/api/v1/executions?limit=10

# Check workflow specifico
curl http://localhost:5678/api/v1/workflows/agent-explain-orchestrator
```

### Backup

```bash
# Backup automatico (via backup-automation.json template)
# Manuale
docker compose exec n8n n8n export:workflow --all --output=/backups/
```

### Update Workflow

```bash
# 1. Esporta versione corrente (backup)
docker compose exec n8n n8n export:workflow --id=agent-explain-orchestrator --output=/backups/agent-explain-v1.2.0.json

# 2. Modifica in UI o via file

# 3. Re-import (sovrascrive)
docker compose exec n8n n8n import:workflow --input=/data/workflows/agent-explain-orchestrator.json

# 4. Verifica
curl http://localhost:5678/webhook/agent-explain -X POST -d '{"test":"data"}'
```

## Troubleshooting

### Workflow Non Triggera

**Check**:
1. Workflow attivo? UI → Active toggle deve essere ON
2. Webhook URL corretto?
3. Network: n8n container raggiungibile?
4. Logs: `docker compose logs n8n --tail=100`

### High Failure Rate

**Diagnosis**:
1. Apri Executions → Filtra "Error"
2. Identifica pattern comune (stesso node fallisce?)
3. Check error message e stack trace
4. Possibili cause:
   - API esterna down
   - Credential scaduta
   - Timeout troppo basso
   - Bug in custom code

**Fix**:
- Aumenta timeout
- Aggiungi retry logic
- Update credentials
- Fix bug e re-deploy

### Slow Performance

**Diagnosis**:
1. Vedi execution timeline in UI
2. Identifica node lento
3. Possibili cause:
   - DB query non ottimizzata
   - API call lenta
   - Large data processing

**Fix**:
- Optimize query (index, limit results)
- Cache API responses
- Batch processing (Split in Batches node)
- Async processing (webhook callback)

## Security

### Webhook Security

Production webhook dovrebbero:
- ✅ Validare HMAC signature (se supportato)
- ✅ Require API key header
- ✅ Rate limiting (max 100 req/min)
- ✅ IP whitelist (opzionale)

### Secrets

**Mai hardcodare**:
- API keys
- Passwords
- Connection strings

**Usa**:
- n8n Credentials (encrypted)
- Docker secrets
- Environment variables

### Audit

Tutte le execution sono loggato:
- Input data (sanitized)
- Execution path
- Error messages
- Output data

Retention: 90 giorni in production

## Related Documentation

- `../templates/README.md` - Template workflow
- `../README.md` - n8n overview
- `../../init/n8n/README.md` - Workflow initialization
- `docs/05-operations/runbooks/n8n-workflow-failure.md`
- n8n docs: https://docs.n8n.io
