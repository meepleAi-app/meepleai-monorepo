# ADMIN-01: Prompt Management System - Implementation Checklist

**Epic**: Admin-Configurable Prompt Management
**Related Docs**:
- `docs/technic/admin-prompt-management-architecture.md`
- `docs/technic/admin-prompt-testing-framework.md`

---

## Implementation Overview

**Total Estimated Effort**: 5-6 weeks (200-240 developer hours)
**Team Size**: 1-2 developers
**Risk Level**: Medium (Redis caching complexity, prompt quality regression risk)

---

## Phase 1: Core Infrastructure (Week 1-2) - 80 hours

### Backend Service Layer

- [ ] **Task 1.1**: Create `IPromptTemplateService` interface (2 hours)
  - Location: `apps/api/src/Api/Services/IPromptTemplateService.cs`
  - Methods: 12 methods (CRUD, caching, retrieval)
  - Complexity: **Low**
  - Dependencies: None
  - Acceptance Criteria:
    - Interface compiles without errors
    - XML documentation for all methods
    - Follows existing MeepleAI service patterns

- [ ] **Task 1.2**: Implement `PromptTemplateService` with Redis caching (16 hours)
  - Location: `apps/api/src/Api/Services/PromptTemplateService.cs`
  - Key Methods:
    - `GetActivePromptAsync(templateName)` - Redis cache-first strategy
    - `ActivateVersionAsync(templateId, versionId)` - Transaction + cache invalidation
    - `CreateTemplateAsync`, `CreateVersionAsync`, `UpdateTemplateAsync`, `DeleteTemplateAsync`
    - `InvalidateCacheAsync`, `WarmCacheAsync`
  - Complexity: **High**
  - Dependencies: MeepleAiDbContext, IConnectionMultiplexer (Redis)
  - Acceptance Criteria:
    - Cache hit/miss logic works correctly
    - Fallback to database on Redis failure
    - Cache invalidation on activation
    - Transaction safety for activation
    - Comprehensive error handling
    - Logging for all operations
    - OPS-02: Add OpenTelemetry metrics

- [ ] **Task 1.3**: Register service in DI container (1 hour)
  - Location: `apps/api/src/Api/Program.cs`
  - Add: `builder.Services.AddScoped<IPromptTemplateService, PromptTemplateService>();`
  - Complexity: **Low**
  - Acceptance Criteria:
    - Service resolves correctly in integration tests
    - No DI circular dependencies

- [ ] **Task 1.4**: Unit tests for `PromptTemplateService` (20 hours)
  - Location: `apps/api/tests/Api.Tests/Services/PromptTemplateServiceTests.cs`
  - Test Count: 25+ tests
  - Coverage Target: 90%+
  - Test Scenarios:
    - Cache hit returns cached prompt (< 10ms)
    - Cache miss queries database + populates cache
    - Cache invalidation on activation
    - Redis unavailable fallback
    - Concurrent activation handling (race conditions)
    - Transaction rollback on error
    - Version deactivation logic
    - Audit log creation
    - TTL expiration behavior
  - Complexity: **High**
  - Tools: xUnit, Moq, SQLite in-memory
  - Acceptance Criteria:
    - 90%+ code coverage
    - All edge cases covered
    - Tests run in < 30 seconds

- [ ] **Task 1.5**: Integration tests with Testcontainers (16 hours)
  - Location: `apps/api/tests/Api.Tests/Integration/PromptTemplateServiceIntegrationTests.cs`
  - Test Count: 10+ tests
  - Test Scenarios:
    - End-to-end: create template → create version → activate → retrieve from cache
    - Multiple instances: verify cache consistency across two API instances
    - Redis failure recovery
    - Database transaction atomicity
    - Cache warming on startup
  - Complexity: **High**
  - Tools: xUnit, Testcontainers (Postgres + Redis)
  - Acceptance Criteria:
    - Tests use real Postgres + Redis containers
    - Tests pass in CI environment
    - Tests clean up resources properly

### Admin API Endpoints

