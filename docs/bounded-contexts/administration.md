# Administration Bounded Context

**User Management, System Config, Audit Logs, Analytics, Token Management, Batch Jobs, Alerts, AI Model Admin**

---

## Responsibilities

**User & Access**:
- User CRUD (create, update, suspend, delete)
- Role management (User/Editor/Admin)
- Tier management (Free/Basic/Premium/Enterprise)
- Account lockout administration
- Bulk operations (import/export, password reset, role changes)
- User impersonation (debugging)

**Token & Billing**:
- Token management system (balances, consumption, credits)
- Financial ledger (Epic 4 - Issues #3720-#3724)
- Usage tracking per user/tier/service
- Cost analysis & forecasting

**AI System**:
- AI model CRUD + tier routing (Issues #2567, #2596)
- Agent typology approval workflow
- Prompt template management + versioning
- Prompt evaluation + comparison
- RAG pipeline builder (visual strategy editor)
- Agent metrics dashboard

**System Ops**:
- Audit logging (Issue #3691)
- Alert management (rules, execution, history)
- Batch job system (Epic 1 - Issue #3693)
- Health monitoring + infrastructure metrics
- Analytics & reporting (usage, performance, quality)
- Configuration management (PDF limits, tier limits)

**Dev & Testing**:
- Test data seeding (E2E users, admin users)
- Chess knowledge indexing (test domain)
- Error simulation (diagnostics)

---

## Domain Model

| Aggregate | Key Fields | Purpose |
|-----------|-----------|---------|
| **User** (extended) | Tier, Level, IsSuspended, SuspensionReason | Admin view of auth user |
| **AuditLog** | AdminUserId, TargetUserId, Action, Resource, Result, Details, IpAddress | Audit trail |
| **TokenBalance** | UserId, AvailableTokens, ConsumedTokens, TotalAllocated | Token tracking |
| **TokenTransaction** | UserId, Amount, Operation, Metadata | Transaction history |
| **BatchJob** | JobType, Status, TotalItems, ProcessedItems, FailedItems, ErrorMessage | Batch processing |
| **AlertRule** | Name, RuleExpression, Severity, IsEnabled, EmailRecipients | Alert configuration |
| **Alert** | AlertRuleId, Severity, Message, IsResolved, TriggeredAt | Alert instances |

**Value Objects**: UserTier (Free/Basic/Premium/Enterprise), BatchJobStatus (Pending/Running/Completed/Failed/Cancelled), AlertSeverity (Info/Warning/Critical)

**Domain Methods**: `SetTier()`, `Suspend()`, `Unsuspend()`, `AddCredits()`, `ConsumeTokens()`, `RecordProgress()`, `Complete()`, `Fail()`, `Enable()`, `RecordTrigger()`, `Resolve()`

---

## API Operations (100 total)

**38 Commands**: CreateUser, UpdateUser, DeleteUser, SetUserLevel, UpdateUserTier, SuspendUser, UnsuspendUser, ResetUserPassword, SendUserEmail, ImpersonateUser, EndImpersonation, BulkPasswordReset, BulkRoleChange, BulkImportUsers, AddTokenCredits, CreateBatchJob, CancelBatchJob, RetryBatchJob, DeleteBatchJob, CreateAlertRule, UpdateAlertRule, DeleteAlertRule, EnableAlertRule, TestAlert, SendAlert, ResolveAlert, CreateAiModel, UpdateAiModel, DeleteAiModel, UpdateModelPriority, ToggleAiModelActive, UpdateTierRouting, CreatePromptTemplate, CreatePromptVersion, ActivatePromptVersion, EvaluatePrompt, ComparePromptVersions, SaveRagPipelineStrategy

**62 Queries**: SearchUsers, GetAllUsers, GetUserById, GetUserActivity, GetUserLibraryStats, GetUserBadges, GetUserRoleHistory, BulkExportUsers, GetAuditLogs, ExportAuditLogs, GetTokenBalance, GetTokenConsumption, GetTokenTierUsage, GetTopConsumers, GetBatchJob, GetAllBatchJobs, GetAlertRuleById, GetAllAlertRules, GetAlertHistory, GetActiveAlerts, GetAllAiModels, GetAiModelById, GetTierRouting, GetPendingTypologiesCount, GetAgentMetrics, GetSingleAgentMetrics, GetTopAgents, GetPromptTemplates, GetPromptTemplateById, GetPromptVersionHistory, GetActivePromptVersion, GetEvaluationHistory, GetPromptAuditLog, GetRagPipelineStrategies, GetRagPipelineStrategyById, GetAdminStats, GetAiUsageStats, GetApiRequestsByDay, GetPerformanceMetrics, GetE2EMetrics, GetAccessibilityMetrics, GetFeedbackStats, GetLowQualityResponses, GenerateReport, ScheduleReport, UpdateReportSchedule, GetScheduledReports, GetReportExecutions, ExportStats, GenerateQualityReport, GenerateEvaluationReport, GetAllPdfLimits, GetInfrastructureHealth, GetInfrastructureDetails, GetLlmHealth, GetPrometheusMetrics, IndexChessKnowledge, SearchChessKnowledge, DeleteChessKnowledge, +more

---

## Key Endpoints (150+)

| Method | Endpoint | Auth | Purpose |
|--------|----------|------|---------|
| GET | `/api/v1/users/search` | Session | Search users (@mention) |
| GET | `/api/v1/admin/users` | Admin | List all users (filtered) |
| GET | `/api/v1/admin/users/{userId}` | Admin | User details |
| POST | `/api/v1/admin/users` | Admin | Create user |
| PUT | `/api/v1/admin/users/{id}` | Admin | Update user |
| DELETE | `/api/v1/admin/users/{id}` | Admin | Soft-delete user |
| POST | `/api/v1/admin/users/{id}/suspend` | Admin | Suspend account |
| POST | `/api/v1/admin/users/{id}/unsuspend` | Admin | Reactivate account |
| POST | `/api/v1/admin/users/bulk/import` | Admin | Import CSV |
| GET | `/api/v1/admin/users/bulk/export` | Admin | Export CSV |
| GET | `/api/v1/admin/audit-log` | Admin | Filtered audit logs |
| GET | `/api/v1/admin/audit-log/export` | Admin | Export audit logs |
| GET | `/api/v1/admin/tokens/balance/{userId}` | Admin | Token balance |
| GET | `/api/v1/admin/tokens/consumption` | Admin | Consumption by user/service |
| POST | `/api/v1/admin/tokens/credits` | Admin | Add credits |
| GET | `/api/v1/admin/tokens/top-consumers` | Admin | Top consumers |
| POST | `/api/v1/admin/batch-jobs` | Admin | Create batch job |
| GET | `/api/v1/admin/batch-jobs/{id}` | Admin | Job details + progress |
| POST | `/api/v1/admin/batch-jobs/{id}/cancel` | Admin | Cancel job |
| POST | `/api/v1/admin/alerts/rules` | Admin | Create alert rule |
| GET | `/api/v1/admin/alerts/active` | Admin | Active alerts |
| POST | `/api/v1/admin/alerts/test/{id}` | Admin | Test alert rule |
| GET | `/api/v1/admin/ai-models` | Admin | List AI models |
| POST | `/api/v1/admin/ai-models` | Admin | Create model |
| PUT | `/api/v1/admin/tier-routing` | Admin | Configure tier routing |
| PUT | `/api/v1/admin/agent-typologies/{id}/approve` | Admin | Approve typology |
| GET | `/api/v1/admin/agents/metrics` | Admin | Agent metrics |
| POST | `/api/v1/admin/prompts/templates` | Admin | Create prompt template |
| POST | `/api/v1/admin/prompts/evaluate` | Admin | Run prompt evaluation |
| POST | `/api/v1/admin/prompts/compare` | Admin | Compare versions |
| GET | `/api/v1/admin/rag-pipeline/strategies` | Admin | List RAG strategies |
| POST | `/api/v1/admin/rag-pipeline/test` | Admin | **SSE test stream** |
| GET | `/api/v1/admin/stats` | Admin | Dashboard stats |
| GET | `/api/v1/admin/stats/ai-usage` | Admin | AI usage stats |
| POST | `/api/v1/admin/reports/generate` | Admin | Generate custom report |
| POST | `/api/v1/admin/reports/schedule` | Admin | Schedule recurring report |

---

## User Management

**GetAllUsersQuery** filters:
- search (email/name), tier, role, isActive, isSuspended, page, pageSize

**SuspendUserCommand**:
- Sets: IsSuspended=true, SuspendedAt=UtcNow
- Side effects: Invalidate all sessions, raise `UserSuspendedEvent`

**Bulk Operations**:
- BulkImportUsers: CSV input, partial success allowed
- BulkExportUsers: CSV output
- BulkPasswordReset: Multiple users
- BulkRoleChange: Batch role updates

---

## Audit Logging (Issue #3691)

**GetAuditLogsQuery** filters:
- limit, offset, adminUserId, action, resource, result (Success/Failure), startDate, endDate

**Audit Decorator**: `[AuditableAction]` attribute marks commands for auto-logging

**ExportAuditLogsQuery**: CSV/JSON export

---

## Token Management (Issue #3692)

**GetTokenBalanceQuery**: AvailableTokens, ConsumedTokens, TotalAllocated, PercentageUsed

**AddTokenCreditsCommand**: Add credits with reason (e.g., "Premium subscription renewal")

**GetTopConsumersQuery**: Ranked list by consumption (startDate, endDate, limit)

---

## Batch Job System (Epic 1 - Issue #3693)

**Job Types**:
- UserSeeding: Bulk user creation
- DataExport: Generate CSV/JSON exports
- BulkProcessing: Mass operations
- ReportGeneration: Scheduled reports

**GetBatchJobQuery** returns: Status, TotalItems, ProcessedItems, FailedItems, Progress, EstimatedCompletion

---

## Alert Management

**CreateAlertRuleCommand**:
- Rule expressions: SQL-like syntax (e.g., `token_usage_percentage > 80`)
- Execution: Scheduled job checks rules every 5 min
- Severity: Info | Warning | Critical

**TestAlertCommand**: Dry-run showing if rule would trigger + affected entities

---

## AI Model Administration (Issues #2567, #2596)

**CreateAiModelCommand**:
- Fields: Name, ProviderId, ModelId, Priority, IsActive, MaxTokens, CostPerMillionTokens

**UpdateTierRoutingCommand**:
- Configure which models each tier can use
- Fields: Tier, AllowedModels[], DefaultModel, FallbackModel

---

## Agent Metrics (Issue #3382)

**GetAgentMetricsQuery**:
- Filters: startDate, endDate, typologyId, strategy
- Returns: Invocations, tokens, cost, latency, confidence, time series

**GetTopAgentsQuery**: Top agents by usage/cost

---

## Prompt Management

**Templates & Versions**:
- CreatePromptTemplate, CreatePromptVersion, ActivatePromptVersion
- Version history tracking

**Evaluation**:
- EvaluatePrompt: Test with evaluation dataset, returns accuracy/confidence/latency
- ComparePromptVersions: A/B comparison

---

## RAG Pipeline Builder (Issues #3463-#3464)

**TestRagPipelineCommand (SSE)**:
- Real-time RAG pipeline testing
- Events: PipelineTestStarted, BlockExecutionStarted, DocumentsRetrieved, ValidationResult, PipelineTestCompleted

---

## Analytics & Reporting

**GetAdminStatsQuery** returns:
- Users: total, active, suspended
- Games: total, published, pending
- Tokens: consumed24h, cost24h
- Sessions: active, totalToday
- API requests: today, errors

**Reports**:
- GenerateReport: Custom report generation
- ScheduleReport: Recurring reports (daily/weekly/monthly)
- ExportStats: CSV/JSON export

---

## Domain Events

| Event | When | Subscribers |
|-------|------|-------------|
| `UserSuspendedEvent` | User suspended | UserNotifications |
| `UserUnsuspendedEvent` | User reactivated | UserNotifications |
| `TierChangedEvent` | Tier updated | TokenManagement (recalc quotas) |
| `BatchJobCompletedEvent` | Job finished | Administration (cleanup) |
| `AlertTriggeredEvent` | Alert fired | UserNotifications (emails) |
| `TypologyApprovedEvent` | Typology approved | UserNotifications |

---

## Integration Points

**Subscribes to**:
- Authentication: UserCreatedEvent, UserLoggedInEvent
- KnowledgeBase: AgentInvokedEvent, TypologyProposedEvent
- SharedGameCatalog: GamePublishedEvent, BadgeEarnedEvent
- All Contexts: For audit logging ([AuditableAction])

**Publishes to**:
- UserNotifications: Alerts, suspension notices
- KnowledgeBase: Model config changes, typology approvals

---

## Testing

**Unit Tests** (`tests/Api.Tests/Administration/`):
- User_Tests.cs (suspend/unsuspend, tier changes)
- AuditLog_Tests.cs (decorator, audit trail)
- TokenBalance_Tests.cs (add credits, consume tokens)
- BatchJob_Tests.cs (state transitions, progress)
- AlertRule_Tests.cs (trigger logic, execution)

**Integration Tests** (Testcontainers):
1. Create user → suspend → verify sessions invalidated
2. Add token credits → consume → verify balance updated
3. Create batch job → monitor progress → verify completion
4. Create alert rule → trigger → verify email sent
5. Bulk import users CSV → verify partial success handling

---

## Code Location

`apps/api/src/Api/BoundedContexts/Administration/`

**Endpoint Files** (11):
- AdminDashboardEndpoints.cs
- AdminUserManagementEndpoints.cs
- AlertManagementEndpoints.cs
- BatchJobEndpoints.cs
- AdminConfigurationEndpoints.cs
- AdminAuditLogEndpoints.cs
- AdminReportingEndpoints.cs
- +4 specialized

---

**Status**: ✅ Production
**Commands**: 38 | **Queries**: 62 | **Endpoints**: 150+ | **Workflow Areas**: 19
