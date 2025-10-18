# N8N-01: Deployment Guide

**Issue**: #288
**Component**: n8n Webhook /agent/explain
**Version**: 1.0
**Last Updated**: 2025-10-11

---

## Quick Start

This guide walks you through deploying the N8N-01 webhook for the `/agent/explain` endpoint.

### Prerequisites

- Docker and Docker Compose installed
- MeepleAI API running (see `docs/SETUP-CHECKLIST.md`)
- Admin access to MeepleAI
- PowerShell (for setup scripts)

---

## Step 1: Environment Configuration

### 1.1 Configure API Environment

Copy the example file and update credentials:

```bash
cd infra/env
cp api.env.dev.example api.env.dev
```

**Important**: Set the `N8N_ENCRYPTION_KEY` to a secure value:

```bash
# In api.env.dev
N8N_ENCRYPTION_KEY=your-secure-32-byte-random-key-here
```

**Generate a secure key** (PowerShell):

```powershell
# Generate random 32-character alphanumeric key
-join ((48..57) + (65..90) + (97..122) | Get-Random -Count 32 | ForEach-Object {[char]$_})
```

### 1.2 Configure n8n Environment

Copy the example file:

```bash
cp n8n.env.dev.example n8n.env.dev
```

**Update the following** in `n8n.env.dev`:

```bash
# Security - CHANGE THESE IN PRODUCTION
N8N_BASIC_AUTH_USER=your-admin-username
N8N_BASIC_AUTH_PASSWORD=your-secure-password

# Encryption - MUST MATCH api.env.dev
N8N_ENCRYPTION_KEY=your-secure-32-byte-random-key-here
```

**Note**: `MEEPLEAI_API_URL` and `N8N_SERVICE_SESSION` will be configured automatically.

---

## Step 2: Start Services

### 2.1 Start Infrastructure

```bash
cd infra
docker compose up -d postgres qdrant redis
```

Wait for health checks to pass (~30 seconds).

### 2.2 Start API

```bash
docker compose up -d api
```

Wait for API to be healthy (~1 minute). Verify:

```bash
curl http://localhost:8080/
# Expected: {"ok":true,"name":"MeepleAgentAI"}
```

### 2.3 Start n8n

```bash
docker compose up -d n8n
```sql
Access n8n UI at: http://localhost:5678

**Login** with credentials from `n8n.env.dev`:
- Username: `N8N_BASIC_AUTH_USER`
- Password: `N8N_BASIC_AUTH_PASSWORD`

---

## Step 3: Service Account Setup

### 3.1 Run Setup Script

From repository root:

```powershell
pwsh tools/setup-n8n-service-account.ps1
```json
**What this does**:
1. Creates service account: `n8n-service@meepleai.dev`
2. Authenticates and retrieves session token
3. Saves credentials to `infra/env/n8n-service-session.env`

**Expected Output**:

```
====================================================
N8N-01: n8n Service Account Setup
====================================================

Step 1: Checking if service account exists...
✓ Service account does not exist yet

Step 2: Creating service account...
✓ Service account created successfully

Step 3: Authenticating and retrieving session token...
✓ Session token retrieved
  Token (first 20 chars): abc123...

Step 4: Verifying session...
✓ Session valid
  User: n8n Service Account (n8n-service@meepleai.dev)
  Role: User

Step 5: Saving configuration...
✓ Configuration saved to: infra/env/n8n-service-session.env

====================================================
Setup Complete!
====================================================
```

### 3.2 Configure n8n Environment Variable

**Option A**: Update `n8n.env.dev` manually:

```bash
# Extract session token from generated file
cat infra/env/n8n-service-session.env

# Copy the N8N_SERVICE_SESSION value to infra/env/n8n.env.dev
```

**Option B**: Merge automatically (PowerShell):

```powershell
# Read session token
$sessionFile = Get-Content infra/env/n8n-service-session.env | Where-Object { $_ -match "^N8N_SERVICE_SESSION=" }
$sessionToken = ($sessionFile -split "=", 2)[1]

# Update n8n.env.dev
$envContent = Get-Content infra/env/n8n.env.dev
$envContent = $envContent -replace "^N8N_SERVICE_SESSION=.*", "N8N_SERVICE_SESSION=$sessionToken"
$envContent | Set-Content infra/env/n8n.env.dev

Write-Host "✓ Session token updated in n8n.env.dev"
```

### 3.3 Restart n8n

```bash
docker compose restart n8n
```sql
---

## Step 4: Import n8n Workflow

### 4.1 Access n8n UI

Navigate to: http://localhost:5678

### 4.2 Import Workflow

1. Click **"Import from File"** or **"+"** → **"Import from file"**
2. Select: `infra/n8n/workflows/agent-explain-orchestrator.json`
3. Click **"Import"**

### 4.3 Activate Workflow

1. Open the imported workflow: **"Agent Explain Orchestrator"**
2. Toggle **"Active"** switch in top-right corner
3. Verify status: **"Active"** (green indicator)

### 4.4 Get Webhook URL

