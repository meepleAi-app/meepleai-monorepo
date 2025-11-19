# Remaining Legacy Service Coupling

**Date**: 2025-11-17
**Branch**: `claude/fix-api-test-coupling-014SPx2WyPCq2LfaZKGJf1jA`
**Issue**: BGAI-034 - Fix API test coupling and remove legacy code

## Summary

This document tracks the remaining legacy service coupling in the API routing layer that needs to be migrated to CQRS pattern.

## Completed Migrations ✅

### OAuth Login (AuthEndpoints.cs) - Commit e9675dc
- ✅ `InitiateOAuthLoginCommand` + Handler
- ✅ Endpoint migrated: `GET /auth/oauth/{provider}/login`
- ✅ Removed: Direct `IOAuthService` injection from endpoint
- ✅ OAuth callback already used CQRS (consistency achieved)

### Password Reset (AuthEndpoints.cs) - Commit e9675dc
- ✅ `RequestPasswordResetCommand` + Handler
- ✅ `ValidatePasswordResetTokenQuery` + Handler
- ✅ `ResetPasswordCommand` + Handler
- ✅ Endpoints migrated:
  - `POST /auth/password-reset/request`
  - `GET /auth/password-reset/verify`
  - `PUT /auth/password-reset/confirm`
- ✅ Removed: Direct `IPasswordResetService` injection from 3 endpoints

### Prompt Template Activation (AdminEndpoints.cs) - Commit 6311ed9 ⭐ CRITICAL
- ✅ `ActivatePromptVersionCommand` updated to Guid types
- ✅ `ActivatePromptVersionCommandHandler` updated to Guid types
- ✅ Endpoint migrated: `POST /admin/prompts/{id}/versions/{versionId}/activate`
- ✅ Removed: Direct `IPromptTemplateService` injection from critical endpoint
- ✅ Complete audit trail for compliance (activation + deactivation logs)
- ✅ Transaction-safe with automatic rollback
- ⭐ **Impact**: System-critical endpoint now has full audit trail and CQRS pattern

### 2FA/TOTP Operations (AuthEndpoints.cs) - Commit 3075a33
- ✅ `GenerateTotpSetupCommand` + Handler (TwoFactor directory)
- ✅ `Verify2FACommand` + Handler (TwoFactor directory)
- ✅ Endpoints migrated:
  - `POST /auth/2fa/setup` (TOTP secret + QR code generation)
  - `POST /auth/2fa/verify` (TOTP/backup code verification)
- ✅ Removed: Direct `ITotpService` injection from 2 endpoints
- ✅ Removed: Direct `ITempSessionService` injection from verify endpoint
- ✅ Security maintained: Single-use temp sessions (5-min TTL), rate limiting

### N8n Template Management (AdminEndpoints.cs) - Commit f286da0
- ✅ `GetN8nTemplatesQuery` + Handler (WorkflowIntegration context)
- ✅ `GetN8nTemplateByIdQuery` + Handler (WorkflowIntegration context)
- ✅ `ImportN8nTemplateCommand` + Handler (WorkflowIntegration context)
- ✅ `ValidateN8nTemplateQuery` + Handler (WorkflowIntegration context)
- ✅ Endpoints migrated:
  - `GET /n8n/templates` (List templates with optional category filter)
  - `GET /n8n/templates/{id}` (Get template details)
  - `POST /n8n/templates/{id}/import` (Import with parameter substitution)
  - `POST /n8n/templates/validate` (Validate template JSON structure)
- ✅ Removed: Direct `N8nTemplateService` injection from 4 endpoints
- ✅ N8nTemplateService kept as infrastructure adapter (file I/O, n8n API, encryption)

### Quality Reports (AdminEndpoints.cs) - Commit e331661
- ✅ `GenerateQualityReportQuery` + Handler (Administration context)
- ✅ Endpoint migrated:
  - `GET /admin/quality/report` (Generate quality report with date range)
- ✅ Removed: Direct `IQualityReportService` injection from endpoint
- ✅ QualityReportService kept as infrastructure adapter + BackgroundService (periodic reports)

