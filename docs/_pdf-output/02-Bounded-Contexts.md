# MeepleAI - Bounded Contexts (DDD)

Documentazione dei 15 bounded context del dominio, responsabilita, interazioni e diagrammi.

**Data generazione**: 8 marzo 2026

**File inclusi**: 17

---

## Indice

1. bounded-contexts/diagrams/README.md
2. bounded-contexts/README.md
3. bounded-contexts/administration.md
4. bounded-contexts/authentication.md
5. bounded-contexts/business-simulations.md
6. bounded-contexts/diagram-summary.md
7. bounded-contexts/diagrams/INDEX.md
8. bounded-contexts/document-processing.md
9. bounded-contexts/game-management.md
10. bounded-contexts/gamification.md
11. bounded-contexts/knowledge-base.md
12. bounded-contexts/session-tracking.md
13. bounded-contexts/shared-game-catalog.md
14. bounded-contexts/system-configuration.md
15. bounded-contexts/user-library.md
16. bounded-contexts/user-notifications.md
17. bounded-contexts/workflow-integration.md

---



<div style="page-break-before: always;"></div>

## bounded-contexts/diagrams/README.md

# Bounded Context Diagrams

Visual documentation for all MeepleAI bounded contexts using Mermaid diagrams.

## 📂 Structure

Each bounded context has 4 diagram types with **dual formats** (.mmd source + .svg compiled):

1. **entities.mmd/.svg**: Entity Relationship Diagram
   - Shows aggregates, entities, and value objects
   - Includes primary keys, foreign keys, and key properties
   - Displays cardinality relationships (1:1, 1:N, N:M)

2. **flow-{primary-command}.mmd/.svg**: Primary Command Sequence
   - Illustrates CQRS flow for most important command
   - Shows: Client → Endpoint → Mediator → Validator → Handler → Domain → DB → Events

3. **flow-{secondary}.mmd/.svg**: Secondary Flow (Command or Query)
   - Important secondary operation sequence
   - May show query patterns, background jobs, or specialized workflows

4. **integration-flow.mmd/.svg**: Integration Diagram
   - Context integration with other bounded contexts
   - Event-driven communication patterns
   - Direct dependencies and external services

### File Formats

| Format | Purpose | Size | Quality |
|--------|---------|------|---------|
| `.mmd` | Source (editable) | 1-5KB | N/A |
| `.svg` | Vector graphics (recommended) | 25-260KB | Infinite ✅ |
| `.png` | Raster (legacy) | 40-50KB | 2048px max |

**Recommendation**: Use `.svg` for all documentation. SVG files are:
- ✅ Infinitely scalable (no quality loss)
- ✅ Smaller file sizes than high-res PNG
- ✅ Editable in vector tools (Figma, Illustrator)
- ✅ Browser-native support

## 📋 Contexts Documented

### ✅ Complete (8 Contexts)

| Context | Diagrams | Key Features |
|---------|----------|--------------|
| **UserLibrary** | entities, flow-add-game, flow-upload-private-pdf, integration-flow | Collection management, PDF association, labels, sharing |
| **Administration** | entities, flow-suspend-user, flow-add-token-credits, integration-flow | Token management, batch jobs, audit logging |
| **DocumentProcessing** | entities, flow-extract-pdf, flow-upload-pdf, integration-flow | 3-stage extraction pipeline, chunked uploads |
| **SharedGameCatalog** | entities, flow-approve-publication, integration-flow | Publication workflow, share requests, soft-delete |
| **SystemConfiguration** | entities, flow-update-config, flow-tier-routing, integration-flow | Runtime config, feature flags, tier routing |
| **UserNotifications** | entities, flow-create-notification, flow-get-notifications, integration-flow | Event-driven notifications, email integration |
| **WorkflowIntegration** | entities, flow-create-n8n-config, flow-test-connection, integration-flow | n8n integration, webhook system |
| **SessionTracking** | entities, flow-create-session, flow-roll-dice, integration-flow | Real-time features, SSE streaming, dice/cards |

### 🎯 Previously Complete (3 Contexts)

- **Authentication**: Login, registration, 2FA flows
- **GameManagement**: Game CRUD, sessions, rule specs
- **KnowledgeBase**: RAG system, agent typologies, multi-agent chat

## 🎨 Diagram Conventions

### Entity Relationship Diagrams
*(blocco di codice rimosso)*

**Relationship Symbols**:
- `||--o{`: One-to-many
- `||--||`: One-to-one
- `}o--||`: Many-to-one
- `}o--o{`: Many-to-many

**Property Annotations**:
- `PK`: Primary Key
- `FK`: Foreign Key
- `UK`: Unique Key

### Sequence Diagrams

**Standard CQRS Flow**:
*(blocco di codice rimosso)*

**Participants**:
- `Client`: HTTP client (web app, API consumer)
- `Endpoint`: ASP.NET Minimal API endpoint
- `Mediator`: MediatR orchestration
- `Validator`: FluentValidation
- `Handler`: Command/Query handler
- `Domain`: Domain entity/aggregate
- `DB`: PostgreSQL database
- `Events`: Domain event publisher

### Integration Diagrams

**Arrow Types**:
- `-->` : Event-driven communication (domain events)
- `-.->` : Direct dependencies (queries, FK references)
- Solid lines: Strong coupling
- Dashed lines: Loose coupling

**Styling Classes**:
- Authentication: Blue (`#0066cc`)
- Administration: Orange (`#ff9800`)
- GameManagement: Light Orange (`#ff9800`)
- KnowledgeBase: Purple (`#9c27b0`)
- UserLibrary: Green (`#4caf50`)
- DocumentProcessing: Blue (`#2196f3`)
- SharedGameCatalog: Purple (`#9c27b0`)
- SystemConfiguration: Teal (`#009688`)
- UserNotifications: Pink (`#e91e63`)
- SessionTracking: Purple (`#9c27b0`)
- WorkflowIntegration: Light Blue (`#0288d1`)

## 🔧 Usage

### View in VS Code
Install Mermaid Preview extension and open `.mmd` files.

### Generate Single Diagram
*(blocco di codice rimosso)*

### Regenerate All SVG Diagrams (Batch)
*(blocco di codice rimosso)*

### Embed in Markdown
*(blocco di codice rimosso)*

## 📚 Related Documentation

- [Bounded Context Complete Docs](../): Full API references for each context
- [Architecture ADRs](../../01-architecture/adr/): Architectural Decision Records
- [CQRS Pattern](../../02-development/coding-standards.md): Implementation guidelines
- [Domain Events](../../01-architecture/domain-events.md): Event catalog

## 🔄 Maintenance

**Update Frequency**: Update diagrams when:
- New aggregates or entities added
- Key command/query flows change
- Integration patterns evolve
- Domain events modified

**Regeneration Process**:
1. Edit `.mmd` source files
2. Run batch SVG generation (see Usage section)
3. Verify output: `find . -name "*.svg" | wc -l` should show 43
4. Commit both `.mmd` and `.svg` files

**Review Schedule**: Quarterly review for accuracy

---

**Last Updated**: 2026-02-07 (SVG generation completed)
**Total Diagrams**: 43 (11 contexts × 3-4 diagrams each)
**Formats**: 43 `.mmd` sources + 43 `.svg` compiled + 43 `.png` legacy
**Status**: ✅ Complete


---



<div style="page-break-before: always;"></div>

## bounded-contexts/README.md

# Bounded Contexts Documentation

**Quick Navigation** - Guida ai 13 Bounded Contexts DDD di MeepleAI

---

## 🎯 Cos'è un Bounded Context?

Un **Bounded Context** è un confine esplicito all'interno del quale un particolare **modello di dominio** è definito e applicabile. In MeepleAI, ogni bounded context rappresenta un'area funzionale autonoma con:

- **Linguaggio Ubiquo**: Terminologia condivisa tra sviluppatori e domain experts
- **Modello di Dominio Autonomo**: Entità, value objects, aggregates specifici
- **Confini Chiari**: Comunicazione tra contexts via Domain Events o API
- **Team Ownership**: Ogni context può essere sviluppato indipendentemente

---

## 📂 Bounded Contexts Overview

MeepleAI ha **13 Bounded Contexts** organizzati per area funzionale:

| # | Context | Responsabilità | File | Status |
|---|---------|----------------|------|--------|
| 1 | **Administration** | Gestione utenti, ruoli, audit logs, analytics | `administration.md` | ✅ Production |
| 2 | **Authentication** | Autenticazione, sessioni, OAuth, 2FA, API keys | `authentication.md` | ✅ Production |
| 3 | **BusinessSimulations** | Ledger entries, cost scenarios, resource forecasts | `business-simulations.md` | ✅ Production |
| 4 | **DocumentProcessing** | PDF upload, extraction, chunking, validation | `document-processing.md` | ✅ Production |
| 5 | **Gamification** | Achievements, badges, leaderboards | `gamification.md` | ✅ Production |
| 6 | **GameManagement** | Catalogo giochi, sessioni di gioco, FAQ | `game-management.md` | ✅ Production |
| 7 | **KnowledgeBase** | RAG system, AI agents, chat threads, vector search | `knowledge-base.md` | ✅ Production |
| 8 | **SessionTracking** | Session notes, scoring, deck tracking, activity | `session-tracking.md` | ✅ Production |
| 9 | **SharedGameCatalog** | Database community giochi con soft-delete | `shared-game-catalog.md` | ✅ Production |
| 10 | **SystemConfiguration** | Config runtime, feature flags, environment settings | `system-configuration.md` | ✅ Production |
| 11 | **UserLibrary** | Collezioni giochi utente, wishlist, played history | `user-library.md` | ✅ Production |
| 12 | **UserNotifications** | Notifiche in-app, email, push notifications | `user-notifications.md` | ✅ Production |
| 13 | **WorkflowIntegration** | n8n workflows, webhooks, error logging | `workflow-integration.md` | 🚧 Beta |

---

## 🔍 Trova per Scenario

**Se vuoi...** | **Context** | **File**
---|---|---
Implementare login/registrazione | Authentication | `authentication.md`
Aggiungere nuovo gioco | GameManagement | `game-management.md`
Implementare chat RAG | KnowledgeBase | `knowledge-base.md`
Processare PDF regolamento | DocumentProcessing | `document-processing.md`
Gestire catalogo community | SharedGameCatalog | `shared-game-catalog.md`
Tracciare collezione utente | UserLibrary | `user-library.md`
Gestire notifiche utente | UserNotifications | `user-notifications.md`
Gestire utenti admin | Administration | `administration.md`
Configurare feature flags | SystemConfiguration | `system-configuration.md`
Integrare webhook esterno | WorkflowIntegration | `workflow-integration.md`
Tracciare costi e budget | BusinessSimulations | `business-simulations.md`
Gestire achievements/badges | Gamification | `gamification.md`
Gestire sessioni di gioco live | SessionTracking | `session-tracking.md`

---

## 🏗️ Architettura Layer per Context

Ogni Bounded Context segue la **Clean Architecture** con 3 layer:

*(blocco di codice rimosso)*

**Codice**: `apps/api/src/Api/BoundedContexts/{Context}/`

---

## 📖 Context Dependencies Map

*(blocco di codice rimosso)*

**Legenda**:
- `→` Dipendenza diretta (forte accoppiamento)
- `-.->` Dipendenza via eventi/config (debole accoppiamento)

---

## 🎯 Quick Start per Context

### Adding a New Feature

**Pattern**: Domain → Application → Infrastructure → Endpoint → Tests

**Example: Add "MarkGameAsPlayed" in GameManagement**

1. **Domain** (`Domain/Entities/Game.cs`):
*(blocco di codice rimosso)*

2. **Application** (`Application/Commands/MarkGameAsPlayedCommand.cs`):
*(blocco di codice rimosso)*

3. **Endpoint** (`Routing/GameEndpoints.cs`):
*(blocco di codice rimosso)*

4. **Tests** (`tests/Api.Tests/GameManagement/MarkGameAsPlayedTests.cs`):
*(blocco di codice rimosso)*

---

## 🔗 Context Interactions

### High-Level Context Map

*(blocco di codice rimosso)*

**Legenda**:
- `→` Dipendenza diretta (forte accoppiamento)
- `-.->` Dipendenza via config/eventi (debole accoppiamento)

### Event-Driven Communication

**Pattern**: Domain Events per comunicazione asincrona tra contexts

**Example**: DocumentProcessing → KnowledgeBase

*(blocco di codice rimosso)*

### Direct API Calls

**Pattern**: REST API calls tra contexts (quando necessario)

**Example**: KnowledgeBase → GameManagement

*(blocco di codice rimosso)*

**Regola**: Preferire Domain Events (asincroni) quando possibile, API calls solo per sincronizzazione critica.

---

## 📚 Risorse per Context

### Code Location
*(blocco di codice rimosso)*

### Documentation Location
*(blocco di codice rimosso)*

### Related Documentation
- [DDD Quick Reference](../architecture/ddd/quick-reference.md) - DDD patterns
- [CQRS Flow Diagram](../architecture/diagrams/cqrs-mediatr-flow.md) - Request flow
- [Bounded Contexts Diagram](../architecture/diagrams/bounded-contexts-interactions.md) - Context map
- [Development Guide](../development/README.md) - Adding features workflow

---

**Last Updated**: 2026-02-18
**Maintainers**: Architecture Team
**Total Contexts**: 13
**Pattern**: DDD + CQRS + Event-Driven

---



<div style="page-break-before: always;"></div>

## bounded-contexts/administration.md

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


---



<div style="page-break-before: always;"></div>

## bounded-contexts/authentication.md

# Authentication Bounded Context - API Reference

**Gestione autenticazione, sessioni, OAuth, 2FA, e API keys**

---

## 📋 Responsabilità

- Registrazione e login utenti (email/password)
- Gestione sessioni (cookie-based con sliding expiration)
- OAuth 2.0 (Google, GitHub, Discord)
- Two-Factor Authentication (TOTP con backup codes)
- API Key generation, rotation, e revocation
- Password reset e email verification
- Account lockout e admin management
- Session management multi-device
- User profile e preferences

---

## 🏗️ Domain Model

### Aggregates

| Aggregate/Entity | Key Properties | Factory Methods |
|------------------|----------------|-----------------|
| **User** (Root) | Id, Email, PasswordHash, DisplayName, Role, EmailConfirmed, TwoFactorEnabled, BackupCodes | `Create()`, `EnableTwoFactor()`, `DisableTwoFactor()`, `GenerateApiKey()`, `LockAccount()` |
| **ApiKey** | Id, UserId, KeyHash, Name, Scopes, ExpiresAt, IsRevoked | `Create()`, `Revoke()`, `RecordUsage()` |
| **Session** | Id, UserId, SessionToken, DeviceInfo, ExpiresAt, IsRevoked | `Create()`, `Extend()`, `Revoke()` |
| **OAuthAccount** | Id, UserId, Provider, ProviderUserId, Email | `Link()`, `Unlink()` |

### Value Objects

| Value Object | Purpose | Validation |
|--------------|---------|------------|
| **Email** | Email address with validation | RFC 5322 format, lowercase normalization |
| **PasswordHash** | PBKDF2 password hash | 210,000 iterations, per-password salt |

**Implementation Examples**: See `tests/Api.Tests/BoundedContexts/Authentication/Domain/`

---

## 📡 Application Layer (CQRS)

> **Note**: This context implements **57 commands and queries** (36 commands + 21 queries).
> All endpoints use `IMediator.Send()` pattern per CQRS architecture.

---

### CORE AUTHENTICATION

#### Registration & Login

| Command/Query | HTTP Method | Endpoint | Auth | Request | Response |
|---------------|-------------|----------|------|---------|----------|
| `RegisterCommand` | POST | `/api/v1/auth/register` | None | `RegisterPayload` | `{ user: UserDto, expiresAt }` |
| `LoginCommand` | POST | `/api/v1/auth/login` | None | `LoginPayload` | `{ user: UserDto, expiresAt }` OR `{ requiresTwoFactor, sessionToken }` |
| `LogoutCommand` | POST | `/api/v1/auth/logout` | Cookie | None | `{ ok: bool }` |
| `LoginWithApiKeyCommand` | POST | `/api/v1/auth/apikey/login` | None | `ApiKeyLoginPayload` | `{ user: UserDto, message }` |
| `LogoutApiKeyCommand` | POST | `/api/v1/auth/apikey/logout` | API Key Cookie | None | `{ ok: bool, message }` |

**RegisterCommand**:
- Request: email, password, displayName, role
- Response: UserDto + expiresAt
- Validation: Email format/unique, password strength (min 8 chars, uppercase, lowercase, digit, special)
- Side Effects: Session cookie created, verification email sent, UserCreatedEvent raised
- Errors: 400 (validation), 409 (duplicate email)

**LoginCommand**:
- Request: email, password
- Response: UserDto + expiresAt OR requiresTwoFactor + sessionToken (if 2FA enabled)
- Validation: Account not locked, password matches hash
- Side Effects: Failed login count incremented/reset, LastLoginAt updated, session cookie created
- Errors: 400 (missing fields), 401 (invalid credentials), 403 (account locked)
- Events: UserLoggedInEvent, LoginFailedEvent

**Implementation Examples**: See `tests/Api.Tests/BoundedContexts/Authentication/Application/`

---

### SESSION MANAGEMENT

#### Session Lifecycle

| Command/Query | HTTP Method | Endpoint | Auth | Request | Response |
|---------------|-------------|----------|------|---------|----------|
| `CreateSessionCommand` | POST | `/api/v1/auth/sessions` | None | `CreateSessionPayload` | `SessionDto` |
| `GetSessionStatusQuery` | GET | `/api/v1/auth/session/status` | Cookie | None | `SessionStatusResponse` |
| `ExtendSessionCommand` | POST | `/api/v1/auth/session/extend` | Cookie | None | `{ expiresAt }` |
| `GetUserSessionsQuery` | GET | `/api/v1/users/me/sessions` | Cookie | None | `List<SessionDto>` |
| `RevokeSessionCommand` | POST | `/api/v1/auth/sessions/{sessionId}/revoke` | Cookie | None | `{ ok, message }` |
| `LogoutAllDevicesCommand` | POST | `/api/v1/auth/sessions/revoke-all` | Cookie | `LogoutAllDevicesPayload` | `{ ok, revokedCount, currentSessionRevoked, message }` |
| `RevokeAllUserSessionsCommand` | POST | `/api/v1/admin/users/{userId}/sessions/revoke` | Cookie + Admin | Path: userId | `{ ok, revokedCount }` |
| `RevokeInactiveSessionsCommand` | POST | `/api/v1/admin/sessions/cleanup` | Cookie + Admin | Query: inactiveDays? | `{ ok, revokedCount }` |
| `GetAllSessionsQuery` | GET | `/api/v1/admin/sessions` | Cookie + Admin | Query: userId?, isActive?, page, pageSize | `PaginatedList<SessionDto>` |

**Key Session Operations**:
- **GetSessionStatus**: Returns expiresAt, lastSeenAt, remainingMinutes (Redis cached 1 min)
- **ExtendSession**: Adds 30 days from now (sliding window, configurable)
- **LogoutAllDevices**: Revokes all sessions, requires password confirmation, returns revokedCount

---

### TWO-FACTOR AUTHENTICATION (TOTP)

| Command/Query | HTTP Method | Endpoint | Auth | Request | Response |
|---------------|-------------|----------|------|---------|----------|
| `GenerateTotpSetupCommand` | POST | `/api/v1/auth/2fa/setup` | Cookie | None | `TotpSetupDto` |
| `Enable2FACommand` | POST | `/api/v1/auth/2fa/enable` | Cookie | `TwoFactorEnableRequest` | `{ Success, BackupCodes, ErrorMessage }` |
| `Verify2FACommand` | POST | `/api/v1/auth/2fa/verify` | None | `TwoFactorVerifyRequest` | `{ message, user }` |
| `Disable2FACommand` | POST | `/api/v1/auth/2fa/disable` | Cookie | `TwoFactorDisableRequest` | `{ message }` |
| `Get2FAStatusQuery` | GET | `/api/v1/users/me/2fa/status` | Cookie | None | `TwoFactorStatusDto` |
| `AdminDisable2FACommand` | POST | `/api/v1/auth/admin/2fa/disable` | Cookie + Admin | `AdminDisable2FARequest` | `{ message }` |

**2FA Workflow**:
1. **GenerateTotpSetup**: Returns QR code + secret + 8 backup codes (8 digits each, single-use)
2. **Enable2FA**: Validates 6-digit TOTP code, stores encrypted secret, raises TwoFactorEnabledEvent (rate limited: 3 attempts/min)
3. **Verify2FA**: Exchanges temp login token for permanent session using TOTP code or backup code (8 digits)
4. **AdminDisable2FA**: Admin-only emergency recovery (requires Admin role, creates audit log)

---

### OAUTH 2.0

| Command/Query | HTTP Method | Endpoint | Auth | Request | Response |
|---------------|-------------|----------|------|---------|----------|
| `InitiateOAuthLoginCommand` | GET | `/api/v1/auth/oauth/{provider}/login` | None | Path: provider | 302 Redirect |
| `HandleOAuthCallbackCommand` | GET | `/api/v1/auth/oauth/{provider}/callback` | None | Query: code, state | 302 Redirect |
| `LinkOAuthAccountCommand` | POST | `/api/v1/auth/oauth/{provider}/link` | Cookie | Path: provider, Query: code, state | `{ ok, message }` |
| `UnlinkOAuthAccountCommand` | DELETE | `/api/v1/auth/oauth/{provider}/unlink` | Cookie | Path: provider | 204 No Content |
| `GetLinkedOAuthAccountsQuery` | GET | `/api/v1/users/me/oauth-accounts` | Cookie | None | `List<OAuthAccountDto>` |

