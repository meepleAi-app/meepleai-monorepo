# n8n Workflow Automation

**Version**: 2.0 (Consolidated)
**Last Updated**: 2025-12-13T10:59:23.970Z
**Status**: Production Ready
**Location**: Consolidated from `infra/n8n/README.md` + `templates/README.md` + `workflows/README.md`

---

## Table of Contents

1. [Overview](#overview)
2. [Access & Setup](#access--setup)
3. [Workflow Templates (13)](#workflow-templates-13)
4. [Production Workflows](#production-workflows)
5. [MeepleAI Integration](#meepleai-integration)
6. [Usage Guide](#usage-guide)
7. [Best Practices](#best-practices)
8. [Monitoring](#monitoring)
9. [Security](#security)
10. [Troubleshooting](#troubleshooting)
11. [Related Documentation](#related-documentation)

---

## Overview

n8n workflow automation for MeepleAI with 13 ready-to-use templates and production workflows for notifications, data processing, and system orchestration.

### Key Features

- **13 Workflow Templates**: Notifications, monitoring, data processing, automation
- **1 Production Workflow**: Agent Explain Orchestrator (RAG + Vision)
- **Multi-Channel Notifications**: Email, Slack, Discord, PagerDuty
- **Domain Events Integration**: API events trigger workflows automatically
- **Visual Editor**: Web UI for creating/modifying workflows

### Components

```
infra/n8n/
├── README.md                      # Main n8n guide
├── templates/                     # 13 workflow templates (ready to import)
│   ├── README.md                  # Template documentation
│   ├── email-notification.json
│   ├── slack-notification.json
│   ├── discord-webhook.json
│   ├── integration-slack-notifications.json
│   ├── error-alerting.json
│   ├── health-monitor.json
│   ├── daily-reports.json
│   ├── pdf-processing-pipeline.json
│   ├── data-export.json
│   ├── bgg-game-sync.json
│   ├── backup-automation.json
│   ├── cache-warming.json
│   └── user-onboarding.json
└── workflows/                     # Production workflows
    ├── README.md                  # Production workflow docs
    └── agent-explain-orchestrator.json
```

---

## Access & Setup

### Access n8n UI

- **URL**: http://localhost:5678
- **Authentication**: Basic Auth (configured via env vars)
- **Editor**: Visual workflow editor

### Docker Compose Integration

n8n is part of the automation profile (Issue #702):

```bash
# Start with automation profile
docker compose --profile automation up -d

# Or with full stack
docker compose --profile full up -d

# Access UI
open http://localhost:5678
```

### Initial Configuration

**Required Credentials**:
- SMTP (for email notifications)
- Slack Webhook (for Slack notifications)
- PostgreSQL (auto-configured from env vars)

**Environment Variables**:
```yaml
N8N_BASIC_AUTH_ACTIVE: true
N8N_BASIC_AUTH_USER: admin
N8N_BASIC_AUTH_PASSWORD: ${N8N_PASSWORD}  # From Docker secret
N8N_ENCRYPTION_KEY: ${N8N_ENCRYPTION_KEY}  # For credential encryption
```

---

## Workflow Templates (13)

### Notifications (5 Templates)

#### 1. email-notification.json
**Purpose**: Generic email sending template

**Trigger**: HTTP Webhook

**Features**:
- HTML + Plain text support
- Template variables (Handlebars)
- Attachments support
- CC, BCC, Reply-To

**Required Credentials**: SMTP (Gmail, SendGrid, etc.)

**Usage**:
```bash
POST /webhook/send-email
{
  "to": "user@example.com",
  "subject": "Welcome to MeepleAI",
  "body": "Hello {{name}}, welcome!",
  "variables": { "name": "John" }
}
```

#### 2. slack-notification.json
**Purpose**: Send messages to Slack channels

**Trigger**: HTTP Webhook

**Features**:
- Rich formatting (markdown)
- User/channel mentions (@user, @channel)
- Attachments and blocks
- Thread replies

**Required Credentials**: Slack Webhook URL

**Usage**:
```bash
POST /webhook/slack-notify
{
  "channel": "#alerts",
  "message": "🚨 API error rate high!",
  "severity": "critical"
}
```

#### 3. discord-webhook.json
**Purpose**: Send messages to Discord channels

**Features**:
- Embed messages with colors
- Custom avatars
- Multiple embeds
- Rich formatting

**Required Credentials**: Discord Webhook URL

#### 4. integration-slack-notifications.json
**Purpose**: Advanced Slack integration with threading and severity colors

**Features**:
- Thread management
- Color coding by severity (red=critical, yellow=warning, green=info)
- Automatic retry on failure
- Rate limiting

#### 5. error-alerting.json
**Purpose**: Multi-channel error alerting with severity routing

**Trigger**: API error event

**Flow**:
1. Receive error details
2. Determine severity (critical/warning/info)
3. Route notification:
   - Critical → Email + Slack + PagerDuty
   - Warning → Email + Slack
   - Info → Email only
4. Log in database

**Required Credentials**: SMTP, Slack Webhook, PagerDuty API Key

### Monitoring & Health (2 Templates)

#### 6. health-monitor.json
**Purpose**: Periodic health check for all services

**Trigger**: Cron (every 5 minutes)

**Checks**:
- API: `GET /health`
- PostgreSQL: Connection test
- Qdrant: `GET /healthz`
- Redis: PING

**Notifications**: Slack + Email if service down

#### 7. daily-reports.json
**Purpose**: Daily automated reports

**Trigger**: Cron (daily at 09:00)

**Metrics**:
- Active users (last 24h)
- PDFs processed
- RAG questions answered
- Error rate
- Cache hit rate

**Output**: HTML email with charts

**Recipients**: Admin team

### Data Processing (3 Templates)

#### 8. pdf-processing-pipeline.json
**Purpose**: Orchestrate complete PDF processing workflow

**Trigger**: Webhook `DocumentProcessing.PdfProcessed`

**Flow**:
1. Receive PDF uploaded event
2. Trigger extraction (3-stage pipeline)
3. Poll status every 10s (max 5min timeout)
4. If success:
   - Validate quality score
   - Index in Qdrant
   - Notify user (email)
5. If failure:
   - Log error
   - Notify admin
   - Retry (max 3 times)

**Integration**: API `/api/v1/documents/pdf/{id}/process`

#### 9. data-export.json
**Purpose**: Periodic data export

**Trigger**: Cron (weekly, Sunday 02:00)

**Exports**:
- Games catalog → CSV
- Users → JSON
- Audit logs → Excel

**Storage Options**:
- S3 bucket
- Local filesystem
- Email attachment

**Retention**: 90 days

#### 10. bgg-game-sync.json
**Purpose**: BoardGameGeek data synchronization

**Trigger**: Cron (weekly, Monday 03:00)

**Flow**:
1. Fetch games from BGG API
2. For each game:
   - Check if exists in MeepleAI
   - Update metadata (players, duration, complexity)
   - Download image (if missing)
3. Log sync statistics
4. Send summary email

**Rate Limiting**: 1 req/sec (BGG policy)

### Automation (3 Templates)

#### 11. backup-automation.json
**Purpose**: Automated database and file backups

**Trigger**: Cron (daily at 02:00)

**Backups**:
- PostgreSQL dump
- Qdrant collections
- PDF files
- n8n workflows

**Storage**:
- S3 bucket (primary)
- Local NFS (secondary)

**Retention**: 30 days
**Encryption**: AES-256

See: [Backup Strategy](../backup/backup-strategy.md)

#### 12. cache-warming.json
**Purpose**: Pre-load cache on startup

**Trigger**: API startup webhook

**Cache Items**:
- Top 100 games
- System configuration
- Feature flags
- Popular RAG queries

**Target**: 90%+ cache hit rate within 5min of startup

#### 13. user-onboarding.json
**Purpose**: Automated user onboarding sequence

**Trigger**: Webhook `Authentication.UserRegistered`

**Flow**:
1. Send welcome email (personalized template)
2. Wait 2 hours
3. Send tutorial email (how to use MeepleAI)
4. Wait 3 days
5. Send feedback survey
6. Track engagement metrics

**Personalization**: User name, preferred language

---

## Production Workflows

### agent-explain-orchestrator.json

**Status**: ✅ Production-ready
**Version**: v1.2.0
**Last Updated**: 2025-12-13T10:59:23.970Z

**Purpose**: Orchestrate AI explanations for complex game rules, combining RAG + Vision analysis.

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

**Parameters**:
- `gameId` (required): Game GUID
- `question` (required): User question
- `userId` (required): User GUID
- `includeVisualAnalysis` (optional): Default `true`, trigger SmolAgent if confidence low
- `language` (optional): Default `it`, supports `en`, `it`

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
   └─ If confidence < 0.70 → Continue to Visual Analysis

4. Visual Analysis (Low Confidence Fallback)
   ├─ Check includeVisualAnalysis = true
   ├─ Trigger SmolAgent VLM
   │   ├─ Load PDF images for game
   │   ├─ Extract relevant pages (OCR + layout)
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

**Managed Errors**:
- `GAME_NOT_FOUND`: gameId doesn't exist
- `INVALID_QUESTION`: Question too short/long
- `RAG_SERVICE_ERROR`: API /chat not responding
- `VISUAL_ANALYSIS_FAILED`: SmolAgent error
- `VALIDATION_FAILED`: Response fails validation

**Retry Policy**:
- RAG query: 3 attempts, backoff 2s, 4s, 8s
- Visual analysis: 1 attempt (slow, fallback to RAG only)
- Max execution time: 30s (timeout)

#### Performance SLA

**Targets**:
- P50 latency: <3s (RAG only)
- P95 latency: <10s (RAG + Vision)
- Success rate: >95%

**Actual (Last 30 Days)**:
- P50: 2.1s
- P95: 8.7s
- Success rate: 97.3%

#### Monitoring Metrics

```
n8n_agent_explain_executions_total{status}
n8n_agent_explain_duration_seconds
n8n_agent_explain_confidence_score
n8n_agent_explain_visual_fallback_rate
```

**Alerts**:
- `HighAgentExplainFailureRate`: Failure >5% per 1h
- `SlowAgentExplain`: P95 >15s per 30min

**Dashboard**: `ai-quality-monitoring.json` in Grafana

---

## MeepleAI Integration

### Domain Events → n8n

API publishes domain events that trigger workflows automatically.

**Available Events**:
- `DocumentProcessing.PdfProcessed` → `pdf-processing-pipeline`
- `Authentication.UserRegistered` → `user-onboarding`
- `Administration.AlertTriggered` → `error-alerting`
- `KnowledgeBase.LowConfidenceAnswer` → Quality review workflow

**API Configuration**:
```csharp
// WorkflowIntegration bounded context
services.Configure<N8nOptions>(options =>
{
    options.BaseUrl = "http://n8n:5678";
    options.Webhooks = new Dictionary<string, string>
    {
        ["PdfProcessed"] = "/webhook/pdf-processed",
        ["UserRegistered"] = "/webhook/user-registered",
        ["AlertTriggered"] = "/webhook/alert-triggered",
        ["LowConfidenceAnswer"] = "/webhook/quality-review"
    };
});
```

### n8n → API Callbacks

Workflows can call API endpoints for data access and operations.

**HTTP Request Node Configuration**:
- **Method**: POST
- **URL**: `http://api:8080/api/v1/webhooks/n8n`
- **Authentication**: API Key header
- **Body**: JSON with workflow results

**Example Payload**:
```json
{
  "workflowId": "agent-explain-orchestrator",
  "executionId": "abc-123",
  "status": "success",
  "result": { ... }
}
```

**API Endpoint**: `WorkflowIntegration/Application/Handlers/ProcessN8nCallbackHandler.cs`

---

## Usage Guide

### Import Workflow Template

#### Via UI (Recommended)

1. Open http://localhost:5678
2. Click "Workflows" → "Add workflow"
3. Click menu (⋮) → "Import from file"
4. Select template from `infra/n8n/templates/`
5. Review and customize
6. Save

#### Via CLI (If n8n CLI configured)

```bash
n8n import:workflow --input=templates/email-notification.json
```

#### Via API

```bash
curl -X POST http://localhost:5678/api/v1/workflows/import \
  -H "X-N8N-API-KEY: your-api-key" \
  -F "file=@email-notification.json"
```

### Configure Credentials

n8n requires credentials for external services.

#### Email (SMTP)

1. Settings → Credentials → Add Credential → "Email (SMTP)"
2. Configuration:
   - **Host**: smtp.gmail.com
   - **Port**: 587
   - **User**: your-email@gmail.com
   - **Password**: App Password (not account password!)
   - **TLS**: Enabled

**Gmail Setup**:
1. Enable 2FA on Google Account
2. Generate App Password: https://myaccount.google.com/apppasswords
3. Use App Password in n8n

#### Slack

1. Settings → Credentials → Add Credential → "Slack"
2. **Webhook URL**: https://hooks.slack.com/services/YOUR/WEBHOOK/URL

**Slack Setup**:
1. Go to https://api.slack.com/apps
2. Create new app or select existing
3. Add "Incoming Webhooks"
4. Copy webhook URL

#### PostgreSQL (Auto-configured)

Already configured from environment:
- **Host**: postgres
- **Port**: 5432
- **Database**: meepleai
- **User**: meepleai
- **Password**: From env var (`POSTGRES_PASSWORD`)

### Activate Workflow

1. Open workflow in editor
2. Toggle "Active" switch (top right)
3. Workflow now responds to triggers

### Test Workflow

**Manual Trigger** (via UI):
1. Open workflow
2. Click "Execute Workflow" button
3. Provide test data if needed
4. Review execution results

**Webhook Trigger** (via curl):
```bash
curl -X POST http://localhost:5678/webhook/test \
  -H "Content-Type: application/json" \
  -d '{"test": "data"}'
```

---

## Best Practices

### 1. Error Handling

**Always add error handling nodes**:
- Try-Catch pattern with "On Error" workflow
- Retry with exponential backoff (3 attempts)
- Log errors to database or notification service

**Example**:
```javascript
// In "Error Trigger" node
const maxRetries = 3;
const currentRetry = $json.retryCount || 0;

if (currentRetry < maxRetries) {
  return [{ json: { ...originalData, retryCount: currentRetry + 1 } }];
} else {
  throw new Error(`Failed after ${maxRetries} retries`);
}
```

### 2. Secrets Management

**NEVER hardcode secrets** in workflows:
- ✅ Use n8n Credentials (encrypted)
- ✅ Fetch from API `/api/v1/configuration/{key}`
- ✅ Environment variables (via Docker secrets)
- ❌ Don't embed API keys in workflow JSON
- ❌ Don't commit credentials to git

### 3. Idempotency

Workflows should be idempotent:
- Check if operation already executed
- Use transaction IDs or unique keys
- Database deduplication logic

**Example**:
```javascript
// Check if already processed
const existingRecord = await db.query(
  'SELECT * FROM processed_items WHERE transaction_id = $1',
  [transactionId]
);

if (existingRecord.length > 0) {
  return { status: 'already_processed' };
}
```

### 4. Performance Optimization

**Batching**:
- Process items in batches (e.g., 100 at a time)
- Use "Split in Batches" node

**Async Processing**:
- Use webhooks for long-running tasks
- Return immediately, callback when done

**Pagination**:
- Don't load all records at once
- Use limit/offset or cursor-based pagination

**Example**:
```javascript
// Batch processing
const batchSize = 100;
const batches = chunk(allItems, batchSize);

for (const batch of batches) {
  await processBatch(batch);
  await sleep(1000); // Rate limiting
}
```

### 5. Versioning

**Workflow Versioning**:
- Export workflow after major changes
- Commit to git: `workflows/my-workflow-v2.json`
- Include changelog in commit message

**Example**:
```bash
# Export current version
n8n export:workflow --id=my-workflow --output=workflows/my-workflow-v1.2.0.json

# Git commit
git add workflows/my-workflow-v1.2.0.json
git commit -m "feat(n8n): Update my-workflow to v1.2.0

- Added retry logic
- Improved error handling
- Added performance metrics"
```

### 6. Documentation

**For Each Workflow Document**:
- Purpose and use case
- Trigger type and configuration
- Input/output schemas
- Required credentials
- Performance SLA
- Error handling strategy
- Testing procedures

---

## Monitoring

### n8n UI

**Executions Page** (http://localhost:5678/executions):
- View all executions (success/error)
- Inspect input/output of each node
- Retry failed executions
- Filter by status, workflow, date

**Workflow Analytics**:
- Success rate per workflow
- Average execution time
- Error rate trends

### Docker Logs

```bash
# Real-time logs
docker compose logs -f n8n

# Last 100 lines
docker compose logs --tail=100 n8n

# Filter errors only
docker compose logs n8n | grep ERROR
```

### Prometheus Metrics (Experimental)

Enable metrics in configuration:

```yaml
N8N_METRICS: true
N8N_METRICS_PREFIX: n8n_
```

**Metrics Exposed**:
```
n8n_workflow_executions_total{workflow,status}
n8n_workflow_execution_duration_seconds{workflow}
n8n_workflow_errors_total{workflow}
n8n_webhook_requests_total{workflow}
```

**Grafana Dashboard**: Create custom dashboard or add to existing

---

## Security

### Authentication

n8n supports multiple authentication methods:

**Basic Auth** (Development):
```yaml
N8N_BASIC_AUTH_ACTIVE: true
N8N_BASIC_AUTH_USER: admin
N8N_BASIC_AUTH_PASSWORD: ${N8N_PASSWORD}  # From Docker secret
```

**OAuth2** (Production Recommended):
- Google, GitHub, GitLab, etc.
- Configuration in n8n UI settings
- See: [OAuth Security](../../06-security/oauth-security.md)

**API Key** (For Webhook Calls):
- Generate in n8n UI
- Include in `X-N8N-API-KEY` header

### Network Isolation

**Best Practices**:
- ❌ Don't expose n8n publicly (use reverse proxy with auth)
- ✅ Keep in same Docker network as api/postgres
- ✅ Use TLS for external communications
- ✅ Implement reverse proxy (Traefik) for production

See: [Traefik Guide](../deployment/traefik-guide.md)

### Webhook Security

**Production webhooks should**:
- Validate HMAC signature (if supported)
- Require API key header
- Implement rate limiting (max 100 req/min)
- Use IP whitelist (optional)

**Example Validation**:
```javascript
// In "Webhook" node settings
const expectedKey = $env.WEBHOOK_SECRET;
const providedKey = $request.headers['x-api-key'];

if (providedKey !== expectedKey) {
  throw new Error('Unauthorized');
}
```

### Secrets Protection

**Credential Encryption**:
- All credentials encrypted at rest
- Encryption key from Docker secret (`N8N_ENCRYPTION_KEY`)
- Never commit workflow JSON with credentials

**Audit Logging**:
- All executions logged
- Input data sanitized (remove sensitive fields)
- Retention: 90 days in production

---

## Troubleshooting

### Workflow Not Triggering

**Diagnosis**:

1. **Check workflow is active**:
   - UI → Workflow → "Active" toggle should be ON (green)

2. **Verify webhook URL**:
   - Workflow settings → Webhook node → Copy URL
   - Test: `curl -X POST <webhook-url>`

3. **Check credentials**:
   - Settings → Credentials → All required credentials configured

4. **Review logs**:
   ```bash
   docker compose logs n8n --tail=100
   ```

### Execution Failed

**Diagnosis**:

1. **Open execution in UI**:
   - Executions → Click failed execution
   - See which node failed (red indicator)

2. **Review error message**:
   - Click failed node
   - Read error message and stack trace

3. **Common causes**:
   - External API down
   - Credential expired
   - Timeout too low
   - Invalid data format

**Fix**:
- Update credentials
- Increase timeout
- Add retry logic
- Fix data transformation

4. **Retry execution**:
   - Click "Retry execution" button
   - Or fix and test again

### Slow Performance

**Diagnosis**:

1. **View execution timeline** in UI
2. **Identify slow node**
3. **Common causes**:
   - Unoptimized database query
   - Slow external API call
   - Large data processing

**Fixes**:

- **Optimize queries**: Add indexes, limit results
- **Cache API responses**: Use "Cache" node or Redis
- **Batch processing**: Use "Split in Batches" node
- **Async processing**: Webhook callback pattern

**Example Batch Processing**:
```javascript
// Instead of processing all at once
const batchSize = 100;
return chunk($items, batchSize);
```

### Webhook Not Reachable

**Diagnosis**:

1. **Check container running**:
   ```bash
   docker compose ps n8n
   ```

2. **Verify network**:
   - n8n and api must be in same Docker network
   - Check `docker compose config | grep networks`

3. **Check firewall**:
   - Port 5678 should be accessible

4. **DNS resolution**:
   - Use container hostname: `http://n8n:5678` (not `localhost`)

**Fix**:
```bash
# Restart n8n
docker compose restart n8n

# Check network connectivity
docker compose exec api curl http://n8n:5678/healthz
```

### High Failure Rate

**Diagnosis**:

1. **Open Executions → Filter "Error"**
2. **Identify common pattern**:
   - Same node always fails?
   - Same error message?
   - Specific time of day?

3. **Possible causes**:
   - External API down or rate limiting
   - Credential expired
   - Timeout too low
   - Bug in custom JavaScript code

**Fixes**:

- Increase timeout settings
- Add retry logic with backoff
- Update/refresh credentials
- Fix bugs and redeploy
- Add circuit breaker pattern

---

## Adding New Workflows

### From Template to Production

**Checklist**:

```
Development:
☐ Created and tested in development n8n
☐ Error handling implemented
☐ Retry logic configured
☐ Input validation added
☐ Output schema documented

Quality:
☐ Code review completed
☐ Performance tested (if applicable)
☐ Security review (secrets, validation)
☐ Documentation written

Monitoring:
☐ Metrics defined
☐ Prometheus alert rules created
☐ Grafana dashboard updated (if needed)
☐ Runbook written (if critical)

Deployment:
☐ Tested in staging environment
☐ Workflow JSON committed to git
☐ Production credentials configured
☐ Gradual rollout plan (10% → 50% → 100%)
```

**Process**:

1. **Development**:
   ```bash
   # Create in n8n UI (localhost)
   # Test manually with various inputs
   # Export workflow
   ```

2. **Staging**:
   ```bash
   # Import to staging n8n
   docker compose -f docker-compose.yml -f compose.staging.yml exec n8n \
     n8n import:workflow --input=/data/workflows/my-workflow.json

   # Activate and monitor
   # Load test with k6 (if applicable)
   ```

3. **Production**:
   ```bash
   # Commit workflow file
   git add infra/n8n/workflows/my-workflow.json
   git commit -m "feat(n8n): Add my-workflow production workflow"

   # Deploy via CI/CD or manual
   docker compose exec n8n n8n import:workflow --input=/data/workflows/my-workflow.json

   # Gradual activation (canary: 10% → 50% → 100%)
   ```

4. **Monitoring**:
   ```bash
   # Monitor executions
   open http://localhost:5678/executions

   # Check metrics in Grafana
   open http://localhost:3001

   # Watch alerts
   open http://localhost:9090/alerts
   ```

---

## Maintenance

### Health Checks

```bash
# Check active workflows
curl http://localhost:5678/api/v1/workflows?active=true

# Check recent executions
curl http://localhost:5678/api/v1/executions?limit=10

# Check specific workflow
curl http://localhost:5678/api/v1/workflows/agent-explain-orchestrator
```

### Backup Workflows

**Automated** (via `backup-automation.json` template):
- Runs daily at 02:00
- Backs up all workflows to S3

**Manual**:
```bash
# Backup all workflows
docker compose exec n8n n8n export:workflow --all --output=/backups/

# Backup specific workflow
docker compose exec n8n n8n export:workflow \
  --id=agent-explain-orchestrator \
  --output=/backups/agent-explain-v1.2.0.json
```

### Update Workflow

**Process**:

1. **Backup current version**:
   ```bash
   docker compose exec n8n n8n export:workflow \
     --id=agent-explain-orchestrator \
     --output=/backups/agent-explain-v1.2.0.json
   ```

2. **Make changes** (in UI or edit JSON)

3. **Re-import** (overwrites existing):
   ```bash
   docker compose exec n8n n8n import:workflow \
     --input=/data/workflows/agent-explain-orchestrator.json
   ```

4. **Test**:
   ```bash
   curl -X POST http://localhost:5678/webhook/agent-explain \
     -H "Content-Type: application/json" \
     -d '{"test": "data"}'
   ```

5. **Monitor** for errors and performance

---

## Customization Examples

### Change Email Template

```javascript
// In "Compose Email" node
const subject = "{{$json.subject}}";
const body = `
  <html>
    <h1>Ciao {{$json.userName}}</h1>
    <p>{{$json.message}}</p>
    <footer>MeepleAI Team</footer>
  </html>
`;
```

### Add Retry Logic

```javascript
// In "On Error" node
const maxRetries = 3;
const currentRetry = $json.retryCount || 0;

if (currentRetry < maxRetries) {
  // Exponential backoff
  const delaySeconds = Math.pow(2, currentRetry);
  await sleep(delaySeconds * 1000);

  return [{
    json: {
      ...originalData,
      retryCount: currentRetry + 1
    }
  }];
} else {
  // Max retries exceeded, alert admin
  throw new Error(`Failed after ${maxRetries} retries`);
}
```

### Change Slack Channel by Severity

```javascript
// In "Slack Message" node
const channel = severity === 'critical' ? '#incidents' :
                severity === 'warning' ? '#alerts' :
                '#notifications';

const color = severity === 'critical' ? '#FF0000' :
              severity === 'warning' ? '#FFA500' :
              '#00FF00';
```

### Add Custom Validation

```javascript
// In "Validate Input" node
const errors = [];

if (!$json.email || !$json.email.includes('@')) {
  errors.push('Invalid email format');
}

if (!$json.gameId || !isValidGuid($json.gameId)) {
  errors.push('Invalid gameId format');
}

if (errors.length > 0) {
  throw new Error(`Validation failed: ${errors.join(', ')}`);
}
```

---

## Related Documentation

### n8n Setup & Configuration
- **[Infrastructure Overview](../infrastructure-overview.md)** - Complete infrastructure guide
- **[Environment Configuration](../../infra/env/README.md)** - Environment variables
- **[Secrets Management](../../infra/secrets/README.md)** - Docker secrets guide
- **[Initialization Scripts](../../infra/init/n8n/README.md)** - Workflow initialization

### Integration & Development
- **[API Workflow Integration](../../03-api/workflow-integration-api.md)** - API endpoints
- **[Domain Events](../../01-architecture/ddd/domain-events.md)** - Event publishing
- **[WorkflowIntegration Context](../../apps/api/src/Api/BoundedContexts/WorkflowIntegration/)** - Source code

### Operations
- **[Deployment Guide](../deployment-guide.md)** - Production deployment
- **[Backup Strategy](../backup/backup-strategy.md)** - Backup automation (Issue #704)
- **[Monitoring Strategy](../monitoring/monitoring-strategy.md)** - Overall monitoring

### Templates & Workflows
- **[Template Directory](../../infra/n8n/templates/)** - All 13 templates
- **[Production Workflows](../../infra/n8n/workflows/)** - Active workflows

### External Resources
- **[n8n Official Docs](https://docs.n8n.io)** - Complete n8n documentation
- **[n8n Community](https://community.n8n.io)** - Forums and support
- **[Workflow Library](https://n8n.io/workflows)** - Community workflows

---

## Changelog

### 2025-12-08: Documentation Consolidation

**Changes**:
- ✅ Consolidated `infra/n8n/README.md` + `templates/README.md` + `workflows/README.md`
- ✅ Added complete template documentation (all 13 templates)
- ✅ Added production workflow details (agent-explain-orchestrator)
- ✅ Added comprehensive usage, testing, and troubleshooting guides
- ✅ Moved to `docs/05-operations/workflow-automation.md`
- ✅ Updated all cross-references to new docs structure

---

**Version**: 2.0 (Post-Consolidation)
**Last Updated**: 2025-12-13T10:59:23.970Z
**Maintainer**: Operations Team
**Templates**: 13 ready-to-use workflows
**Production Workflows**: 1 (agent-explain-orchestrator)
**Documentation**: Comprehensive single-source guide