- [ ] **Task 1.6**: Implement 10 admin API endpoints (16 hours)
  - Location: `apps/api/src/Api/Program.cs` (v1Api group)
  - Endpoints:
    1. `GET /api/v1/admin/prompts` - List templates
    2. `GET /api/v1/admin/prompts/{id}` - Get template
    3. `POST /api/v1/admin/prompts` - Create template
    4. `PUT /api/v1/admin/prompts/{id}` - Update template
    5. `DELETE /api/v1/admin/prompts/{id}` - Delete template
    6. `GET /api/v1/admin/prompts/{id}/versions` - Version history
    7. `POST /api/v1/admin/prompts/{id}/versions` - Create version
    8. `POST /api/v1/admin/prompts/{id}/versions/{versionId}/activate` - Activate version
    9. `GET /api/v1/admin/prompts/{id}/audit` - Audit log
    10. `POST /api/v1/admin/prompts/{id}/versions/{versionId}/evaluate` - Run tests (Phase 4)
  - Complexity: **Medium**
  - Authorization: Admin role required
  - Acceptance Criteria:
    - All endpoints documented in Swagger
    - Authorization enforced via `RequireRole("admin")`
    - Proper HTTP status codes (200, 201, 400, 401, 404)
    - Request/response validation
    - Error handling with structured errors

- [ ] **Task 1.7**: Integration tests for admin endpoints (12 hours)
  - Location: `apps/api/tests/Api.Tests/Integration/PromptManagementEndpointsTests.cs`
  - Test Count: 15+ tests
  - Test Scenarios:
    - CRUD operations (create, read, update, delete)
    - Authorization: non-admin users get 403
    - Validation: invalid requests get 400
    - Activation workflow
    - Pagination for list endpoints
    - Audit log tracking
  - Complexity: **Medium**
  - Acceptance Criteria:
    - Uses WebApplicationFactory
    - Tests run with Testcontainers
    - Tests verify database state

### Configuration & Documentation

- [ ] **Task 1.8**: Add configuration settings (2 hours)
  - Location: `apps/api/src/Api/appsettings.json`
  - Settings:
    ```json
    {
      "Features": {
        "PromptDatabase": false
      },
      "PromptManagement": {
        "CacheTtlSeconds": 3600,
        "MaxPromptSizeBytes": 16384,
        "EnableAutomaticCacheWarming": true,
        "CriticalPrompts": ["qa-system-prompt", "chess-system-prompt", "setup-guide-system-prompt"]
      }
    }
    ```
  - Complexity: **Low**

- [ ] **Task 1.9**: Update Swagger documentation (2 hours)
  - Location: `apps/api/src/Api/Program.cs` (Swagger configuration)
  - Add: Admin Prompts tag, examples, descriptions
  - Complexity: **Low**

- [ ] **Task 1.10**: Document API usage in README (2 hours)
  - Location: `docs/guide/admin-prompt-management-guide.md`
  - Content: API examples, workflow diagrams, troubleshooting
  - Complexity: **Low**

---

## Phase 2: Admin UI (Week 2-3) - 80 hours

### Frontend Pages

- [ ] **Task 2.1**: Create prompt list page (8 hours)
  - Location: `apps/web/src/pages/admin/prompts/index.tsx`
  - Features:
    - Table view with sorting
    - Category filter dropdown
    - Search by name
    - Pagination (50 per page)
    - "Create Template" button
  - Complexity: **Medium**
  - Acceptance Criteria:
    - Loads templates via API
    - Responsive design
    - Loading states
    - Error handling
    - Authentication gate (redirect if not admin)

- [ ] **Task 2.2**: Create template detail page (12 hours)
  - Location: `apps/web/src/pages/admin/prompts/[id].tsx`
  - Features:
    - Template metadata display
    - Version history table (expandable)
    - Active version indicator
    - "Create New Version" button
    - "Edit Metadata" button
    - "View Audit Log" button
  - Complexity: **Medium**
  - Acceptance Criteria:
    - Fetches template + versions via API
    - Displays version history
    - Navigates to version detail on click