**OAuth Providers**: Google, GitHub, Discord

**OAuth Flow**:
1. **InitiateOAuth**: Generates state token (10-min expiry, IP-bound), stores in Redis, redirects to provider
2. **HandleCallback**: Validates state, exchanges code for token, fetches profile, creates/links user, creates session, redirects to frontend
3. **LinkOAuthAccount**: Links OAuth to existing authenticated user (requires active session)
4. **UnlinkOAuthAccount**: Removes OAuth link (validation: cannot unlink last auth method)

**Security**: State expires in 10 min, tied to IP (replay protection), defensive transactions (Issue #2600)
**Edge Cases**: Email exists → link account; OAuth exists → return session; email mismatch → reject

---

### PASSWORD MANAGEMENT

| Command/Query | HTTP Method | Endpoint | Auth | Request | Response |
|---------------|-------------|----------|------|---------|----------|
| `ChangePasswordCommand` | PUT | `/api/v1/users/profile/password` | Cookie | `ChangePasswordPayload` | `{ ok, message }` |
| `RequestPasswordResetCommand` | POST | `/api/v1/auth/password-reset/request` | None | `PasswordResetRequestPayload` | `{ ok, message }` |
| `ValidatePasswordResetTokenQuery` | GET | `/api/v1/auth/password-reset/verify` | None | Query: token | `{ ok, message }` |
| `ResetPasswordCommand` | PUT | `/api/v1/auth/password-reset/confirm` | None | `PasswordResetConfirmPayload` | `{ ok, message }` |

**Password Management**:
- **ChangePassword**: Requires current password, validates strength, invalidates all other sessions, creates audit log
- **RequestPasswordReset**: Generates 256-bit token (1-hour expiry), sends email, always returns success (anti-enumeration), rate limited (1/min per email)
- **ResetPassword**: Validates token (single-use), enforces strength requirements, invalidates ALL sessions, creates audit log

---

### EMAIL VERIFICATION

| Command/Query | HTTP Method | Endpoint | Auth | Request | Response |
|---------------|-------------|----------|------|---------|----------|
| `VerifyEmailCommand` | POST | `/api/v1/auth/email/verify` | None | `VerifyEmailPayload` | `{ ok, message }` |
| `ResendVerificationCommand` | POST | `/api/v1/auth/email/resend` | None | `ResendVerificationPayload` | `{ ok, message }` |

**Email Verification**:
- **VerifyEmail**: Validates token, sets EmailConfirmed = true, raises EmailVerifiedEvent
- **ResendVerification**: Rate limited (1/min per email), always returns success (anti-enumeration)

---

### API KEY MANAGEMENT

| Command/Query | HTTP Method | Endpoint | Auth | Request | Response |
|---------------|-------------|----------|------|---------|----------|
| `CreateApiKeyManagementCommand` | POST | `/api/v1/api-keys` | Cookie | `CreateApiKeyRequest` | `{ ApiKey: ApiKeyDto, RawKey: string }` |
| `ListApiKeysQuery` | GET | `/api/v1/api-keys` | Cookie | Query: includeRevoked, page, pageSize | `PaginatedList<ApiKeyDto>` |
| `GetApiKeyQuery` | GET | `/api/v1/api-keys/{keyId}` | Cookie | None | `ApiKeyDto` |
| `UpdateApiKeyManagementCommand` | PUT | `/api/v1/api-keys/{keyId}` | Cookie | `UpdateApiKeyRequest` | `ApiKeyDto` |
| `RevokeApiKeyManagementCommand` | DELETE | `/api/v1/api-keys/{keyId}` | Cookie | None | 204 No Content |
| `RotateApiKeyCommand` | POST | `/api/v1/api-keys/{keyId}/rotate` | Cookie | `RotateApiKeyRequest` | `{ OldApiKey, NewApiKey }` |
| `GetApiKeyUsageQuery` | GET | `/api/v1/api-keys/{keyId}/usage` | Cookie | None | `ApiKeyUsageDto` |
| `GetApiKeyUsageStatsQuery` | GET | `/api/v1/api-keys/{keyId}/stats` | Cookie | None | `ApiKeyUsageStatsDto` |
| `GetApiKeyUsageLogsQuery` | GET | `/api/v1/api-keys/{keyId}/logs` | Cookie | Query: skip, take | `{ logs: List<ApiKeyUsageLogDto>, pagination }` |

**API Key Management**:
- **CreateApiKey**: Generates `mpl_{env}_{base64}` (32 bytes), PBKDF2 hash (10K iterations), returns raw key ONCE, validates name (3-100 chars), expiry (max 2 years)
- **RotateApiKey**: Atomic rotation, immediate revocation (0s grace period), recommended every 90 days
- **GetApiKeyUsageStats**: Returns totalRequests, dailyUsage, topEndpoints, errorRate, avgResponseTime

---

#### Admin API Key Management

| Command/Query | HTTP Method | Endpoint | Auth | Request | Response |
|---------------|-------------|----------|------|---------|----------|
| `DeleteApiKeyCommand` | DELETE | `/api/v1/admin/api-keys/{keyId}` | Cookie + Admin | None | 204 No Content |
| `GetAllApiKeysWithStatsQuery` | GET | `/api/v1/admin/api-keys/stats` | Cookie + Admin | Query: userId?, includeRevoked | `{ keys: List<ApiKeyWithStatsDto>, count, filters }` |
| `BulkExportApiKeysQuery` | GET | `/api/v1/admin/api-keys/bulk/export` | Cookie + Admin | Query: userId?, isActive?, searchTerm? | CSV File |
| `BulkImportApiKeysCommand` | POST | `/api/v1/admin/api-keys/bulk/import` | Cookie + Admin | CSV content (raw text) | `{ SuccessCount, FailedCount, Errors }` |

**Bulk Operations** (Admin only):
- **BulkExportApiKeys**: CSV export with UserId, KeyName, KeyPreview, CreatedAt, ExpiresAt, IsRevoked, TotalRequests
- **BulkImportApiKeys**: CSV import with row validation, requires Admin + password confirmation, returns successCount + error details

---

### USER PROFILE & PREFERENCES

| Command/Query | HTTP Method | Endpoint | Auth | Request | Response |
|---------------|-------------|----------|------|---------|----------|
| `GetUserProfileQuery` | GET | `/api/v1/users/profile` | Cookie | None | `UserProfileDto` |
| `UpdateUserProfileCommand` | PUT | `/api/v1/users/profile` | Cookie | `UpdateProfilePayload` | `{ ok, message }` |
| `UpdatePreferencesCommand` | PUT | `/api/v1/users/preferences` | Cookie | `UpdatePreferencesPayload` | `UserProfileDto` |
| `GetUserDevicesQuery` | GET | `/api/v1/users/me/devices` | Cookie | None | `List<DeviceDto>` |
| `GetUserByIdQuery` | GET | `/api/v1/users/{userId}` | Cookie | None | `UserDto` |
| `GetUserUploadQuotaQuery` | GET | `/api/v1/users/me/upload-quota` | Cookie | None | `PdfUploadQuotaInfo` |
| `GetUserActivityQuery` | GET | `/api/v1/users/me/activity` | Cookie | Query: filters | `GetUserActivityResult` |
| `GetUserDetailedAiUsageQuery` | GET | `/api/v1/users/me/ai-usage` | Cookie | Query: days? | `UserAiUsageDto` |
| `GetUserAvailableFeaturesQuery` | GET | `/api/v1/users/me/features` | Cookie | None | `List<UserFeatureDto>` |

**User Preferences & Usage**:
- **UpdatePreferences**: Language (it/en), Theme (light/dark/auto), DataRetentionDays (30-365, GDPR)
- **GetUserDetailedAiUsage**: Returns totalTokens, totalCostUsd, breakdown by model/operationType, dailyTimeSeries (days param: 30-365)

---

### ACCOUNT LOCKOUT & ADMIN

| Command/Query | HTTP Method | Endpoint | Auth | Request | Response |
|---------------|-------------|----------|------|---------|----------|
| `GetAccountLockoutStatusQuery` | GET | `/api/v1/users/me/lockout-status` | Cookie | None | `AccountLockoutDto` |
| `UnlockAccountCommand` | POST | `/api/v1/auth/admin/unlock-account` | Cookie + Admin | `UnlockAccountRequest` | `{ ok, message }` |

**Account Lockout**:
- **Trigger**: 5 failed logins in 15 min
- **Duration**: 15 min (auto-reset OR admin unlock)
- **Notification**: Email sent on lockout
- **UnlockAccount**: Admin-only, creates audit log with reason

---

### SHARE LINKS (Document Sharing)

| Command/Query | HTTP Method | Endpoint | Auth | Request | Response |
|---------------|-------------|----------|------|---------|----------|
| `CreateShareLinkCommand` | POST | `/api/v1/share-links` | Cookie | `CreateShareLinkPayload` | `ShareLinkDto` |
| `ValidateShareLinkQuery` | GET | `/api/v1/share-links/{token}/validate` | None | None | `{ ok, document }` |
| `RevokeShareLinkCommand` | DELETE | `/api/v1/share-links/{linkId}` | Cookie | None | 204 No Content |

**Share Links**:
- **CreateShareLink**: Generates token for PDF document, supports expiresAt + maxUses, read-only access, cannot cross users
- **ValidateShareLink**: Validates token, returns document if valid
- **RevokeShareLink**: Immediately invalidates token

---

## 🔄 Domain Events

| Event | When Raised | Payload | Subscribers |
|-------|-------------|---------|-------------|
| `UserCreatedEvent` | After successful registration | `{ UserId, Email, Role }` | Administration (audit), UserLibrary (init) |
| `UserLoggedInEvent` | After successful login | `{ UserId, IpAddress, SessionId }` | Administration (audit), SessionTracking |
| `TwoFactorEnabledEvent` | After 2FA setup complete | `{ UserId }` | Administration (security audit) |
| `TwoFactorDisabledEvent` | After 2FA disabled | `{ UserId, DisabledBy }` | Administration (security audit) |
| `PasswordChangedEvent` | After password change | `{ UserId }` | Administration (audit), UserNotifications (email) |
| `PasswordResetEvent` | After password reset | `{ UserId }` | Administration (audit), UserNotifications (email) |
| `ApiKeyCreatedEvent` | After API key generation | `{ UserId, KeyId, KeyName }` | Administration (audit) |
| `ApiKeyRevokedEvent` | After API key revocation | `{ UserId, KeyId }` | Administration (audit) |
| `SessionCreatedEvent` | After session creation | `{ UserId, SessionId, IpAddress }` | SessionTracking |
| `SessionRevokedEvent` | After session revocation | `{ UserId, SessionId, RevokedBy }` | SessionTracking |
| `OAuthAccountLinkedEvent` | After OAuth account linked | `{ UserId, Provider }` | Administration (audit) |
| `OAuthAccountUnlinkedEvent` | After OAuth unlink | `{ UserId, Provider }` | Administration (audit) |
| `EmailVerifiedEvent` | After email verification | `{ UserId, Email }` | Administration (audit) |
| `AccountLockedEvent` | After account lockout | `{ UserId, LockedUntil, Reason }` | Administration (security), UserNotifications (email) |
| `AccountUnlockedEvent` | After admin unlock | `{ UserId, UnlockedBy }` | Administration (audit) |

---

## 🔗 Integration Points

### Dependencies

**Inbound** (Events Published To):
- Administration: All auth events for audit logging
- SessionTracking: Session lifecycle events for analytics
- UserLibrary: UserId from session for collections
- UserNotifications: Security events for email alerts

**Outbound**: None (foundational context)

---

## 🔐 Security & Authorization

### Authentication Methods

| Method | Header/Cookie | Format | Use Case |
|--------|---------------|--------|----------|
| **Session Cookie** | Cookie: `meepleai_session_{env}` | JWT-like token, httpOnly | Web application |
| **API Key Cookie** | Cookie: `meepleai_apikey_{env}` | API key value, httpOnly | CLI/scripts via web login |
| **API Key Header** | Header: `Authorization: ApiKey {key}` | `mpl_{env}_{base64}` | Programmatic access |

### Authorization Levels

| Level | Requirements | Endpoints |
|-------|--------------|-----------|
| **Public** | None | Registration, login, OAuth, password reset, email verify |
| **Authenticated** | Valid session OR API key | Profile, preferences, API key CRUD, 2FA, sessions |
| **Admin** | Cookie + Admin role | User management, bulk operations, unlock accounts |

### Security Configuration

| Component | Specification |
|-----------|---------------|
| **Password Hash** | PBKDF2, 210K iterations, 128-bit salt, 256-bit key |
| **Password Strength** | Min 8 chars, 1 uppercase, 1 lowercase, 1 digit, 1 special |
| **API Key** | 256-bit random, Base64 URL-safe, `mpl_{env}_{token}`, PBKDF2 hash (10K iter) |
| **Session Cookie** | HttpOnly, Secure, SameSite=Lax, 30-day sliding window, SHA256 hash |
| **Session Storage** | Redis-backed, configurable expiry via config |

**Rate Limits**:
- Login: 5 attempts/5min (per IP+Email)
- 2FA Verify: 3 attempts/1min (per session)
- OAuth: 10 requests/1min (per IP)
- Email Resend: 1 request/1min (per email)
- Password Reset: 1 request/1min (per email)

## 🎯 Usage Examples

**Implementation Examples**: See comprehensive test suite for detailed usage patterns:
- **E2E Flows**: `apps/web/__tests__/e2e/authentication/`
- **Integration Tests**: `tests/Api.Tests/BoundedContexts/Authentication/Integration/`
- **Unit Tests**: `tests/Api.Tests/BoundedContexts/Authentication/Application/`

**Key Flows**:
1. **Registration**: Email/password → Session cookie → Verification email
2. **Login with 2FA**: Credentials → Temp token → TOTP code → Session
3. **API Key**: Generate (save raw key immediately) → Use via Header OR Cookie
4. **OAuth**: Initiate → Provider redirect → Callback → Session creation
5. **Password Reset**: Request → Email link → Validate token → Set new password → Invalidate all sessions

---

## 📊 Performance Characteristics

### Caching Strategy

| Operation | Cache Layer | TTL | Invalidation Trigger |
|-----------|-------------|-----|---------------------|
| `GetUserByIdQuery` | Redis | 5 minutes | UserUpdatedEvent, PasswordChangedEvent |
| `GetSessionStatusQuery` | Redis | 1 minute | SessionExtendedEvent, SessionRevokedEvent |
| `ValidateApiKeyQuery` | Redis | 10 minutes | ApiKeyRevokedEvent, ApiKeyRotatedEvent |
| `Get2FAStatusQuery` | Redis | 5 minutes | TwoFactorEnabledEvent, TwoFactorDisabledEvent |
| `GetLinkedOAuthAccountsQuery` | Redis | 30 minutes | OAuthAccountLinkedEvent, OAuthAccountUnlinkedEvent |

### Database Indexes

| Index | Columns | Filter | Purpose |
|-------|---------|--------|---------|
| `idx_users_email` | Email | NOT IsDeleted | User lookup by email |
| `idx_users_role` | Role | NOT IsDeleted | Role-based queries |
| `idx_sessions_userid_active` | UserId, ExpiresAt | NOT IsRevoked | Active session queries |
| `idx_sessions_token_hash` | SessionToken | - | Session validation |
| `idx_apikeys_userid_active` | UserId, ExpiresAt | NOT IsRevoked | Active API key queries |
| `idx_apikeys_hash` | KeyHash | - | API key validation |
| `idx_oauth_userid_provider` | UserId, Provider | - | OAuth account lookup |
| `idx_oauth_provider_userid` | Provider, ProviderUserId | - | Unique constraint |

### Query Performance Targets

| Query Type | Target Latency | Cache Hit Rate |
|------------|----------------|----------------|
| User lookup (by ID) | <10ms | >90% |
| Session validation | <5ms | >95% |
| API key validation | <8ms | >85% |
| Login (full flow) | <200ms | N/A |
| OAuth callback | <500ms | N/A |

---

## 🧪 Testing Strategy

**Coverage Target**: 90%+ (unit), 85%+ (integration), 50+ (E2E flows)

### Test Locations

| Test Type | Location | Tools |
|-----------|----------|-------|
| **Unit Tests** | `tests/Api.Tests/BoundedContexts/Authentication/` | xUnit, FluentAssertions |
| **Integration Tests** | `tests/Api.Tests/BoundedContexts/Authentication/Integration/` | Testcontainers (PostgreSQL, Redis) |
| **E2E Tests** | `apps/web/__tests__/e2e/authentication/` | Playwright |

### Test Categories

**Unit Tests**:
- Domain: Password verification, 2FA secret validation, account lockout logic, API key generation
- Validators: Email format, password strength, required fields, code format validation
- Handlers: Success/failure scenarios, duplicate emails, 2FA flows, lockout behavior

**Integration Tests**:
- Session persistence (Redis caching, expiration cleanup)
- OAuth flow (state validation, account linking, mocked provider)
- Account lockout (5 failed logins, auto-unlock, admin unlock)
- API key rotation (create, use, rotate, verify revocation)

**E2E Tests**:
- Registration flow (form → submit → session cookie → redirect)
- Login with 2FA (credentials → TOTP prompt → code entry → session)
- OAuth login (Google redirect → callback → session creation)
- Password reset (request → email link → new password → login)

**Test Examples**: See test suite for comprehensive implementation examples

---

## 📂 Code Location

**Source**: `apps/api/src/Api/BoundedContexts/Authentication/`
- **Domain**: Entities (User, ApiKey, Session, OAuthAccount), ValueObjects (Email, PasswordHash), Repositories, Events (15+)
- **Application**: Commands (36), Queries (21), Handlers (57), DTOs (25+), Validators (20+)
- **Infrastructure**: Persistence (Repositories), Services (OAuth providers), DependencyInjection

**Routing**: `apps/api/src/Api/Routing/AuthenticationEndpoints.cs`
**Tests**: `tests/Api.Tests/BoundedContexts/Authentication/`

---

## 🔗 Related Documentation

**ADRs**: [ADR-027 Infrastructure Services](../01-architecture/adr/adr-027-infrastructure-services-policy.md), [ADR-009 Error Handling](../01-architecture/adr/adr-009-centralized-error-handling.md), [ADR-008 CQRS](../01-architecture/adr/adr-008-streaming-cqrs-migration.md)

**Bounded Contexts**: [Administration](./administration.md), [SessionTracking](../03-api/session-tracking/sse-integration.md), [UserNotifications](./user-notifications.md)

**Security**: [OAuth Testing](../05-testing/backend/oauth-testing.md), [TOTP Analysis](../06-security/totp-vulnerability-analysis.md), [Secrets Management](../04-deployment/secrets-management.md)

**API**: [Scalar Docs](http://localhost:8080/scalar/v1), [Endpoints](../03-api/endpoints/)

---

## 📈 Metrics & Monitoring

### Key Performance Indicators

| Metric | Target | Current | Trend |
|--------|--------|---------|-------|
| Login Success Rate | >95% | TBD | - |
| Session Creation Time | <200ms (P95) | TBD | - |
| 2FA Verification Time | <100ms (P95) | TBD | - |
| API Key Validation | <10ms (P95) | TBD | - |
| OAuth Flow Completion | >90% | TBD | - |

### Monitoring Queries

| Metric | Query |
|--------|-------|
| **Active Sessions** | `SELECT COUNT(*) FROM Sessions WHERE NOT IsRevoked AND ExpiresAt > NOW()` |
| **Failed Login Rate** | `SELECT COUNT(*) FROM AuditLogs WHERE Action = 'LoginFailed' AND Timestamp > NOW() - INTERVAL '1 hour'` |
| **Locked Accounts** | `SELECT COUNT(*) FROM Users WHERE IsLocked AND LockedUntil > NOW()` |

---

## 🚨 Known Issues & Limitations

### Limitations

| Issue | Impact | Workaround/Plan |
|-------|--------|-----------------|
| OAuth Email Mismatch | Cannot link OAuth if provider email differs from user | Planned enhancement |
| API Key Scopes | Scopes field exists but not enforced | Future roadmap |
| Session Revocation Cache | Revoked sessions valid up to 1 min (cache TTL) | Acceptable trade-off |
| Backup Codes | Single-use, cannot regenerate without re-enabling 2FA | Design limitation |

### Future Enhancements

**Planned**: WebAuthn/Passkeys, API key scope enforcement, session device fingerprinting, passwordless auth (magic links)
**Under Consideration**: Multi-tenancy, SSO (SAML/LDAP), audit retention policies, geolocation-based security

---

**Status**: ✅ Production (⚠️ Issue #3782 blocking login/register)
**Last Updated**: 2026-02-07
**Total Commands**: 36
**Total Queries**: 21
**Total Endpoints**: 50+
**Test Coverage**: 90%+ (unit), 85%+ (integration), 50+ (E2E flows)
**Domain Events**: 15+


---



<div style="page-break-before: always;"></div>

## bounded-contexts/business-simulations.md

# BusinessSimulations Bounded Context

**Financial ledger, cost scenarios, resource forecasts, usage analytics**

---

## Responsibilities

**Financial Ledger**:
- Income/expense transaction tracking (manual + auto-generated)
- Ledger entry CRUD with category classification
- Monthly ledger reports (scheduled job)
- Ledger summary and export (CSV)

**Cost Scenarios**:
- Agent cost estimation (per strategy, model, usage)
- Saved scenario management (create, list, delete)
- Cost-per-request and daily/monthly projections

**Resource Forecasting**:
- Resource forecast creation and management
- Growth pattern modeling (Linear, Exponential, Logarithmic, Step)
- Resource type tracking (Compute, Storage, Bandwidth, API Calls, Tokens)

**Usage Analytics**:
- Application usage statistics aggregation
- Token consumption tracking via domain events
- Infrastructure cost tracking (scheduled job)

---

## Domain Model

| Aggregate | Key Fields | Purpose |
|-----------|-----------|---------|
| **LedgerEntry** | Date, Type, Category, Amount (Money), Source, Description, Metadata | Financial transaction record |
| **CostScenario** | Name, Strategy, ModelId, MessagesPerDay, ActiveUsers, CostPerRequest, MonthlyProjection | Saved cost estimation |
| **ResourceForecast** | ResourceType, CurrentUsage, GrowthPattern, ForecastPeriodMonths | Resource projection |

**Value Objects**: Money (Amount + Currency), LedgerCategory, LedgerEntryType (Income/Expense), LedgerEntrySource (Auto/Manual), ResourceType, GrowthPattern

**Domain Events**: `LedgerEntryCreatedEvent`, `LedgerEntryUpdatedEvent`, `LedgerEntryDeletedEvent` (handled by `TokenUsageLedgerEventHandler`)

**Domain Methods**: `LedgerEntry.Create()`, `LedgerEntry.CreateAutoEntry()`, `LedgerEntry.CreateManualEntry()`, `UpdateDescription()`, `UpdateMetadata()`, `UpdateCategory()`

---

## CQRS Operations

### Commands
| Command | Handler | Description |
|---------|---------|-------------|
| `CreateManualLedgerEntryCommand` | `CreateManualLedgerEntryCommandHandler` | Create manual ledger entry |
| `UpdateLedgerEntryCommand` | `UpdateLedgerEntryCommandHandler` | Update existing entry |
| `DeleteLedgerEntryCommand` | `DeleteLedgerEntryCommandHandler` | Delete ledger entry |
| `SaveCostScenarioCommand` | `SaveCostScenarioCommandHandler` | Save cost estimation scenario |
| `DeleteCostScenarioCommand` | `DeleteCostScenarioCommandHandler` | Delete saved scenario |
| `SaveResourceForecastCommand` | `SaveResourceForecastCommandHandler` | Save resource forecast |
| `DeleteResourceForecastCommand` | `DeleteResourceForecastCommandHandler` | Delete forecast |

### Queries
| Query | Handler | Description |
|-------|---------|-------------|
| `GetLedgerEntriesQuery` | `GetLedgerEntriesQueryHandler` | List ledger entries with filters |
| `GetLedgerEntryByIdQuery` | `GetLedgerEntryByIdQueryHandler` | Get single entry |
| `GetLedgerSummaryQuery` | `GetLedgerSummaryQueryHandler` | Aggregated financial summary |
| `ExportLedgerQuery` | `ExportLedgerQueryHandler` | Export ledger to CSV |
| `GetCostScenariosQuery` | `GetCostScenariosQueryHandler` | List saved scenarios |
| `EstimateAgentCostQuery` | `EstimateAgentCostQueryHandler` | Calculate agent cost estimate |
| `GetResourceForecastsQuery` | `GetResourceForecastsQueryHandler` | List resource forecasts |
| `EstimateResourceForecastQuery` | `EstimateResourceForecastQueryHandler` | Calculate resource projection |
| `GetAppUsageStatsQuery` | `GetAppUsageStatsQueryHandler` | Application usage statistics |

---

## Scheduled Jobs

| Job | Description |
|-----|-------------|
| `InfrastructureCostTrackingJob` | Auto-generates ledger entries for infrastructure costs |
| `MonthlyLedgerReportJob` | Generates monthly financial summaries |

---

## Infrastructure

### Repositories
- `ILedgerEntryRepository` → `LedgerEntryRepository`
- `ICostScenarioRepository` → `CostScenarioRepository`
- `IResourceForecastRepository` → `ResourceForecastRepository`

### Services
- `ILedgerTrackingService` → `LedgerTrackingService` (auto-tracking via events)

### DI Registration
- `BusinessSimulationsServiceExtensions.AddBusinessSimulations()`

---

## Dependencies

- **Upstream**: Administration (token/billing data), KnowledgeBase (token usage events)
- **Downstream**: None (leaf context)
- **Events consumed**: Token usage events from KnowledgeBase → `TokenUsageLedgerEventHandler`

---

## Related Issues

- Epic #3688: Business and Simulations
- Issue #3720: Financial Ledger Data Model
- Issue #3725: Agent Cost Calculator

---

**Last Updated**: 2026-02-18
**Status**: Production
**Code**: `apps/api/src/Api/BoundedContexts/BusinessSimulations/`


---



<div style="page-break-before: always;"></div>

## bounded-contexts/diagram-summary.md

# Bounded Context Diagrams - Creation Summary

**Task Completion**: Issue #3794 - Mermaid diagram generation for all bounded contexts

---

## ✅ Deliverables

### Created Diagrams: 32 New Files

**8 Contexts × 4 Diagrams Each** = 32 new Mermaid diagrams

| Context | Entities | Command Flow 1 | Flow 2 | Integration | Total |
|---------|----------|----------------|--------|-------------|-------|
| UserLibrary | ✅ entities.mmd | ✅ flow-add-game.mmd | ✅ flow-upload-private-pdf.mmd | ✅ integration-flow.mmd | 4 |
| Administration | ✅ entities.mmd | ✅ flow-suspend-user.mmd | ✅ flow-add-token-credits.mmd | ✅ integration-flow.mmd | 4 |
| DocumentProcessing | ✅ entities.mmd | ✅ flow-extract-pdf.mmd | ✅ flow-upload-pdf.mmd | ✅ integration-flow.mmd | 4 |
| SharedGameCatalog | ✅ entities.mmd | ✅ flow-approve-publication.mmd | ✅ flow-search-games.mmd | ✅ integration-flow.mmd | 4 |
| SystemConfiguration | ✅ entities.mmd | ✅ flow-update-config.mmd | ✅ flow-tier-routing.mmd | ✅ integration-flow.mmd | 4 |
| UserNotifications | ✅ entities.mmd | ✅ flow-create-notification.mmd | ✅ flow-get-notifications.mmd | ✅ integration-flow.mmd | 4 |
| WorkflowIntegration | ✅ entities.mmd | ✅ flow-create-n8n-config.mmd | ✅ flow-test-connection.mmd | ✅ integration-flow.mmd | 4 |
| SessionTracking | ✅ entities.mmd | ✅ flow-create-session.mmd | ✅ flow-roll-dice.mmd | ✅ integration-flow.mmd | 4 |

**Supporting Documentation**: 2 files
- ✅ README.md: Usage guide and conventions
- ✅ INDEX.md: Quick navigation reference

**Total New Files**: 34

---

## 📊 Complete Diagram Inventory

### Total Diagrams: 43 Mermaid Files

| Context | Diagrams | Status |
|---------|----------|--------|
| Authentication | 4 | 🟢 Previously Complete |
| GameManagement | 1 | 🟢 Previously Complete |
| KnowledgeBase | 1 | 🟢 Previously Complete |
| UserLibrary | 4 | ✅ **NEW** |
| Administration | 4 | ✅ **NEW** |
| DocumentProcessing | 4 | ✅ **NEW** |
| SharedGameCatalog | 4 | ✅ **NEW** |
| SystemConfiguration | 4 | ✅ **NEW** |
| UserNotifications | 4 | ✅ **NEW** |
| WorkflowIntegration | 4 | ✅ **NEW** |
| SessionTracking | 4 | ✅ **NEW** |

---

## 🎯 Diagram Features

### Entity Relationship Diagrams
- **Total Entities Documented**: 50+ aggregates and entities
- **Relationships Mapped**: 100+ cardinality relationships
- **Properties Documented**: Primary keys, foreign keys, key business fields

### Command/Query Flow Diagrams
- **CQRS Pattern**: All flows show MediatR pipeline
- **Validation**: FluentValidation integration shown
- **Domain Events**: Event raising and publishing visualized
- **Side Effects**: Database operations, cache invalidation, external calls

### Integration Diagrams
- **Event-Driven**: Domain event communication between contexts
- **Direct Dependencies**: FK references and query patterns
- **External Services**: S3, Qdrant, SMTP, n8n, BGG API
- **Cache Layer**: Redis integration patterns

---

## 📁 File Locations

*(blocco di codice rimosso)*

---

## 🔍 Key Highlights

### UserLibrary Context
- **Quota Enforcement**: Tier-based library limits in add-game flow
- **Private PDFs**: UserLibrary → DocumentProcessing integration with SSE progress
- **Agent Configuration**: Per-game AI model preferences (Value Object)

### Administration Context
- **Most Complex**: 100 operations across 19 workflow areas
- **Token System**: Complete credit/debit tracking with transaction history
- **Batch Jobs**: Background job system with progress tracking
- **Alert System**: Rule-based monitoring with admin notifications

### DocumentProcessing Context
- **3-Stage Pipeline**: Unstructured → SmolDocling → Docnet fallback chain
- **Quality Thresholds**: 0.80 (high), 0.70 (medium), <0.70 (fallback)
- **Chunked Uploads**: Resumable upload for large PDFs (>50 MB)
- **External Services**: 3 Python microservices + S3 + Qdrant

### SharedGameCatalog Context
- **Largest Context**: 69 operations, 80+ endpoints
- **Publication Workflow**: Draft → PendingApproval → Published
- **Soft Delete**: ADR-019 pattern with audit trail
- **Badge System**: Contribution tracking with gamification

### SystemConfiguration Context
- **Runtime Config**: Key-value store with versioning and rollback
- **Tier Routing**: LLM model selection by subscription tier
- **Feature Flags**: Tier + role-based feature enablement
- **Cache Invalidation**: Event-driven config propagation

### UserNotifications Context
- **Event-Driven**: 11 event handlers from all contexts
- **20+ Types**: Comprehensive notification type system
- **Email Integration**: SMTP for critical notifications
- **Background Jobs**: Daily digest emails to admins

### WorkflowIntegration Context
- **n8n Integration**: Workflow automation platform
- **Webhook System**: Domain events → n8n workflow triggers
- **API Key Encryption**: AES-256 encrypted storage
- **Error Logging**: Workflow execution failure tracking

### SessionTracking Context
- **Real-Time**: SSE streaming for live session updates
- **Cryptographic Features**: CSPRNG for dice, shuffle, session codes
- **Note System**: AES-256 encrypted private notes with reveal mechanism
- **Game Tools**: Dice, cards, spinner, coin flip with secure randomness

---

## 🎨 Diagram Quality Standards

### Entity Diagrams
✅ All aggregates and entities documented
✅ Primary/Foreign/Unique keys annotated
✅ Cardinality relationships specified
✅ Key business properties included

### Flow Diagrams
✅ Complete CQRS pipeline shown (Endpoint → Mediator → Handler → Domain)
✅ Validation logic illustrated
✅ Domain events raised and published
✅ Database operations and cache invalidation
✅ Side effects documented with notes

### Integration Diagrams
✅ Event-driven communication (solid arrows)
✅ Direct dependencies (dashed arrows)
✅ External service integration
✅ Consistent color-coding by context
✅ Clear subgraph boundaries

---

## 🔗 Cross-References

### Documentation Links
- [Bounded Context Complete Docs](D:\Repositories\meepleai-monorepo-backend\docs\09-bounded-contexts\)
- [Architecture ADRs](D:\Repositories\meepleai-monorepo-backend\docs\01-architecture\adr\)
- [CQRS Guidelines](D:\Repositories\meepleai-monorepo-backend\docs\02-development\coding-standards.md)

### Related Issues
- #3794: Complete bounded context documentation
- #3692: Token management system (Administration diagrams)
- #3693: Batch job system (Administration diagrams)
- #3489: Private PDF support (UserLibrary + DocumentProcessing)
- #3511: Label system (UserLibrary diagrams)
- #3691: Audit logging (Administration diagrams)

---

**Generated**: 2026-02-07
**By**: Claude Code
**Method**: Automated from *-COMPLETE.md documentation
**Quality**: Production-ready
**Total Time**: ~15 minutes
**Token Usage**: ~190K tokens


---



<div style="page-break-before: always;"></div>

## bounded-contexts/diagrams/INDEX.md

# Bounded Context Diagrams - Complete Index

**Visual architecture documentation for all 11 bounded contexts**

## 📊 Quick Navigation

| Context | Entities | Primary Command | Secondary Flow | Integration |
|---------|----------|-----------------|----------------|-------------|
| **UserLibrary** | [entities.mmd](./user-library/entities.mmd) | [Add Game](./user-library/flow-add-game.mmd) | [Upload PDF](./user-library/flow-upload-private-pdf.mmd) | [integration](./user-library/integration-flow.mmd) |
| **Administration** | [entities.mmd](./administration/entities.mmd) | [Suspend User](./administration/flow-suspend-user.mmd) | [Add Credits](./administration/flow-add-token-credits.mmd) | [integration](./administration/integration-flow.mmd) |
| **DocumentProcessing** | [entities.mmd](./document-processing/entities.mmd) | [Extract PDF](./document-processing/flow-extract-pdf.mmd) | [Upload PDF](./document-processing/flow-upload-pdf.mmd) | [integration](./document-processing/integration-flow.mmd) |
| **SharedGameCatalog** | [entities.mmd](./shared-game-catalog/entities.mmd) | [Approve Publication](./shared-game-catalog/flow-approve-publication.mmd) | - | [integration](./shared-game-catalog/integration-flow.mmd) |
| **SystemConfiguration** | [entities.mmd](./system-configuration/entities.mmd) | [Update Config](./system-configuration/flow-update-config.mmd) | [Tier Routing](./system-configuration/flow-tier-routing.mmd) | [integration](./system-configuration/integration-flow.mmd) |
| **UserNotifications** | [entities.mmd](./user-notifications/entities.mmd) | [Create Notification](./user-notifications/flow-create-notification.mmd) | [Get Notifications](./user-notifications/flow-get-notifications.mmd) | [integration](./user-notifications/integration-flow.mmd) |
| **WorkflowIntegration** | [entities.mmd](./workflow-integration/entities.mmd) | [Create n8n Config](./workflow-integration/flow-create-n8n-config.mmd) | [Test Connection](./workflow-integration/flow-test-connection.mmd) | [integration](./workflow-integration/integration-flow.mmd) |
| **SessionTracking** | [entities.mmd](./session-tracking/entities.mmd) | [Create Session](./session-tracking/flow-create-session.mmd) | [Roll Dice](./session-tracking/flow-roll-dice.mmd) | [integration](./session-tracking/integration-flow.mmd) |
| **Authentication** | [entities.mmd](./authentication/entities.mmd) | [Registration](./authentication/flow-registration.mmd) | [Login 2FA](./authentication/flow-login-2fa.mmd) | [integration](./authentication/integration-flow.mmd) |
| **GameManagement** | [entities.mmd](./game-management/entities.mmd) | - | - | - |
| **KnowledgeBase** | [entities.mmd](./knowledge-base/entities.mmd) | - | - | - |

## 🎯 Diagram Type Descriptions

### 1. Entity Relationship Diagrams (entities.mmd)

**Purpose**: Database schema and domain model visualization

**Key Elements**:
- Aggregates and entities
- Value objects
- Primary keys (PK), Foreign keys (FK), Unique keys (UK)
- Cardinality relationships
- Key properties for each entity

**Example Use Cases**:
- Understanding data model structure
- Planning database migrations
- Identifying relationships between entities
- Domain-Driven Design reference

### 2. Primary Command Flow Diagrams (flow-*.mmd)

**Purpose**: CQRS command execution visualization

**Key Elements**:
- HTTP request/response cycle
- MediatR pipeline (Command → Validator → Handler)
- Domain method invocations
- Database operations
- Event publishing
- Side effects

**Example Use Cases**:
- Understanding command execution flow
- Debugging business logic issues
- Implementing new commands
- Testing strategy planning

### 3. Secondary Flow Diagrams (flow-*.mmd)

**Purpose**: Additional important operations (queries, specialized commands)

**Key Elements**:
- Query execution patterns
- Caching strategies
- Background job processing
- Complex workflows
- External service integration

**Example Use Cases**:
- Understanding query optimization
- Planning caching strategies
- Debugging performance issues
- Integration testing design

### 4. Integration Flow Diagrams (integration-flow.mmd)

**Purpose**: Cross-context communication and dependencies

**Key Elements**:
- Event-driven communication (solid arrows)
- Direct dependencies (dashed arrows)
- External service integration
- Context boundaries
- Communication patterns

**Example Use Cases**:
- Understanding system architecture
- Planning new features across contexts
- Identifying event subscribers
- Debugging integration issues

## 🔍 Diagram Details by Context

### UserLibrary Context
**Aggregates**: UserLibraryEntry, Label, LibraryShareLink
**Key Flows**:
- Add game to library with quota enforcement
- Upload private PDF with SSE progress tracking
**Integrations**: GameManagement (game metadata), DocumentProcessing (PDF pipeline), UserNotifications (share link access)

### Administration Context
**Aggregates**: User (extended), AuditLog, TokenBalance, TokenTransaction, BatchJob, AlertRule, Alert
**Key Flows**:
- Suspend user with session invalidation
- Add token credits with transaction history
**Integrations**: All contexts (audit logging), KnowledgeBase (token tracking), UserNotifications (alerts)

### DocumentProcessing Context
**Aggregates**: PdfDocument, ExtractionAttempt, DocumentCollection, ChunkedUploadSession
**Key Flows**:
- 3-stage extraction pipeline (Unstructured → SmolDocling → Docnet)
- Upload PDF with background job processing
**Integrations**: UserLibrary (private PDFs), KnowledgeBase (RAG indexing), External Python services

### SharedGameCatalog Context
**Aggregates**: SharedGame, ShareRequest, DeleteRequest, Badge, UserBadge
**Key Flows**:
- Approve publication with badge system integration
**Integrations**: GameManagement (private games), DocumentProcessing (PDF processing), BoardGameGeek API

### SystemConfiguration Context
**Aggregates**: Configuration, AiModel, FeatureFlag, TierRouting, various Limits
**Key Flows**:
- Update configuration with versioning and cache invalidation
- Update tier routing for LLM model selection
**Integrations**: All contexts (runtime config), KnowledgeBase (model routing)

### UserNotifications Context
**Aggregates**: Notification
**Key Flows**:
- Create notification from domain events
- Get notifications with caching
**Integrations**: All contexts (event listeners), External SMTP/FCM

### WorkflowIntegration Context
**Aggregates**: N8NConfiguration, WorkflowErrorLog
**Key Flows**:
- Create n8n configuration with encryption
- Test connection with health check
**Integrations**: All contexts (webhook triggers), External n8n instance

### SessionTracking Context
**Aggregates**: GameSession, SessionNote, SessionDeck, DiceRoll, ScoreEntry
**Key Flows**:
- Create session with code generation
- Roll dice with cryptographic randomness
**Integrations**: GameManagement (game metadata), Redis (SSE pub/sub)

## 🎨 Viewing Recommendations

### VS Code
1. Install: **Markdown Preview Mermaid Support** extension
2. Open any `.mmd` file
3. Right-click → "Open Preview to the Side"

### IntelliJ/WebStorm
1. Enable built-in Mermaid support (Settings → Languages → Mermaid)
2. Open `.mmd` files
3. Preview pane shows rendered diagram

### Online
1. Visit [Mermaid Live Editor](https://mermaid.live)
2. Copy/paste diagram content
3. Export as PNG/SVG

## 🔗 Cross-Reference Links

### By Feature
- **PDF Processing**: DocumentProcessing [entities](./document-processing/entities.mmd), [extraction flow](./document-processing/flow-extract-pdf.mmd)
- **Token System**: Administration [entities](./administration/entities.mmd), [add credits flow](./administration/flow-add-token-credits.mmd)
- **Share Requests**: SharedGameCatalog [entities](./shared-game-catalog/entities.mmd), [approval flow](./shared-game-catalog/flow-approve-publication.mmd)
- **Real-Time Sessions**: SessionTracking [entities](./session-tracking/entities.mmd), [create session](./session-tracking/flow-create-session.mmd)

### By Integration Pattern
- **Event-Driven**: All [integration-flow.mmd](./user-library/integration-flow.mmd) diagrams
- **External Services**: [DocumentProcessing integration](./document-processing/integration-flow.mmd), [WorkflowIntegration](./workflow-integration/integration-flow.mmd)
- **Caching Patterns**: [SystemConfiguration flows](./system-configuration/flow-update-config.mmd), [UserNotifications queries](./user-notifications/flow-get-notifications.mmd)

### By Technical Pattern
- **CQRS**: All flow-* diagrams show MediatR pipeline
- **Soft Delete**: SharedGameCatalog [entities](./shared-game-catalog/entities.mmd)
- **Encryption**: SessionTracking [entities](./session-tracking/entities.mmd), WorkflowIntegration [config flow](./workflow-integration/flow-create-n8n-config.mmd)
- **SSE Streaming**: DocumentProcessing [upload flow](./document-processing/flow-upload-pdf.mmd), SessionTracking [dice roll](./session-tracking/flow-roll-dice.mmd)

---

**Created**: 2026-02-07
**Total Diagrams**: 42 .mmd files + 1 README + 1 INDEX
**Contexts Covered**: 8 new + 3 existing = 11 total
**Diagram Types**: Entities (8), Command Flows (11), Query Flows (3), Integration (8)


---



<div style="page-break-before: always;"></div>

## bounded-contexts/document-processing.md

# DocumentProcessing Bounded Context

**PDF Upload, 3-Stage Extraction, Chunking, Vector Indexing**

---

## Responsibilities

- PDF upload & storage (S3/local)
- 3-stage extraction (Unstructured → SmolDocling → Docnet) per ADR-003b
- Chunked uploads (resumable, 5MB chunks)
- Private PDF support
- Document collections (multi-PDF organization)
- Processing progress (SSE streaming)
- Quality validation (text coverage, structure)
- Vector indexing (Qdrant integration)
- Background jobs
- Quota enforcement (tier-based)

---

## Domain Model

| Aggregate | Key Fields | Purpose |
|-----------|-----------|---------|
| **PdfDocument** | Id, GameId, UploadedByUserId, FileName, Status, ExtractedText, QualityScore, IsDeleted | Aggregate root |
| **ExtractionAttempt** | PdfDocumentId, Stage, QualityScore, Succeeded, DurationMs | Pipeline tracking |
| **DocumentCollection** | GameId, UserId, Name, Documents[] | Multi-PDF grouping |
| **ChunkedUploadSession** | SessionId, TotalChunks, ReceivedChunks, IsComplete | Resumable uploads |

**Value Objects**: ExtractionStage (Unstructured/SmolDocling/Docnet), ProcessingStatus (Pending/Extracting/Completed/Failed), ProcessingProgress

**Domain Methods**: `StartProcessing()`, `RecordAttempt()`, `MarkCompleted()`, `MarkFailed()`, `SoftDelete()`, `AddDocument()`

---

## API Operations (26 total)

**14 Commands**: UploadPdf, UploadPrivatePdf, InitChunkedUpload, UploadChunk, CompleteChunkedUpload, ExtractPdfText, IndexPdf, DeletePdf, SetPdfVisibility, CancelPdfProcessing, CreateDocumentCollection, AddDocumentToCollection, RemoveDocumentFromCollection, GenerateRuleSpecFromPdf

**10 Queries**: GetPdfText, DownloadPdf, GetPdfDocumentsByGame, GetPdfDocumentById, GetPdfOwnership, GetPdfProgress, GetCollectionByGame, GetCollectionById, GetCollectionsByUser, ExtractBggGamesFromPdf

**2 Background Jobs**: PDF extraction pipeline, Qdrant indexing

---

## Key Endpoints

| Method | Endpoint | Auth | Purpose |
|--------|----------|------|---------|
| POST | `/api/v1/ingest/pdf` | Session | Upload PDF |
| POST | `/api/v1/users/{userId}/library/entries/{entryId}/pdf` | Session | Upload private PDF |
| POST | `/api/v1/ingest/pdf/chunked/init` | Session | Init chunked upload |
| POST | `/api/v1/ingest/pdf/chunked/chunk` | Session | Upload chunk |
| POST | `/api/v1/ingest/pdf/chunked/complete` | Session | Complete chunked |
| POST | `/api/v1/ingest/pdf/{pdfId}/extract` | Admin | Extract text |
| POST | `/api/v1/ingest/pdf/{pdfId}/index` | Admin | Vector index |
| GET | `/api/v1/pdfs/{pdfId}/text` | Session | Get extracted text |
| GET | `/api/v1/pdfs/{pdfId}/download` | Session | Download PDF |
| GET | `/api/v1/pdfs/{pdfId}/progress` | Session | Progress (SSE) |
| DELETE | `/api/v1/pdf/{pdfId}` | Session+Owner | Soft-delete |

---

## 3-Stage Extraction Pipeline (ADR-003b)

| Stage | Service | Target Quality | Success Rate | Latency |
|-------|---------|----------------|--------------|---------|
| **Stage 1** | Unstructured.py | ≥ 0.80 | 80% | <2s P95 |
| **Stage 2** | SmolDocling VLM | ≥ 0.70 | 15% | <10s P95 |
| **Stage 3** | Docnet OCR | Accept any | 5% | <5s P95 |

**Quality Calculation**: `TextQuality = 0.5 + (chars_per_page - 500) / 500 * 0.5`

**Decision Logic**: Stage N quality ≥ threshold → RETURN ✅ | Else → fallback to Stage N+1

**Guaranteed Success**: Stage 3 always returns result (best-effort)

---

## Domain Events

| Event | When | Subscribers |
|-------|------|-------------|
| `PdfUploadedEvent` | Upload complete | Administration (audit) |
| `PdfExtractedEvent` | Extraction done | KnowledgeBase (index trigger) |
| `ExtractionStageCompletedEvent` | Stage done | Administration (metrics) |
| `PdfIndexedEvent` | Indexing done | KnowledgeBase, Administration |
| `PdfDeletedEvent` | PDF deleted | KnowledgeBase (cleanup vectors) |
| `ProcessingFailedEvent` | Error | UserNotifications |
| `ChunkedUploadCompletedEvent` | Chunks assembled | Administration |

---

## Integration Points

**Inbound**:
- UserLibrary → UploadPrivatePdf
- SharedGameCatalog → ExtractBggGamesFromPdf

**Outbound**:
- Blob Storage (S3/R2/Local) via `IBlobStorageService`
- Python Services: Unstructured (8000), SmolDocling (8001), Embedding (8002)
- Qdrant (vector DB) via collection: `pdfs`, namespace: `game_{gameId}`
- KnowledgeBase → PdfIndexedEvent enables RAG queries

---

## Security & Quotas

**Access Control**:
- Upload: Authenticated users (quota enforced)
- Download: Owner OR Admin (row-level security)
- Processing: Admin/Editor only
- Private PDFs: Owner only (no admin override)

**Quota Limits**:

| Tier | Storage | File Size | Daily Uploads |
|------|---------|-----------|---------------|
| Free | 100 MB | 10 MB | 5 |
| Basic | 1 GB | 50 MB | 20 |
| Premium | 10 GB | 100 MB | 100 |
| Enterprise | Unlimited | 500 MB | Unlimited |

---

## Performance

**Latency Targets**:
- Total pipeline: <15s P95 (all stages)
- Stage 1: <2s, Stage 2: <10s, Stage 3: <5s

**Caching**:
- GetPdfText: Redis 1h (invalidate: PdfExtractedEvent)
- GetPdfProgress: HybridCache 10s (event-driven)
- GetPdfDocumentsByGame: Redis 30m (invalidate: PdfUploadedEvent, PdfDeletedEvent)

**Database Indexes**:
*(blocco di codice rimosso)*

---

## Testing

**Unit Tests** (`tests/Api.Tests/DocumentProcessing/`):
- EnhancedPdfProcessingOrchestrator_Tests.cs (3-stage pipeline logic)
- QualityCalculator_Tests.cs (quality score formulas)
- ChunkedUploadSession_Tests.cs (out-of-order chunks)
- PdfDocument_Tests.cs (state transitions)

**Integration Tests** (Testcontainers: PostgreSQL, MinIO, Qdrant):
1. End-to-End Upload → Extract → Index
2. 3-Stage Fallback (force Stage 1 failure → verify Stage 2 called)
3. Chunked Upload (10 chunks, missing chunk → complete → reassemble)
4. Private PDF Namespace (isolation verification)

---

## Code Location

`apps/api/src/Api/BoundedContexts/DocumentProcessing/`

---

## Related Documentation

**ADRs**:
- [ADR-003b: Unstructured PDF Processing](../../01-architecture/adr/adr-003b-unstructured-pdf.md)
- [ADR-026: Document Collections](../../01-architecture/adr/adr-026-document-collections.md)

**Contexts**:
- [KnowledgeBase](./knowledge-base.md) - RAG indexing
- [UserLibrary](./user-library.md) - Private PDFs
- [SharedGameCatalog](./shared-game-catalog.md) - BGG extraction

---

**Status**: ✅ Production
**Commands**: 14 | **Queries**: 10 | **Extraction Success**: 100% (3-stage fallback) | **Avg Processing**: <15s P95


---



<div style="page-break-before: always;"></div>

## bounded-contexts/game-management.md

# GameManagement Bounded Context

**Game catalog, sessions, rules, FAQs, and state management**

> 📖 **Testing Reference**: See `tests/Api.Tests/GameManagement/` for implementation examples

---

## 📋 Responsibilities

- Game catalog CRUD (BGG integration, image upload)
- Game sessions (lifecycle: Setup → Active → Paused → Completed/Abandoned)
- Game state tracking (JSON state + snapshots, undo/redo support)
- Rule specifications (versioning, collaborative editing with locks)
- Rule comments (threaded discussions, resolution tracking)
- Game FAQs (community-driven Q&A)
- AI move suggestions (Player Mode integration)
- Similar games (RAG-based recommendations)
- Publication workflow (SharedGameCatalog integration)

---

## 🏗️ Domain Model

### Aggregates & Entities

| Aggregate/Entity | Key Fields | Domain Methods |
|-----------------|------------|----------------|
| **Game** (Root) | Title, Publisher, YearPublished, PlayerCount, PlayTime, BggId, SharedGameId, IsPublished, ApprovalStatus | UpdateDetails(), LinkToBggGame(), LinkToSharedGame(), Publish(), SetApprovalStatus() |
| **GameSession** (Root) | GameId, CreatedByUserId, Status, StartedAt, CompletedAt, WinnerName, Players | Start(), Pause(), Resume(), Complete(), Abandon(), AddPlayer() |
| **SessionPlayer** (Entity) | PlayerName, PlayerOrder, Color | - |
| **GameSessionState** (Entity) | GameSessionId, CurrentTurn, CurrentPhase, StateData (JSON), Snapshots | UpdateState(), CreateSnapshot(), RestoreFromSnapshot() |
| **GameStateSnapshot** (Entity) | TurnNumber, SnapshotData, Description | - |
| **RuleSpec** (Root) | GameId, Version, Content (Markdown), IsCurrent, CreatedBy, Comments | UpdateContent(), AddComment() |
| **RuleComment** (Entity) | RuleSpecId, LineNumber, CommentText, IsResolved, ParentCommentId | Resolve(), Unresolve(), Update() |
| **EditorLock** (Entity) | GameId, LockedBy, ExpiresAt | Refresh() |

### Value Objects

| Value Object | Validation Rules | Purpose |
|-------------|------------------|---------|
| **GameTitle** | 1-200 chars, required, trimmed | Game name with validation |
| **Publisher** | 1-200 chars, optional | Publisher name |
| **YearPublished** | 1900-current year | Publication year constraint |
| **PlayerCount** | Min/Max: 1-100, Min ≤ Max | Player count range |
| **PlayTime** | Min/Max: 1-10000 minutes, Min ≤ Max | Play duration range |
| **SessionStatus** | Enum: Setup, Active, Paused, Completed, Abandoned | Session lifecycle state |

---

## 📡 Application Layer (CQRS)

> **Total**: 47 operations (26 commands + 21 queries)

### Operation Matrix

| Category | Commands | Queries | Key Features |
|----------|----------|---------|--------------|
| **Game Retrieval** | 0 | 4 | Public access, pagination, search, similar games (RAG) |
| **Game Management** | 4 | 0 | Admin/Editor only, BGG import, image upload, publish |
| **Session Lifecycle** | 7 | 0 | Session-required, quota enforcement |
| **Session Queries** | 0 | 6 | Analytics, history, stats |
| **Game State** | 4 | 2 | Initialize, update, snapshot, restore |
| **AI Suggestions** | 2 | 0 | Move suggestions, apply |
| **Rule Specs** | 3 | 4 | Versioning, history, diff |
| **Rule Comments** | 5 | 2 | Threading, resolution |
| **Editor Locks** | 3 | 1 | Collaborative editing |
| **Bulk Operations** | 2 | 0 | Export, PDF import |

### A. GAME RETRIEVAL (Public)

| Query | Endpoint | Auth | Test Reference |
|-------|----------|------|----------------|
| `GetAllGamesQuery` | GET `/api/v1/games` | 🟢 Public | `GetAllGamesQueryHandler_Tests.cs` |
| `GetGameByIdQuery` | GET `/api/v1/games/{id}` | 🟢 Public | `GetGameByIdQueryHandler_Tests.cs` |
| `GetGameDetailsQuery` | GET `/api/v1/games/{id}/details` | 🟡 Auth | `GetGameDetailsQueryHandler_Tests.cs` |
| `GetSimilarGamesQuery` | GET `/api/v1/games/{id}/similar` | 🟢 Public | `SimilarGamesService_Tests.cs` |

**Query Parameters**:
- `GetAllGamesQuery`: `search?`, `page?`, `pageSize?` (max: 100)
- `GetSimilarGamesQuery`: `limit?` (default: 5), `minSimilarity?` (0-1)

### B. GAME MANAGEMENT (Admin/Editor)

| Command | Endpoint | Auth | Test Reference |
|---------|----------|------|----------------|
| `CreateGameCommand` | POST `/api/v1/games` | 🔴 Admin/Editor | `CreateGameCommandHandler_Tests.cs`, `CreateGameCommandValidator_Tests.cs` |
| `UpdateGameCommand` | PUT `/api/v1/games/{id}` | 🔴 Admin/Editor | `UpdateGameCommandHandler_Tests.cs` |
| `PublishGameCommand` | PUT `/api/v1/games/{id}/publish` | 🔴 Admin | `PublishGameCommandHandler_Tests.cs` |
| `UploadGameImageCommand` | POST `/api/v1/games/upload-image` | 🔴 Admin/Editor | `UploadGameImageCommandHandler_Tests.cs` |

**Validation**:
- Title: 1-200 chars
- YearPublished: 1900-current
- PlayerCount: 1-100, Min ≤ Max
- PlayTime: 1-10000 minutes

### C. SESSION LIFECYCLE (Session-Required)

| Command | Endpoint | Status Transition | Test Reference |
|---------|----------|------------------|----------------|
| `StartGameSessionCommand` | POST `/api/v1/sessions` | - → Setup | `StartGameSessionCommandHandler_Tests.cs` |
| `AddPlayerToSessionCommand` | POST `/api/v1/sessions/{id}/players` | - | `AddPlayerToSessionCommandHandler_Tests.cs` |
| `PauseGameSessionCommand` | POST `/api/v1/sessions/{id}/pause` | Active → Paused | `PauseGameSessionCommandHandler_Tests.cs` |
| `ResumeGameSessionCommand` | POST `/api/v1/sessions/{id}/resume` | Paused → Active | `ResumeGameSessionCommandHandler_Tests.cs` |
| `CompleteGameSessionCommand` | POST `/api/v1/sessions/{id}/complete` | Active → Completed | `CompleteGameSessionCommandHandler_Tests.cs` |
| `AbandonGameSessionCommand` | POST `/api/v1/sessions/{id}/abandon` | Any → Abandoned | `AbandonGameSessionCommandHandler_Tests.cs` |
| `EndGameSessionCommand` | POST `/api/v1/sessions/{id}/end` | Active → Completed | (Alias for Complete) |

**Session Status Flow**:
*(blocco di codice rimosso)*

### D. SESSION QUERIES (Authenticated)

| Query | Endpoint | Test Reference |
|-------|----------|----------------|
| `GetGameSessionByIdQuery` | GET `/api/v1/sessions/{id}` | `GetGameSessionByIdQueryHandler_Tests.cs` |
| `GetGameSessionsQuery` | GET `/api/v1/games/{gameId}/sessions` | `GetGameSessionsQueryHandler_Tests.cs` |
| `GetActiveSessionsByGameQuery` | GET `/api/v1/games/{gameId}/sessions/active` | `GetActiveSessionsByGameQueryHandler_Tests.cs` |
| `GetActiveSessionsQuery` | GET `/api/v1/sessions/active` | `GetActiveSessionsQueryHandler_Tests.cs` |
| `GetSessionHistoryQuery` | GET `/api/v1/sessions/history` | `GetSessionHistoryQueryHandler_Tests.cs` |
| `GetSessionStatsQuery` | GET `/api/v1/sessions/statistics` | `GetSessionStatsQueryHandler_Tests.cs` |

**Analytics Data**: Total sessions, play time averages, most played games, top players, win rates

### E. GAME STATE MANAGEMENT (Session-Required)

| Command/Query | Endpoint | Purpose | Test Reference |
|---------------|----------|---------|----------------|
| `InitializeGameStateCommand` | POST `/api/v1/sessions/{sessionId}/state/initialize` | Create state tracking | `InitializeGameStateCommandHandler_Tests.cs` |
| `UpdateGameStateCommand` | PATCH `/api/v1/sessions/{sessionId}/state` | Update after move | `UpdateGameStateCommandHandler_Tests.cs` |
| `CreateStateSnapshotCommand` | POST `/api/v1/sessions/{sessionId}/state/snapshots` | Checkpoint state | `CreateStateSnapshotCommandHandler_Tests.cs` |
| `RestoreStateSnapshotCommand` | POST `/api/v1/sessions/{sessionId}/state/restore/{snapshotId}` | Undo to snapshot | `RestoreStateSnapshotCommandHandler_Tests.cs` |
| `GetGameStateQuery` | GET `/api/v1/sessions/{sessionId}/state` | Current state | `GetGameStateQueryHandler_Tests.cs` |
| `GetStateSnapshotsQuery` | GET `/api/v1/sessions/{sessionId}/state/snapshots` | Snapshot list | `GetStateSnapshotsQueryHandler_Tests.cs` |

**State Storage**: Flexible JSON schema, game-specific structure

### F. AI MOVE SUGGESTIONS (Player Mode)

| Command | Endpoint | Integration | Test Reference |
|---------|----------|-------------|----------------|
| `SuggestMoveCommand` | POST `/api/v1/sessions/{sessionId}/suggest-move` | KnowledgeBase AgentTypology | `SuggestMoveCommandHandler_Tests.cs` |
| `ApplySuggestionCommand` | POST `/api/v1/sessions/{sessionId}/apply-suggestion` | Game state update | `ApplySuggestionCommandHandler_Tests.cs` |

**AI Response**: Move description, reasoning, confidence (0-1), expected points, alternatives

### G-I. RULE SPECIFICATIONS

| Category | Operations | Test Reference |
|----------|-----------|----------------|
| **Core** | 3 commands, 1 query | `RuleSpec_Tests.cs`, `UpdateRuleSpecCommandHandler_Tests.cs` |
| **Versioning** | 4 queries | `GetVersionHistoryQueryHandler_Tests.cs`, `ComputeRuleSpecDiffQueryHandler_Tests.cs` |
| **Comments** | 5 commands, 2 queries | `RuleComment_Tests.cs`, `CreateRuleCommentCommandHandler_Tests.cs` |

**Versioning**: Auto-increment on update, IsCurrent flag, history tracking
**Comments**: Line-specific or general, threading support, resolution tracking

### J. COLLABORATIVE EDITING

| Command/Query | Endpoint | Purpose | Test Reference |
|---------------|----------|---------|----------------|
| `AcquireEditorLockCommand` | POST `/api/v1/games/{gameId}/rulespec/lock` | Lock before edit | `AcquireEditorLockCommandHandler_Tests.cs` |
| `ReleaseEditorLockCommand` | DELETE `/api/v1/games/{gameId}/rulespec/lock` | Release lock | `ReleaseEditorLockCommandHandler_Tests.cs` |
| `RefreshEditorLockCommand` | POST `/api/v1/games/{gameId}/rulespec/lock/refresh` | Keep-alive | `RefreshEditorLockCommandHandler_Tests.cs` |
| `GetEditorLockStatusQuery` | GET `/api/v1/games/{gameId}/rulespec/lock` | Check lock | `GetEditorLockStatusQueryHandler_Tests.cs` |

**Lock Configuration**: 5-minute timeout, auto-expiration, 409 Conflict on conflict

---

## 🔄 Domain Events

| Event | Trigger | Payload | Subscribers |
|-------|---------|---------|-------------|
| `GameCreatedEvent` | Game creation | GameId, Title, CreatedBy | Administration (audit), SharedGameCatalog |
| `GamePublishedEvent` | Publish command | GameId, SharedGameId | SharedGameCatalog (publication request) |
| `GameSessionStartedEvent` | Session start | SessionId, GameId, PlayerCount | SessionTracking, UserLibrary |
| `GameSessionCompletedEvent` | Session complete | SessionId, WinnerName, Duration | SessionTracking, UserLibrary |
| `GameStateUpdatedEvent` | State update | SessionId, TurnNumber | SessionTracking (real-time) |
| `StateSnapshotCreatedEvent` | Snapshot created | SessionId, SnapshotId | Administration (audit) |
| `RuleSpecUpdatedEvent` | RuleSpec update | GameId, Version, UpdatedBy | Administration, KnowledgeBase (re-index) |
| `RuleCommentCreatedEvent` | Comment created | CommentId, GameId, CreatedBy | UserNotifications (mentions) |
| `EditorLockAcquiredEvent` | Lock acquired | GameId, LockedBy | Administration (tracking) |

---

## 🔗 Integration Points

### Context Diagram

*(blocco di codice rimosso)*

### Integration Summary

| Context | Relationship | Purpose |
|---------|-------------|---------|
| **KnowledgeBase** | Bidirectional | RAG context (inbound), AI suggestions (outbound) |
| **UserLibrary** | Inbound + Events | Game references, play history tracking |
| **SharedGameCatalog** | Bidirectional | Publication workflow, community linking |
| **Administration** | Events | Audit logging for all operations |
| **SessionTracking** | Events | Real-time session monitoring |
| **DocumentProcessing** | Outbound | PDF rulebook association |

---

## 🔐 Authorization Matrix

| Endpoint Pattern | Anonymous | User | Editor | Admin |
|------------------|-----------|------|--------|-------|
| `GET /games` | ✅ | ✅ | ✅ | ✅ |
| `GET /games/{id}` | ✅ | ✅ | ✅ | ✅ |
| `POST /games` | ❌ | ❌ | ✅ | ✅ |
| `PUT /games/{id}` | ❌ | ❌ | ✅ | ✅ |
| `PUT /games/{id}/publish` | ❌ | ❌ | ❌ | ✅ |
| `POST /sessions` | ❌ | ✅ | ✅ | ✅ |
| `PATCH /sessions/{id}/state` | ❌ | ✅ (owner) | ✅ | ✅ |
| `PUT /rulespec` | ❌ | ❌ | ✅ | ✅ |
| `POST /comments` | ❌ | ❌ | ✅ | ✅ |

**Authentication Levels**:
- 🟢 Public: No auth required
- 🟡 Authenticated: Session OR API Key (dual auth)
- 🔵 Session-Required: Active session mandatory
- 🔴 Admin/Editor: Role-based access control

---

## 📊 Performance

### Caching Strategy

| Query | TTL | Invalidation Trigger |
|-------|-----|---------------------|
| `GetGameByIdQuery` | 30 min | GameUpdatedEvent, GamePublishedEvent |
| `GetAllGamesQuery` | 5 min | GameCreatedEvent, GameUpdatedEvent |
| `GetSimilarGamesQuery` | 1 hour | GameUpdatedEvent (source game) |
| `GetGameSessionByIdQuery` | 2 min | Session lifecycle events |
| `GetActiveSessionsQuery` | 30 sec | SessionStartedEvent, SessionCompletedEvent |
| `GetRuleSpecQuery` | 1 hour | RuleSpecUpdatedEvent |

### Database Indexes

*(blocco di codice rimosso)*

### Performance Targets

| Query Type | Target Latency | Cache Hit Rate |
|------------|----------------|----------------|
| Game by ID | <15ms | >85% |
| All games (paginated) | <50ms | >70% |
| Similar games (RAG) | <500ms | >80% |
| Active sessions | <20ms | >90% |
| RuleSpec (current) | <25ms | >90% |

---

## 🧪 Testing

### Test Coverage

| Category | Test Files | Coverage Target |
|----------|-----------|-----------------|
| **Domain** | `Game_Tests.cs`, `GameSession_Tests.cs`, `RuleSpec_Tests.cs`, `RuleComment_Tests.cs` | 90%+ |
| **Validators** | `CreateGameCommandValidator_Tests.cs`, `StartGameSessionCommandValidator_Tests.cs`, etc. | 90%+ |
| **Handlers** | `*CommandHandler_Tests.cs`, `*QueryHandler_Tests.cs` (47 files) | 90%+ |
| **Integration** | `GameManagementIntegration_Tests.cs` (Testcontainers) | 85%+ |
| **E2E** | `apps/web/__tests__/e2e/game-management/` (Playwright) | Critical flows |

### Test Locations

*(blocco di codice rimosso)*

---

## 📂 Code Structure

*(blocco di codice rimosso)*

**Routing**: `apps/api/src/Api/Routing/GameEndpoints.cs`

---

## 📈 Metrics

### KPIs

| Metric | Target | Description |
|--------|--------|-------------|
| Game Catalog Size | 500+ games | Total games in system |
| Active Sessions | 50+ concurrent | Peak concurrent sessions |
| Session Completion Rate | >80% | Completed vs abandoned |
| BGG Import Success | >95% | Successful metadata fetches |
| Similar Games Accuracy | >75% | User approval rate |
| RuleSpec Lock Duration | <5min avg | Editor lock time |

### Monitoring Queries

**Active Sessions by Game**:
*(blocco di codice rimosso)*

**Session Completion Rate**:
*(blocco di codice rimosso)*

---

## 🚨 Known Issues

### Current Limitations

1. **BGG API Rate Limits**: 2 requests/second (implemented with throttling)
2. **Session Concurrency**: No quota limit (Issue #3070 adds per-tier limits)
3. **State Validation**: JSON not validated against schema (flexible but risky)
4. **Lock Timeout**: 5-minute expiration may be too short

### Related Issues

- ✅ **#1446**: Dual authentication (session or API key)
- ✅ **#2055**: Collaborative editing locks
- ✅ **#2373**: SharedGameCatalog linking
- ✅ **#2403**: GameSessionState with snapshots
- ✅ **#2404**: Player Mode move suggestions
- ✅ **#3353**: Similar games via RAG
- ✅ **#3481**: Publish to SharedGameCatalog
- 🔄 **#3070**: Session quota per user tier (In Progress)

---

## 🔗 Related Documentation

- [SharedGameCatalog BC](./shared-game-catalog.md) - Publication workflow
- [KnowledgeBase BC](./knowledge-base.md) - RAG, AI agents, similar games
- [DocumentProcessing BC](./document-processing.md) - PDF rulebook linking
- [UserLibrary BC](./user-library.md) - Collections, play history
- [SessionTracking BC](./session-tracking.md) - Real-time analytics
- [Administration BC](./administration.md) - Audit logs

### ADRs
- [ADR-018: PostgreSQL FTS](../01-architecture/adr/adr-018-postgresql-fts-for-shared-catalog.md)
- [ADR-023: Share Request Workflow](../01-architecture/adr/adr-023-share-request-workflow.md)
- [ADR-008: Streaming CQRS](../01-architecture/adr/adr-008-streaming-cqrs-migration.md)

---

**Status**: ✅ Production
**Last Updated**: 2026-02-12
**Operations**: 47 (26 commands + 21 queries)
**Test Coverage**: 90%+ (unit), 85%+ (integration)
**Domain Events**: 12+
**Integration Points**: 6 contexts


---



<div style="page-break-before: always;"></div>

## bounded-contexts/gamification.md

# Gamification Bounded Context

**Achievements, badges, progress tracking, rule-based evaluation**

---

## Responsibilities

**Achievement System**:
- Achievement definition management (code, name, description, icon, points)
- Rarity classification (Common, Uncommon, Rare, Epic, Legendary)
- Category grouping (Collection, Session, Social, Exploration, Mastery)
- Threshold-based rule evaluation

**User Progress**:
- Per-user achievement tracking (0-100% progress)
- Automatic unlock when progress reaches 100%
- Recent achievements listing
- Achievement history

**Evaluation Engine**:
- Rule-based achievement evaluation (`AchievementRuleEvaluator`)
- Scheduled evaluation job (`AchievementEvaluationJob`)
- Activity-driven progress updates

---

## Domain Model

| Aggregate | Key Fields | Purpose |
|-----------|-----------|---------|
| **Achievement** | Code, Name, Description, IconUrl, Points, Rarity, Category, Threshold, IsActive | Achievement definition |
| **UserAchievement** | UserId, AchievementId, Progress, UnlockedAt | Per-user tracking |

**Enums**: AchievementRarity (Common/Uncommon/Rare/Epic/Legendary), AchievementCategory (Collection/Session/Social/Exploration/Mastery)

**Domain Methods**:
- `Achievement.Create()` - Factory method with validation
- `Achievement.Activate()` / `Achievement.Deactivate()` - Toggle availability
- `UserAchievement.Create()` - Start tracking for a user
- `UserAchievement.UpdateProgress(int)` - Update progress, auto-unlock at 100%
- `UserAchievement.IsUnlocked` - Check unlock status

---

## CQRS Operations

### Queries
| Query | Handler | Description |
|-------|---------|-------------|
| `GetAchievementsQuery` | `GetAchievementsQueryHandler` | List all achievements (with filters) |
| `GetRecentAchievementsQuery` | `GetRecentAchievementsQueryHandler` | Recent user achievements |

### Validators
- `GetAchievementsQueryValidator` - Input validation for listing
- `GetRecentAchievementsQueryValidator` - Input validation for recent query

---

## Scheduled Jobs

| Job | Description |
|-----|-------------|
| `AchievementEvaluationJob` | Periodic rule evaluation across all users |

---

## Infrastructure

### Repositories
- `IAchievementRepository` → `AchievementRepository`
- `IUserAchievementRepository` → `UserAchievementRepository`

### Services
- `IAchievementRuleEvaluator` → `AchievementRuleEvaluator` (evaluation engine)

### DI Registration
- `GamificationServiceExtensions.AddGamification()`

---

## Dependencies

- **Upstream**: Authentication (user identity), UserLibrary (collection data), SessionTracking (session data), GameManagement (game data)
- **Downstream**: UserNotifications (unlock notifications)
- **Events**: Achievement unlock events may trigger notifications

---

## Related Issues

- Issue #3922: Achievement System and Badge Engine

---

**Last Updated**: 2026-02-18
**Status**: Production
**Code**: `apps/api/src/Api/BoundedContexts/Gamification/`


---



<div style="page-break-before: always;"></div>

## bounded-contexts/knowledge-base.md

# KnowledgeBase Bounded Context

**RAG System (Production) + AgentTypology POC (6-month lifecycle)**

> ⚠️ **Two Systems**: (1) RAG - Hybrid search, Chat, Q&A | (2) AgentTypology POC → Future: LangGraph Multi-Agent (Issue #3780)

---

## 📋 Responsibilities

| System | Features |
|--------|----------|
| **RAG (Production)** | Hybrid search (Vector+Keyword RRF) • Multi-model LLM consensus • Chat threads • 5-layer confidence validation |
| **AgentTypology POC** | Template-based agents • Approval workflow (Draft→Approved) • Phase-model config • Session management • Runtime config |

---

## 🏗️ Domain Model

### RAG Entities

| Entity | Key Fields | Domain Methods |
|--------|-----------|----------------|
| **ChatThread** | Id, UserId, GameId, AgentId, Title, Status (Active\|Closed), Messages[] | AddMessage(), UpdateMessage(), DeleteMessage(), Close(), Reopen(), Export(json\|markdown) |
| **ChatMessage** | Id, ThreadId, Content, Role (User\|Assistant\|System), ConfidenceScore, Sources[], CreatedAt | Update(), Delete() |
| **EmbeddingChunk** | Id, DocumentId, Content, PageNumber, ChunkIndex, Embedding (1024-dim BGE-M3), CreatedAt | N/A (read-only) |

### AgentTypology POC Entities

| Entity | Key Fields | Domain Methods |
|--------|-----------|----------------|
| **AgentTypology** | Id, Name, Description, BasePrompt, DefaultStrategy, Status (Draft\|Approved\|Rejected), PhaseModels, CreatedBy, ApprovedBy | Approve(), Reject(), Archive(), UpdatePhaseModels() |
| **AgentSession** | Id, AgentId, GameSessionId, TypologyId, Config, CurrentGameState, StartedAt, IsActive | UpdateGameState(), UpdateTypology(), UpdateConfig(), End() |
| **AgentTestResult** | Id, TypologyId, TestQuery, Success, Response, ConfidenceScore, ErrorMessage, TestedAt | N/A (audit trail) |

### Value Objects

| Value Object | Values | Usage |
|--------------|--------|-------|
| **AgentStrategyType** | Fast (~2K tok), Balanced (~2.8K), Precise (~22K), Expert, Consensus, Custom | Cost/accuracy tradeoff |
| **PhaseModelConfiguration** | RetrievalModel, AnalysisModel, SynthesisModel, ValidationModel, CragEvalModel, WebSearchModel, ConsensusVoter1-3 | Per-phase LLM selection |
| **AgentConfig** | ModelType, Temperature (0.0-2.0), MaxTokens, RagStrategy, RagParams | Runtime config |

---

## 📡 Endpoints (45 Total)

### RAG System (17 Endpoints)

| Operation | Method | Endpoint | Auth | Key Features |
|-----------|--------|----------|------|--------------|
| **Search** | POST | `/api/v1/knowledge-base/search` | Session | Hybrid (Vector+Keyword), topK, minScore, language |
| **Ask Question** | POST | `/api/v1/knowledge-base/ask` | Session | Full RAG pipeline, confidence scoring, citations |
| **Create Thread** | POST | `/api/v1/chat-threads` | Session | Auto-add initialMessage if provided |
| **Get Thread** | GET | `/api/v1/chat-threads/{id}` | Owner | Full thread with messages |
| **List Threads** | GET | `/api/v1/chat-threads?gameId={id}` | Session | Filter by game |
| **My Chat History** | GET | `/api/v1/knowledge-base/my-chats` | Session | Paginated, skip/take |
| **Delete Thread** | DELETE | `/api/v1/chat-threads/{id}` | Owner | 204 No Content |
| **Add Message** | POST | `/api/v1/chat-threads/{id}/messages` | Owner | Updates LastMessageAt |
| **Update Message** | PUT | `/api/v1/chat-threads/{id}/messages/{msgId}` | Session | Edit existing |
| **Delete Message** | DELETE | `/api/v1/chat-threads/{id}/messages/{msgId}` | Session | 204 No Content |
| **Close Thread** | POST | `/api/v1/chat-threads/{id}/close` | Owner | Sets Status=Closed |
| **Reopen Thread** | POST | `/api/v1/chat-threads/{id}/reopen` | Owner | Sets Status=Active |
| **Update Title** | PATCH | `/api/v1/chat-threads/{id}` | Owner | 1-200 chars |
| **Export Chat** | GET | `/api/v1/chat-threads/{id}/export?format={json\|markdown}` | Owner | Full export |

**Search Request**:
*(blocco di codice rimosso)*

**Ask Question Response**:
*(blocco di codice rimosso)*

**Confidence Thresholds** (ADR-005):
- Search: ≥0.70 | LLM: ≥0.70 | Low-quality: <0.60 triggers warning

### AgentTypology POC (24 Endpoints)

| Operation | Method | Endpoint | Auth | Purpose |
|-----------|--------|----------|------|---------|
| **List Typologies** | GET | `/api/v1/agent-typologies` | Role-based | User: Approved only • Editor: +own Draft • Admin: ALL |
| **Get by ID** | GET | `/api/v1/agent-typologies/{id}` | Session | Single typology |
| **Get Pending** | GET | `/api/v1/agent-typologies/pending` | Admin | Approval queue |
| **Get My Proposals** | GET | `/api/v1/agent-typologies/my-proposals` | Editor/Admin | Own drafts |
| **Propose Typology** | POST | `/api/v1/agent-typologies/propose` | Editor/Admin | Creates Draft, notifies admins |
| **Test Typology** | POST | `/api/v1/agent-typologies/{id}/test` | Editor/Admin | Sandbox test, no user charge |
| **Create (Admin)** | POST | `/api/v1/admin/agent-typologies` | Admin | Full config + auto-approve option |
| **Update** | PUT | `/api/v1/admin/agent-typologies/{id}/phase-models` | Admin | Edit config |
| **Delete** | DELETE | `/api/v1/admin/agent-typologies/{id}` | Admin | 204 No Content |
| **Approve** | PUT | `/api/v1/admin/agent-typologies/{id}/approve` | Admin | Notify proposer, activate |
| **Reject** | PUT | `/api/v1/admin/agent-typologies/{id}/reject` | Admin | Notify proposer, reason required |
| **Launch Agent** | POST | `/api/v1/game-sessions/{id}/agent/launch` | Session | Create AgentSession |
| **Chat with Agent** | POST | `/api/v1/game-sessions/{id}/agent/chat` | Session | **SSE Stream** |
| **End Agent** | DELETE | `/api/v1/game-sessions/{id}/agent` | Session | Preserve chat log |
| **Update State** | PUT | `/api/v1/game-sessions/{id}/agent/state` | Session | Sync game board |
| **Switch Typology** | PATCH | `/api/v1/game-sessions/{id}/agent/typology` | Session | Change mid-session |
| **Update Config** | PATCH | `/api/v1/game-sessions/{id}/agent/config` | Session | Runtime tuning |
| **Get Metrics** | GET | `/api/v1/admin/agents/metrics` | Admin | Aggregated stats |
| **Get Agent Metrics** | GET | `/api/v1/admin/agents/metrics/{id}` | Admin | Single agent stats |
| **Top Agents** | GET | `/api/v1/admin/agents/metrics/top` | Admin | Ranked by invocations\|cost\|confidence |
| **Cost Estimate** | GET | `/api/v1/admin/agent-typologies/{id}/cost-estimate` | Admin | Projected costs |

**Strategy Presets** (Issue #3245):

| Strategy | Tokens/Query | Cost/Query | Use Case |
|----------|--------------|------------|----------|
| Fast | ~2,060 | $0.008 | High volume, quick answers |
| Balanced | ~2,820 | $0.012 | Standard accuracy |
| Precise | ~22,396 | $0.089 | High-stakes rules |
| Expert | ~30,000+ | $0.15+ | Research, multi-hop |
| Consensus | ~8,400 | $0.036 | Vote-based validation |

**SSE Chat Response**:
*(blocco di codice rimosso)*

### Context Engineering (2 Endpoints)

| Operation | Method | Endpoint | Purpose |
|-----------|--------|----------|---------|
| **Assemble Context** | POST | `/api/v1/context-engineering/assemble` | Multi-source context assembly (StaticKnowledge, ConversationMemory, AgentState, ToolMetadata) |
| **Get Sources** | GET | `/api/v1/context-engineering/sources` | List available sources |

**Assemble Request**:
*(blocco di codice rimosso)*

---

## 🔐 Authorization Matrix

| Endpoint Pattern | Anonymous | User | Editor | Admin |
|------------------|-----------|------|--------|-------|
| `/knowledge-base/search` | ❌ | ✅ | ✅ | ✅ |
| `/knowledge-base/ask` | ❌ | ✅ | ✅ | ✅ |
| `/chat-threads` (GET) | ❌ | ✅ (own) | ✅ (own) | ✅ (all) |
| `/agent-typologies/propose` | ❌ | ❌ | ✅ | ✅ |
| `/agent-typologies/{id}/test` | ❌ | ❌ | ✅ (own) | ✅ (all) |
| `/admin/agent-typologies/{id}/approve` | ❌ | ❌ | ❌ | ✅ |
| `/admin/agents/metrics` | ❌ | ❌ | ❌ | ✅ |

**Rate Limits**:

| Endpoint | Limit | Window | Scope |
|----------|-------|--------|-------|
| `/knowledge-base/ask` | 30 req | 1 min | Per user |
| `/agent-typologies/propose` | 5 req | 1 hour | Per editor |
| `/agent-typologies/{id}/test` | 10 req | 1 hour | Per typology |
| `/game-sessions/{id}/agent/chat` | 60 msg | 1 min | Per session |

---

## 🔄 Domain Events

| Event | When | Payload | Subscribers |
|-------|------|---------|-------------|
| ChatThreadCreatedEvent | Thread created | ThreadId, UserId, GameId | Administration (audit) |
| MessageAddedEvent | Message added | ThreadId, MessageId, Role | SessionTracking (activity) |
| ThreadClosedEvent | Thread closed | ThreadId, ClosedBy | Administration |
| ChunksEmbeddedEvent | Document chunked | DocumentId, ChunkCount | KnowledgeBase (Qdrant index) |
| TypologyProposedEvent | Editor proposes | TypologyId, ProposedBy, Name | UserNotifications (admins) |
| TypologyApprovedEvent | Admin approves | TypologyId, ApprovedBy | UserNotifications (proposer) |
| TypologyRejectedEvent | Admin rejects | TypologyId, RejectedBy, Reason | UserNotifications (proposer) |
| AgentSessionLaunchedEvent | Session started | AgentSessionId, UserId, TypologyId | Administration (tracking) |
| AgentSessionEndedEvent | Session ended | AgentSessionId, Duration, MessageCount | Administration (analytics) |
| AgentInvokedEvent | Agent query | AgentId, UserId, Tokens, Cost | Administration (billing) |

---

## 🔗 Integration Points

**Inbound**:
- GameManagement → Fetch game metadata
- DocumentProcessing → Receive chunked documents (`DocumentChunkedEvent`)
- UserLibrary → Link chat sessions to library entries

**Outbound**:
- Qdrant → Vector storage & similarity search
- Redis → 3-tier caching (memory → Redis → Qdrant)
- OpenRouter → GPT-4, Claude 3, DeepSeek (multi-model consensus)
- Embedding Service → sentence-transformers/all-MiniLM-L6-v2 (384-dim)
- Reranker Service → cross-encoder/ms-marco-MiniLM-L-6-v2

---

## 📊 Performance

### Caching (3-Tier)

| Query | Memory | Redis | Qdrant | TTL | Invalidation Trigger |
|-------|--------|-------|--------|-----|---------------------|
| SearchQuery | ✅ | ✅ | Fallback | 5min | DocumentChunkedEvent |
| AskQuestionQuery | ✅ | ✅ | N/A | 10min | ThreadUpdatedEvent |
| GetChatThreadById | ❌ | ✅ | N/A | 2min | MessageAddedEvent |
| GetAgentTypologies | ❌ | ✅ | N/A | 30min | TypologyApprovedEvent |
| GetAgentMetrics | ❌ | ✅ | N/A | 1hour | AgentInvokedEvent |

**Cache Hit Target**: >80%

### Indexes

*(blocco di codice rimosso)*

### Targets

| Operation | Latency (P95) | Cache Hit |
|-----------|---------------|-----------|
| Hybrid search | <200ms | >80% |
| RAG Q&A (cached) | <500ms | >75% |
| RAG Q&A (uncached) | <2s | N/A |
| Chat message add | <50ms | N/A |
| Agent invocation | <3s | >70% |
| Typology list | <20ms | >90% |

---

## 🧪 Testing

### Coverage Target
- RAG System: **90%+** ✅
- AgentTypology POC: **0%** ⚠️ MISSING (Issue #3794 blocker)

### Unit Tests

**Location**: `tests/Api.Tests/KnowledgeBase/`

| Test Suite | Focus |
|------------|-------|
| ChatThread_Tests.cs | AddMessage, Export, Close/Reopen |
| HybridSearchService_Tests.cs | Vector+Keyword fusion, RRF logic |
| ConfidenceValidator_Tests.cs | 5-layer validation (ADR-005) |
| MultiModelConsensus_Tests.cs | 3-voter ensemble (ADR-007) |
| AgentTypology_Tests.cs | **MISSING** - Approve, Reject, Archive |
| AgentSession_Tests.cs | **MISSING** - Launch, UpdateState, End |
| ProposeTypologyValidator_Tests.cs | **MISSING** - Proposal validation |
| TestTypologyHandler_Tests.cs | **MISSING** - Sandbox testing logic |

### Integration Tests

**Tools**: Testcontainers (PostgreSQL, Redis, Qdrant)

**Scenarios**:
1. End-to-End RAG: Upload PDF → Extract → Embed → Search → Ask
2. Typology Lifecycle: Propose → Test → Approve → Invoke
3. Agent Session: Launch → Chat (5 turns) → Update state → End
4. Multi-Model Consensus: 3 voters, voting logic validation

### E2E Tests (Playwright)

**Location**: `apps/web/__tests__/e2e/knowledge-base/`

**Flows**:
1. RAG Chat: Navigate → Type question → See SSE stream → Verify citations
2. Typology Proposal (Editor): Navigate → Fill form → Test sandbox → Submit
3. Typology Approval (Admin): Review pending → View tests → Approve/Reject
4. Agent Session: Start game → Launch agent → Chat with SSE → Update state → End

---

## 📂 Code Structure

*(blocco di codice rimosso)*

**Routing**: `Api/Routing/KnowledgeBaseEndpoints.cs` (RAG) + `AgentEndpoints.cs` (POC)

---

## 📈 KPIs

### RAG System

| Metric | Target | Description |
|--------|--------|-------------|
| Search Accuracy | >90% | Relevant chunks in top-5 |
| Answer Accuracy | >85% | Correct answers with confidence >0.7 |
| Low-Quality Rate | <10% | Answers below threshold |
| Avg Latency | <2s (P95) | End-to-end pipeline |
| Cache Hit Rate | >80% | Redis effectiveness |

### AgentTypology POC

| Metric | Target | Description |
|--------|--------|-------------|
| Approval Rate | >70% | Editor proposals approved |
| Avg Test Confidence | >0.75 | Sandbox results |
| Active Sessions | 50+ | Concurrent peak |
| Avg Session Duration | 15-30min | Typical interaction |
| Cost Per Session | <$0.50 | Fast/Balanced strategies |

---

## 🚨 Known Issues

### RAG System
- **Issue #3231** (BLOCKER): ResponseEnded crash in AskQuestionQueryHandler (0/20 tests passing)
- Language support: Italian + English only
- Context window: 8K max tokens
- Reranking latency: +100-200ms
- Consensus cost: 3x for ~5-8% accuracy gain

### AgentTypology POC
- **Test Coverage**: 0% (violates 90%+ target)
- **Documentation Gap** (Issue #3795): No POC architecture doc
- Session state limit: 64KB JSON (PostgreSQL text column)
- No concurrent session limits per user
- No typology versioning (updates overwrite)
- Test results retained forever (no cleanup)

---

## 📋 Future

**RAG Enhancements**:
- GraphRAG (entity relationships)
- Multi-document reasoning
- Temporal RAG (time-aware retrieval)
- Fine-tuned embeddings

**AgentTypology → LangGraph Migration** (6+ months):
1. Current (0-6mo): AgentTypology POC in production
2. Transition (6-7mo): LangGraph implementation (Tutor/Arbitro/Decisore)
3. Coexistence (7-8mo): Both systems, gradual migration
4. Completion (9mo): POC deprecated

**LangGraph Features** (Issue #3780):
- Specialized agents: Tutor (onboarding), Arbitro (rules), Decisore (strategy)
- Event-driven orchestration
- Advanced context engineering
- MCTS engine for strategic AI

---

## 🔗 Related Docs

**Architecture**:
- [ADR-001: Hybrid RAG](../01-architecture/adr/adr-001-hybrid-rag.md)
- [ADR-005: Cosine Similarity Consensus](../01-architecture/adr/adr-005-cosine-similarity-consensus.md)
- [ADR-007: Hybrid LLM](../01-architecture/adr/adr-007-hybrid-llm.md)
- [ADR-008: Streaming CQRS](../01-architecture/adr/adr-008-streaming-cqrs-migration.md)

**Other Contexts**:
- [GameManagement](./game-management.md), [DocumentProcessing](./document-processing.md)
- [UserLibrary](./user-library.md), [Administration](./administration.md)

**Testing**:
- [RAG Validation 20Q](../../05-testing/rag-validation-20q.md)
- [Backend Testing Patterns](../../05-testing/backend/backend-testing-patterns.md)

---

**Status**: ✅ Production (RAG) + 🟡 POC (AgentTypology - 6-month lifecycle)
**Last Updated**: 2026-02-07
**Commands**: 25+ | **Queries**: 20+ | **Endpoints**: 45+
**Test Coverage**: 90%+ (RAG) | 0% (POC) | **Events**: 15+ | **External Services**: 5


---



<div style="page-break-before: always;"></div>

## bounded-contexts/session-tracking.md

# SessionTracking Bounded Context - Complete API Reference

**User Activity Tracking, Session Analytics, Real-Time Game Session Features**

> 📖 **Complete Documentation**: Part of Issue #3794
> 🎮 **Game Session Toolkit**: Core context for Epic #3167

---

## 📋 Responsabilità

- Session activity tracking (page views, API calls, user events)
- Real-time game session features (SSE streaming)
- Session notes (private + reveal mechanism)
- Session decks (card management with shuffle, draw, discard)
- Dice rolling with cryptographic randomness
- Score tracking e leaderboards
- Random tools (spinner, coin flip, card draw)
- Session lifecycle management (create, pause, finalize)
- Session code generation (public session join)

---

## 🏗️ Domain Model

### Aggregates

**GameSession** (Aggregate Root):
*(blocco di codice rimosso)*

**SessionNote** (Entity):
*(blocco di codice rimosso)*

**SessionDeck** (Entity):
*(blocco di codice rimosso)*

**DiceRoll** (Entity):
*(blocco di codice rimosso)*

**ScoreEntry** (Entity):
*(blocco di codice rimosso)*

### Value Objects

**ParticipantInfo**:
*(blocco di codice rimosso)*

**SessionResult**:
*(blocco di codice rimosso)*

---

## 📡 Application Layer (CQRS)

> **Total Operations**: 25+ (15 commands + 10 queries)

---

### Session Lifecycle

| Command/Query | HTTP Method | Endpoint | Auth | Request | Response |
|---------------|-------------|----------|------|---------|----------|
| `CreateSessionCommand` | POST | `/api/v1/tracking/sessions` | Session | `CreateSessionDto` | `SessionDto` (201) |
| `AddParticipantCommand` | POST | `/api/v1/tracking/sessions/{id}/participants` | Session + Host | `ParticipantInfoDto` | `SessionDto` |
| `FinalizeSessionCommand` | POST | `/api/v1/tracking/sessions/{id}/finalize` | Session + Host | `FinalizeDto` | `SessionResultDto` |
| `GetActiveSessionQuery` | GET | `/api/v1/tracking/sessions/active` | Session | None | `SessionDto` or null |
| `GetSessionDetailsQuery` | GET | `/api/v1/tracking/sessions/{id}` | Session | None | `SessionDetailsDto` |
| `GetSessionByCodeQuery` | GET | `/api/v1/tracking/sessions/join?code={code}` | Public | Query: code | `SessionDto` |
| `GetSessionHistoryQuery` | GET | `/api/v1/tracking/sessions/history` | Session | Query: page, pageSize | `PaginatedList<SessionDto>` |

**CreateSessionCommand**:
- **Purpose**: Create new game session for tracking
- **Request Schema**:
  *(blocco di codice rimosso)*
- **Side Effects**:
  - Generates 6-char SessionCode for public join
  - Sets Status = Active
  - Host participant automatically added
- **Domain Events**: `SessionCreatedEvent`

**GetSessionByCodeQuery**:
- **Purpose**: Join session via public code (no direct invite needed)
- **Query**: `?code=ABC123`
- **Authorization**: Public (if session allows public join)
- **Use Case**: Friend shares session code verbally/text

---

### Score Tracking

| Command/Query | HTTP Method | Endpoint | Auth | Request | Response |
|---------------|-------------|----------|------|---------|----------|
| `UpdateScoreCommand` | POST | `/api/v1/tracking/sessions/{id}/scores` | Session | `UpdateScoreDto` | `ScoreEntryDto` |
| `GetScoreboardQuery` | GET | `/api/v1/tracking/sessions/{id}/scoreboard` | Session | None | `List<ScoreEntryDto>` (ranked) |

**UpdateScoreCommand**:
- **Purpose**: Record score for participant
- **Request Schema**:
  *(blocco di codice rimosso)*
- **Behavior**: Creates new ScoreEntry (cumulative scores calculated in query)
- **Domain Events**: `ScoreUpdatedEvent`

**GetScoreboardQuery**:
- **Response Schema**:
  *(blocco di codice rimosso)*
- **Sorting**: Descending by totalPoints

---

### Session Notes (Private + Reveal)

| Command/Query | HTTP Method | Endpoint | Auth | Request | Response |
|---------------|-------------|----------|------|---------|----------|
| `AddNoteCommand` | POST | `/api/v1/tracking/sessions/{id}/notes` | Session | `AddNoteDto` | `SessionNoteDto` (201) |
| `SaveNoteCommand` | PUT | `/api/v1/tracking/sessions/{id}/notes/{noteId}` | Session + Owner | `SaveNoteDto` | `SessionNoteDto` |
| `DeleteNoteCommand` | DELETE | `/api/v1/tracking/sessions/{id}/notes/{noteId}` | Session + Owner | None | 204 No Content |
| `RevealNoteCommand` | POST | `/api/v1/tracking/sessions/{id}/notes/{noteId}/reveal` | Session + Owner | None | `SessionNoteDto` |
| `HideNoteCommand` | POST | `/api/v1/tracking/sessions/{id}/notes/{noteId}/hide` | Session + Owner | None | `SessionNoteDto` |
| `GetSessionNotesQuery` | GET | `/api/v1/tracking/sessions/{id}/notes` | Session | Query: participantId? | `List<SessionNoteDto>` |

**AddNoteCommand**:
- **Purpose**: Create private note (secret strategy, card tracking, etc.)
- **Request Schema**:
  *(blocco di codice rimosso)*
- **Security**: Content AES-256 encrypted, only owner can decrypt
- **Reveal Mechanism**: Owner can reveal to all participants

**RevealNoteCommand**:
- **Purpose**: Share private note with all participants
- **Side Effects**:
  - Sets IsRevealed = true
  - All participants can now read EncryptedContent
  - Raises `NoteRevealedEvent` → SSE broadcast to participants

---

### Deck Management

| Command/Query | HTTP Method | Endpoint | Auth | Request | Response |
|---------------|-------------|----------|------|---------|----------|
| `CreateDeckCommand` | POST | `/api/v1/tracking/sessions/{id}/decks` | Session + Host | `CreateDeckDto` | `SessionDeckDto` (201) |
| `ShuffleDeckCommand` | POST | `/api/v1/tracking/sessions/{id}/decks/{deckId}/shuffle` | Session + Host | None | `SessionDeckDto` |
| `DrawCardsCommand` | POST | `/api/v1/tracking/sessions/{id}/decks/{deckId}/draw` | Session | `DrawCardsDto` | `{ cards: Card[], remainingInDeck: number }` |
| `DiscardCardsCommand` | POST | `/api/v1/tracking/sessions/{id}/decks/{deckId}/discard` | Session | `DiscardCardsDto` | `SessionDeckDto` |
| `GetSessionDecksQuery` | GET | `/api/v1/tracking/sessions/{id}/decks` | Session | None | `List<SessionDeckDto>` |

**CreateDeckCommand**:
- **Request Schema**:
  *(blocco di codice rimosso)*
- **Deck Types**:
  - Standard52: 52-card deck (no jokers)
  - Standard54: 54-card deck (2 jokers)
  - Custom: User-defined cards
- **Side Effects**: Automatically shuffles on creation

**DrawCardsCommand**:
- **Request Schema**:
  *(blocco di codice rimosso)*
- **Response**:
  *(blocco di codice rimosso)*
- **Domain Events**: `CardDrawnEvent` → SSE broadcast

---

### Dice Rolling

| Command/Query | HTTP Method | Endpoint | Auth | Request | Response |
|---------------|-------------|----------|------|---------|----------|
| `RollDiceCommand` | POST | `/api/v1/tracking/sessions/{id}/dice/roll` | Session | `RollDiceDto` | `DiceRollDto` |
| `GetDiceRollHistoryQuery` | GET | `/api/v1/tracking/sessions/{id}/dice/history` | Session | Query: page, pageSize | `PaginatedList<DiceRollDto>` |

**RollDiceCommand**:
- **Purpose**: Cryptographically secure dice rolling
- **Request Schema**:
  *(blocco di codice rimosso)*
- **Supported Formats**:
  - `XdY`: X dice with Y sides (e.g., `2d6`, `3d10`)
  - `XdY+Z`: X dice + modifier (e.g., `1d20+5`)
  - `XdY-Z`: X dice - penalty (e.g., `2d6-1`)
  - Supported: d4, d6, d8, d10, d12, d20, d100
- **Response Schema**:
  *(blocco di codice rimosso)*
- **RNG**: Uses `RandomNumberGenerator.GetBytes()` (cryptographically secure)
- **Domain Events**: `DiceRolledEvent` → SSE broadcast to participants

---

### Real-Time Updates (SSE)

| Query | HTTP Method | Endpoint | Auth | Response |
|-------|-------------|----------|------|----------|
| `GetSessionStreamQuery` | GET | `/api/v1/tracking/sessions/{id}/stream` | Session | Server-Sent Events |

**GetSessionStreamQuery**:
- **Purpose**: Real-time event stream for session updates
- **Headers**: `text/event-stream`, `Cache-Control: no-cache`
- **Events Broadcasted**:
  - `participant-joined`: New player joined
  - `score-updated`: Score change
  - `note-revealed`: Note revealed
  - `card-drawn`: Card operation
  - `dice-rolled`: Dice roll result
  - `session-paused`: Host paused session
  - `session-resumed`: Host resumed session
  - `session-finalized`: Session ended
- **Example Event**:
  *(blocco di codice rimosso)*

---

### Random Tools

| Command | HTTP Method | Endpoint | Auth | Request | Response |
|---------|-------------|----------|------|---------|----------|
| `SpinSpinnerCommand` | POST | `/api/v1/tracking/sessions/{id}/random/spinner` | Session | `{ options: string[], label?: string }` | `{ result: string, index: number }` |
| `FlipCoinCommand` | POST | `/api/v1/tracking/sessions/{id}/random/coin` | Session | `{ label?: string }` | `{ result: "Heads"|"Tails" }` |
| `DrawRandomCardCommand` | POST | `/api/v1/tracking/sessions/{id}/random/card` | Session | `{ deckType?: string }` | `Card` |

**SpinSpinnerCommand**:
- **Purpose**: Random selection from options (e.g., first player, turn order)
- **Request Schema**:
  *(blocco di codice rimosso)*
- **Response**:
  *(blocco di codice rimosso)*
- **RNG**: Cryptographically secure (not predictable)

---

### Export & Analytics

| Query | HTTP Method | Endpoint | Auth | Query Params | Response |
|-------|-------------|----------|------|--------------|----------|
| `ExportSessionQuery` | GET | `/api/v1/tracking/sessions/{id}/export` | Session | `format=json|csv|pdf` | Exported file |
| `GetSessionStatsQuery` | GET | `/api/v1/tracking/stats` | Session | `startDate?`, `endDate?` | `SessionStatsDto` |

**ExportSessionQuery**:
- **Formats**:
  - JSON: Complete session data (for backup/analysis)
  - CSV: Scores and participants (for spreadsheets)
  - PDF: Formatted session report (printable)
- **Use Cases**: Post-game analysis, record keeping, sharing results

---

## 🔄 Domain Events

| Event | When Raised | Payload | Subscribers |
|-------|-------------|---------|-------------|
| `SessionCreatedEvent` | Session created | `{ SessionId, UserId, GameId }` | Administration (analytics) |
| `SessionFinalizedEvent` | Session ended | `{ SessionId, Winner, Duration }` | GameManagement (play history), UserLibrary |
| `ParticipantAddedEvent` | Player joined | `{ SessionId, ParticipantId }` | Real-time SSE broadcast |
| `ScoreUpdatedEvent` | Score recorded | `{ SessionId, ParticipantId, Points }` | Real-time SSE broadcast |
| `DiceRolledEvent` | Dice rolled | `{ SessionId, ParticipantId, Formula, Total }` | Real-time SSE broadcast |
| `CardDrawnEvent` | Cards drawn | `{ SessionId, ParticipantId, Count }` | Real-time SSE broadcast |
| `NoteRevealedEvent` | Note revealed | `{ SessionId, NoteId, Content }` | Real-time SSE broadcast |

---

## 🔗 Integration Points

### Inbound Dependencies

**GameManagement Context**:
- Links sessions to games (GameId foreign key)
- Uses game metadata for session context

### Outbound Dependencies

**Redis (SSE)**:
- Publishes events to Redis channels
- SSE connections subscribe to channels
- Real-time broadcast to participants

### Event-Driven Communication

*(blocco di codice rimosso)*

---

## 🔐 Security & Authorization

### Access Control

- **Host Privileges**: Create decks, shuffle, finalize session
- **Participant Privileges**: Draw cards, roll dice, add notes, update own score
- **Note Privacy**: Encrypted, owner-only until revealed

### Cryptographic Features

- **Dice Rolls**: `RandomNumberGenerator.GetBytes()` (CSPRNG)
- **Deck Shuffle**: Fisher-Yates with CSPRNG
- **Note Encryption**: AES-256-GCM
- **Session Codes**: Cryptographically random 6-char codes

---

## 🎯 Common Usage Examples

### Example: Complete Session Flow

**Create Session**:
*(blocco di codice rimosso)*

**Roll Dice**:
*(blocco di codice rimosso)*

**Update Score**:
*(blocco di codice rimosso)*

**Finalize Session**:
*(blocco di codice rimosso)*

---

## 📊 Performance Characteristics

### Real-Time Performance

| Operation | Target Latency | Description |
|-----------|----------------|-------------|
| SSE Event Broadcast | <50ms | Redis pub/sub latency |
| Dice Roll | <20ms | CSPRNG generation |
| Score Update | <30ms | DB write + SSE broadcast |
| Note Reveal | <40ms | Decryption + broadcast |

### Caching

- SSE channels cached in Redis (1 hour expiry)
- Active session queries cached (30 seconds)

---

## 📂 Code Location

`apps/api/src/Api/BoundedContexts/SessionTracking/`

---

**Status**: ✅ Production (SSE infrastructure: Issue #3324)
**Last Updated**: 2026-02-07
**Total Commands**: 15+
**Total Queries**: 10+
**Real-Time**: SSE streaming
**Features**: Notes, Decks, Dice, Scores, Random Tools


---



<div style="page-break-before: always;"></div>

## bounded-contexts/shared-game-catalog.md

# SharedGameCatalog Bounded Context

**Community Catalog, Publication Workflow, Approval System, BGG Integration, Soft-Delete**

---

## Responsibilities

- Community-driven game catalog
- Publication workflow (Draft → PendingApproval → Published)
- Admin approval system + review locking
- Soft-delete workflow + audit trail (ADR-019)
- PostgreSQL Full-Text Search (Italian + English - ADR-018)
- BoardGameGeek API integration
- Share request management (user proposals)
- Document versioning + RAG approval
- Game state template generation
- FAQ, Errata, Quick Questions
- Badge system (contributor recognition)
- Bulk operations (batch approval/rejection)

---

## Domain Model

| Aggregate | Key Fields | Purpose |
|-----------|-----------|---------|
| **SharedGame** | Id, Title, Status, BggId, PublishedAt, ApprovedBy, IsDeleted, DeletedAt, Faqs[], Errata[], QuickQuestions[], Documents[] | Aggregate root |
| **ShareRequest** | Id, UserId, PrivateGameId, Status, ReviewedBy, IsReviewLocked, LockedBy, LockExpires | User proposals |
| **DeleteRequest** | Id, SharedGameId, RequestedBy, Status, Reason, ReviewedBy | Editor delete requests |
| **Badge** | Id, Name, Description, Type, RequiredCount, IsHidden | Contributor badges |
| **UserBadge** | UserId, BadgeId, EarnedAt, IsDisplayed | Earned badges |

**Value Objects**: PublicationStatus (Draft/PendingApproval/Published/Archived), ShareRequestStatus (Draft/Submitted/UnderReview/ChangesRequested/Approved/Rejected), DeleteRequestStatus (Pending/Approved/Rejected), BadgeType (Contribution/Milestone/Achievement)

**Domain Methods**: `SubmitForApproval()`, `Approve()`, `Reject()`, `Archive()`, `SoftDelete()`, `Restore()`, `AddFaq()`, `AcquireReviewLock()`

---

## API Operations (69 total)

**46 Commands**: Create/Update/Delete SharedGame, SubmitForApproval, Approve/RejectPublication, BatchApprove/Reject, RequestDelete, ApproveDeleteRequest, Add/Update/Delete Faq, Add/Update/Delete Errata, GenerateQuickQuestions, AddManualQuickQuestion, AddDocument, SetActiveDocumentVersion, ApproveDocumentForRag, GenerateGameStateTemplate, SearchBggGames, ImportGameFromBgg, UpdateFromBgg, BulkImportGames, CreateShareRequest, ApproveShareRequest, RejectShareRequest, RequestChanges, WithdrawShareRequest, BulkApproveShareRequests, +22 others

**23 Queries**: SearchSharedGames, GetSharedGameById, GetGameFaqs, GetQuickQuestions, GetCategories, GetMechanics, GetAllSharedGames, GetPendingApprovalGames, GetPendingDeleteRequests, GetDocumentsBySharedGame, GetActiveDocuments, GetActiveRulebookAnalysis, CheckBggDuplicate, GetByBggId, GetPendingShareRequests, GetShareRequestDetails, GetMyActiveReviews, GetApprovalQueue, GetAllBadges, GetUserBadges, GetUserContributions, GetBadgeLeaderboard, GetGameContributors

---

## Key Endpoints (80+)

| Method | Endpoint | Auth | Purpose |
|--------|----------|------|---------|
| GET | `/api/v1/shared-games` | 🟢 Public | Search catalog (FTS) |
| GET | `/api/v1/shared-games/{id}` | 🟢 Public | Game details |
| GET | `/api/v1/games/{gameId}/faqs` | 🟢 Public | FAQs (paginated) |
| POST | `/api/v1/faqs/{faqId}/upvote` | 🟢 Public | Upvote FAQ |
| POST | `/api/v1/admin/shared-games` | Editor+ | Create game (draft) |
| PUT | `/api/v1/admin/shared-games/{id}` | Editor+ | Update game |
| DELETE | `/api/v1/admin/shared-games/{id}` | Admin (direct) / Editor (request) | Delete game |
| POST | `/api/v1/admin/shared-games/{id}/submit-for-approval` | Editor+ | Submit for review |
| POST | `/api/v1/admin/shared-games/{id}/approve-publication` | Admin | Approve → Published |
| POST | `/api/v1/admin/shared-games/{id}/reject-publication` | Admin | Reject → Draft |
| POST | `/api/v1/admin/shared-games/batch-approve` | Admin | Batch approve |
| POST | `/api/v1/admin/shared-games/{id}/faq` | Editor+ | Add FAQ |
| POST | `/api/v1/admin/shared-games/{id}/quick-questions/generate` | Editor+ | AI-generate Q&A |
| POST | `/api/v1/admin/shared-games/{id}/documents/{docId}/approve` | Admin | Approve for RAG |
| GET | `/api/v1/admin/shared-games/bgg/search` | Editor+ | Search BGG |
| POST | `/api/v1/admin/shared-games/import-bgg` | Editor+ | Import from BGG |
| POST | `/api/v1/share-requests` | User | Propose private game |
| GET | `/api/v1/admin/share-requests` | Admin | Pending requests |
| POST | `/api/v1/admin/share-requests/{id}/approve` | Admin | Approve request |
| GET | `/api/v1/admin/shared-games/approval-queue` | Admin | Smart approval queue |
| GET | `/api/v1/badges` | 🟢 Public | All badges |
| GET | `/api/v1/badges/leaderboard` | 🟢 Public | Badge leaderboard |

---

## Publication Workflow (Issue #2514)

**Status Flow**:
*(blocco di codice rimosso)*

**ApproveSharedGamePublicationCommand**:
- Sets: Status = Published, PublishedAt = UtcNow, ApprovedBy = AdminId
- Raises: `GamePublishedEvent` → UserNotifications (notify submitter)
- Side Effect: Increment submitter contribution count (badge system)

**BatchApproveGamesCommand** (Issue #3350):
- Partial success allowed (continues on failures)
- Returns: `{ successCount, failedCount, errors[] }`

---

## Soft-Delete Workflow (ADR-019)

**Fields**: IsDeleted, DeletedAt, DeletedBy

**Global Filter**: `modelBuilder.Entity<SharedGame>().HasQueryFilter(g => !g.IsDeleted);`

**Editor**: Creates DeleteRequest (awaits admin approval)

**Admin**: Direct soft-delete (sets IsDeleted=true immediately)

**Restore**: `Restore()` method resets IsDeleted, DeletedAt, DeletedBy

---

## PostgreSQL FTS (ADR-018)

**Index**:
*(blocco di codice rimosso)*

**SearchSharedGamesQuery**:
- Query params: `q` (search term), `categories[]`, `mechanics[]`, `minPlayers`, `maxPlayers`, `playingTime`, `page`, `pageSize`
- Searches: Title, Description, Rules content
- Fallback: English FTS if Italian finds no results
- Target: <100ms P95

---

## Share Request Workflow

**Status Flow**:
*(blocco di codice rimosso)*

**Review Lock**:
- Duration: 30 minutes (configurable)
- Auto-release if admin doesn't complete
- Conflict: 409 if another admin holds lock

**ApproveShareRequest**:
1. Creates SharedGame from request data
2. Sets SharedGame.Status = Published
3. Transfers documents to SharedGame
4. Sets ShareRequest.Status = Approved
5. Awards badge to submitter (if eligible)
6. Raises `ShareRequestApprovedEvent`

---

## BGG Integration

**ImportGameFromBggCommand**:
- Rate limiting: 2 req/s to BGG API
- Creates SharedGame with Status = Draft
- Optionally imports: categories, mechanics, image

**CheckBggDuplicateQuery**:
- Returns: `{ exists: bool, gameId: guid, differences: {...} }`
- Use case: Prevent duplicate imports

**BulkImportGamesCommand**:
- Input: CSV file OR BGG ID list
- Implementation: Queue job system (Hangfire)
- Returns: Job ID for tracking

---

## Approval Queue (Issue #3533)

**GetApprovalQueueQuery**:
- Combines: Pending publication + pending share requests
- Features: Document status, submitter reputation, urgency flags
- Filters: urgency (Low/Medium/High), submitter, hasPdfs

**Response includes**:
- Document status: hasPdfs, pdfCount, ragProcessed, qualityScore
- Submitter reputation: totalContributions, approvalRate, badges
- Urgency: ageInDays, urgency level

---

## Badge System (Issue #2736)

**Badge Types**:
- Contribution: 1st FAQ, 10th FAQ, 100th FAQ, First Game Published
- Milestone: 50 Contributions, 100 Contributions, Quality Contributor (90%+ approval)
- Achievement: Top Contributor (monthly), BGG Importer (10+ games)

**GetBadgeLeaderboardQuery**:
- Query params: period (week/month/year/allTime), badgeType, page, pageSize
- Returns: Ranked users by contribution count or badge count

---

## Domain Events

| Event | When | Subscribers |
|-------|------|-------------|
| `SharedGameCreatedEvent` | Game created | Administration (audit) |
| `GamePublishedEvent` | Approved | UserNotifications, Administration (badge calc) |
| `ShareRequestCreatedEvent` | User submits | UserNotifications (notify admins) |
| `ShareRequestApprovedEvent` | Admin approves | UserNotifications, Administration (badge) |
| `ShareRequestRejectedEvent` | Admin rejects | UserNotifications |
| `DeleteRequestCreatedEvent` | Editor requests delete | Administration |
| `GameSoftDeletedEvent` | Game deleted | KnowledgeBase (cleanup vectors) |
| `DocumentApprovedEvent` | Doc approved for RAG | DocumentProcessing |
| `BadgeEarnedEvent` | Badge awarded | UserNotifications |

---

## Integration Points

**Inbound**:
- GameManagement → SharedGameId linking
- UserLibrary → ShareRequest proposals

**Outbound**:
- DocumentProcessing → PDF processing, RAG indexing
- KnowledgeBase → AI generation (Q&A, state templates, analysis)
- BoardGameGeek API → Import metadata (rate limited: 2 req/s)

---

## Security & Authorization

| Endpoint Pattern | Anonymous | User | Editor | Admin |
|------------------|-----------|------|--------|-------|
| GET /shared-games | ✅ | ✅ | ✅ | ✅ |
| POST /share-requests | ❌ | ✅ | ✅ | ✅ |
| POST /admin/shared-games | ❌ | ❌ | ✅ | ✅ |
| POST /admin/.../approve | ❌ | ❌ | ❌ | ✅ |
| DELETE /admin/.../... (direct) | ❌ | ❌ | ❌ | ✅ |
| DELETE /admin/.../... (request) | ❌ | ❌ | ✅ | ✅ |

**Data Access**:
- Published: Public (searchable by all)
- Draft: Creator + admins
- PendingApproval: Admins only
- Soft-Deleted: Hidden (global filter)

---

## Performance

**Caching**:
- SearchSharedGames: Redis 5m (invalidate: GamePublishedEvent, GameUpdatedEvent)
- GetSharedGameById: Redis 30m (invalidate: GameUpdatedEvent)
- GetPendingApprovals: Redis 1m (invalidate: SubmitForApprovalEvent, ApproveEvent)
- GetBadgeLeaderboard: Redis 1h (invalidate: BadgeEarnedEvent)

---

## Testing

**Unit Tests** (`tests/Api.Tests/SharedGameCatalog/`):
- SharedGame_Tests.cs (publication workflow, soft-delete)
- ShareRequest_Tests.cs (review lock, status transitions)
- DeleteRequest_Tests.cs (approval workflow)
- Badge_Tests.cs (earning logic)

**Integration Tests** (Testcontainers):
1. Create draft → submit → approve → verify published
2. Share request → lock → approve → verify SharedGame created
3. BGG import → check duplicate → import → verify draft created
4. Soft-delete (editor creates request, admin approves, verify IsDeleted=true)
5. Badge earning (approve 10 games, verify badge awarded)

---

## Code Location

`apps/api/src/Api/BoundedContexts/SharedGameCatalog/`

---

## Related Documentation

**ADRs**:
- [ADR-018: PostgreSQL FTS](../../01-architecture/adr/adr-018-postgresql-fts-for-shared-catalog.md)
- [ADR-019: Soft-Delete Workflow](../../01-architecture/adr/adr-019-shared-catalog-delete-workflow.md)
- [ADR-025: SharedGameCatalog Bounded Context](../../01-architecture/adr/adr-025-shared-catalog-bounded-context.md)

**Contexts**:
- [GameManagement](./game-management.md) - Private → Shared linking
- [UserLibrary](./user-library.md) - Share request workflow
- [DocumentProcessing](./document-processing.md) - PDF processing
- [KnowledgeBase](./knowledge-base.md) - RAG indexing

---

**Status**: ✅ Production
**Commands**: 46 | **Queries**: 23 | **Endpoints**: 80+ | **Workflow Areas**: 11 | **Integration Points**: 5 contexts + BGG API


---



<div style="page-break-before: always;"></div>

## bounded-contexts/system-configuration.md

# SystemConfiguration Bounded Context - Complete API Reference

**Runtime Configuration, Feature Flags, AI Model Management, Tier Routing, Quota Limits**

> 📖 **Complete Documentation**: Part of Issue #3794

---

## 📋 Responsabilità

- Runtime configuration management (key-value config store)
- Feature flag system (tier + role-based flags)
- AI model administration (CRUD, priority, tier routing)
- PDF upload limits (global + per-tier quotas)
- Game library limits (per-tier catalog size)
- Session limits (concurrent sessions per tier)
- Rate limit configuration (tier-based + user overrides)
- Configuration versioning (history + rollback)
- Cache invalidation (config change propagation)
- Import/export (environment migration)

---

## 🏗️ Domain Model

### Aggregates

**Configuration** (Aggregate Root):
*(blocco di codice rimosso)*

**AiModel** (Aggregate Root - Issues #2567, #2596):
*(blocco di codice rimosso)*

**FeatureFlag** (Entity):
*(blocco di codice rimosso)*

---

## 📡 Application Layer (CQRS)

> **Total Operations**: 57 (33 commands + 24 queries)
> **Categories**: Config, AI Models, Tier Routing, Feature Flags, PDF Limits, Library Limits, Session Limits, Rate Limits

---

### RUNTIME CONFIGURATION

| Command/Query | HTTP Method | Endpoint | Auth | Purpose |
|---------------|-------------|----------|------|---------|
| `GetAllConfigsQuery` | GET | `/api/v1/admin/configurations` | Admin | List configs with pagination + category filter |
| `GetConfigByIdQuery` | GET | `/api/v1/admin/configurations/{id}` | Admin | Single config details |
| `GetConfigByKeyQuery` | GET | `/api/v1/admin/configurations/key/{key}` | Admin | Config by key |
| `GetConfigCategoriesQuery` | GET | `/api/v1/admin/configurations/categories` | Admin | Category list |
| `GetConfigHistoryQuery` | GET | `/api/v1/admin/configurations/{id}/history` | Admin | Version history |
| `ExportConfigsQuery` | GET | `/api/v1/admin/configurations/export` | Admin | Export all configs |
| `CreateConfigurationCommand` | POST | `/api/v1/admin/configurations` | Admin | Create config |
| `UpdateConfigValueCommand` | PUT | `/api/v1/admin/configurations/{id}` | Admin | Update value + increment version |
| `DeleteConfigurationCommand` | DELETE | `/api/v1/admin/configurations/{id}` | Admin | Delete config |
| `ToggleConfigurationCommand` | PATCH | `/api/v1/admin/configurations/{id}/toggle` | Admin | Toggle IsActive |
| `BulkUpdateConfigsCommand` | POST | `/api/v1/admin/configurations/bulk-update` | Admin | Update multiple configs |
| `ValidateConfigCommand` | POST | `/api/v1/admin/configurations/validate` | Admin | Validate config value |
| `ImportConfigsCommand` | POST | `/api/v1/admin/configurations/import` | Admin | Import from JSON/CSV |
| `RollbackConfigCommand` | POST | `/api/v1/admin/configurations/{id}/rollback/{version}` | Admin | Rollback to version |
| `InvalidateCacheCommand` | POST | `/api/v1/admin/configurations/cache/invalidate` | Admin | Invalidate config cache |

**CreateConfigurationCommand**:
- **Request Schema**:
  *(blocco di codice rimosso)*

**BulkUpdateConfigsCommand**:
- **Request Schema**:
  *(blocco di codice rimosso)*
- **Response**: List of updated configs

**RollbackConfigCommand**:
- **Purpose**: Revert to previous version (undo config change)
- **Path**: `/api/v1/admin/configurations/{id}/rollback/{version}`
- **Example**: Rollback to version 3

---

### AI MODEL MANAGEMENT

| Command/Query | HTTP Method | Endpoint | Auth | Purpose |
|---------------|-------------|----------|------|---------|
| `GetAllAiModelsQuery` | GET | `/api/v1/admin/ai-models` | Admin | List models with filters |
| `GetAiModelByIdQuery` | GET | `/api/v1/admin/ai-models/{id}` | Admin | Model details |
| `CreateAiModelCommand` | POST | `/api/v1/admin/ai-models` | Admin | Create model |
| `UpdateAiModelCommand` | PUT | `/api/v1/admin/ai-models/{id}` | Admin | Update model |
| `DeleteAiModelCommand` | DELETE | `/api/v1/admin/ai-models/{id}` | Admin | Delete model |
| `UpdateModelPriorityCommand` | PATCH | `/api/v1/admin/ai-models/{id}/priority` | Admin | Set priority (1-10) |
| `ToggleAiModelActiveCommand` | PATCH | `/api/v1/admin/ai-models/{id}/toggle` | Admin | Toggle active status |

**CreateAiModelCommand** (Issue #2567):
- **Request Schema**:
  *(blocco di codice rimosso)*
- **Priority System**:
  - 1 = Primary model (used first)
  - 2-5 = Fallbacks (tried in order if primary fails/unavailable)
  - 10 = Disabled (not used)

**GetAllAiModelsQuery**:
- **Query Parameters**:
  - `provider`: Filter by providerId
  - `isActive`: true | false
  - `minPriority`, `maxPriority`: Priority range
- **Response**: Ordered by priority ASC

---

### TIER ROUTING (Issue #2596)

| Command/Query | HTTP Method | Endpoint | Auth | Purpose |
|---------------|-------------|----------|------|---------|
| `GetTierRoutingQuery` | GET | `/api/v1/admin/tier-routing` | Admin | All tier routing configs |
| `UpdateTierRoutingCommand` | PUT | `/api/v1/admin/tier-routing` | Admin | Configure tier routing |

**UpdateTierRoutingCommand**:
- **Purpose**: Configure which models each tier can use
- **Request Schema**:
  *(blocco di codice rimosso)*
- **Tiers**:
  - Anonymous: Haiku only
  - User (Free): Haiku + GPT-4 Mini
  - Editor: All models
  - Admin: All models + test models
  - Premium: GPT-4 + Claude Sonnet

---

### FEATURE FLAGS (Issue #3073)

| Command/Query | HTTP Method | Endpoint | Auth | Purpose |
|---------------|-------------|----------|------|---------|
| `GetAllFeatureFlagsQuery` | GET | `/api/v1/admin/feature-flags` | Admin | List all flags |
| `IsFeatureEnabledQuery` | GET | `/api/v1/admin/feature-flags/{key}` | Admin | Check flag status |
| `UpdateFeatureFlagCommand` | PUT | `/api/v1/admin/feature-flags/{key}` | Admin | Update flag |
| `ToggleFeatureFlagCommand` | POST | `/api/v1/admin/feature-flags/{key}/toggle` | Admin | Toggle enabled |
| `EnableFeatureForTierCommand` | POST | `/api/v1/admin/feature-flags/{key}/tier/{tier}/enable` | Admin | Enable for specific tier |
| `DisableFeatureForTierCommand` | POST | `/api/v1/admin/feature-flags/{key}/tier/{tier}/disable` | Admin | Disable for specific tier |

**GetAllFeatureFlagsQuery**:
- **Response Schema**:
  *(blocco di codice rimosso)*

**EnableFeatureForTierCommand**:
- **Purpose**: Enable feature for specific tier (e.g., Premium-only features)
- **Path**: `/api/v1/admin/feature-flags/MultiAgentChat/tier/Premium/enable`

---

### PDF UPLOAD LIMITS

| Command/Query | HTTP Method | Endpoint | Auth | Purpose |
|---------------|-------------|----------|------|---------|
| `GetPdfUploadLimitsQuery` | GET | `/api/v1/admin/system/pdf-upload-limits` | Admin | Global PDF limits |
| `UpdatePdfUploadLimitsCommand` | PUT | `/api/v1/admin/system/pdf-upload-limits` | Admin | Update global limits |
| `GetAllPdfLimitsQuery` | GET | `/api/v1/admin/config/pdf-limits` | Admin | Tier-specific limits |
| `UpdatePdfLimitsCommand` | PUT | `/api/v1/admin/config/pdf-limits/{tier}` | Admin | Update tier limits |

**UpdatePdfUploadLimitsCommand** (Issue #3072):
- **Request Schema**:
  *(blocco di codice rimosso)*
- **Global Limits**: Apply to all tiers

**UpdatePdfLimitsCommand** (Issue #3333):
- **Request Schema**:
  *(blocco di codice rimosso)*
- **Tier-Specific**: Quotas per subscription level

---

### GAME LIBRARY LIMITS (Issue #2444)

| Command/Query | HTTP Method | Endpoint | Auth | Purpose |
|---------------|-------------|----------|------|---------|
| `GetGameLibraryLimitsQuery` | GET | `/api/v1/admin/config/game-library-limits` | Admin | Per-tier game limits |
| `UpdateGameLibraryLimitsCommand` | PUT | `/api/v1/admin/config/game-library-limits` | Admin | Update limits |

**UpdateGameLibraryLimitsCommand**:
- **Request Schema**:
  *(blocco di codice rimosso)*
- **Value**: -1 = Unlimited

---

### SESSION LIMITS (Issue #3070)

| Command/Query | HTTP Method | Endpoint | Auth | Purpose |
|---------------|-------------|----------|------|---------|
| `GetSessionLimitsQuery` | GET | `/api/v1/admin/system/session-limits` | Admin | Per-tier session quotas |
| `UpdateSessionLimitsCommand` | PUT | `/api/v1/admin/system/session-limits` | Admin | Update limits |
| `GetUserSessionQuotaQuery` | GET | `/api/v1/users/{id}/session-quota` | User/Admin | User's quota status |

**GetUserSessionQuotaQuery**:
- **Authorization**: User can check own quota, Admin can check any user
- **Response Schema**:
  *(blocco di codice rimosso)*

---

### RATE LIMIT MANAGEMENT (Issue #2738)

| Command/Query | HTTP Method | Endpoint | Auth | Purpose |
|---------------|-------------|----------|------|---------|
| `GetRateLimitConfigQuery` | GET | `/api/v1/admin/config/share-request-limits` | Admin | Tier rate limit configs |
| `UpdateRateLimitConfigCommand` | PUT | `/api/v1/admin/config/share-request-limits/{tier}` | Admin | Update tier limits |
| `GetUserRateLimitStatusQuery` | GET | `/api/v1/admin/users/{id}/rate-limit-status` | Admin | User's current status |
| `GetAllRateLimitOverridesQuery` | GET | `/api/v1/admin/rate-limit-overrides` | Admin | List user overrides |
| `CreateUserRateLimitOverrideCommand` | POST | `/api/v1/admin/users/{id}/rate-limit-override` | Admin | Create override |
| `RemoveUserRateLimitOverrideCommand` | DELETE | `/api/v1/admin/users/{id}/rate-limit-override` | Admin | Remove override |

**UpdateRateLimitConfigCommand**:
- **Request Schema**:
  *(blocco di codice rimosso)*

**CreateUserRateLimitOverrideCommand**:
- **Request Schema**:
  *(blocco di codice rimosso)*
- **Use Case**: Grant specific users higher limits temporarily

---

## 🔄 Domain Events

| Event | When Raised | Subscribers |
|-------|-------------|-------------|
| `ConfigurationUpdatedEvent` | Config value changed | All services (cache invalidation) |
| `FeatureFlagToggledEvent` | Flag enabled/disabled | Frontend (feature availability) |
| `AiModelPriorityChangedEvent` | Model priority updated | KnowledgeBase (LLM selection) |
| `TierRoutingUpdatedEvent` | Tier routing changed | KnowledgeBase, Administration |
| `QuotaLimitChangedEvent` | Any limit updated | Affected services (quota enforcement) |

---

## 🔗 Integration Points

### Inbound Dependencies

**All Contexts**:
- Query configuration values at runtime
- Example: Authentication queries "SessionExpirationDays"

### Outbound Dependencies

**Redis (Cache)**:
- Caches configuration values (1 hour TTL)
- Invalidates on ConfigurationUpdatedEvent

### Event-Driven Communication

*(blocco di codice rimosso)*

---

## 🎯 Common Usage Examples

### Example 1: Update RAG Configuration

*(blocco di codice rimosso)*

**Result**: Next RAG query uses TopK=15

---

### Example 2: Configure Tier Routing

*(blocco di codice rimosso)*

---

### Example 3: Enable Feature for Premium Users

*(blocco di codice rimosso)*

---

## 📊 Performance Characteristics

### Caching

| Query | Cache | TTL | Invalidation |
|-------|-------|-----|--------------|
| Config values | Redis | 1 hour | ConfigurationUpdatedEvent |
| Feature flags | Redis | 30 min | FeatureFlagToggledEvent |
| AI models | Redis | 1 hour | AiModelPriorityChangedEvent |
| Tier routing | Redis | 1 hour | TierRoutingUpdatedEvent |

---

## 📂 Code Location

`apps/api/src/Api/BoundedContexts/SystemConfiguration/`

**Endpoint Files**:
- ConfigurationEndpoints.cs
- AiModelAdminEndpoints.cs
- FeatureFlagEndpoints.cs
- PdfUploadLimitsConfigEndpoints.cs
- AdminConfigEndpoints.cs
- GameLibraryConfigEndpoints.cs
- SessionLimitsConfigEndpoints.cs
- RateLimitAdminEndpoints.cs

---

**Status**: ✅ Production
**Last Updated**: 2026-02-07
**Total Commands**: 33
**Total Queries**: 24
**Total Endpoints**: 43
**Configuration Areas**: 8


---



<div style="page-break-before: always;"></div>

## bounded-contexts/user-library.md

# UserLibrary Bounded Context

**Personal Collections, Wishlist, Played History, Labels, Private PDFs, Sharing**

---

## Responsibilities

- Personal game collection (add/remove/organize)
- Wishlist management
- Played history (sessions + stats)
- Custom labels & categorization
- Private PDFs per game
- Library sharing (public links)
- Game state tracking (ownership, condition, loans)
- Agent configuration per game
- Quota enforcement (tier-based)
- Private games (Phase 2 - pending)

---

## Domain Model

| Aggregate | Key Fields | Purpose |
|-----------|-----------|---------|
| **UserLibraryEntry** | Id, UserId, GameId, TimesPlayed, LastPlayedAt, PrivatePdfDocumentId, AgentConfig, LabelIds[] | Aggregate root |
| **Label** | Id, UserId, Name, Color, IsSystem | Custom labels |
| **LibraryShareLink** | Id, UserId, ShareToken, IsActive, ExpiresAt, MaxViews, ViewCount | Public sharing |

**Value Objects**: AgentConfiguration (ModelType, Temperature, RagStrategy)

**Domain Methods**: `RecordPlay()`, `AssociatePrivatePdf()`, `ConfigureAgent()`, `LoanTo()`, `ReturnFromLoan()`, `AddLabel()`

---

## API Operations (42 total)

**24 Commands**: AddGameToLibrary, RemoveGameFromLibrary, UpdateLibraryEntry, UploadCustomGamePdf, ResetGamePdf, RemovePrivatePdf, ConfigureGameAgent, ResetGameAgent, SaveAgentConfig, CreateCustomLabel, DeleteCustomLabel, AddLabelToGame, RemoveLabelFromGame, CreateLibraryShareLink, UpdateLibraryShareLink, RevokeLibraryShareLink, RecordGameSession, SendLoanReminder, +6 private game commands (pending)

**15 Queries**: GetUserLibrary, GetLibraryStats, GetLibraryQuota, GetGameInLibraryStatus, GetGamePdfs, GetGameAgentConfig, GetLabels, GetGameLabels, GetLibraryShareLink, GetSharedLibrary, GetGameDetail, GetGameChecklist, +3 private game queries (pending)

---

## Key Endpoints

| Method | Endpoint | Auth | Purpose |
|--------|----------|------|---------|
| GET | `/api/v1/library` | Session | List collection (paginated, filtered) |
| GET | `/api/v1/library/stats` | Session | Dashboard stats |
| GET | `/api/v1/library/quota` | Session | Tier quota usage |
| POST | `/api/v1/library/games/{gameId}` | Session | Add game |
| DELETE | `/api/v1/library/games/{gameId}` | Session | Remove game |
| PATCH | `/api/v1/library/games/{gameId}` | Session | Update entry |
| POST | `/api/v1/library/games/{gameId}/pdf` | Session | Upload private PDF |
| GET | `/api/v1/library/{entryId}/pdf/progress` | Session | SSE progress |
| PUT | `/api/v1/library/games/{gameId}/agent` | Session | Configure agent |
| POST | `/api/v1/library/labels` | Session | Create label |
| POST | `/api/v1/library/share` | Session | Create share link |
| GET | `/api/v1/library/shared/{shareToken}` | 🟢 Public | View shared library |

---

## Quota Limits

| Tier | Max Games |
|------|-----------|
| Free | 10 |
| Basic | 50 |
| Premium | 500 |
| Enterprise | Unlimited |

---

## PDF Management (Issue #3489)

**Upload Flow**:
1. User uploads private PDF
2. DocumentProcessing extracts text
3. SSE progress updates (Issue #3653)
4. PDF associated with library entry

**SSE Events**: `progress` (extraction/chunking), `error`, `complete`, `heartbeat` (30s)

**GetGamePdfs** returns: Shared (community) + Private (user-uploaded) PDFs

---

## Agent Configuration (Issue #3212)

**Purpose**: Per-game AI preferences (model, temperature, RAG strategy)

**Use Case**: User wants GPT-4 for Azul but Haiku for simpler games

**ConfigureGameAgent** accepts:
- ModelType: Valid LLM model
- Temperature: 0.0-2.0
- RagStrategy: Fast | Balanced | Precise | Expert | Consensus

---

## Labels & Organization (Epic #3511)

**System Labels** (pre-defined):
- Favorites (#FF5722)
- Wishlist (#2196F3)
- Played (#4CAF50)

**Custom Labels**: User-created, 1-50 chars, unique per user, hex color

**Filtering**: GetUserLibrary accepts `labelIds` query parameter

---

## Library Sharing

**CreateLibraryShareLink** generates:
- ShareToken (URL-safe)
- ShareUrl: `https://meepleai.dev/library/shared/{token}`
- ExpiresAt, MaxViews

**GetSharedLibrary** (public access):
- Excludes: Purchase prices, loan status, agent configs, private notes
- Excludes: Private PDFs

**Rate Limiting**: 10 share links/day per user

---

## Domain Events

| Event | When | Subscribers |
|-------|------|-------------|
| `GameAddedToLibraryEvent` | Game added | Administration (analytics) |
| `GameRemovedFromLibraryEvent` | Game removed | Administration |
| `PrivatePdfUploadedEvent` | PDF upload started | DocumentProcessing |
| `PlayRecordedEvent` | Session recorded | GameManagement |
| `LabelCreatedEvent` | Custom label created | Administration |
| `ShareLinkCreatedEvent` | Share link generated | Administration |
| `ShareLinkAccessedEvent` | Shared library viewed | UserNotifications |

---

## Integration Points

**Inbound**:
- GameManagement → game metadata (title, image, publisher)
- DocumentProcessing → private PDF processing + SSE progress

**Outbound**:
- DocumentProcessing → trigger PDF extraction
- UserNotifications → loan reminders, share link notifications
- GameManagement → PlayRecordedEvent for stats

---

## Security

**Access Control**:
- User isolation (only own library)
- Admin override (audit only)
- Share links: public with token validation (expiry + view limits)
- Private PDFs: uploader only (not shared)

**Data Privacy (Share Link Sanitization)**:
- Excludes: Purchase prices, loan status, agent configs, private notes, private PDFs

---

## Performance

**Caching**:
- GetUserLibrary: Redis 2m (invalidate: GameAddedEvent, GameRemovedEvent, EntryUpdatedEvent)
- GetLibraryStats: Redis 5m (invalidate: PlayRecordedEvent)
- GetLabels: Redis 30m (invalidate: LabelCreatedEvent, LabelDeletedEvent)
- GetSharedLibrary: Redis 10m (invalidate: ShareLinkRevokedEvent)

**Database Indexes**:
*(blocco di codice rimosso)*

---

## Testing

**Unit Tests** (`tests/Api.Tests/UserLibrary/`):
- UserLibraryEntry_Tests.cs (domain logic)
- Label_Tests.cs (system vs custom)
- LibraryShareLink_Tests.cs (view count, expiry)
- AgentConfiguration_Tests.cs (validation)

**Integration Tests** (Testcontainers):
1. Add game → configure agent → upload PDF → record play
2. Create custom label → filter library by label
3. Create share link → verify public access → check sanitization
4. Upload private PDF → verify SSE progress → check isolation

---

## Code Location

`apps/api/src/Api/BoundedContexts/UserLibrary/`

---

## Related Documentation

**Contexts**:
- [GameManagement](./game-management.md) - Game catalog source
- [DocumentProcessing](./document-processing.md) - Private PDF processing
- [SharedGameCatalog](./shared-game-catalog.md) - Private game proposals (Phase 4)

---

**Status**: ✅ Production (Private Games: 🔴 Phase 2-5 pending)
**Commands**: 24 | **Queries**: 15 | **Endpoints Mapped**: 34 (8 pending)


---



<div style="page-break-before: always;"></div>

## bounded-contexts/user-notifications.md

# UserNotifications Bounded Context - Complete API Reference

**Notifiche in-app, email, push notifications, e gestione preferenze**

> 📖 **Complete Documentation**: Part of Issue #3794

---

## 📋 Responsabilità

- Notifiche in-app (sistema badge per frontend)
- Email notifications (security alerts, share requests, achievements)
- Push notifications (future - infrastructure ready)
- Gestione preferenze notifiche utente
- Event-driven notification triggers (cross-context integration)
- Notification severity levels (Info, Success, Warning, Error)
- Admin alerts e digest emails

---

## 🏗️ Domain Model

### Aggregates

**Notification** (Aggregate Root):
*(blocco di codice rimosso)*

### Value Objects

**NotificationType** (20+ Types):
*(blocco di codice rimosso)*

**NotificationSeverity**:
*(blocco di codice rimosso)*

---

## 📡 Application Layer (CQRS)

> **Total Operations**: 13 (4 commands + 4 queries + 5 background jobs)
> **Event Handlers**: 11 (responds to cross-context events)

---

### Commands

| Command | HTTP Method | Endpoint | Auth | Request | Response |
|---------|-------------|----------|------|---------|----------|
| `MarkNotificationReadCommand` | PUT | `/api/v1/notifications/{id}/read` | Session | None | 204 No Content |
| `MarkAllNotificationsReadCommand` | PUT | `/api/v1/notifications/read-all` | Session | None | `{ count: number }` |

**MarkNotificationReadCommand**:
- **Purpose**: Mark single notification as read (clear badge)
- **Side Effects**:
  - Sets IsRead = true, ReadAt = UtcNow
  - Decrements unread count (cached in Redis)
- **Authorization**: Can only mark own notifications

**MarkAllNotificationsReadCommand**:
- **Purpose**: Bulk clear all unread notifications
- **Response Schema**:
  *(blocco di codice rimosso)*
- **Side Effects**: Updates all user's unread notifications in batch

---

### Queries

| Query | HTTP Method | Endpoint | Auth | Query Params | Response |
|-------|-------------|----------|------|--------------|----------|
| `GetNotificationsQuery` | GET | `/api/v1/notifications` | Session | `page?`, `pageSize?`, `unreadOnly?`, `severity?`, `type?` | `PaginatedList<NotificationDto>` |
| `GetUnreadCountQuery` | GET | `/api/v1/notifications/unread-count` | Session | None | `{ count: number }` |

**GetNotificationsQuery**:
- **Purpose**: Fetch user's notifications with filtering and pagination
- **Query Parameters**:
  - `page` (default: 1): Page number
  - `pageSize` (default: 20, max: 100): Items per page
  - `unreadOnly` (default: false): Filter to unread only
  - `severity` (optional): Filter by Info|Success|Warning|Error
  - `type` (optional): Filter by notification type
- **Response Schema**:
  *(blocco di codice rimosso)*

**GetUnreadCountQuery**:
- **Purpose**: Badge count for notification bell icon
- **Response**: `{ "count": 8 }`
- **Caching**: Redis cached (1 minute TTL)

---

### Event Handlers (Cross-Context Integration)

| Event Handler | Source Event | Notification Created | Recipients |
|---------------|--------------|----------------------|------------|
| `ShareRequestCreatedNotificationHandler` | SharedGameCatalog: `ShareRequestCreatedEvent` | ShareRequestCreated (Info) | User (requester) |
| `ShareRequestApprovedNotificationHandler` | SharedGameCatalog: `ShareRequestApprovedEvent` | ShareRequestApproved (Success) | User (requester) |
| `ShareRequestRejectedNotificationHandler` | SharedGameCatalog: `ShareRequestRejectedEvent` | ShareRequestRejected (Warning) | User (requester) + reason |
| `ShareRequestChangesRequestedNotificationHandler` | SharedGameCatalog: `ChangesRequestedEvent` | ShareRequestChangesRequested (Warning) | User (requester) + feedback |
| `NewShareRequestAdminAlertHandler` | SharedGameCatalog: `ShareRequestCreatedEvent` | AdminNewShareRequest (Info) | All Admins |
| `BadgeEarnedNotificationHandler` | Administration: `BadgeEarnedEvent` | BadgeEarned (Success) | User (achiever) |
| `MilestoneBadgeNotificationHandler` | Administration: `MilestoneAchievedEvent` | BadgeEarned (Success) | User + milestone details |
| `UserSuspendedEventHandler` | Administration: `UserSuspendedEvent` | SessionTerminated (Error) | Suspended user |
| `UserUnsuspendedEventHandler` | Administration: `UserUnsuspendedEvent` | SessionTerminated (Success) | Restored user |
| `RateLimitApproachingHandler` | Administration: `RateLimitWarningEvent` | RateLimitApproaching (Warning) | User (approaching limit) |
| `RateLimitReachedHandler` | Administration: `RateLimitReachedEvent` | RateLimitReached (Error) | User (limit hit) |

**Pattern**: Event handlers listen to domain events from other contexts and create appropriate notifications.

---

### Background Jobs (Scheduled)

| Job | Schedule | Purpose | Notification Created |
|-----|----------|---------|----------------------|
| `AdminShareRequestDigestJob` | Daily 9:00 AM | Digest email to admins: pending share requests | AdminStaleShareRequests (if >5 days old) |
| `CooldownEndReminderJob` | Every 15 min | Remind users when rate limit cooldown ends | CooldownEnded (Info) |
| `StaleShareRequestWarningJob` | Daily 6:00 PM | Alert admins about aging share requests | AdminStaleShareRequests (Warning) |

**AdminShareRequestDigestJob**:
- **Purpose**: Daily summary email to admins
- **Email Content**:
  *(blocco di codice rimosso)*
- **Recipients**: All users with Admin role

---

## 🔗 Integration Points

### Inbound Dependencies (Event Listeners)

**SharedGameCatalog Context**:
- Listens: ShareRequestCreatedEvent, ApprovedEvent, RejectedEvent, ChangesRequestedEvent
- Notifies: Requester + Admins

**Administration Context**:
- Listens: BadgeEarnedEvent, UserSuspendedEvent, RateLimitWarningEvent
- Notifies: Affected users

**DocumentProcessing Context**:
- Listens: PdfUploadCompletedEvent, ProcessingFailedEvent, RuleSpecGeneratedEvent
- Notifies: PDF uploader

**GameManagement Context**:
- Listens: RuleCommentCreatedEvent, GamePublishedEvent
- Notifies: Comment author, game owner

### Outbound Dependencies

**Email Service** (SMTP):
- Sends email notifications for critical events
- Example: Password changed, account suspended, share request approved

**Push Notification Service** (Future):
- Firebase Cloud Messaging (FCM) for mobile
- Web Push API for browser notifications

### Event-Driven Communication

*(blocco di codice rimosso)*

---

## 🔐 Security & Authorization

### Data Access

- **User Isolation**: Users can only access own notifications
- **Admin Override**: Admins can view all notifications (audit purposes)
- **Notification Retention**: 90 days (GDPR compliance, auto-cleanup job)

### Privacy

- **No Sensitive Data**: Notifications contain summaries only, no passwords/tokens
- **Deep Links**: Use resource IDs (not sensitive data in URLs)
- **Metadata**: JSON field for extensibility, sanitized before storage

---

## 🎯 Common Usage Examples

### Example 1: Get Unread Notifications

*(blocco di codice rimosso)*

**Response**:
*(blocco di codice rimosso)*

### Example 2: Mark All as Read

*(blocco di codice rimosso)*

**Response**:
*(blocco di codice rimosso)*

---

## 📊 Performance Characteristics

### Caching

| Query | Cache | TTL | Invalidation |
|-------|-------|-----|--------------|
| GetUnreadCountQuery | Redis | 1 min | NotificationCreatedEvent, MarkAsReadEvent |
| GetNotificationsQuery (recent) | Redis | 30 sec | NotificationCreatedEvent |

### Database Indexes

*(blocco di codice rimosso)*

---

## 🔗 Related Documentation

- [SharedGameCatalog](./shared-game-catalog.md) - Share request notifications
- [Administration](./administration.md) - Badge system, rate limiting
- [DocumentProcessing](./document-processing.md) - PDF processing notifications

---

**Status**: ✅ Production
**Last Updated**: 2026-02-07
**Total Commands**: 4
**Total Queries**: 4
**Event Handlers**: 11
**Background Jobs**: 3
**Notification Types**: 20+


---



<div style="page-break-before: always;"></div>

## bounded-contexts/workflow-integration.md

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
*(blocco di codice rimosso)*

**WorkflowErrorLog** (Aggregate Root):
*(blocco di codice rimosso)*

### Value Objects

**WorkflowUrl**:
*(blocco di codice rimosso)*

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
  *(blocco di codice rimosso)*
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
  *(blocco di codice rimosso)*

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
  *(blocco di codice rimosso)*
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
  *(blocco di codice rimosso)*
- **Side Effects**:
  - Creates WorkflowErrorLog entry
  - If RetryCount > 3: raises alert to admins
  - Raises `WorkflowErrorLoggedEvent`
- **Domain Events**: `WorkflowErrorLoggedEvent`

**GetWorkflowErrorsQuery**:
- **Purpose**: Admin dashboard for workflow debugging
- **Filters**: ConfigId, date range, pagination
- **Response Schema**:
  *(blocco di codice rimosso)*

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
*(blocco di codice rimosso)*

**Test Connection**:
*(blocco di codice rimosso)*

**Response**:
*(blocco di codice rimosso)*

---

### Example 2: Import n8n Template

*(blocco di codice rimosso)*

---

### Example 3: Query Workflow Errors

*(blocco di codice rimosso)*

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


---

