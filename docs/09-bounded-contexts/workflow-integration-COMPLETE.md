# WorkflowIntegration Bounded Context - Complete API Reference

**n8n Workflow Integration, Webhooks, Error Logging**

> 📖 **Complete Documentation**: Part of Issue #3794

---

## 📋 Responsabilità

- n8n workflow configuration e management
- Webhook integration (eventi domain → n8n triggers)
- Workflow error logging e monitoring
- Template import/export
- Connection testing e validation
- Event-driven automation triggers

---

## 🏗️ Domain Model

### Aggregates

**N8NConfiguration** (Aggregate Root):
```csharp
public class N8NConfiguration
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public WorkflowUrl BaseUrl { get; private set; }       // Value Object
    public string ApiKeyEncrypted { get; private set; }    // Encrypted at rest
    public WorkflowUrl? WebhookUrl { get; private set; }   // Optional webhook endpoint
    public bool IsActive { get; private set; }
    public DateTime? LastTestedAt { get; private set; }
    public string? LastTestResult { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public Guid CreatedByUserId { get; private set; }

    // Domain methods
    public void RecordTestResult(bool success, string message) { }
    public void UpdateConfiguration(WorkflowUrl? baseUrl, string? apiKey, WorkflowUrl? webhookUrl) { }
    public void Activate() { }
    public void Deactivate() { }
}
```

**WorkflowErrorLog** (Aggregate Root):
```csharp
public class WorkflowErrorLog
{
    public Guid Id { get; private set; }
    public string WorkflowId { get; private set; }         // n8n workflow ID
    public string ExecutionId { get; private set; }        // n8n execution ID
    public string ErrorMessage { get; private set; }
    public string? NodeName { get; private set; }          // Which n8n node failed
    public int RetryCount { get; private set; }
    public string? StackTrace { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Domain methods
    public void IncrementRetry() { }
}
```

### Value Objects

**WorkflowUrl**:
```csharp
public record WorkflowUrl
{
    public string Value { get; init; }

    public static WorkflowUrl Create(string url)
    {
        // Validation: HTTP/HTTPS only, valid URI format
        // Throws: ArgumentException if invalid
    }
}
```

---

## 📡 Application Layer (CQRS)

> **Total Operations**: 17 (8 commands + 9 queries)

---

### Configuration Management

| Command/Query | HTTP Method | Endpoint | Auth | Request | Response |
|---------------|-------------|----------|------|---------|----------|
| `CreateN8NConfigCommand` | POST | `/api/v1/admin/workflows/n8n` | Admin | `CreateN8NConfigDto` | `N8NConfigurationDto` (201) |
| `UpdateN8NConfigCommand` | PUT | `/api/v1/admin/workflows/n8n/{id}` | Admin | `UpdateN8NConfigDto` | `N8NConfigurationDto` |
| `DeleteN8NConfigCommand` | DELETE | `/api/v1/admin/workflows/n8n/{id}` | Admin | None | 204 No Content |
| `TestN8nConnectionCommand` | POST | `/api/v1/admin/workflows/n8n/{id}/test` | Admin | None | `{ success: bool, message: string }` |
| `GetActiveN8nConfigQuery` | GET | `/api/v1/admin/workflows/n8n/active` | Admin | None | `N8NConfigurationDto` |
| `GetAllN8nConfigsQuery` | GET | `/api/v1/admin/workflows/n8n` | Admin | Query: page, pageSize | `PaginatedList<N8NConfigurationDto>` |
| `GetN8nConfigByIdQuery` | GET | `/api/v1/admin/workflows/n8n/{id}` | Admin | None | `N8NConfigurationDto` |

**CreateN8NConfigCommand**:
- **Purpose**: Configure new n8n instance integration
- **Request Schema**:
  ```json
  {
    "name": "Production n8n",
    "baseUrl": "https://n8n.meepleai.dev",
    "apiKey": "n8n_api_key_here",
    "webhookUrl": "https://n8n.meepleai.dev/webhook/meepleai"
  }
  ```
- **Security**:
  - API key encrypted before storage (AES-256)
  - API key NEVER returned in responses (masked as "***")
- **Side Effects**: Deactivates other configs if setting as active
- **Domain Events**: `N8NConfigurationCreatedEvent`

**TestN8nConnectionCommand**:
- **Purpose**: Validate n8n connectivity and API access
- **Behavior**:
  - Makes HTTP call to n8n `/healthz` endpoint
  - Tests API key authentication
  - Records result in LastTestedAt + LastTestResult
- **Response Schema**:
  ```json
  {
    "success": true,
    "message": "Connected to n8n v0.220.0",
    "testedAt": "2026-02-07T12:00:00Z"
  }
  ```

---

### Template Management

| Command/Query | HTTP Method | Endpoint | Auth | Request | Response |
|---------------|-------------|----------|------|---------|----------|
| `ImportN8nTemplateCommand` | POST | `/api/v1/admin/workflows/templates/import` | Admin | `ImportTemplateDto` | `{ templateId, message }` |
| `GetN8nTemplatesQuery` | GET | `/api/v1/admin/workflows/n8n/{configId}/templates` | Admin | None | `List<N8nTemplateDto>` |
| `GetN8nTemplateByIdQuery` | GET | `/api/v1/admin/workflows/n8n/{configId}/templates/{templateId}` | Admin | None | `N8nTemplateDto` |
| `ValidateN8nTemplateQuery` | POST | `/api/v1/admin/workflows/templates/{templateId}/validate` | Admin | None | `{ valid: bool, issues: [...] }` |