### N8n Config Test (AdminEndpoints.cs) - Commit e331661
- ✅ `TestN8nConnectionCommand` + Handler (WorkflowIntegration context)
- ✅ Endpoint migrated:
  - `POST /admin/n8n/{configId}/test` (Test n8n connection with latency measurement)
- ✅ Removed: Direct `N8nConfigService` injection from endpoint
- ✅ N8nConfigService kept as infrastructure adapter (HTTP calls, encryption, database updates)

### Workflow Error Logging (AdminEndpoints.cs) - Commit e331661
- ✅ `LogWorkflowErrorCommand` + Handler (WorkflowIntegration context)
- ✅ `GetWorkflowErrorsQuery` + Handler (WorkflowIntegration context)
- ✅ `GetWorkflowErrorByIdQuery` + Handler (WorkflowIntegration context)
- ✅ Endpoints migrated:
  - `POST /logs/workflow-error` (n8n webhook, no auth)
  - `GET /admin/workflows/errors` (List errors with filters/pagination)
  - `GET /admin/workflows/errors/{id}` (Get specific error)
- ✅ Removed: Direct `IWorkflowErrorLoggingService` injection from 3 endpoints
- ✅ WorkflowErrorLoggingService kept as infrastructure adapter (database, caching, security sanitization)

### AI Request Logging (AiEndpoints.cs) - Commit d2150c2
- ✅ Reused existing `LogAiRequestCommand` (Administration context)
- ✅ Endpoints migrated:
  - `POST /agents/explain` (RAG explain endpoint)
  - `POST /agents/qa/stream` (Streaming QA with SSE)
  - `POST /agents/setup` (Setup guide streaming)
  - `POST /agents/chess` (Chess conversational agent)
- ✅ Removed: Direct `AiRequestLogService` injection from 4 endpoints
- ✅ Pattern: Direct CQRS usage - no new handlers needed (reused existing)
- ✅ Fire-and-forget telemetry pattern maintained

### BoardGameGeek API Integration (AiEndpoints.cs) - Commit 3235022
- ✅ `SearchBggGamesQuery` + Handler (GameManagement/BggApi context)
- ✅ `GetBggGameDetailsQuery` + Handler (GameManagement/BggApi context)
- ✅ Endpoints migrated:
  - `GET /bgg/search` (Search BGG by name with optional exact match)
  - `GET /bgg/game/{id}` (Get detailed game info from BGG by ID)
- ✅ Removed: Direct `IBggApiService` injection from 2 endpoints
- ✅ BggApiService kept as infrastructure adapter (external BoardGameGeek XML API v2, HTTP calls, XML parsing, HybridCache, rate limiting)
- ✅ Pattern: Business logic in handlers (empty query validation), infrastructure delegation for external API

### Prompt Evaluation & Testing (AdminEndpoints.cs) - Commit 697fac2
- ✅ `EvaluatePromptCommand` + Handler (Administration/PromptEvaluation context)
- ✅ `ComparePromptVersionsCommand` + Handler (Administration/PromptEvaluation context)
- ✅ `GetEvaluationHistoryQuery` + Handler (Administration/PromptEvaluation context)
- ✅ `GenerateEvaluationReportQuery` + Handler (Administration/PromptEvaluation context)
- ✅ Endpoints migrated:
  - `POST /admin/prompts/{id}/versions/{versionId}/evaluate` (Automated prompt testing with datasets)
  - `POST /admin/prompts/{id}/compare` (A/B testing with recommendations)
  - `GET /admin/prompts/{id}/evaluations` (Historical evaluation results)
  - `GET /admin/prompts/evaluations/{id}/report` (Markdown/JSON report generation)
- ✅ Removed: Direct `IPromptEvaluationService` injection from 4 endpoints
- ✅ PromptEvaluationService kept as infrastructure adapter (complex evaluation logic: dataset loading/validation, file I/O with security checks, RAG orchestration, metric calculations, recommendation algorithms)
- ✅ Pattern: Business logic in handlers, infrastructure delegation for evaluation algorithms (5 metrics: accuracy, hallucination, confidence, citations, latency)

