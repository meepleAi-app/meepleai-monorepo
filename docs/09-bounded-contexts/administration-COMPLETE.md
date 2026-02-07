# Administration Bounded Context - Complete API Reference

**User Management, System Configuration, Audit Logs, Analytics, Token Management, Batch Jobs, Alert System, AI Model Admin**

> 📖 **Complete Documentation**: Part of Issue #3794
> 🏆 **Most Complex Context**: 100 operations (38 commands + 62 queries), 150+ endpoints, 19 workflow areas

---

## 📋 Responsabilità

**User & Access Management**:
- User CRUD (create, update, suspend, delete)
- Role management (User, Editor, Admin)
- Tier management (Free, Basic, Premium, Enterprise)
- Account lockout administration
- Bulk user operations (import, export, password reset, role changes)
- User impersonation (debugging)

**Token & Billing**:
- Token management system (Issue #3692 - balances, consumption, credits)
- Financial ledger (Epic 4 - Issues #3720-#3724)
- Usage tracking per user/tier/service
- Cost analysis and forecasting

**AI System Administration**:
- AI model CRUD + tier routing (Issues #2567, #2596)
- Agent typology approval workflow (Issue #3381)
- Prompt template management + versioning
- Prompt evaluation + comparison
- RAG pipeline builder (visual strategy editor - Issues #3463-#3464)
- Agent metrics dashboard (Issue #3382)

**System Operations**:
- Audit logging (Issue #3691)
- Alert management system (rules, execution, history)
- Batch job system (Epic 1 - Issue #3693)
- Health monitoring + infrastructure metrics
- Analytics & reporting (usage, performance, quality)
- Configuration management (PDF limits, tier limits)

**Development & Testing**:
- Test data seeding (E2E users, admin users)
- Chess knowledge indexing (specialized domain)
- Error simulation (diagnostics)

---

## 🏗️ Domain Model

### User Management Aggregates

**User** (Referenced from Authentication):
```csharp
// Extended with Administration concerns
public class User
{
    // ... (Authentication fields)

    // Administration fields
    public UserTier Tier { get; private set; }        // Free | Basic | Premium | Enterprise
    public int Level { get; private set; }            // Gamification level
    public bool IsSuspended { get; private set; }
    public DateTime? SuspendedAt { get; private set; }
    public string? SuspensionReason { get; private set; }

    // Admin methods
    public void SetTier(UserTier tier) { }
    public void SetLevel(int level) { }
    public void Suspend(string reason) { }
    public void Unsuspend() { }
}
```

**AuditLog** (Aggregate Root - Issue #3691):
```csharp
public class AuditLog
{
    public Guid Id { get; private set; }
    public Guid? AdminUserId { get; private set; }
    public Guid? TargetUserId { get; private set; }
    public string Action { get; private set; }          // "CreateUser", "ApproveGame", etc.
    public string? Resource { get; private set; }       // "User", "SharedGame", "ApiKey"
    public Guid? ResourceId { get; private set; }
    public string? Result { get; private set; }         // "Success" | "Failure"
    public string? Details { get; private set; }        // JSON details
    public string? IpAddress { get; private set; }
    public DateTime Timestamp { get; private set; }
}
```

### Token Management Aggregates

**TokenBalance** (Aggregate Root - Issue #3692):
```csharp
public class TokenBalance
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public long AvailableTokens { get; private set; }
    public long ConsumedTokens { get; private set; }
    public long TotalAllocated { get; private set; }
    public DateTime LastUpdatedAt { get; private set; }

    // Domain methods
    public void AddCredits(long amount, string reason) { }
    public void ConsumeTokens(long amount, string operation) { }
    public bool HasSufficientBalance(long required) { }
}
```

**TokenTransaction** (Entity):
```csharp
public class TokenTransaction
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public long Amount { get; private set; }            // Positive = credit, Negative = debit
    public string Operation { get; private set; }        // "RAG Query", "Agent Invoke", "Manual Credit"
    public string? Metadata { get; private set; }        // JSON
    public DateTime CreatedAt { get; private set; }
}
```

### Batch Job System (Epic 1 - Issue #3693)

**BatchJob** (Aggregate Root):
```csharp
public class BatchJob
{
    public Guid Id { get; private set; }
    public string JobType { get; private set; }         // "UserSeeding", "DataExport", etc.
    public Guid CreatedBy { get; private set; }
    public BatchJobStatus Status { get; private set; }  // Pending | Running | Completed | Failed | Cancelled
    public string? Parameters { get; private set; }     // JSON job config
    public int TotalItems { get; private set; }
    public int ProcessedItems { get; private set; }
    public int FailedItems { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? ErrorMessage { get; private set; }

    // Domain methods
    public void Start() { }
    public void RecordProgress(int processed, int failed) { }
    public void Complete() { }
    public void Fail(string error) { }
    public void Cancel() { }
}
```

### Alert System

**AlertRule** (Aggregate Root):
```csharp
public class AlertRule
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public string RuleExpression { get; private set; }  // SQL-like or logic expression
    public AlertSeverity Severity { get; private set; } // Info | Warning | Critical
    public bool IsEnabled { get; private set; }
    public string? EmailRecipients { get; private set; } // Comma-separated
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastTriggeredAt { get; private set; }

    // Domain methods
    public void Enable() { }
    public void Disable() { }
    public void RecordTrigger() { }
}
```

**Alert** (Entity):
```csharp
public class Alert
{
    public Guid Id { get; private set; }
    public Guid AlertRuleId { get; private set; }
    public AlertSeverity Severity { get; private set; }
    public string Message { get; private set; }
    public bool IsResolved { get; private set; }
    public DateTime TriggeredAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public Guid? ResolvedBy { get; private set; }

    // Domain methods
    public void Resolve(Guid adminId) { }
}
```

---

## 📡 Application Layer (CQRS)

> **Total Operations**: 100 (38 commands + 62 queries)
> **Workflow Areas**: 19 (User mgmt, Tokens, Batch jobs, Alerts, AI models, Analytics, etc.)

---

### USER MANAGEMENT

| Command/Query | HTTP Method | Endpoint | Auth | Purpose |
|---------------|-------------|----------|------|---------|
| `SearchUsersQuery` | GET | `/api/v1/users/search` | Session | Search users (@mention autocomplete) |
| `GetAllUsersQuery` | GET | `/api/v1/admin/users` | Admin | List all users with filters |
| `GetUserByIdQuery` | GET | `/api/v1/admin/users/{userId}` | Admin | User details |
| `CreateUserCommand` | POST | `/api/v1/admin/users` | Admin | Create user |
| `UpdateUserCommand` | PUT | `/api/v1/admin/users/{id}` | Admin | Update user |
| `DeleteUserCommand` | DELETE | `/api/v1/admin/users/{id}` | Admin | Soft-delete user |
| `SetUserLevelCommand` | PATCH | `/api/v1/admin/users/{userId}/level` | Admin | Set gamification level |
| `UpdateUserTierCommand` | PUT | `/api/v1/admin/users/{id}/tier` | Admin | Update subscription tier |
| `SuspendUserCommand` | POST | `/api/v1/admin/users/{id}/suspend` | Admin | Suspend account |
| `UnsuspendUserCommand` | POST | `/api/v1/admin/users/{id}/unsuspend` | Admin | Reactivate account |
| `GetUserActivityQuery` | GET | `/api/v1/admin/users/{userId}/activity` | Admin | Activity timeline |
| `GetUserLibraryStatsQuery` | GET | `/api/v1/admin/users/{userId}/library/stats` | Admin | Library statistics |
| `GetUserBadgesQuery` | GET | `/api/v1/admin/users/{userId}/badges` | Admin | All badges (visible + hidden) |
| `GetUserRoleHistoryQuery` | GET | `/api/v1/admin/users/{userId}/role-history` | Admin | Role change history |
| `ResetUserPasswordCommand` | POST | `/api/v1/admin/users/{userId}/reset-password` | Admin | Force password reset |
| `SendUserEmailCommand` | POST | `/api/v1/admin/users/{userId}/send-email` | Admin | Send custom email |
| `ImpersonateUserCommand` | POST | `/api/v1/admin/users/{userId}/impersonate` | Admin | Debug as user |
| `EndImpersonationCommand` | POST | `/api/v1/admin/impersonation/end` | Admin | End impersonation |

**GetAllUsersQuery**:
- **Query Parameters**:
  - `search`: Email/name filter
  - `tier`: Free | Basic | Premium | Enterprise
  - `role`: User | Editor | Admin
  - `isActive`: true | false
  - `isSuspended`: true | false
  - `page`, `pageSize`: Pagination
- **Response**: Paginated user list with stats

**SuspendUserCommand**:
- **Request Schema**:
  ```json
  {
    "reason": "Violated community guidelines - spam submissions"
  }
  ```
- **Side Effects**:
  - Sets IsSuspended = true, SuspendedAt = UtcNow
  - Invalidates all user sessions
  - Raises `UserSuspendedEvent` → UserNotifications
- **Domain Events**: `UserSuspendedEvent`

---

### BULK OPERATIONS (Issue #905)

| Command/Query | HTTP Method | Endpoint | Auth | Purpose |
|---------------|-------------|----------|------|---------|
| `BulkPasswordResetCommand` | POST | `/api/v1/admin/users/bulk/password-reset` | Admin | Reset passwords for multiple users |
| `BulkRoleChangeCommand` | POST | `/api/v1/admin/users/bulk/role-change` | Admin | Change roles in batch |
| `BulkImportUsersCommand` | POST | `/api/v1/admin/users/bulk/import` | Admin | Import from CSV |
| `BulkExportUsersQuery` | GET | `/api/v1/admin/users/bulk/export` | Admin | Export to CSV |

**BulkImportUsersCommand**:
- **Request**: CSV file with headers
  ```csv
  Email,DisplayName,Role,Tier
  alice@example.com,Alice,User,Free
  bob@example.com,Bob,Editor,Premium
  ```
- **Response**:
  ```json
  {
    "successCount": 98,
    "failedCount": 2,
    "errors": [
      {"row": 5, "error": "Email already exists"},
      {"row": 12, "error": "Invalid tier"}
    ]
  }
  ```
- **Behavior**: Partial success allowed

---

### AUDIT LOGGING (Issue #3691)

| Query | HTTP Method | Endpoint | Auth | Purpose |
|-------|-------------|----------|------|---------|
| `GetAuditLogsQuery` | GET | `/api/v1/admin/audit-log` | Admin | Filtered audit log retrieval |
| `ExportAuditLogsQuery` | GET | `/api/v1/admin/audit-log/export` | Admin | Export to CSV/JSON |

**GetAuditLogsQuery**:
- **Query Parameters**:
  - `limit`, `offset`: Pagination
  - `adminUserId`: Filter by admin who performed action
  - `action`: Filter by action type
  - `resource`: Filter by resource type
  - `result`: Success | Failure
  - `startDate`, `endDate`: Date range
- **Response Schema**:
  ```json
  {
    "logs": [
      {
        "id": "guid",
        "adminUserId": "guid",
        "adminName": "Alice Admin",
        "action": "ApproveShareRequest",
        "resource": "ShareRequest",
        "resourceId": "guid",
        "result": "Success",
        "details": "{\"gameTitle\":\"Azul\"}",
        "ipAddress": "192.168.1.1",
        "timestamp": "2026-02-07T10:00:00Z"
      }
    ],
    "pagination": {...}
  }
  ```

**Audit Decorator**: `[AuditableAction]` attribute marks commands for auto-logging

---

### TOKEN MANAGEMENT SYSTEM (Issue #3692)

| Command/Query | HTTP Method | Endpoint | Auth | Purpose |
|---------------|-------------|----------|------|---------|
| `GetTokenBalanceQuery` | GET | `/api/v1/admin/tokens/balance/{userId}` | Admin | User token balance |
| `GetTokenConsumptionQuery` | GET | `/api/v1/admin/tokens/consumption` | Admin | Consumption by user/service |
| `GetTokenTierUsageQuery` | GET | `/api/v1/admin/tokens/tier-usage` | Admin | Usage by subscription tier |
| `AddTokenCreditsCommand` | POST | `/api/v1/admin/tokens/credits` | Admin | Add credits to user |
| `GetTopConsumersQuery` | GET | `/api/v1/admin/tokens/top-consumers` | Admin | Top token consumers |

**GetTokenBalanceQuery**:
- **Response**:
  ```json
  {
    "userId": "guid",
    "availableTokens": 500000,
    "consumedTokens": 1500000,
    "totalAllocated": 2000000,
    "percentageUsed": 75.0,
    "lastUpdatedAt": "2026-02-07T14:00:00Z"
  }
  ```

**AddTokenCreditsCommand**:
- **Request Schema**:
  ```json
  {
    "userId": "guid",
    "amount": 100000,
    "reason": "Premium subscription renewal"
  }
  ```
- **Side Effects**: Creates TokenTransaction record

**GetTopConsumersQuery**:
- **Query Parameters**: `limit` (default: 10), `startDate`, `endDate`
- **Response**: Ranked list of users by token consumption

---

### BATCH JOB SYSTEM (Epic 1 - Issue #3693)

| Command/Query | HTTP Method | Endpoint | Auth | Purpose |
|---------------|-------------|----------|------|---------|
| `CreateBatchJobCommand` | POST | `/api/v1/admin/batch-jobs` | Admin | Create batch job |
| `GetBatchJobQuery` | GET | `/api/v1/admin/batch-jobs/{id}` | Admin | Job details + progress |
| `GetAllBatchJobsQuery` | GET | `/api/v1/admin/batch-jobs` | Admin | List jobs with pagination |
| `CancelBatchJobCommand` | POST | `/api/v1/admin/batch-jobs/{id}/cancel` | Admin | Cancel running job |
| `RetryBatchJobCommand` | POST | `/api/v1/admin/batch-jobs/{id}/retry` | Admin | Retry failed job |
| `DeleteBatchJobCommand` | DELETE | `/api/v1/admin/batch-jobs/{id}` | Admin | Delete job record |

**CreateBatchJobCommand**:
- **Request Schema**:
  ```json
  {
    "jobType": "BulkImportGames",
    "parameters": {
      "sourceFile": "bgg_top_100.csv",
      "autoApprove": false
    }
  }
  ```
- **Job Types**:
  - UserSeeding: Bulk user creation
  - DataExport: Generate CSV/JSON exports
  - BulkProcessing: Mass operations
  - ReportGeneration: Scheduled reports
- **Response**: Job ID for tracking

**GetBatchJobQuery**:
- **Response Schema**:
  ```json
  {
    "id": "guid",
    "jobType": "BulkImportGames",
    "status": "Running",
    "totalItems": 100,
    "processedItems": 67,
    "failedItems": 3,
    "progress": 67,
    "createdAt": "2026-02-07T10:00:00Z",
    "estimatedCompletion": "2026-02-07T10:15:00Z"
  }
  ```

---

### ALERT MANAGEMENT SYSTEM

#### Alert Rules

| Command/Query | HTTP Method | Endpoint | Auth | Purpose |
|---------------|-------------|----------|------|---------|
| `CreateAlertRuleCommand` | POST | `/api/v1/admin/alerts/rules` | Admin | Create alert rule |
| `UpdateAlertRuleCommand` | PUT | `/api/v1/admin/alerts/rules/{id}` | Admin | Update rule |
| `DeleteAlertRuleCommand` | DELETE | `/api/v1/admin/alerts/rules/{id}` | Admin | Delete rule |
| `EnableAlertRuleCommand` | POST | `/api/v1/admin/alerts/rules/{id}/enable` | Admin | Enable rule |
| `GetAlertRuleByIdQuery` | GET | `/api/v1/admin/alerts/rules/{id}` | Admin | Rule details |
| `GetAllAlertRulesQuery` | GET | `/api/v1/admin/alerts/rules` | Admin | List rules |
| `GetAlertHistoryQuery` | GET | `/api/v1/admin/alerts/history` | Admin | Rule execution history |
| `GetActiveAlertsQuery` | GET | `/api/v1/admin/alerts/active` | Admin | Currently triggered alerts |

**CreateAlertRuleCommand**:
- **Request Schema**:
  ```json
  {
    "name": "High Token Usage",
    "description": "Alert when user exceeds 80% of quota",
    "ruleExpression": "token_usage_percentage > 80",
    "severity": "Warning",
    "emailRecipients": "admin@meepleai.dev,ops@meepleai.dev"
  }
  ```
- **Rule Expressions**: SQL-like syntax for metric evaluation
- **Execution**: Scheduled job checks rules periodically (every 5 min)

---

#### Alert Execution

| Command | HTTP Method | Endpoint | Auth | Purpose |
|---------|-------------|----------|------|---------|
| `TestAlertCommand` | POST | `/api/v1/admin/alerts/test/{id}` | Admin | Test rule execution |
| `SendAlertCommand` | POST | `/api/v1/admin/alerts/send` | Admin | Send alert notification |
| `ResolveAlertCommand` | POST | `/api/v1/admin/alerts/{id}/resolve` | Admin | Mark alert resolved |

**TestAlertCommand**:
- **Response**: Dry-run result showing if rule would trigger + affected entities

---

### AI MODEL ADMINISTRATION (Issues #2567, #2596)

| Command/Query | HTTP Method | Endpoint | Auth | Purpose |
|---------------|-------------|----------|------|---------|
| `GetAllAiModelsQuery` | GET | `/api/v1/admin/ai-models` | Admin | List models with filters |
| `GetAiModelByIdQuery` | GET | `/api/v1/admin/ai-models/{id}` | Admin | Model details |
| `CreateAiModelCommand` | POST | `/api/v1/admin/ai-models` | Admin | Create model |
| `UpdateAiModelCommand` | PUT | `/api/v1/admin/ai-models/{id}` | Admin | Update model |
| `DeleteAiModelCommand` | DELETE | `/api/v1/admin/ai-models/{id}` | Admin | Delete model |
| `UpdateModelPriorityCommand` | PATCH | `/api/v1/admin/ai-models/{id}/priority` | Admin | Set priority (1=primary) |
| `ToggleAiModelActiveCommand` | PATCH | `/api/v1/admin/ai-models/{id}/toggle` | Admin | Toggle active status |
| `GetTierRoutingQuery` | GET | `/api/v1/admin/tier-routing` | Admin | Tier-based model routing |
| `UpdateTierRoutingCommand` | PUT | `/api/v1/admin/tier-routing` | Admin | Configure tier routing |

**CreateAiModelCommand**:
- **Request Schema**:
  ```json
  {
    "name": "GPT-4 Turbo",
    "providerId": "openrouter",
    "modelId": "openai/gpt-4-turbo",
    "priority": 1,
    "isActive": true,
    "maxTokens": 128000,
    "costPerMillionTokens": 10.00
  }
  ```

**UpdateTierRoutingCommand**:
- **Purpose**: Configure which models each tier can use
- **Request Schema**:
  ```json
  {
    "tier": "Premium",
    "allowedModels": ["gpt-4", "claude-3-sonnet"],
    "defaultModel": "gpt-4",
    "fallbackModel": "haiku"
  }
  ```

---

### AGENT TYPOLOGY APPROVAL (Issue #3381)

| Command/Query | HTTP Method | Endpoint | Auth | Purpose |
|---------------|-------------|----------|------|---------|
| `CreateAgentTypologyWithPhaseModelsCommand` | POST | `/api/v1/admin/agent-typologies` | Admin | Create with phase-model config |
| `UpdateAgentTypologyCommand` | PUT | `/api/v1/admin/agent-typologies/{id}/phase-models` | Admin | Update phase models |
| `ApproveAgentTypologyCommand` | PUT | `/api/v1/admin/agent-typologies/{id}/approve` | Admin | Approve typology |
| `RejectAgentTypologyCommand` | PUT | `/api/v1/admin/agent-typologies/{id}/reject` | Admin | Reject typology |
| `GetPendingTypologiesCountQuery` | GET | `/api/v1/admin/agent-typologies/pending/count` | Admin | Pending count (badge) |

*(See KnowledgeBase context for full AgentTypology details)*

---

### AGENT METRICS DASHBOARD (Issue #3382)

| Query | HTTP Method | Endpoint | Auth | Purpose |
|-------|-------------|----------|------|---------|
| `GetAgentMetricsQuery` | GET | `/api/v1/admin/agents/metrics` | Admin | Aggregated agent metrics |
| `GetSingleAgentMetricsQuery` | GET | `/api/v1/admin/agents/metrics/{id}` | Admin | Single typology metrics |
| `GetTopAgentsQuery` | GET | `/api/v1/admin/agents/metrics/top` | Admin | Top agents by usage/cost |

**GetAgentMetricsQuery**:
- **Filters**: startDate, endDate, typologyId, strategy
- **Response**: Invocations, tokens, cost, latency, confidence, time series

---

### PROMPT MANAGEMENT

#### Templates & Versions

| Command/Query | HTTP Method | Endpoint | Auth | Purpose |
|---------------|-------------|----------|------|---------|
| `CreatePromptTemplateCommand` | POST | `/api/v1/admin/prompts/templates` | Admin | Create template |
| `GetPromptTemplatesQuery` | GET | `/api/v1/admin/prompts/templates` | Admin | List templates |
| `GetPromptTemplateByIdQuery` | GET | `/api/v1/admin/prompts/templates/{id}` | Admin | Template details |
| `CreatePromptVersionCommand` | POST | `/api/v1/admin/prompts/{id}/versions` | Admin | Create version |
| `ActivatePromptVersionCommand` | POST | `/api/v1/admin/prompts/{id}/activate` | Admin | Set active version |
| `GetPromptVersionHistoryQuery` | GET | `/api/v1/admin/prompts/{id}/versions` | Admin | Version history |
| `GetActivePromptVersionQuery` | GET | `/api/v1/admin/prompts/{id}/active` | Admin | Active version |

---

#### Evaluation & Comparison

| Command/Query | HTTP Method | Endpoint | Auth | Purpose |
|---------------|-------------|----------|------|---------|
| `EvaluatePromptCommand` | POST | `/api/v1/admin/prompts/evaluate` | Admin | Run evaluation tests |
| `ComparePromptVersionsCommand` | POST | `/api/v1/admin/prompts/compare` | Admin | Compare 2 versions |
| `GetEvaluationHistoryQuery` | GET | `/api/v1/admin/prompts/evaluation/history` | Admin | Evaluation results |
| `GetPromptAuditLogQuery` | GET | `/api/v1/admin/prompts/audit` | Admin | Audit trail |

**EvaluatePromptCommand**:
- **Purpose**: Test prompt with evaluation dataset
- **Request Schema**:
  ```json
  {
    "promptVersionId": "guid",
    "testDataset": "azul_rules_20q",
    "metrics": ["accuracy", "confidence", "latency"]
  }
  ```
- **Response**: Accuracy %, avg confidence, avg latency

---

### RAG PIPELINE BUILDER (Issues #3463-#3464)

| Command/Query | HTTP Method | Endpoint | Auth | Purpose |
|---------------|-------------|----------|------|---------|
| `GetRagPipelineStrategiesQuery` | GET | `/api/v1/admin/rag-pipeline/strategies` | Admin | List strategies + templates |
| `GetRagPipelineStrategyByIdQuery` | GET | `/api/v1/admin/rag-pipeline/strategies/{id}` | Admin | Strategy with nodes/edges |
| `SaveRagPipelineStrategyCommand` | POST/PUT | `/api/v1/admin/rag-pipeline/strategies` | Admin | Create/update strategy |
| `DeleteRagPipelineStrategyCommand` | DELETE | `/api/v1/admin/rag-pipeline/strategies/{id}` | Admin | Delete strategy |
| Export Strategy | GET | `/api/v1/admin/rag-pipeline/strategies/{id}/export` | Admin | Export JSON |
| Import Strategy | POST | `/api/v1/admin/rag-pipeline/strategies/import` | Admin | Import JSON |
| `TestRagPipelineCommand` | POST | `/api/v1/admin/rag-pipeline/test` | Admin | **SSE Stream** test events |

**TestRagPipelineCommand** (SSE):
- **Purpose**: Real-time RAG pipeline testing with visual feedback
- **Response**: Server-Sent Events stream
  ```
  event: PipelineTestStartedEvent
  data: {"strategyId":"guid","query":"test question"}

  event: BlockExecutionStartedEvent
  data: {"blockName":"Retrieval","blockType":"RetrievalBlock"}

  event: DocumentsRetrievedEvent
  data: {"count":5,"avgScore":0.87}

  event: ValidationResultEvent
  data: {"passed":true,"confidence":0.91}

  event: PipelineTestCompletedEvent
  data: {"success":true,"answer":"...","totalLatency":1340}
  ```

---

### ANALYTICS & REPORTING

#### Dashboard Stats

| Query | Endpoint | Purpose |
|-------|----------|---------|
| `GetAdminStatsQuery` | `/api/v1/admin/stats` | Dashboard overview |
| `GetAiUsageStatsQuery` | `/api/v1/admin/stats/ai-usage` | AI usage statistics |
| `GetApiRequestsByDayQuery` | `/api/v1/admin/stats/api-requests` | API trends |
| `GetPerformanceMetricsQuery` | `/api/v1/admin/stats/performance` | Performance KPIs |
| `GetE2EMetricsQuery` | `/api/v1/admin/stats/e2e` | E2E test results |
| `GetAccessibilityMetricsQuery` | `/api/v1/admin/stats/accessibility` | A11y compliance |
| `GetFeedbackStatsQuery` | `/api/v1/admin/stats/feedback` | User feedback analysis |
| `GetLowQualityResponsesQuery` | `/api/v1/admin/stats/low-quality` | Low-quality RAG responses |

**GetAdminStatsQuery**:
- **Response**:
  ```json
  {
    "users": {"total": 1250, "active": 890, "suspended": 15},
    "games": {"total": 450, "published": 380, "pending": 25},
    "tokens": {"consumed24h": 5000000, "cost24h": 125.50},
    "sessions": {"active": 42, "totalToday": 234},
    "apiRequests": {"today": 15234, "errors": 52}
  }
  ```

---

#### Reports

| Command/Query | Endpoint | Purpose |
|---------------|----------|---------|
| `GenerateReportCommand` | POST `/api/v1/admin/reports/generate` | Generate custom report |
| `ScheduleReportCommand` | POST `/api/v1/admin/reports/schedule` | Schedule recurring report |
| `UpdateReportScheduleCommand` | PUT `/api/v1/admin/reports/schedule/{id}` | Update schedule |
| `GetScheduledReportsQuery` | GET `/api/v1/admin/reports/scheduled` | List schedules |
| `GetReportExecutionsQuery` | GET `/api/v1/admin/reports/executions` | Execution history |
| `ExportStatsCommand` | POST `/api/v1/admin/stats/export` | Export stats to file |
| `GenerateQualityReportQuery` | - | Quality metrics report |
| `GenerateEvaluationReportQuery` | - | Evaluation report |

---

### SYSTEM CONFIGURATION

| Command/Query | HTTP Method | Endpoint | Auth | Purpose |
|---------------|-------------|----------|------|---------|
| `GetAllPdfLimitsQuery` | GET | `/api/v1/admin/config/pdf-limits` | Admin | PDF limits for all tiers |
| `UpdatePdfLimitsCommand` | PUT | `/api/v1/admin/config/pdf-limits/{tier}` | Admin | Update tier PDF limits |

---

### HEALTH MONITORING

| Query | Purpose |
|-------|---------|
| `GetInfrastructureHealthQuery` | Infrastructure status (DB, Redis, Qdrant, services) |
| `GetInfrastructureDetailsQuery` | Detailed infra metrics |
| `GetLlmHealthQuery` | LLM provider health status |
| `GetPrometheusMetricsQuery` | Prometheus metrics export |

---

### SPECIALIZED FEATURES

#### Chess Knowledge (Test Domain)

| Command/Query | Endpoint | Purpose |
|---------------|----------|---------|
| `IndexChessKnowledgeCommand` | POST `/api/v1/admin/chess/index` | Index chess knowledge to Qdrant |
| `SearchChessKnowledgeQuery` | GET `/api/v1/admin/chess/search` | Search chess knowledge |
| `DeleteChessKnowledgeCommand` | DELETE `/api/v1/admin/chess/index` | Clear chess index |

---

#### Test Data Seeding

| Command | Purpose |
|---------|---------|
| `SeedE2ETestUsersCommand` | Create E2E test users (dev only) |
| `SeedTestUserCommand` | Seed single test user |
| `SeedAdminUserCommand` | Seed admin user |

---

#### Diagnostics

| Command | Purpose |
|---------|---------|
| `SimulateErrorCommand` | Test error handling |
| `LogAiRequestCommand` | Manual AI request logging |

---

## 🔄 Domain Events

| Event | When Raised | Subscribers |
|-------|-------------|-------------|
| `UserSuspendedEvent` | User suspended | UserNotifications (email alert) |
| `UserUnsuspendedEvent` | User reactivated | UserNotifications |
| `TierChangedEvent` | Tier updated | TokenManagement (recalculate quotas) |
| `BatchJobCompletedEvent` | Job finished | Administration (cleanup) |
| `AlertTriggeredEvent` | Alert fired | UserNotifications (send emails) |
| `TypologyApprovedEvent` | Typology approved | UserNotifications (notify proposer) |

---

## 🔗 Integration Points

### Subscribes to Events From:

- **Authentication**: UserCreatedEvent, UserLoggedInEvent
- **KnowledgeBase**: AgentInvokedEvent (token tracking), TypologyProposedEvent
- **SharedGameCatalog**: GamePublishedEvent (badge calculation), BadgeEarnedEvent
- **All Contexts**: For audit logging ([AuditableAction] commands)

### Publishes Events To:

- **UserNotifications**: Alert notifications, suspension notices
- **KnowledgeBase**: Model configuration changes, typology approvals

---

## 🎯 Common Usage Example

**Suspend User**:
```bash
curl -X POST http://localhost:8080/api/v1/admin/users/user-guid/suspend \
  -H "Content-Type: application/json" \
  -H "Cookie: meepleai_session_dev={admin_token}" \
  -d '{
    "reason": "Spam submissions"
  }'
```

**Add Token Credits**:
```bash
curl -X POST http://localhost:8080/api/v1/admin/tokens/credits \
  -H "Content-Type: application/json" \
  -H "Cookie: meepleai_session_dev={admin_token}" \
  -d '{
    "userId": "guid",
    "amount": 100000,
    "reason": "Premium renewal"
  }'
```

**Create Batch Job**:
```bash
curl -X POST http://localhost:8080/api/v1/admin/batch-jobs \
  -H "Content-Type: application/json" \
  -H "Cookie: meepleai_session_dev={admin_token}" \
  -d '{
    "jobType": "BulkImportGames",
    "parameters": {"file": "bgg_top_100.csv"}
  }'
```

---

## 📂 Code Location

`apps/api/src/Api/BoundedContexts/Administration/`

**Endpoint Files** (11):
- AdminDashboardEndpoints.cs
- AdminUserManagementEndpoints.cs
- AlertManagementEndpoints.cs
- BatchJobEndpoints.cs
- AdminConfigurationEndpoints.cs
- AdminAuditLogEndpoints.cs
- AdminReportingEndpoints.cs
- (and 4 more specialized)

---

**Status**: ✅ Production
**Last Updated**: 2026-02-07
**Total Commands**: 38
**Total Queries**: 62
**Total Endpoints**: 150+
**Workflow Areas**: 19