- [ ] **Task 2.3**: Create version creation page with Monaco editor (16 hours)
  - Location: `apps/web/src/pages/admin/prompts/[id]/versions/new.tsx`
  - Features:
    - Monaco editor for prompt content (markdown syntax)
    - Metadata JSON textarea
    - "Activate immediately" checkbox
    - "Preview" button (renders markdown)
    - "Save" button
  - Complexity: **High**
  - Dependencies: `@monaco-editor/react`
  - Acceptance Criteria:
    - Monaco editor loads correctly
    - Syntax highlighting works
    - Form validation (content required)
    - POSTs to API on save
    - Redirects to template detail on success

- [ ] **Task 2.4**: Create version detail page (readonly Monaco) (8 hours)
  - Location: `apps/web/src/pages/admin/prompts/[id]/versions/[versionId].tsx`
  - Features:
    - Readonly Monaco editor with version content
    - Metadata display
    - "Activate" button (if not active)
    - "Compare with another version" button
    - "Run Tests" button (Phase 4)
  - Complexity: **Medium**
  - Acceptance Criteria:
    - Monaco in readonly mode
    - Activation triggers API call + shows success message
    - Confirmation dialog before activation

- [ ] **Task 2.5**: Create version comparison page (16 hours)
  - Location: `apps/web/src/pages/admin/prompts/[id]/compare.tsx`
  - Features:
    - Two version selectors (dropdowns)
    - Side-by-side Monaco editors (readonly)
    - Diff highlighting (additions/deletions)
    - Metadata comparison
    - "Load Comparison" button
  - Complexity: **High**
  - Dependencies: `monaco-editor` diff mode
  - Acceptance Criteria:
    - Diff highlighting works correctly
    - Handles large prompts (16KB)
    - Responsive layout (stacks on mobile)

- [ ] **Task 2.6**: Create template creation page (8 hours)
  - Location: `apps/web/src/pages/admin/prompts/new.tsx`
  - Features:
    - Form: name, description, category, initial content
    - Monaco editor for initial content
    - Form validation
    - "Create" button
  - Complexity: **Medium**
  - Acceptance Criteria:
    - Validation: name required, unique check via API
    - POSTs to API on submit
    - Redirects to new template detail page

- [ ] **Task 2.7**: Create audit log viewer (8 hours)
  - Location: `apps/web/src/pages/admin/prompts/[id]/audit.tsx`
  - Features:
    - Table: timestamp, user, action, details
    - Pagination
    - Date filter
    - Action filter dropdown
  - Complexity: **Medium**
  - Acceptance Criteria:
    - Fetches audit logs via API
    - Sortable by timestamp
    - Displays user email

### UI Components

- [ ] **Task 2.8**: Create reusable PromptEditor component (4 hours)
  - Location: `apps/web/src/components/PromptEditor.tsx`
  - Props: `value`, `onChange`, `readonly`, `height`
  - Complexity: **Low**
  - Acceptance Criteria:
    - Wraps Monaco editor with consistent styling
    - Reusable across pages

- [ ] **Task 2.9**: Create PromptVersionCard component (4 hours)
  - Location: `apps/web/src/components/PromptVersionCard.tsx`
  - Props: `version`, `onActivate`, `onCompare`
  - Complexity: **Low**
  - Acceptance Criteria:
    - Displays version metadata
    - Active badge indicator
    - Action buttons

### Testing

- [ ] **Task 2.10**: Jest unit tests for UI components (12 hours)
  - Location: `apps/web/src/pages/admin/prompts/__tests__/`
  - Test Count: 20+ tests
  - Coverage Target: 90%+
  - Test Scenarios:
    - List page: renders templates, handles loading, handles errors
    - Detail page: renders version history, handles activation
    - Editor page: validates form, handles Monaco events
    - Comparison page: displays diff correctly
  - Complexity: **Medium**
  - Tools: Jest, @testing-library/react
  - Acceptance Criteria:
    - 90%+ component coverage
    - Mocks API calls with MSW
    - Tests pass in CI

- [ ] **Task 2.11**: Playwright E2E tests (12 hours)
  - Location: `apps/web/e2e/admin-prompts.spec.ts`
  - Test Count: 5+ scenarios
  - Test Scenarios:
    1. Create template → create version → activate
    2. Compare two versions
    3. View audit log
    4. Edit template metadata
    5. Delete template
  - Complexity: **High**
  - Tools: Playwright
  - Acceptance Criteria:
    - Tests run against local dev server
    - Tests use admin test user
    - Tests clean up data after run