### Cleanup
- ✅ Removed unused `using Api.Services;` from GameEndpoints.cs
- ✅ Removed TODO comment about legacy agent endpoint (Issue #866)

**Total Removed**: 26 legacy service injections from routing layer (including 1 CRITICAL)

**Updated Counts** (after PromptEvaluation migration):
- **Initial**: 37 legacy service injections
- **Migrated**: 26 injections (OAuth, Password Reset x3, Prompt, 2FA x2, N8n x5, Quality, Workflow x3, AiRequestLog x4, BggApi x2, PromptEvaluation x4)
- **Confirmed Orchestration**: 2 injections (IRagService x2 in AiEndpoints)
- **Remaining to Migrate**: 9 legacy service injections

---

## Remaining Legacy Coupling ⚠️

**Updated**: 9 remaining instances requiring migration (down from 37, -26 migrated, -2 confirmed orchestration)

### AuthEndpoints.cs (MIGRATED ✅)

| Line | Service | Method | Endpoint | Status |
|------|---------|--------|----------|--------|
| ~~199~~ | ~~ITotpService~~ | ~~GenerateSetupAsync()~~ | ~~POST /auth/2fa/setup~~ | **✅ MIGRATED** (commit 3075a33) |
| ~~260~~ | ~~ITotpService~~ | ~~VerifyCodeAsync()~~ | ~~POST /auth/2fa/verify~~ | **✅ MIGRATED** (commit 3075a33) |
| ~~260~~ | ~~ITempSessionService~~ | ~~ValidateAndConsumeTempSessionAsync()~~ | ~~POST /auth/2fa/verify~~ | **✅ MIGRATED** (commit 3075a33) |
| 260+ | IRateLimitService | CheckRateLimitAsync() | Multiple 2FA/OAuth endpoints | **KEEP** (infrastructure)* |

*IRateLimitService is cross-cutting infrastructure concern, acceptable to keep at endpoint level

---

### AiEndpoints.cs (3 instances remaining)

| Line | Service | Method | Endpoint | Status |
|------|---------|--------|----------|--------|
| 25 | IRagService | AskWithHybridSearchAsync() | POST /ai/chat | **KEEP** ✅ (orchestration)* |
| 178 | IRagService | ExplainAsync() | POST /ai/explain | **KEEP** ✅ (orchestration)* |
| ~~178~~ | ~~AiRequestLogService~~ | ~~LogRequestAsync()~~ | ~~POST /agents/explain~~ | **✅ MIGRATED** (commit d2150c2) |
| ~~267~~ | ~~AiRequestLogService~~ | ~~LogRequestAsync()~~ | ~~POST /agents/qa/stream~~ | **✅ MIGRATED** (commit d2150c2) |
| ~~424~~ | ~~AiRequestLogService~~ | ~~LogRequestAsync()~~ | ~~POST /agents/setup~~ | **✅ MIGRATED** (commit d2150c2) |
| ~~563~~ | ~~AiRequestLogService~~ | ~~LogRequestAsync()~~ | ~~POST /agents/chess~~ | **✅ MIGRATED** (commit d2150c2) |
| ~~630~~ | ~~IBggApiService~~ | ~~SearchGamesAsync()~~ | ~~GET /bgg/search~~ | **✅ MIGRATED** (commit 3235022) |
| ~~653~~ | ~~IBggApiService~~ | ~~GetGameDetailsAsync()~~ | ~~GET /bgg/game/{id}~~ | **✅ MIGRATED** (commit 3235022) |

*IRagService confirmed as legitimate orchestration service - no migration needed

**AiRequestLogService Migration Complete** (2025-11-17 commit d2150c2):
- ✅ 4 endpoints migrated to use LogAiRequestCommand via IMediator
- ✅ LogAiRequestCommand already existed in Administration context (fire-and-forget telemetry)
- ✅ Pattern: Direct CQRS usage instead of legacy service wrapper
- ✅ No new command/query files needed - reused existing infrastructure

**BggApiService Migration Complete** (2025-11-17 commit 3235022):
- ✅ 2 endpoints migrated to CQRS pattern via IMediator
- ✅ Created `SearchBggGamesQuery` + Handler (GameManagement/BggApi context)
- ✅ Created `GetBggGameDetailsQuery` + Handler (GameManagement/BggApi context)
- ✅ Endpoints migrated: GET /bgg/search, GET /bgg/game/{id}
- ✅ BggApiService kept as infrastructure adapter (HTTP calls to BoardGameGeek XML API, XML parsing, caching, rate limiting)
- ✅ Pattern: Business logic in handlers (empty query validation), infrastructure in service

---

### AdminEndpoints.cs (18 instances - CRITICAL)

#### Quality Reports (MIGRATED ✅)
| Line | Service | Method | Endpoint | Status |
|------|---------|--------|----------|--------|
| ~~206~~ | ~~IQualityReportService~~ | ~~GenerateReportAsync()~~ | ~~GET /admin/quality/report~~ | **✅ MIGRATED** (commit e331661) |

#### N8n Workflow Integration (PARTIALLY MIGRATED)
| Line | Service | Method | Endpoint | Status |
|------|---------|--------|----------|--------|
| ~~324~~ | ~~N8nConfigService~~ | ~~TestConnectionAsync()~~ | ~~POST /admin/n8n/{configId}/test~~ | **✅ MIGRATED** (commit e331661) |
| ~~338-425~~ | ~~N8nTemplateService~~ | ~~4 methods~~ | ~~4 N8n template endpoints~~ | **✅ MIGRATED** (commit f286da0) |

**N8n Migration Complete** (2025-11-17):
- ✅ N8n Template Management: 4 endpoints (commit f286da0)
- ✅ N8n Config Test: 1 endpoint (commit e331661)
- ✅ Total: 5 endpoints migrated in WorkflowIntegration context

#### Workflow Error Logging (MIGRATED ✅)
| Line | Service | Method | Endpoint | Status |
|------|---------|--------|----------|--------|
| ~~547~~ | ~~IWorkflowErrorLoggingService~~ | ~~LogErrorAsync()~~ | ~~POST /logs/workflow-error~~ | **✅ MIGRATED** (commit e331661) |
| ~~562~~ | ~~IWorkflowErrorLoggingService~~ | ~~GetErrorsAsync()~~ | ~~GET /admin/workflows/errors~~ | **✅ MIGRATED** (commit e331661) |
| ~~587~~ | ~~IWorkflowErrorLoggingService~~ | ~~GetErrorByIdAsync()~~ | ~~GET /admin/workflows/errors/{id}~~ | **✅ MIGRATED** (commit e331661) |

#### Prompt Management (MIGRATED ✅)
| Line | Service | Method | Endpoint | Status |
|------|---------|--------|----------|--------|
| ~~837~~ | ~~IPromptTemplateService~~ | ~~ActivateVersionAsync()~~ | ~~POST /admin/prompts/{id}/versions/{versionId}/activate~~ | **✅ MIGRATED** (commit 6311ed9) |

**Migration Complete** (2025-11-17):
- ✅ Endpoint now uses `ActivatePromptVersionCommand` via IMediator
- ✅ Handler includes complete audit trail (activation + deactivation logs)
- ✅ Transaction management with automatic rollback
- ✅ Guid types (was string, now type-safe)
- ✅ Removed direct service injection from routing layer
- 🔜 Domain events (future enhancement)

#### Prompt Evaluation (MIGRATED ✅)
| Line | Service | Method | Endpoint | Status |
|------|---------|--------|----------|--------|
| ~~927~~ | ~~IPromptEvaluationService~~ | ~~EvaluateAsync()~~ | ~~POST /admin/prompts/{id}/versions/{versionId}/evaluate~~ | **✅ MIGRATED** (commit 697fac2) |
| ~~966~~ | ~~IPromptEvaluationService~~ | ~~CompareVersionsAsync()~~ | ~~POST /admin/prompts/{id}/compare~~ | **✅ MIGRATED** (commit 697fac2) |
| ~~1000~~ | ~~IPromptEvaluationService~~ | ~~GetHistoricalResultsAsync()~~ | ~~GET /admin/prompts/{id}/evaluations~~ | **✅ MIGRATED** (commit 697fac2) |
| ~~1020~~ | ~~IPromptEvaluationService~~ | ~~GenerateReport()~~ | ~~GET /admin/prompts/evaluations/{id}/report~~ | **✅ MIGRATED** (commit 697fac2) |

**Migration Complete** (2025-11-17):
- ✅ 4 endpoints migrated to CQRS pattern via IMediator
- ✅ 2 Commands: EvaluatePromptCommand, ComparePromptVersionsCommand
- ✅ 2 Queries: GetEvaluationHistoryQuery, GenerateEvaluationReportQuery
- ✅ PromptEvaluationService kept as infrastructure adapter (872 lines)
- ✅ Complex evaluation logic: dataset loading/validation, metric calculations (5 metrics), RAG orchestration
- ✅ Security: Path traversal protection, file size limits, input validation
- ✅ Pattern: Handlers delegate to service for infrastructure concerns

---

## Migration Recommendations

### ~~Priority 1: CRITICAL - Prompt Template Activation~~ ✅ COMPLETED (2025-11-17)
**Status**: Migrated in commit 6311ed9

**What was done**:
- ✅ Updated existing `ActivatePromptVersionCommand` to use Guid types (type safety)
- ✅ Updated handler with complete audit trail (activation + deactivation logs)
- ✅ Migrated endpoint in `AdminEndpoints.cs` to use IMediator
- ✅ Removed direct `IPromptTemplateService` injection
- ✅ Transaction management with automatic rollback already present
- ✅ Handler existed but wasn't being used (now active)

**Benefits Achieved**:
- ✅ Complete audit trail for all prompt changes (compliance ready)
- ✅ Transaction-safe activation with rollback capability
- ✅ Type-safe Guid usage (was string before)
- ✅ CQRS pattern consistency with rest of codebase
- 🔜 Domain events (future enhancement, infrastructure ready)

---

### ~~Priority 1 (New): HIGH - RAG Orchestration~~ ✅ ANALYSIS COMPLETE (2025-11-17)
**Status**: **KEEP AS ORCHESTRATION** - No migration needed

**Analysis Results** (RagService.cs - 995 lines):

**Architecture**:
- **11+ Infrastructure Dependencies**: IEmbeddingService, IQdrantService, IHybridSearchService, ILlmService, IAiResponseCacheService, IPromptTemplateService, IQueryExpansionService, ISearchResultReranker, ICitationExtractorService, IConfigurationService, IConfiguration
- **4 Public Methods**: AskAsync(), ExplainAsync(), AskWithHybridSearchAsync(), AskWithCustomPromptAsync()
- **Complex Orchestration**: Each method coordinates 6-10 service calls in a specific sequence

**Responsibilities**:
1. **Orchestration** (Primary): Coordinates RAG pipeline (embed → search → fusion → LLM → cache)
2. **Infrastructure Delegation**: All actual work delegated to specialized services
3. **Cross-Cutting Concerns**: Caching, metrics, distributed tracing, error handling
4. **Configuration Management**: 3-tier fallback (DB → appsettings → defaults)

**Business Logic Analysis**:
- ❌ No significant business logic (only input validation)
- ❌ No business rule enforcement (just null/whitespace checks)
- ❌ No state management (stateless service)
- ❌ No transaction boundaries
- ❌ No domain event publishing
- ✅ Pure orchestration pattern

**Decision Rationale**:
- CLAUDE.md explicitly lists: `RagService (orchestration/infrastructure)` in "Retained" services
- Migrating to CQRS would create 4 command handlers that just replicate the same orchestration logic
- No separation of concerns gained (would still be orchestration, just in different files)
- Violates DRY principle (duplicate orchestration patterns across handlers)
- Makes maintenance harder (spread 995 lines across 8+ files with no benefit)

**Conclusion**: ✅ **KEEP RagService AS-IS** - Legitimate orchestration service per DDD architecture

---

### ~~Priority 2: MEDIUM - 2FA/TOTP Operations~~ ✅ COMPLETED (2025-11-17)
**Status**: Migrated in commit 3075a33

**What was done**:
- ✅ Created `GenerateTotpSetupCommand` + Handler in TwoFactor directory
- ✅ Created `Verify2FACommand` + Handler in TwoFactor directory
- ✅ Migrated 2 endpoints to CQRS pattern:
  - `POST /auth/2fa/setup`
  - `POST /auth/2fa/verify`
- ✅ Removed direct `ITotpService` injection from routing layer
- ✅ Removed direct `ITempSessionService` injection from routing layer
- ✅ `ITotpService` and `ITempSessionService` now pure infrastructure adapters (TOTP crypto, temp session management)

**Benefits Achieved**:
- ✅ Consistent CQRS pattern across authentication endpoints
- ✅ Clear separation: business logic in handlers, crypto in services
- ✅ Security maintained: Single-use temp sessions, rate limiting
- ✅ 2 legacy service injections removed from routing layer

---

### Priority 2 (New): MEDIUM - N8n Template Management
**Estimated Effort**: 3-5 hours

1. Create commands/queries in `WorkflowIntegration` bounded context:
   - `GetN8nTemplatesQuery`
   - `GetN8nTemplateByIdQuery`
   - `ImportN8nTemplateCommand`
   - `ValidateN8nTemplateQuery`
2. Migrate 4 endpoints
3. `N8nTemplateService` becomes infrastructure adapter for n8n API

---

### Priority 3 (New): LOW - Infrastructure Services
**Decision Required**: Keep or migrate?

**May be acceptable as infrastructure**:
- `IRateLimitService` (Redis-backed rate limiting)
- `IBggApiService` (external BoardGameGeek API adapter)
- `IWorkflowErrorLoggingService` (cross-cutting logging concern)

**Criteria for keeping**:
- Pure infrastructure (no domain logic)
- Cross-cutting concern
- Used in middleware/filters

---

## Services to Remove After Migration

These services can be **deleted** once CQRS migration is complete:

1. ❌ `PasswordResetService` - Already migrated, can be removed now
2. ❌ `OAuthService` - Partially used by handlers (keep for now as infrastructure adapter)
3. ❌ `TotpService` - After 2FA migration
4. ❌ `PromptTemplateService` - After prompt migration (CRITICAL)
5. ❌ `PromptEvaluationService` - After prompt evaluation migration
6. ❌ `N8nTemplateService` - After N8n migration

**Services to KEEP** (infrastructure/orchestration):
- ✅ `RagService` (orchestration per CLAUDE.md)
- ✅ `ConfigurationService` (3-tier config)
- ✅ `AlertingService` (external notifications)
- ✅ `AdminStatsService` (analytics aggregation)

---

## Code Removal Statistics

### Already Removed (from CLAUDE.md)
- 5,387 lines legacy code (Services: 3,710 lines | Error handling: 1,677 lines)
- 53 try-catch blocks from endpoints

### Can Be Removed After This Work
- `PasswordResetService.cs`: ~300 lines (READY NOW)
- `TotpService.cs`: ~180 lines (after 2FA migration)
- `PromptTemplateService.cs`: ~600 lines (after prompt migration)
- `PromptEvaluationService.cs`: ~900 lines (after evaluation migration)
- `N8nTemplateService.cs`: ~500 lines (after N8n migration)

**Total Potential Removal**: ~2,480 additional lines

---

## Next Steps

1. ~~⚠️ **CRITICAL**: Migrate Prompt Template activation endpoint~~ ✅ DONE (commit 6311ed9)
2. ~~Review RAG service role (orchestration vs application)~~ ✅ DONE (confirmed orchestration, commit c6ce130)
3. ~~Plan 2FA/TOTP migration sprint~~ ✅ DONE (commit 3075a33)
4. ~~N8n Template Management migration (4 endpoints)~~ ✅ DONE (commit f286da0)
5. **NEXT**: Prompt Evaluation endpoints migration (4 endpoints, MEDIUM priority)
6. **OR**: Quality Reports migration (1 endpoint, MEDIUM priority)
7. **OR**: N8n Config Test migration (1 endpoint, MEDIUM priority)
8. **OR**: Workflow Error Logging migration (3 endpoints, LOW priority)
9. Update CLAUDE.md with new DDD completion percentage

---

## References

- **Original Analysis**: Task report from 2025-11-17
- **Architecture**: `docs/01-architecture/adr/adr-002-cqrs-pattern.md`
- **CLAUDE.md**: Project overview (DDD Migration Status section)
- **Issue**: BGAI-034 (API test coupling removal)