1. Click on the **"Webhook - Explain"** node
2. Copy the **"Test URL"** or **"Production URL"**
3. Example: `http://localhost:5678/webhook/explain`

---

## Step 5: Register Webhook in Database

### 5.1 Run Registration Script

From repository root:

```powershell
pwsh tools/register-n8n-webhook.ps1
```

**Custom parameters** (optional):

```powershell
pwsh tools/register-n8n-webhook.ps1 `
    -ApiBaseUrl "http://localhost:8080" `
    -N8nBaseUrl "http://localhost:5678" `
    -WebhookUrl "http://n8n:5678/webhook/explain" `
    -AdminEmail "admin@meepleai.dev" `
    -AdminPassword "Demo123!"
```

**Expected Output**:

```
====================================================
N8N-01: n8n Webhook Registration
====================================================

Step 1: Authenticating as admin...
✓ Authenticated as admin

Step 2: Checking existing configurations...
✓ No existing configuration found

Step 3: Creating webhook configuration...
✓ Configuration created successfully

Step 4: Testing n8n connection...
⚠️  Connection test failed
  Message: Connection failed: 401
  This is expected if n8n API key is placeholder

Step 5: Configuration details

  Configuration ID: abc-123-def-456
  Name: Agent Explain Webhook
  n8n Base URL: http://localhost:5678
  Webhook URL: http://n8n:5678/webhook/explain
  Status: Active

====================================================
Registration Complete!
====================================================
```json
### 5.2 Update n8n API Key (Optional)

The registration script uses a placeholder API key by default. To update:

1. **Generate n8n API Key**:
   - In n8n UI: **Settings** → **API** → **Generate API Key**
   - Copy the generated key

2. **Update via API**:

```bash
curl -X PUT http://localhost:8080/admin/n8n/{configId} \
  -H "Content-Type: application/json" \
  -H "Cookie: session=YOUR_ADMIN_SESSION" \
  -d '{
    "apiKey": "n8n-api-key-from-settings"
  }'
```

---

## Step 6: Testing

### 6.1 Test Webhook - Valid Request

```bash
curl -X POST http://localhost:5678/webhook/explain \
  -H "Content-Type: application/json" \
  -d '{
    "gameId": "tic-tac-toe",
    "topic": "winning conditions"
  }'
```

**Expected Response** (HTTP 200):

```json
{
  "success": true,
  "data": {
    "outline": {
      "mainTopic": "winning conditions",
      "sections": ["Section 1", "Section 2"]
    },
    "script": "# Explanation: winning conditions\n\n## Overview...",
    "citations": [
      {
        "text": "A player wins when...",
        "source": "PDF:pdf-id",
        "page": 3,
        "line": 0
      }
    ],
    "estimatedReadingTimeMinutes": 2,
    "metadata": {
      "promptTokens": 150,
      "completionTokens": 450,
      "totalTokens": 600,
      "confidence": 0.95
    }
  },
  "timestamp": "2025-10-11T10:30:00Z",
  "version": "1.0"
}
```

### 6.2 Test Webhook - Missing gameId

```bash
curl -X POST http://localhost:5678/webhook/explain \
  -H "Content-Type: application/json" \
  -d '{
    "topic": "rules"
  }'
```

**Expected Response** (HTTP 400):

```json
{
  "success": false,
  "error": {
    "code": "INVALID_REQUEST",
    "message": "gameId is required",
    "details": {
      "missingFields": ["gameId"]
    }
  },
  "timestamp": "2025-10-11T10:30:00Z",
  "version": "1.0"
}
```

### 6.3 Test Webhook - No Content

```bash
curl -X POST http://localhost:5678/webhook/explain \
  -H "Content-Type: application/json" \
  -d '{
    "gameId": "non-existent-game",
    "topic": "rules"
  }'
```

**Expected Response** (HTTP 200):

```json
{
  "success": true,
  "data": {
    "outline": {
      "mainTopic": "rules",
      "sections": []
    },
    "script": "No relevant information found about 'rules' in the rulebook.",
    "citations": [],
    "estimatedReadingTimeMinutes": 0,
    "metadata": {
      "promptTokens": 0,
      "completionTokens": 0,
      "totalTokens": 0,
      "confidence": null
    }
  },
  "timestamp": "2025-10-11T10:30:00Z",
  "version": "1.0"
}
```

### 6.4 Run Integration Tests

```bash
cd apps/api
dotnet test --filter "FullyQualifiedName~N8nWebhookIntegrationTests"
```json
**Expected**: All tests passing (6/6)

---

## Step 7: Verification

### 7.1 Check Workflow Execution

1. Navigate to n8n: http://localhost:5678
2. Click **"Executions"** tab
3. Verify recent executions show **"Success"**

### 7.2 Check API Logs

```bash
docker compose logs -f api | grep "Explain request"
```

**Expected output**:

```
[INFO] Explain request from user n8n-service for game tic-tac-toe: winning conditions
[INFO] Explain response delivered for game tic-tac-toe, estimated 2 min read
```

### 7.3 Check Database

```bash
docker exec -it infra-postgres-1 psql -U meeple -d meepleai