**ImportN8nTemplateCommand**:
- **Purpose**: Import n8n workflow template into MeepleAI
- **Request Schema**:
  ```json
  {
    "configId": "guid",
    "templateId": "n8n-template-id",
    "templateName": "PDF Processing Automation"
  }
  ```
- **Flow**:
  1. Fetches template from n8n API
  2. Validates template structure
  3. Imports as workflow definition in MeepleAI
- **Use Cases**: Automate PDF processing, share request notifications, etc.

---

### Error Logging

| Command/Query | HTTP Method | Endpoint | Auth | Request | Response |
|---------------|-------------|----------|------|---------|----------|
| `LogWorkflowErrorCommand` | POST | `/api/v1/admin/workflows/errors` | Admin | `LogWorkflowErrorDto` | `WorkflowErrorLogDto` (201) |
| `GetWorkflowErrorsQuery` | GET | `/api/v1/admin/workflows/errors` | Admin | Query: configId?, startDate?, endDate?, page, pageSize | `PaginatedList<WorkflowErrorLogDto>` |
| `GetWorkflowErrorByIdQuery` | GET | `/api/v1/admin/workflows/errors/{id}` | Admin | None | `WorkflowErrorLogDto` |

**LogWorkflowErrorCommand**:
- **Purpose**: Record workflow execution failure for troubleshooting
- **Request Schema**:
  ```json
  {
    "workflowId": "n8n-workflow-id",
    "executionId": "n8n-execution-id",
    "errorMessage": "HTTP request failed: 500 Internal Server Error",
    "nodeName": "HTTP Request Node",
    "stackTrace": "Error: connect ECONNREFUSED..."
  }
  ```
- **Side Effects**:
  - Creates WorkflowErrorLog entry
  - If RetryCount > 3: raises alert to admins
  - Raises `WorkflowErrorLoggedEvent`
- **Domain Events**: `WorkflowErrorLoggedEvent`

**GetWorkflowErrorsQuery**:
- **Purpose**: Admin dashboard for workflow debugging
- **Filters**: ConfigId, date range, pagination
- **Response Schema**:
  ```json
  {
    "errors": [
      {
        "id": "guid",
        "workflowId": "workflow-123",
        "executionId": "exec-456",
        "errorMessage": "HTTP 500",
        "nodeName": "PDF Upload Node",
        "retryCount": 2,
        "createdAt": "2026-02-07T10:00:00Z"
      }
    ],
    "pagination": {...}
  }
  ```

---

## 🔄 Domain Events

| Event | When Raised | Payload | Subscribers |
|-------|-------------|---------|-------------|
| `N8NConfigurationCreatedEvent` | Config created | `{ ConfigId, Name, BaseUrl }` | Administration (audit) |
| `N8NConfigurationUpdatedEvent` | Config updated | `{ ConfigId, ChangedFields }` | Administration (audit) |
| `N8NConfigurationTestedEvent` | Connection tested | `{ ConfigId, Success, Message }` | Administration (log test results) |
| `WorkflowErrorLoggedEvent` | Error recorded | `{ WorkflowId, ErrorMessage, RetryCount }` | UserNotifications (alert admins if retries exhausted) |
| `WorkflowRetriedEvent` | Retry attempt | `{ ExecutionId, RetryCount }` | Administration (tracking) |

---

## 🎯 Common Usage Examples

### Example 1: Configure n8n Integration

**Create Configuration**:
```bash
curl -X POST http://localhost:8080/api/v1/admin/workflows/n8n \
  -H "Content-Type: application/json" \
  -H "Cookie: meepleai_session_dev={admin_token}" \
  -d '{
    "name": "Production n8n",
    "baseUrl": "https://n8n.meepleai.dev",
    "apiKey": "n8n_api_abc123...",
    "webhookUrl": "https://n8n.meepleai.dev/webhook/meepleai"
  }'
```

**Test Connection**:
```bash
curl -X POST http://localhost:8080/api/v1/admin/workflows/n8n/{configId}/test \
  -H "Cookie: meepleai_session_dev={admin_token}"
```

**Response**:
```json
{
  "success": true,
  "message": "Connected to n8n v0.220.0",
  "testedAt": "2026-02-07T12:00:00Z"
}
```

---

### Example 2: Import n8n Template

```bash
curl -X POST http://localhost:8080/api/v1/admin/workflows/templates/import \
  -H "Content-Type: application/json" \
  -H "Cookie: meepleai_session_dev={admin_token}" \
  -d '{
    "configId": "guid",
    "templateId": "n8n-template-123",
    "templateName": "PDF Processing Automation"
  }'
```

---

### Example 3: Query Workflow Errors

```bash
curl -X GET "http://localhost:8080/api/v1/admin/workflows/errors?startDate=2026-02-01&endDate=2026-02-07" \
  -H "Cookie: meepleai_session_dev={admin_token}"
```

---

## 📂 Code Location

`apps/api/src/Api/BoundedContexts/WorkflowIntegration/`

---

**Status**: 🚧 Beta
**Last Updated**: 2026-02-07
**Total Commands**: 8
**Total Queries**: 9
**Event Handlers**: 5
**Integration Points**: 4 contexts
