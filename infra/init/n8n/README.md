# n8n Workflow Setup

## Overview
This directory contains n8n workflow definitions for MeepleAI agent webhooks. These workflows orchestrate calls between external clients and the backend API, providing retry logic, logging, and standardized responses.

## Available Workflows

### 1. Agent Explain Webhook (`agent-explain-webhook.json`)
**Endpoint**: `POST /webhook/agent/explain`
**Status**: ✅ Implemented (N8N-01)
**Purpose**: Orchestrates RuleSpec+RAG to provide detailed explanations of game rules.

**Features**:
- Webhook trigger on `/agent/explain`
- Request ID generation and correlation tracking
- Automatic retry with exponential backoff (3 attempts)
- Retry on HTTP status codes: 429, 500, 502, 503, 504
- Structured logging with request metadata
- Standardized JSON response format
- Error handling with proper status codes

**Request Payload**:
```json
{
  "tenantId": "string",
  "gameId": "string",
  "topic": "string"
}
```

**Response Format**:
```json
{
  "success": true,
  "requestId": "n8n-1234567890-abc123",
  "timestamp": "2025-10-03T10:30:00.000Z",
  "data": {
    "outline": {
      "mainTopic": "string",
      "sections": ["section1", "section2"]
    },
    "script": "string",
    "citations": [
      {
        "text": "string",
        "source": "string",
        "page": 1,
        "line": 10
      }
    ],
    "estimatedReadingTimeMinutes": 10
  }
}
```

### 2. Agent Setup Webhook (N8N-02)
**Status**: ⏳ Pending

### 3. Agent Q&A Webhook (N8N-03)
**Status**: ⏳ Pending

## Setup Instructions

### 1. Start n8n
Ensure n8n is running via Docker Compose:
```bash
cd infra
docker compose up -d n8n
```

n8n will be available at: http://localhost:5678

### 2. Import Workflow
1. Open n8n at http://localhost:5678
2. Click "Add workflow" or "Import from File"
3. Select the workflow JSON file from `infra/init/n8n/`
4. Click "Import"
5. Activate the workflow by toggling the "Active" switch

### 3. Configure Webhook URL
After importing, the webhook will be available at:
```
http://localhost:5678/webhook/agent/explain
```

For production environments, configure your reverse proxy to route requests to this endpoint.

### 4. Test the Workflow
```bash
curl -X POST http://localhost:5678/webhook/agent/explain \
  -H "Content-Type: application/json" \
  -d '{
    "tenantId": "dev",
    "gameId": "catan",
    "topic": "setup phase"
  }'
```

## Workflow Architecture

Each workflow follows this pattern:

1. **Webhook Trigger**: Receives incoming HTTP requests
2. **Prepare Request**: Extracts payload, generates request ID, adds timestamp
3. **Call Backend API**: Forwards request to the backend API with retry logic
4. **Check Success**: Evaluates response status
5. **Format Response**: Structures success or error response
6. **Respond**: Returns standardized JSON response to client
7. **Log Request**: Logs request metadata and outcome

## Configuration

### Retry Policy
- **Max Attempts**: 3
- **Retry Conditions**: HTTP status codes 429, 500, 502, 503, 504
- **Timeout**: 60 seconds per request
- **Backoff**: Exponential (handled by n8n)

### Logging
All workflows log the following information:
- Request ID (correlation ID)
- Timestamp
- Tenant ID
- Game ID
- Request parameters
- Success/failure status
- Response time
- Error details (if applicable)

## Troubleshooting

### Workflow Not Responding
1. Check if workflow is activated in n8n UI
2. Verify n8n container is running: `docker compose ps n8n`
3. Check n8n logs: `docker compose logs n8n`

### API Connection Errors
1. Ensure backend API is running: `docker compose ps api`
2. Verify network connectivity: `docker compose exec n8n ping api`
3. Check API health: `docker compose exec n8n curl http://api:8080/`

### Request ID Not Generated
The workflow generates a request ID using either:
1. The `X-Correlation-Id` header from the incoming request (if present)
2. A generated ID in format: `n8n-{timestamp}-{random}`

## Development

### Adding a New Webhook Workflow
1. Create a new JSON file in `infra/init/n8n/`
2. Follow the naming convention: `agent-{name}-webhook.json`
3. Include all required nodes: trigger, prepare, call, check, format, respond, log
4. Update this README with the new workflow documentation
5. Test the workflow thoroughly before committing

### Workflow Versioning
- Each workflow includes a `versionId` field
- Update the version when making significant changes
- Document changes in the workflow's meta description