---

## Phase 3: Service Migration (Week 3-4) - 40 hours

### Service Refactoring

- [ ] **Task 3.1**: Refactor `RagService` to use prompt database (8 hours)
  - Location: `apps/api/src/Api/Services/RagService.cs`
  - Changes:
    - Inject `IPromptTemplateService`
    - Add feature flag check
    - Replace hardcoded prompt with `await _promptService.GetActivePromptAsync("qa-system-prompt")`
    - Keep fallback prompt (same as current)
  - Complexity: **Medium**
  - Acceptance Criteria:
    - Feature flag toggles behavior
    - Fallback works if DB prompt not found
    - No behavior change when flag = false
    - Integration tests pass

- [ ] **Task 3.2**: Refactor `StreamingQaService` (8 hours)
  - Location: `apps/api/src/Api/Services/StreamingQaService.cs`
  - Same pattern as Task 3.1
  - Template name: `streaming-qa-system-prompt`
  - Complexity: **Medium**

- [ ] **Task 3.3**: Refactor `ChessAgentService` (8 hours)
  - Location: `apps/api/src/Api/Services/ChessAgentService.cs`
  - Template name: `chess-system-prompt`
  - Note: Dynamic prompt building (FEN position logic)
  - Complexity: **Medium**
  - Acceptance Criteria:
    - FEN position logic still works
    - Dynamic sections inserted correctly

- [ ] **Task 3.4**: Refactor `SetupGuideService` (8 hours)
  - Location: `apps/api/src/Api/Services/SetupGuideService.cs`
  - Template name: `setup-guide-system-prompt`
  - Complexity: **Medium**

- [ ] **Task 3.5**: Create database seed script (4 hours)
  - Location: `apps/api/src/Api/Migrations/Seeds/SeedPromptTemplates.sql`
  - Seed 4 templates with initial versions (v1)
  - Mark all as active
  - Use existing hardcoded prompts as content
  - Complexity: **Low**
  - Acceptance Criteria:
    - Script is idempotent (can run multiple times)
    - Prompts match exact current hardcoded versions

- [ ] **Task 3.6**: Update service unit tests (4 hours)
  - Location: `apps/api/tests/Api.Tests/Services/*ServiceTests.cs`
  - Mock `IPromptTemplateService` in all service tests
  - Return test prompts via mock
  - Complexity: **Low**
  - Acceptance Criteria:
    - No tests break due to refactoring
    - Feature flag tested (on/off)

---

## Phase 4: Prompt Testing Framework (Week 4-5) - 80 hours

### Test Dataset Creation

- [ ] **Task 4.1**: Create test dataset JSON schema (4 hours)
  - Location: `schemas/prompt-evaluation-dataset.schema.json`
  - JSON Schema validation
  - Complexity: **Low**

- [ ] **Task 4.2**: Create test dataset for Q&A prompt (12 hours)
  - Location: `tests/Api.Tests/TestData/prompt-evaluation/qa-system-prompt-test-dataset.json`
  - 50+ queries covering:
    - Setup questions (easy)
    - Gameplay questions (medium)
    - Edge cases (hard)
    - Out-of-context questions (hallucination tests)
  - Ground truth answers
  - Expected citations
  - Complexity: **High**
  - Acceptance Criteria:
    - Covers Tic-Tac-Toe and Chess
    - Balanced difficulty distribution
    - Clear ground truth

- [ ] **Task 4.3**: Create test datasets for other 3 prompts (12 hours)
  - Chess, Setup Guide, Streaming Q&A
  - 30+ queries each
  - Complexity: **Medium**

### Evaluation Service

- [ ] **Task 4.4**: Implement `IPromptEvaluationService` interface (2 hours)
  - Location: `apps/api/src/Api/Services/IPromptEvaluationService.cs`
  - Methods: `LoadDatasetAsync`, `EvaluateAsync`, `CompareVersionsAsync`, `GenerateReport`, `StoreResultsAsync`
  - Complexity: **Low**

