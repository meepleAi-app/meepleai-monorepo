# n8n Error Handling Guide

**Issue**: N8N-05 - Error handling and retry logic for workflows
**Status**: Implemented (Backend ready, workflow configuration required)
**Last Updated**: 2025-10-25

---

## Overview

This guide describes how to add comprehensive error handling to n8n workflows in the MeepleAI system. The error handling system includes:

1. **Automatic Retries**: Exponential backoff for transient failures
2. **Error Logging**: Integration with MeepleAI API for centralized error tracking
3. **Slack Alerts**: Notifications for persistent failures (optional)
4. **Graceful Degradation**: Fallback responses when services are unavailable

## Architecture

```
┌─────────────┐     ┌──────────────┐     ┌─────────────┐
│   n8n       │────▶│  MeepleAI    │────▶│  Postgres   │
│  Workflow   │     │     API      │     │  Database   │
│             │     │              │     │             │
│ Error       │     │ POST /api/v1/│     │ workflow_   │
│ Trigger     │────▶│ logs/        │────▶│ error_logs  │
│             │     │ workflow-    │     │             │
│             │     │ error        │     │             │
└─────────────┘     └──────────────┘     └─────────────┘
       │
       │ (optional)
       ▼
┌─────────────┐
│   Slack     │
│  Webhook    │
│             │
└─────────────┘
```

## Backend API Endpoint

**Endpoint**: `POST /api/v1/logs/workflow-error`
**Authentication**: None required (webhook)
**Rate Limiting**: Applied (RateLimitService)

**Request Body**:
```json
{
  "workflowId": "string (required, max 255 chars)",
  "executionId": "string (required, max 255 chars)",
  "errorMessage": "string (required, max 5000 chars)",
  "nodeName": "string (optional, max 255 chars)",
  "retryCount": "integer (optional, default: 0)",
  "stackTrace": "string (optional, max 10000 chars)"
}
```

**Response** (200 OK):
```json
{
  "message": "Error logged successfully"
}
```

**Security Features**:
- Sensitive data redaction (API keys, tokens, passwords automatically removed)
- Input validation with max length enforcement
- No authentication required for webhook simplicity

## Step-by-Step Implementation

### 1. Configure HTTP Request Node Retry Logic

All HTTP Request nodes calling the MeepleAI API should have retry configuration:

**Settings → Request Options → Retry**:
- **Max Tries**: `3`
- **Wait Between Retries**: Exponential backoff
  - Try 1: 1000ms
  - Try 2: 2000ms
  - Try 3: 4000ms
- **Retry On HTTP Codes**: `429, 500, 502, 503, 504`

**Example** (already configured in existing workflows):
```json
{
  "options": {
    "timeout": 60000,
    "retry": {
      "maxTries": 3,
      "retryOnHttpStatusCodes": "429,500,502,503,504"
    }
  }
}
```

### 2. Add Error Trigger Node

1. **Add Node**: Click `+` → Search "Error Trigger" → Add
2. **Position**: Place below main workflow path
3. **Configuration**:
   - Name: "Workflow Error Handler"
   - Trigger on: "All Errors"

### 3. Add Retry Counter Node

After Error Trigger, add a Code node to track retry attempts:

**Node**: Code (JavaScript)
**Name**: "Count Retries"

```javascript
// Get error details
const error = $input.first().json;
const workflowId = $workflow.id;
const executionId = $execution.id;

// Extract retry count from error or initialize
let retryCount = 0;
if (error.message && error.message.includes('Retry')) {
  const match = error.message.match(/Retry (\d+)/);
  if (match) {
    retryCount = parseInt(match[1]);
  }
}

// Increment retry count
retryCount++;

return {
  workflowId,
  executionId,
  retryCount,
  errorMessage: error.message || 'Unknown error',
  nodeName: error.node?.name || 'Unknown node',
  stackTrace: error.stack || null,
  timestamp: new Date().toISOString()
};
```

### 4. Add Conditional Node (Check Retry Limit)

**Node**: IF
**Name**: "Check Retry Limit"

**Condition**:
- **Value 1**: `{{ $json.retryCount }}`
- **Operation**: `Smaller Than`
- **Value 2**: `3`

**Branches**:
- **True**: Retry workflow execution
- **False**: Log error and send alert

### 5. Add Error Logging Node (False Branch)

**Node**: HTTP Request
**Name**: "Log to API"