SELECT * FROM n8n_configs;
SELECT * FROM ai_request_logs WHERE endpoint = 'explain' ORDER BY created_at DESC LIMIT 10;
```sql
---

## Troubleshooting

### Issue: "401 Unauthorized" from API

**Cause**: Service account session expired or invalid.

**Solution**:
1. Re-run service account setup: `pwsh tools/setup-n8n-service-account.ps1`
2. Update `N8N_SERVICE_SESSION` in `n8n.env.dev`
3. Restart n8n: `docker compose restart n8n`

### Issue: "No relevant information found"

**Cause**: Game has no indexed PDF content.

**Solution**:
1. Upload PDF: `POST /ingest/pdf` (form-data: file, gameId)
2. Index PDF: `POST /ingest/pdf/{pdfId}/index`
3. Retry webhook call

### Issue: n8n workflow not triggering

**Cause**: Workflow inactive or webhook URL incorrect.

**Solution**:
1. Check workflow is **Active** (green toggle)
2. Verify webhook URL matches registered configuration
3. Check n8n logs: `docker compose logs n8n`

### Issue: "Connection test failed" during registration

**Cause**: n8n API key is placeholder or n8n not running.

**Solution**:
1. Verify n8n is running: `docker compose ps n8n`
2. Generate real API key in n8n settings
3. Update configuration: `PUT /admin/n8n/{configId}`

---

## Security Considerations

### Production Deployment

**1. Change Default Credentials**:
```bash
# n8n.env.dev
N8N_BASIC_AUTH_USER=strong-username
N8N_BASIC_AUTH_PASSWORD=very-strong-password
```

**2. Use Strong Encryption Keys**:
```bash
# Generate 32-byte key
openssl rand -base64 32
```

**3. Enable HTTPS**:
- Configure reverse proxy (nginx, Caddy)
- Update `N8N_PROTOCOL=https`
- Update `WEBHOOK_URL=https://...`

**4. Rotate Session Tokens**:
- Service account sessions expire after 30 days
- Re-run setup script monthly
- Monitor session validity

**5. Restrict Network Access**:
```yaml
# docker-compose.yml - Remove public port exposure
n8n:
  # ports:
  #   - "5678:5678"  # Remove or bind to localhost only
  networks:
    - meepleai
```sql
---

## Monitoring

### Key Metrics

1. **Webhook Request Rate**:
   - Query: `SELECT COUNT(*) FROM ai_request_logs WHERE endpoint = 'explain' AND created_at > NOW() - INTERVAL '1 hour'`

2. **Success Rate**:
   - Query: `SELECT status, COUNT(*) FROM ai_request_logs WHERE endpoint = 'explain' GROUP BY status`

3. **Average Latency**:
   - Query: `SELECT AVG(latency_ms) FROM ai_request_logs WHERE endpoint = 'explain'`

4. **Cache Hit Rate** (AI-05):
   - Check Redis logs: `docker compose logs redis | grep explain`

### Logging

**n8n Workflow Logs**:
```bash
docker compose logs -f n8n
```

**API Request Logs**:
```bash
docker compose logs -f api | grep "Explain"
```

**Database Audit Trail**:
```sql
SELECT * FROM audit_logs WHERE action = 'EXPLAIN_REQUEST' ORDER BY created_at DESC;
```

---

## Maintenance

### Monthly Tasks

- [ ] Rotate service account session token
- [ ] Review and archive old workflow executions
- [ ] Check n8n updates: https://github.com/n8n-io/n8n/releases
- [ ] Verify backup of n8n configurations

### Quarterly Tasks

- [ ] Audit API request logs for anomalies
- [ ] Review and optimize caching strategy (AI-05)
- [ ] Performance testing (load test webhook)
- [ ] Update dependencies (n8n, .NET, Node.js)

---

## Rollback Procedure

If deployment fails or issues arise:

### 1. Deactivate Workflow

```bash
# Via n8n UI: Toggle "Active" to OFF
# Or via API:
curl -X PATCH http://localhost:5678/api/v1/workflows/{workflowId} \
  -H "X-N8N-API-KEY: your-api-key" \
  -d '{"active": false}'
```

### 2. Remove Database Configuration

```bash
curl -X DELETE http://localhost:8080/admin/n8n/{configId} \
  -H "Cookie: session=YOUR_ADMIN_SESSION"
```

### 3. Restart Services

```bash
docker compose restart n8n api
```

### 4. Investigate Logs

```bash
docker compose logs --tail=100 n8n
docker compose logs --tail=100 api
```

---

## Support

**Documentation**:
- Technical Spec: `docs/N8N-01-webhook-explain.md`
- API Docs: `docs/README.md`

**Issue**: #288 (N8N-01 - Webhook /agent/explain)

**Related Issues**:
- AI-02 (#280): RAG Explain - CLOSED
- AI-05 (#283): Response Caching
- ADM-01 (#286): Admin Dashboard

---

**Deployment Guide Version**: 1.0
**Last Updated**: 2025-10-11
**Author**: Claude Code