- [ ] **Task 4.5**: Implement `PromptEvaluationService` (24 hours)
  - Location: `apps/api/src/Api/Services/PromptEvaluationService.cs`
  - Key Logic:
    - Load JSON dataset with schema validation
    - Execute each test case via `IRagService.AskWithCustomPromptAsync`
    - Calculate 5 metrics (accuracy, hallucination, confidence, citations, latency)
    - Generate pass/fail based on thresholds
    - Generate Markdown and JSON reports
  - Complexity: **High**
  - Dependencies: IRagService, ILlmService
  - Acceptance Criteria:
    - All 5 metrics calculated correctly
    - Supports concurrent evaluation (background job)
    - Retry logic for LLM failures
    - Progress tracking

- [ ] **Task 4.6**: Add `AskWithCustomPromptAsync` to `IRagService` (4 hours)
  - Location: `apps/api/src/Api/Services/RagService.cs`
  - New method for evaluation service to inject custom prompt
  - Complexity: **Low**

- [ ] **Task 4.7**: Unit tests for evaluation service (12 hours)
  - Location: `apps/api/tests/Api.Tests/Services/PromptEvaluationServiceTests.cs`
  - Test Count: 15+ tests
  - Coverage Target: 90%+
  - Complexity: **Medium**

- [ ] **Task 4.8**: Integration tests for evaluation (8 hours)
  - Location: `apps/api/tests/Api.Tests/Integration/PromptEvaluationIntegrationTests.cs`
  - Test Count: 5+ tests
  - Test end-to-end: load dataset → evaluate → generate report
  - Complexity: **High**
  - Tools: Testcontainers (Postgres + Qdrant)

### Admin UI for Testing

- [ ] **Task 4.9**: Create evaluation results UI (12 hours)
  - Location: `apps/web/src/pages/admin/prompts/[id]/versions/[versionId]/evaluate.tsx`
  - Features:
    - "Run Tests" button
    - Progress indicator (query X/N)
    - Results display (metrics, pass/fail, query breakdown)
    - Download report buttons (JSON, Markdown)
  - Complexity: **Medium**
  - Acceptance Criteria:
    - Real-time progress updates via polling
    - Color-coded pass/fail indicators
    - Expandable query details

- [ ] **Task 4.10**: Create A/B comparison evaluation UI (8 hours)
  - Location: `apps/web/src/pages/admin/prompts/[id]/compare-evaluation.tsx`
  - Features:
    - Side-by-side metrics comparison
    - Delta indicators (↑ ↓)
    - Recommendation summary
    - Query-by-query comparison
  - Complexity: **Medium**

---

## Phase 5: Deployment & Monitoring (Week 5-6) - 40 hours

### Observability

- [ ] **Task 5.1**: Add OpenTelemetry metrics (8 hours)
  - Location: `apps/api/src/Api/Observability/PromptManagementMetrics.cs`
  - Metrics:
    - `meepleai.prompt.cache.hits` (Counter)
    - `meepleai.prompt.cache.misses` (Counter)
    - `meepleai.prompt.retrieval.duration` (Histogram)
    - `meepleai.prompt.activation.total` (Counter)
    - `meepleai.prompt.activation.duration` (Histogram)
    - `meepleai.prompt.evaluation.duration` (Histogram)
  - Complexity: **Medium**
  - Acceptance Criteria:
    - Metrics exported to Prometheus
    - Metrics visible in Grafana

- [ ] **Task 5.2**: Create Grafana dashboard (4 hours)
  - Location: `infra/dashboards/prompt-management.json`
  - Panels:
    - Cache hit rate (gauge)
    - Cache miss rate trend (graph)
    - Prompt retrieval latency p50/p95/p99 (graph)
    - Activation frequency (counter)
    - Evaluation duration distribution (histogram)
  - Complexity: **Medium**

- [ ] **Task 5.3**: Configure alerts (4 hours)
  - Location: `infra/prometheus/alerts/prompt-management.yml`
  - Alerts:
    - Cache hit rate < 90% for 10 minutes
    - Prompt retrieval latency p95 > 20ms
    - Cache unavailable (all misses)
  - Complexity: **Low**

### Deployment