**Configuration**:
- **Method**: POST
- **URL**: `http://api:8080/api/v1/logs/workflow-error`
- **Authentication**: None
- **Send Body**: Yes
- **Body Content Type**: JSON

**Body** (Expression):
```json
{
  "workflowId": "{{ $json.workflowId }}",
  "executionId": "{{ $json.executionId }}",
  "errorMessage": "{{ $json.errorMessage }}",
  "nodeName": "{{ $json.nodeName }}",
  "retryCount": "{{ $json.retryCount }}",
  "stackTrace": "{{ $json.stackTrace }}"
}
```

**Error Handling**: Continue On Fail (don't fail workflow if logging fails)

### 6. Add Slack Alert Node (Optional)

**Node**: HTTP Request
**Name**: "Send Slack Alert"

**Prerequisites**:
1. Create Slack Incoming Webhook: https://api.slack.com/messaging/webhooks
2. Set environment variable: `SLACK_WEBHOOK_URL=https://hooks.slack.com/services/YOUR/WEBHOOK/URL`

**Configuration**:
- **Method**: POST
- **URL**: `{{ $env.SLACK_WEBHOOK_URL }}`
- **Authentication**: None
- **Send Body**: Yes
- **Body Content Type**: JSON

**Body** (Expression):
```json
{
  "text": ":rotating_light: *n8n Workflow Error*",
  "blocks": [
    {
      "type": "header",
      "text": {
        "type": "plain_text",
        "text": ":rotating_light: Workflow Error After 3 Retries"
      }
    },
    {
      "type": "section",
      "fields": [
        {
          "type": "mrkdwn",
          "text": "*Workflow:*\n{{ $json.workflowId }}"
        },
        {
          "type": "mrkdwn",
          "text": "*Execution:*\n{{ $json.executionId }}"
        }
      ]
    },
    {
      "type": "section",
      "fields": [
        {
          "type": "mrkdwn",
          "text": "*Node:*\n{{ $json.nodeName }}"
        },
        {
          "type": "mrkdwn",
          "text": "*Retry Count:*\n{{ $json.retryCount }}"
        }
      ]
    },
    {
      "type": "section",
      "text": {
        "type": "mrkdwn",
        "text": "*Error:*\n```{{ $json.errorMessage }}```"
      }
    },
    {
      "type": "context",
      "elements": [
        {
          "type": "mrkdwn",
          "text": "Time: {{ $json.timestamp }}"
        }
      ]
    }
  ]
}
```

**Error Handling**: Continue On Fail (optional, don't fail if Slack unavailable)

### 7. Add Fallback Response Node

For workflows that need to return a response (like webhooks):

**Node**: Code (JavaScript)
**Name**: "Return Fallback Response"

```javascript
// Return user-friendly error message
return {
  json: {
    error: true,
    message: "I'm temporarily unavailable. Please try again in a moment.",
    retryAfter: 60, // seconds
    executionId: $json.executionId
  }
};
```

## Complete Error Handling Flow

```
[Main Workflow Nodes]
        │
        │ (on error)
        ▼
[Error Trigger]
        │
        ▼
[Count Retries]
        │
        ▼
[Check Retry Limit < 3?]
        │
        ├─── True ──▶ [Wait (exponential)] ──▶ [Retry Workflow]
        │
        └─── False ─▶ [Log to API]
                      │
                      ├─▶ [Send Slack Alert] (optional)
                      │
                      └─▶ [Return Fallback Response]
```

## Workflow-Specific Configuration

### Agent QA Webhook

**Workflow ID**: `agent-qa-webhook`
**Fallback Response**: "I'm temporarily unavailable. Please try again in a moment."
**Critical Nodes**:
- HTTP Request to `/agents/qa` (already has retry: 3 attempts)

### Agent Explain Webhook

**Workflow ID**: `agent-explain-webhook`
**Fallback Response**: "Explanation service temporarily unavailable. Please try again shortly."
**Critical Nodes**:
- HTTP Request to `/agents/explain` (already has retry: 3 attempts)

## Testing Error Handling

### 1. Simulate API Failure

Temporarily stop the MeepleAI API:
```bash
cd infra
docker compose stop api
```

Trigger workflow → Verify:
- ✅ 3 retry attempts with exponential backoff
- ✅ Error logged to `/api/v1/logs/workflow-error`
- ✅ Slack alert sent (if configured)
- ✅ Fallback response returned

### 2. Check Error Logs

**Admin Dashboard**: http://localhost:3000/admin/workflow-errors

**API Endpoint**:
```bash
curl -H "Cookie: session=..." \
  http://localhost:8080/api/v1/admin/workflows/errors
```

**Database Query**:
```sql
SELECT * FROM workflow_error_logs
ORDER BY created_at DESC
LIMIT 10;
```

### 3. Verify Sensitive Data Redaction

Test error with sensitive data:
```json
{
  "errorMessage": "API call failed: API_KEY=sk-1234567890 password='secret'"
}
```

Verify in database:
```sql
SELECT error_message FROM workflow_error_logs WHERE id = '...';
-- Should show: "API call failed: API_KEY=***REDACTED*** password=***REDACTED***"
```

## Environment Variables

Add to `infra/env/n8n.env` (optional):

```bash
# Slack webhook for error alerts (optional)
SLACK_WEBHOOK_URL=https://hooks.slack.com/services/YOUR/WEBHOOK/URL

# Custom timeout for MeepleAI API calls (milliseconds)
MEEPLEAI_API_TIMEOUT=60000

# Error logging endpoint
MEEPLEAI_ERROR_LOG_URL=http://api:8080/api/v1/logs/workflow-error
```

## Monitoring and Alerting

### Admin Dashboard Monitoring

Access: http://localhost:3000/admin/workflow-errors

**Features**:
- Real-time error list with pagination
- Filter by workflow ID, date range
- View error details (message, stack trace, retry count)
- Sort by timestamp (newest first)

### Prometheus Metrics (Future)

If OPS-07 alerting is implemented:

```yaml
# Alert on high workflow error rate
- alert: HighWorkflowErrorRate
  expr: rate(workflow_errors_total[5m]) > 0.1
  for: 5m
  labels:
    severity: warning
  annotations:
    summary: "High n8n workflow error rate"
    description: "{{ $value }} errors/sec in last 5 minutes"
```

## Troubleshooting

### Error Logging Not Working

1. **Check API endpoint**:
   ```bash
   curl -X POST http://localhost:8080/api/v1/logs/workflow-error \
     -H "Content-Type: application/json" \
     -d '{"workflowId":"test","executionId":"test","errorMessage":"test"}'
   ```

2. **Check n8n network connectivity**:
   ```bash
   docker exec -it meepleai-n8n-1 ping api
   ```

3. **Verify API logs**:
   ```bash
   docker compose logs api | grep "workflow-error"
   ```

### Slack Alerts Not Sending

1. **Verify webhook URL**:
   ```bash
   curl -X POST https://hooks.slack.com/services/YOUR/WEBHOOK/URL \
     -H "Content-Type: application/json" \
     -d '{"text":"Test alert"}'
   ```

2. **Check n8n environment variable**:
   - Open n8n UI → Settings → Variables
   - Verify `SLACK_WEBHOOK_URL` exists

### Retry Logic Not Working

1. **Check HTTP Request node settings**:
   - Open workflow → Edit HTTP Request node
   - Verify "Retry" settings in "Request Options"

2. **Check error trigger**:
   - Ensure Error Trigger node exists
   - Set to "All Errors"

## Best Practices

1. **Always Configure Retries**: All external API calls should have retry logic
2. **Use Exponential Backoff**: Prevents overwhelming downstream services
3. **Log All Persistent Failures**: After max retries exceeded
4. **Provide Fallback Responses**: Never leave users without feedback
5. **Monitor Error Rates**: Set up alerts for abnormal error rates
6. **Test Error Paths**: Regularly simulate failures to verify error handling
7. **Keep Error Messages User-Friendly**: Don't expose technical details to end users
8. **Sanitize Before Logging**: Backend automatically redacts sensitive data

## Related Documentation

- **Backend Implementation**: `docs/issue/n8n-05-backend-implementation.md`
- **Admin Dashboard**: `apps/web/src/pages/admin/workflow-errors.tsx`
- **API Documentation**: `CLAUDE.md` - N8N-05 section
- **n8n Official Docs**: https://docs.n8n.io/error-handling/

---

**Implementation Checklist**:
- [ ] Add Error Trigger node to all workflows
- [ ] Configure retry logic on HTTP Request nodes (✅ Already done)
- [ ] Add error logging to MeepleAI API
- [ ] (Optional) Configure Slack webhook and alerts
- [ ] Test error handling with simulated failures
- [ ] Verify errors appear in admin dashboard
- [ ] Document workflow-specific error responses

**Status**: Backend ready, workflow configuration required via n8n UI