- [ ] **Task 5.4**: Create runbook (4 hours)
  - Location: `docs/runbook/prompt-management.md`
  - Content:
    - How to activate a prompt
    - How to rollback
    - How to investigate cache issues
    - Common errors and solutions
  - Complexity: **Low**

- [ ] **Task 5.5**: Conduct admin training (4 hours)
  - Create training video or live session
  - Cover: prompt creation, activation, testing, rollback
  - Complexity: **Low**

- [ ] **Task 5.6**: Gradual rollout (8 hours)
  - Week 1: Enable `Features:PromptDatabase` for RagService only
  - Week 2: Enable for ChessAgentService
  - Week 3: Enable for SetupGuideService and StreamingQaService
  - Monitor metrics and logs between each rollout
  - Complexity: **Medium**
  - Acceptance Criteria:
    - No increase in error rates
    - Cache hit rate > 95%
    - Latency remains < 10ms

- [ ] **Task 5.7**: Create CI/CD pipeline for prompt evaluation (8 hours)
  - Location: `.github/workflows/prompt-evaluation.yml`
  - Triggers: PR with changes to prompt seed data
  - Actions: Run evaluation tests, upload reports, comment on PR
  - Complexity: **Medium**
  - Acceptance Criteria:
    - Tests run in GitHub Actions
    - Reports uploaded as artifacts
    - PR comment with summary

---

## Definition of Done (DoD) Checklist

For each task to be considered complete:

- [ ] Code implemented and follows MeepleAI coding standards
- [ ] Unit tests written and passing (90%+ coverage)
- [ ] Integration tests written and passing (if applicable)
- [ ] Code reviewed by at least one team member
- [ ] Documentation updated (inline comments, README, architecture docs)
- [ ] No compiler warnings or linter errors
- [ ] Manual testing completed (if UI task)
- [ ] Merged to main branch

For the epic to be considered complete:

- [ ] All 5 phases completed
- [ ] All services migrated to database prompts
- [ ] Feature flags enabled in production
- [ ] Admin UI accessible and functional
- [ ] Monitoring dashboards deployed
- [ ] Training completed
- [ ] Runbook reviewed
- [ ] No critical bugs reported after 1 week in production

---

## Risk Mitigation Checklist

- [ ] Cache consistency tests pass (concurrent activation)
- [ ] Redis failure fallback tested
- [ ] Prompt quality regression tests pass
- [ ] Rollback procedure documented and tested
- [ ] Authorization enforcement verified (security audit)
- [ ] Load testing completed (100K req/day simulation)
- [ ] Backup strategy for prompt_templates table
- [ ] Incident response procedure documented

---

## Dependencies & Prerequisites

- [ ] PostgreSQL database with existing prompt tables
- [ ] Redis infrastructure available and configured
- [ ] OpenRouter API key for prompt evaluation
- [ ] Qdrant vector database running (for RAG testing)
- [ ] Admin test user account created
- [ ] Monaco Editor license verified (MIT, OK to use)

---

## Success Metrics

After 4 weeks in production:

- [ ] Cache hit rate > 95%
- [ ] Prompt retrieval latency < 10ms (p95)
- [ ] Zero prompt-related incidents
- [ ] 10+ prompt updates performed successfully
- [ ] 100% of prompts have passing test results
- [ ] Admin user satisfaction score > 8/10

---

## Estimated Timeline

| Phase | Duration | Start | End |
|-------|----------|-------|-----|
| Phase 1: Core Infrastructure | 2 weeks | Week 1 | Week 2 |
| Phase 2: Admin UI | 2 weeks | Week 2 | Week 3 |
| Phase 3: Service Migration | 1 week | Week 3 | Week 4 |
| Phase 4: Testing Framework | 2 weeks | Week 4 | Week 5 |
| Phase 5: Deployment | 1 week | Week 5 | Week 6 |
| **Total** | **6 weeks** | | |

**Buffer**: 1 week for unexpected issues, code reviews, revisions

---

## Next Actions

1. [ ] Review and approve implementation plan
2. [ ] Assign tasks to developers
3. [ ] Set up project tracking (GitHub Projects)
4. [ ] Schedule kickoff meeting
5. [ ] Begin Phase 1 implementation

