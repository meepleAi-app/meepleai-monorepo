# DoD Verification Report - 2025-10-26 01:52:25

Analysis Timestamp: `2025-10-26-015028`

---

## 📊 Executive Summary

- **Total Issues Analyzed**: 59
- **Total Unchecked DoD Items**: 1139
- **Average DoD Items per Issue**: 19.3

### DoD Items by Category

| Category | Count | Percentage |
|----------|-------|------------|
| Other | 722 | 63.4% |
| Testing | 219 | 19.2% |
| Documentation | 104 | 9.1% |
| Cicd | 81 | 7.1% |
| Manual Verification | 10 | 0.9% |
| File Creation | 2 | 0.2% |
| Service Impl | 1 | 0.1% |

### Top 10 Issues by Unchecked DoD Items

| Issue | Title | Unchecked Items |
|-------|-------|-----------------|
| #476 | CONFIG-01: Backend Foundation - Database schema, service, and API endpoints for dynamic configuration | 55 |
| #399 | PDF-09: Add pre-upload PDF validation | 45 |
| #370 | API-01 - API Foundation and Authentication Infrastructure | 44 |
| #394 | TEST-03: Increase frontend test coverage to 90% | 43 |
| #413 | EDIT-05: Enhance comments and annotations system with inline annotations and threading | 40 |
| #412 | EDIT-04: Improve visual diff viewer for version comparison | 37 |
| #400 | CHAT-04: Polish loading states and animations | 36 |
| #398 | PDF-08: Add granular progress tracking for PDF processing | 36 |
| #393 | AUTH-05: Fix inconsistent session timeout behavior | 36 |
| #475 | CONFIG-04: Integration - Dynamic RAG configuration | 35 |

## 💡 Recommendations

### Immediate Actions

1. **Manual Verification Items (10 items)**
   - Create consolidated issue: `MANUAL-VERIFICATION: DoD Items Requiring Human Validation`
   - Assign to QA team for systematic verification
   - Priority: Medium (non-blocking but improves quality assurance)

2. **Testing Items (219 items)**
   - Many items relate to test coverage and test execution
   - Consider: Are these already implemented but checkboxes not updated?
   - Action: Verify actual test coverage vs DoD claims

3. **File Creation Items (2 items)**
   - Automate verification: check if mentioned files exist
   - Update checkboxes for existing files
   - Reopen issues for truly missing files

### Strategic Approach

1. **Phase 1**: Automated file/service existence verification
2. **Phase 2**: Update GitHub issue bodies with verified checkboxes
3. **Phase 3**: Create manual verification issue for remaining items
4. **Phase 4**: Reopen issues with genuinely missing implementations

---

## 📝 Detailed Analysis by Issue

### Issue #511: AI-11.2: Grafana Dashboard & Prometheus Alerts for Quality Metrics

**Status**: CLOSED | **Labels**: enhancement, ai, ops, observability
**Unchecked DoD Items**: 21

<details>
<summary>View 21 unchecked items</summary>

**Cicd** (1 items):
- [ ] Panels for each dimension: RAG, LLM, Citation, Overall confidence

**Documentation** (1 items):
- [ ] Auto-provisioned in `infra/docker-compose.yml` (Grafana volumes)

**File Creation** (2 items):
- [ ] Create `infra/dashboards/ai-quality-monitoring.json`
- [ ] Create `infra/prometheus/alerts/quality-alerts.yml`

**Manual Verification** (4 items):
- [ ] Dashboard accessible at `http://localhost:3001/d/quality-metrics`
- [ ] Dashboard renders correctly with sample data
- [ ] Alerts trigger correctly with test scenarios (manual verification)
- [ ] Alert notifications visible in Prometheus UI

**Other** (13 items):
- [ ] Dashboard shows quality trends over time (1h, 6h, 24h, 7d, 30d)
- [ ] Panel showing low-quality response rate (%)
- [ ] Panel showing response volume by quality tier (high/medium/low)
- [ ] Panel showing average confidence by agent type
- [ ] Panel showing p50/p95/p99 confidence scores
- [ ] Alert: Average overall confidence < 0.60 for 1 hour
- [ ] Alert: Low-quality response rate > 30% for 1 hour
- [ ] Alert: RAG confidence < 0.50 for 30 minutes
- [ ] Alert: LLM confidence < 0.50 for 30 minutes
- [ ] Alert: Zero quality metrics for 15 minutes (service health check)
- [ ] Alerts configured in `infra/prometheus/prometheus.yml`
- [ ] Alert annotations with runbook links and Grafana panel links
- [ ] All panels show data after quality scoring runs

</details>

### Issue #510: AI-11.1: Fix QualityTracking Integration Tests (Testcontainers Setup)

**Status**: CLOSED | **Labels**: enhancement, backend, ai, testing
**Unchecked DoD Items**: 1

<details>
<summary>View 1 unchecked items</summary>

**Other** (1 items):
- [ ] No flaky tests (run 10 times successfully) ÔÜá´©Å (Requires Redis running for fast execution)

</details>

### Issue #490: test(api): Flaky test - PostIngestPdf_WhenEditorUploadsValidPdf crashes intermittently in CI

**Status**: CLOSED | **Labels**: bug, test
**Unchecked DoD Items**: 1

<details>
<summary>View 1 unchecked items</summary>

**Cicd** (1 items):
- [ ] Verify CI stability over 10+ runs ÔÜá´©Å (Pending PR CI runs)

</details>

### Issue #485: TEST-02-P5: CI Integration + Documentation + Final 90% Validation

**Status**: CLOSED | **Labels**: epic:testing, type:tech, sprint:1-2, effort:S, priority:medium
**Unchecked DoD Items**: 2

<details>
<summary>View 2 unchecked items</summary>

**Documentation** (1 items):
- [ ] Coverage badge added to README (optional - deferred)

**Other** (1 items):
- [ ] Coverage badge added (optional - deferred)

</details>

### Issue #484: TEST-02-P4: LlmService + RagService + Infrastructure tests

**Status**: CLOSED | **Labels**: epic:testing, type:tech, sprint:1-2, priority:medium, effort:L
**Unchecked DoD Items**: 12

<details>
<summary>View 12 unchecked items</summary>

**Cicd** (1 items):
- [ ] CI/CD pipeline green

**Documentation** (1 items):
- [ ] Documentation updated

**Other** (5 items):
- [ ] Context window and streaming scenarios tested
- [ ] Database configuration and migration tests added
- [ ] Entity constraint validation tested
- [ ] BDD naming conventions followed
- [ ] Code review approved

**Testing** (5 items):
- [ ] LlmService: ÔëÑ90% coverage (from ~60%)
- [ ] RagService: ÔëÑ95% coverage (from ~85%)
- [ ] Infrastructure layer: ÔëÑ90% coverage (from ~65%)
- [ ] All tests passing locally and in CI
- [ ] Coverage validated (all services ÔëÑ90%)

</details>

### Issue #483: TEST-02-P3: QdrantService + EmbeddingService comprehensive tests

**Status**: CLOSED | **Labels**: epic:testing, priority:high, type:tech, sprint:1-2, effort:L
**Unchecked DoD Items**: 1

<details>
<summary>View 1 unchecked items</summary>

**Testing** (1 items):
- [ ] All tests passing locally and in CI (Ô£à Local, ÔÅ│ CI pending)

</details>

### Issue #482: TEST-02-P2: ChatExportService + Formatters comprehensive tests

**Status**: CLOSED | **Labels**: epic:testing, priority:high, type:tech, sprint:1-2, effort:M
**Unchecked DoD Items**: 14

<details>
<summary>View 14 unchecked items</summary>

**Cicd** (1 items):
- [ ] CI/CD pipeline green

**Documentation** (2 items):
- [ ] Documentation updated
- [ ] `docs/issue/test-02-coverage-90-percent-progress.md` updated

**Other** (6 items):
- [ ] Case-insensitive format matching tested
- [ ] Cancellation token scenarios covered
- [ ] Filename generation edge cases tested
- [ ] BDD naming conventions followed
- [ ] Code implemented and functional
- [ ] Code review approved

**Testing** (5 items):
- [ ] ChatExportService: 100% unit test coverage
- [ ] All formatters have comprehensive unit tests
- [ ] All tests passing locally and in CI
- [ ] Unit tests written and passing (8-12 new tests)
- [ ] Coverage validated (ÔëÑ100% for ChatExportService)

</details>

### Issue #481: AI-08 Phase 2: Display page numbers in citation UI

**Status**: CLOSED | **Labels**: type:story, effort:S, priority:medium, epic:rag
**Unchecked DoD Items**: 21

<details>
<summary>View 21 unchecked items</summary>

**Cicd** (3 items):
- [ ] Update API client types to include `pageNumber?: number` in citation models
- [ ] Display "Page X" in citation component when page number is available
- [ ] CI/CD pipeline green

**Other** (14 items):
- [ ] Handle multi-page chunks: show page range "Pages 5-6" if applicable
- [ ] Handle missing page numbers gracefully: hide page indicator if null/undefined
- [ ] Visual design: small, unobtrusive badge/label with page number
- [ ] Test null/undefined page numbers (no UI errors)
- [ ] Test page ranges (if chunk spans multiple pages)
- [ ] TypeScript types updated (no `any` types)
- [ ] Accessibility: page number should be screen-reader friendly
- [ ] Responsive design: works on mobile/tablet/desktop
- [ ] Code implemented and functional
- [ ] All acceptance criteria satisfied
- [ ] TypeScript types correct (no `any`)
- [ ] Accessibility verified
- [ ] Code review approved
- [ ] No regressions identified

**Testing** (4 items):
- [ ] Jest unit tests for citation component (page number display, edge cases)
- [ ] E2E test: ask question ÔåÆ verify "Page X" appears in citation modal
- [ ] Unit tests written and passing (90% coverage)
- [ ] E2E test passing

</details>

### Issue #476: CONFIG-01: Backend Foundation - Database schema, service, and API endpoints for dynamic configuration

**Status**: CLOSED | **Labels**: enhancement, backend, database, config
**Unchecked DoD Items**: 55

<details>
<summary>View 55 unchecked items</summary>

**Cicd** (4 items):
- [ ] Index on `category` for efficient filtering
- [ ] Proper error handling with specific exceptions (e.g., `ConfigurationNotFoundException`)
- [ ] Role-based fallback logic: Check role-specific config first, then global (role=null), then return default/null
- [ ] CI/CD pipeline green (GitHub Actions)

**Documentation** (2 items):
- [ ] Documentation updated (inline comments, XML docs)
- [ ] API endpoints documented in Swagger/OpenAPI

**Other** (41 items):
- [ ] Create migration `AddSystemConfigurationsTable` with table `system_configurations`
- [ ] Table includes all required columns:
- [ ] Composite unique index on `(key, role)` to prevent duplicate configurations
- [ ] Foreign key constraint from `updated_by_user_id` to `users.id`
- [ ] Migration applies successfully via `dotnet ef database update`
- [ ] Implement `IConfigurationService` interface with methods:
- [ ] Service registered in `Program.cs` DI container with scoped lifetime
- [ ] All configuration changes logged to audit system (`AuditService`)
- [ ] Define `ConfigCategory` enum: RateLimiting, AiLlm, Rag, FeatureFlags
- [ ] Define `ConfigValueType` enum: String, Integer, Float, Boolean, JSON
- [ ] Define `SystemConfigurationDto` with properties matching database schema
- [ ] Define request DTOs:
- [ ] Define `ValidationResult` class with `IsValid` bool and `Errors` list
- [ ] Create `SystemConfigurationEntity` with all database columns as properties
- [ ] Configure entity in `MeepleAiDbContext.OnModelCreating()`:
- [ ] Add `DbSet<SystemConfigurationEntity> SystemConfigurations` to `MeepleAiDbContext`
- [ ] `GET /api/v1/admin/configurations` - List all configurations
- [ ] `GET /api/v1/admin/configurations/{key}` - Get single configuration
- [ ] `POST /api/v1/admin/configurations` - Create new configuration
- [ ] `PUT /api/v1/admin/configurations/{key}` - Update existing configuration
- [ ] `DELETE /api/v1/admin/configurations/{key}` - Delete configuration
- [ ] All endpoints use appropriate HTTP status codes (200, 201, 204, 400, 401, 403, 404)
- [ ] All endpoints properly integrated with existing auth middleware
- [ ] Correlation ID logging on all endpoints
- [ ] Use SQLite in-memory database for fast test execution
- [ ] Arrange-Act-Assert pattern
- [ ] Use Testcontainers for PostgreSQL (realistic DB behavior)
- [ ] BDD-style test naming (Given_When_Then pattern recommended)
- [ ] Code implemented and functional
- [ ] Code review approved
- [ ] Tested in local development environment
- [ ] No regressions identified
- [ ] All acceptance criteria satisfied
- [ ] Database migration tested (up and down)
- [ ] Service registered in DI container
- [ ] Authorization verified (admin-only access)
- [ ] Audit logging confirmed for all configuration changes
- [ ] Type safety validated for configuration value retrieval
- [ ] Role-based fallback logic verified with tests
- [ ] Performance tested (queries complete in < 100ms)
- [ ] Error responses include helpful error messages

**Testing** (8 items):
- [ ] Minimum 20 unit tests covering:
- [ ] All tests passing with clear, descriptive names
- [ ] Minimum 10 integration tests covering:
- [ ] Use WebApplicationFactory for full API integration testing
- [ ] All tests passing
- [ ] Unit tests written and passing (20+ tests)
- [ ] Integration tests written and passing (10+ tests)
- [ ] Test coverage ÔëÑ 90% for new code

</details>

### Issue #475: CONFIG-04: Integration - Dynamic RAG configuration

**Status**: CLOSED | **Labels**: enhancement, backend, config, rag, vector-search
**Unchecked DoD Items**: 35

<details>
<summary>View 35 unchecked items</summary>

**Cicd** (1 items):
- [ ] CI/CD pipeline green

**Documentation** (5 items):
- [ ] `docs/guide/rag-configuration.md` created with complete reference
- [ ] Re-indexing guide documented with step-by-step instructions
- [ ] Migration guide for changing chunking/vector parameters
- [ ] API documentation includes configuration endpoints
- [ ] Documentation updated (`docs/guide/rag-configuration.md`, `CLAUDE.md`)

**Manual Verification** (1 items):
- [ ] Re-indexing guide documented and tested manually

**Other** (22 items):
- [ ] RagService reads all 4 parameters from database configuration
- [ ] TextChunkingService uses configured ChunkSize and ChunkOverlap
- [ ] QdrantService respects VectorDimensions and SimilarityMetric
- [ ] All services fall back to appsettings.json if DB config missing
- [ ] Parameter validation prevents invalid values (throws exceptions)
- [ ] WARNING logs for ChunkSize/ChunkOverlap changes
- [ ] CRITICAL logs for VectorDimensions/SimilarityMetric changes
- [ ] Configuration descriptions include re-indexing warnings
- [ ] Migration seeds 8 default RAG configurations
- [ ] Configurations marked with appropriate categories
- [ ] Destructive parameters have ÔÜá´©Å warnings in descriptions
- [ ] `CLAUDE.md` updated with RAG configuration section
- [ ] Code implemented and functional
- [ ] Code review approved
- [ ] Tested in staging environment
- [ ] No regressions identified
- [ ] All 3 services (RagService, TextChunkingService, QdrantService) use database configuration
- [ ] Parameter validation implemented with appropriate exceptions
- [ ] Warning/error logging implemented for destructive changes
- [ ] Default configurations seeded in database migration
- [ ] Performance tested with different TopK values (no degradation)
- [ ] Backward compatibility verified (appsettings.json fallback works)

**Testing** (6 items):
- [ ] 15+ unit tests for configuration reading and validation
- [ ] 8+ integration tests for RAG behavior with different configs
- [ ] All tests passing (including existing tests)
- [ ] Test coverage maintained at 90%+
- [ ] Unit tests written and passing (15+ tests)
- [ ] Integration tests written and passing (8+ tests)

</details>

### Issue #474: CONFIG-03: Integration - Dynamic AI/LLM configuration

**Status**: CLOSED | **Labels**: enhancement, backend, high-priority, ai, config, llm
**Unchecked DoD Items**: 34

<details>
<summary>View 34 unchecked items</summary>

**Cicd** (1 items):
- [ ] CI pipeline green

**Documentation** (7 items):
- [ ] All configuration keys documented with descriptions and valid ranges
- [ ] Create `docs/guide/ai-configuration.md` documenting:
- [ ] Update existing AI documentation with configuration references
- [ ] Include migration guide from appsettings.json to DB config
- [ ] `docs/guide/ai-configuration.md` created with:
- [ ] Existing AI documentation updated
- [ ] Migration documented in CHANGELOG

**Other** (19 items):
- [ ] `LlmService` reads all AI parameters from DB configuration via `IConfigurationService`
- [ ] Configuration fallback chain works: DB (global) ÔåÆ appsettings.json ÔåÆ hardcoded defaults
- [ ] Parameter validation prevents invalid values (temperature range, token limits, etc.)
- [ ] Streaming responses can be enabled/disabled via `AiLlm.StreamingEnabled` configuration
- [ ] `StreamingQaService` respects streaming configuration flag
- [ ] Default AI/LLM configurations seeded in database migration
- [ ] `LlmService.cs` modified to use `IConfigurationService`
- [ ] Configuration fallback chain implemented (DB ÔåÆ appsettings ÔåÆ defaults)
- [ ] Parameter validation implemented for all AI parameters
- [ ] `StreamingQaService.cs` respects streaming configuration flag
- [ ] Database migration created with default AI configurations
- [ ] All configuration keys seeded with descriptions
- [ ] No regressions in AI/LLM functionality
- [ ] Code comments added for configuration logic
- [ ] Code review approved
- [ ] No new security warnings
- [ ] Performance impact verified (negligible)
- [ ] Logging added for configuration reads and fallbacks
- [ ] Error handling for invalid configurations

**Testing** (7 items):
- [ ] 10+ unit tests covering:
- [ ] 5+ integration tests covering:
- [ ] All existing tests continue passing
- [ ] Code coverage maintained at 90%+
- [ ] 10+ unit tests written and passing (90%+ coverage)
- [ ] 5+ integration tests written and passing
- [ ] All existing tests continue passing

</details>

### Issue #473: CONFIG-05: Feature Flags system for runtime feature toggling

**Status**: CLOSED | **Labels**: enhancement, backend, testing, config, feature-flags
**Unchecked DoD Items**: 25

<details>
<summary>View 25 unchecked items</summary>

**Cicd** (2 items):
- [ ] Support for role-based feature access (global + role-specific overrides)
- [ ] CI/CD pipeline green

**Documentation** (3 items):
- [ ] Documentation complete in `docs/guide/feature-flags.md`
- [ ] Documentation updated (`docs/guide/feature-flags.md`)
- [ ] Admin UI considerations documented (future CONFIG-06)

**Other** (13 items):
- [ ] All 8 initial feature flags seeded in database migration
- [ ] Integration with 6+ endpoints (streaming, setup, PDF, chat export, message edit, n8n)
- [ ] Endpoints return proper 403 responses with structured error messages when features disabled
- [ ] Optional: `[RequireFeature]` attribute middleware implemented
- [ ] Admin endpoints for listing and managing feature flags
- [ ] Code implemented and functional
- [ ] Code review approved
- [ ] Tested in staging environment
- [ ] No regressions identified
- [ ] All acceptance criteria satisfied
- [ ] Performance tested (flag checks do not add significant latency)
- [ ] Audit logging verified (all flag changes logged)
- [ ] Role-based access tested with different user roles

**Service Impl** (1 items):
- [ ] `IFeatureFlagService` interface and implementation complete

**Testing** (6 items):
- [ ] 15+ unit tests covering all service methods and edge cases
- [ ] 10+ integration tests covering endpoint behavior with flags
- [ ] All tests passing (xUnit + Testcontainers)
- [ ] Code coverage: 90%+ for FeatureFlagService
- [ ] Unit tests written and passing (15+ tests)
- [ ] Integration tests written and passing (10+ tests)

</details>

### Issue #472: CONFIG-02: Integration - Dynamic rate limiting configuration

**Status**: CLOSED | **Labels**: enhancement, backend, rate-limiting, config
**Unchecked DoD Items**: 24

<details>
<summary>View 24 unchecked items</summary>

**Cicd** (1 items):
- [ ] Configuration fallback chain implemented: DB (role-specific) ÔåÆ DB (global) ÔåÆ appsettings.json ÔåÆ hardcoded default

**Documentation** (5 items):
- [ ] Configuration entries include descriptive documentation
- [ ] Update `docs/guide/rate-limiting.md` with dynamic configuration approach
- [ ] Document all configuration keys and their meanings
- [ ] Document migration path from appsettings.json to DB config
- [ ] Document restart requirement for configuration changes

**Other** (15 items):
- [ ] `RateLimitService` reads rate limit values from database via `IConfigurationService`
- [ ] Role-based overrides work correctly (user/editor/admin get their respective limits)
- [ ] Global feature flag `RateLimit.Enabled` controls whether rate limiting is active
- [ ] Values validated (positive integers, reasonable upper bounds)
- [ ] Backward compatibility maintained with existing appsettings.json configuration
- [ ] `RateLimit.RequestsPerMinute.{Role}` - HTTP requests per minute
- [ ] `RateLimit.TokensPerDay.{Role}` - AI tokens per day
- [ ] `RateLimit.PdfUploadsPerDay.{Role}` - PDF uploads per day
- [ ] Default rate limit configurations seeded in migration
- [ ] Seed data covers all roles (admin, editor, user)
- [ ] Migration tested with both fresh DB and existing DB
- [ ] Add examples of common configuration scenarios
- [ ] New code follows existing patterns from CONFIG-01
- [ ] Logging added for configuration reading and fallback behavior
- [ ] Error handling for invalid configuration values

**Testing** (3 items):
- [ ] **10+ unit tests** covering:
- [ ] **5+ integration tests** covering:
- [ ] All existing tests continue passing

</details>

### Issue #469: AI-07.2: Implement Adaptive Semantic Chunking for PDF Processing

**Status**: CLOSED | **Labels**: enhancement, area/ai, priority:high, effort:M, epic:rag
**Unchecked DoD Items**: 12

<details>
<summary>View 12 unchecked items</summary>

**Cicd** (1 items):
- [ ] CI/CD pipeline green

**Documentation** (1 items):
- [ ] Documentation updated

**Other** (7 items):
- [ ] Evaluation shows +10-15% improvement in P@5/Recall
- [ ] Code implemented and functional
- [ ] Code review approved
- [ ] Migration script created and tested
- [ ] RAG evaluation metrics improved by 10-15%
- [ ] Performance benchmark: <500ms per page
- [ ] Backward compatibility via strategy pattern verified

**Testing** (3 items):
- [ ] Integration test: re-index Chess rulebook, verify chunk quality
- [ ] Unit tests written and passing (edge cases covered)
- [ ] Integration tests passing

</details>

### Issue #468: AI-07.1: Implement Advanced Prompt Engineering for RAG Responses

**Status**: CLOSED | **Labels**: enhancement, area/ai, priority:high, effort:S, epic:rag
**Unchecked DoD Items**: 9

<details>
<summary>View 9 unchecked items</summary>

**Cicd** (1 items):
- [ ] CI/CD pipeline green

**Documentation** (1 items):
- [ ] Documentation updated

**Other** (5 items):
- [ ] Evaluation shows +10-15% improvement in answer accuracy
- [ ] Code implemented and functional
- [ ] Code review approved
- [ ] RAG evaluation metrics improved by 10-15%
- [ ] No regressions in latency (<2s p95)

**Testing** (2 items):
- [ ] Unit tests written and passing
- [ ] Integration tests passing

</details>

### Issue #463: Fix pre-existing test failures (admin-cache and chat.test.tsx)

**Status**: CLOSED | **Labels**: bug, high-priority, testing
**Unchecked DoD Items**: 5

<details>
<summary>View 5 unchecked items</summary>

**Cicd** (1 items):
- [ ] CI pipeline green on main branch

**Documentation** (1 items):
- [ ] Documentation of testing patterns and best practices

**Testing** (3 items):
- [ ] admin-cache.test.tsx: All 19 tests passing
- [ ] chat.test.tsx: All 96 tests passing
- [ ] Other test files: All tests passing or properly skipped with justification

</details>

### Issue #445: Fix remaining 43 failing frontend tests (chess, setup, and other components)

**Status**: CLOSED | **Labels**: bug, tests, frontend, priority:high, type:tech, effort:M
**Unchecked DoD Items**: 4

<details>
<summary>View 4 unchecked items</summary>

**Cicd** (1 items):
- [ ] Verify in different environments (Windows/Mac/Linux) (CI checks in progress)

**Other** (1 items):
- [ ] Peer review of all fixes (awaiting PR review)

**Testing** (2 items):
- [ ] Overall test suite: ÔëÑ95% pass rate (Ôëñ5% failures) - Currently 95.5%, need 97.8% to reach goal
- [ ] All tests pass in CI environment (CI checks pending)

</details>

### Issue #444: TEST-05: Close final frontend coverage gap to 90%

**Status**: CLOSED | **Labels**: enhancement, kind/test, area/testing, frontend, priority: medium
**Unchecked DoD Items**: 18

<details>
<summary>View 18 unchecked items</summary>

**Cicd** (3 items):
- [ ] Coverage report generated and reviewed ÔÜá´©Å **Partial** (run locally, needs CI)
- [ ] CI pipeline validates coverage on every PR ÔÅ│ **Pending**
- [ ] Coverage reports uploaded as CI artifacts ÔÅ│ **Pending**

**Documentation** (4 items):
- [ ] Documentation updated in `docs/issue/test-05-final-coverage-push.md` ÔÅ│ **Pending**
- [ ] Test patterns documented for new developers ÔÅ│ **Pending**
- [ ] Known limitations documented ÔÜá´©Å **Partial** (in PR #493, docs/issue/test-05-phase2-timer-fixes.md)
- [ ] `docs/issue/test-05-final-coverage-push.md` created ÔÅ│ **Pending**

**Other** (2 items):
- [ ] Jest config updated with 90% threshold ÔÅ│ **Deferred to Phase 3** (when all files reach 90%)
- [ ] Peer review completed ÔÅ│ **Pending** (awaiting Phase 2 PR)

**Testing** (9 items):
- [ ] Overall statements coverage 90% ÔÜá´©Å **In Progress** (AccessibleModal: 98.3%)
- [ ] Branches coverage 85% ÔÜá´©Å **In Progress**
- [ ] Functions coverage 90% ÔÜá´©Å **In Progress**
- [ ] Lines coverage 90% ÔÜá´©Å **In Progress**
- [ ] Coverage report shows 90% for each metric ÔÜá´©Å **Partial** (1/3 files exceeds target)
- [ ] api-enhanced.ts coverage 90% ÔÜá´©Å **80.2%** (timer tests fixed in Phase 2, needs ruleSpecComments tests)
- [ ] TimelineEventList.tsx coverage 90% ÔÜá´©Å **81.81%** (+33.33%, 8% from target)
- [ ] useSessionCheck.ts coverage 95% (2 skipped tests documented) ÔÅ©´©Å **Deferred to Phase 3**
- [ ] All tests pass in CI ÔÅ│ **Pending** (PR #493 merged, Phase 2 in progress)

</details>

### Issue #427: N8N-05: Error handling and retry logic for workflows

**Status**: CLOSED | **Labels**: enhancement, sprint-11-12, medium-priority, n8n, reliability
**Unchecked DoD Items**: 22

<details>
<summary>View 22 unchecked items</summary>

**Cicd** (1 items):
- [ ] Code reviewed and merged

**Documentation** (1 items):
- [ ] Documentation includes error handling guide

**Other** (17 items):
- [ ] All n8n workflows have error handling nodes
- [ ] Automatic retry: 3 attempts with exponential backoff (1s, 2s, 4s)
- [ ] Error notifications: Slack message for persistent failures
- [ ] Error logging: Failed executions logged to Seq with full context
- [ ] Fallback behavior: Graceful degradation (e.g., return cached response)
- [ ] Admin dashboard: View failed workflow executions
- [ ] Test error handling in n8n workflows (simulate API down)
- [ ] Test retry logic: 3 attempts with exponential backoff
- [ ] Test Slack alert sent after 3 failures
- [ ] Test fallback response returned
- [ ] Frontend test: Admin panel displays workflow errors
- [ ] All n8n workflows have error handling nodes
- [ ] Retry logic: 3 attempts with exponential backoff
- [ ] Slack alerts sent for persistent failures
- [ ] Errors logged to Seq via API endpoint
- [ ] Admin dashboard displays workflow errors
- [ ] Fallback behavior works for temporary failures

**Testing** (3 items):
- [ ] Integration test: Error logged to API and Seq
- [ ] E2E test: Simulate workflow failure, verify alert and log
- [ ] All tests pass (unit, integration, E2E)

</details>

### Issue #426: TEST-04: Load testing framework with k6 or Gatling

**Status**: CLOSED | **Labels**: enhancement, high-priority, performance, testing, sprint-1-2
**Unchecked DoD Items**: 20

<details>
<summary>View 20 unchecked items</summary>

**Cicd** (1 items):
- [ ] Code reviewed and merged

**Documentation** (1 items):
- [ ] Documentation includes load testing guide

**Other** (15 items):
- [ ] Load testing framework: k6 or Gatling configured
- [ ] Test scenarios: 100, 500, 1000 concurrent users
- [ ] Test endpoints: `/api/v1/agents/qa`, `/api/v1/chat`, `/api/v1/games`
- [ ] Performance targets:
- [ ] Reports: HTML dashboard with charts (response time, throughput, errors)
- [ ] k6 scripts for 3 endpoints: QA, Chat, Games
- [ ] Test 3 load levels: 100, 500, 1000 concurrent users
- [ ] Verify thresholds: p95 <500ms (100 users), <1s (500 users)
- [ ] Test error rate <1% under load
- [ ] HTML report generated with charts
- [ ] k6 load testing framework configured
- [ ] Load test scripts for 3 key endpoints
- [ ] Performance targets validated: p95 <500ms (100 users)
- [ ] Error rate <1% under 500 concurrent users
- [ ] HTML reports generated with charts

**Testing** (3 items):
- [ ] CI integration: Run load tests on PR to main
- [ ] CI integration: Load tests run on PR to main
- [ ] CI integration: Load tests run on PR to main

</details>

### Issue #423: PERF-03: Response caching optimization with intelligent invalidation

**Status**: CLOSED | **Labels**: enhancement, high-priority, sprint-5-6, performance
**Unchecked DoD Items**: 23

<details>
<summary>View 23 unchecked items</summary>

**Cicd** (1 items):
- [ ] Code reviewed and merged

**Documentation** (1 items):
- [ ] Documentation updated with cache strategy

**Other** (16 items):
- [ ] Cache identical questions for 24 hours (configurable)
- [ ] Cache key includes: question + gameId + context hash
- [ ] Invalidate cache when PDF is re-uploaded for a game
- [ ] Invalidate cache when rule spec is updated
- [ ] "Fresh Answer" button to bypass cache
- [ ] Cache hit rate >60% for common questions
- [ ] Cache reduces LLM API costs by 50%+
- [ ] Test cache warming on app startup
- [ ] Load test: Measure cache hit rate with 1000 repeated questions
- [ ] Monitor cache hit rate in production for 1 week
- [ ] Cache includes game and context in key
- [ ] Cache invalidation triggers on PDF/rule spec changes
- [ ] "Fresh Answer" button works
- [ ] Cache stats endpoint returns hit rate and metrics
- [ ] Cache hit rate >60% in staging tests
- [ ] LLM API costs reduced by 50%+ (verified via metrics)

**Testing** (5 items):
- [ ] Unit tests for cache tag invalidation logic
- [ ] Integration tests: Upload PDF ÔåÆ Cache invalidated
- [ ] Integration tests: Update rule spec ÔåÆ Cache invalidated
- [ ] Test "Fresh Answer" button bypasses cache
- [ ] All tests pass (unit, integration, load)

</details>

### Issue #421: CHAT-06: Message editing and deletion for users

**Status**: CLOSED | **Labels**: enhancement, medium-priority, chat, sprint-5-6
**Unchecked DoD Items**: 1

<details>
<summary>View 1 unchecked items</summary>

**Cicd** (1 items):
- [ ] Code reviewed and merged ÔÅ│ (PR #471 awaiting review)

</details>

### Issue #420: AI-13: BoardGameGeek API integration for game metadata

**Status**: CLOSED | **Labels**: enhancement, sprint-11-12, ai, integration, medium-priority
**Unchecked DoD Items**: 10

<details>
<summary>View 10 unchecked items</summary>

**Cicd** (1 items):
- [ ] Code reviewed and merged

**Other** (4 items):
- [ ] Frontend form integration (component ready, integration pending)
- [ ] Frontend tests for BggSearchModal (pending)
- [ ] Form auto-population integrated
- [ ] HTML sanitization implemented

**Testing** (5 items):
- [ ] All tests pass (build passing, E2E tests pending)
- [ ] Unit tests for `BggApiService` (deferred: HybridCache mocking complexity)
- [ ] Integration tests for BGG endpoints (pending: BGG mock server)
- [ ] E2E test: Search BGG, select result, create game (pending)
- [ ] All E2E tests pass

</details>

### Issue #419: ADMIN-02: Analytics dashboard with key metrics and charts

**Status**: CLOSED | **Labels**: enhancement, admin, sprint-11-12, high-priority
**Unchecked DoD Items**: 19

<details>
<summary>View 19 unchecked items</summary>

**Cicd** (1 items):
- [ ] Code reviewed and merged

**Documentation** (1 items):
- [ ] Documentation updated with dashboard guide

**Other** (13 items):
- [ ] Dashboard displays key metrics: total users, active sessions, API requests/day, PDF uploads, chat messages
- [ ] Time-series charts show trends over last 7/30/90 days
- [ ] Real-time metrics update every 30 seconds without page refresh
- [ ] Export dashboard data as CSV/JSON
- [ ] Filter metrics by date range, user role, game
- [ ] Performance: Dashboard loads in <2 seconds with 100k+ data points
- [ ] Frontend tests for chart rendering and data transformation
- [ ] Performance tests: Query time <500ms for 90-day range
- [ ] Dashboard displays 10+ key metrics with charts
- [ ] Time-series data for 7/30/90-day periods
- [ ] Real-time updates every 30 seconds
- [ ] Export functionality works (CSV/JSON)
- [ ] Query performance <500ms for 90-day range

**Testing** (4 items):
- [ ] Unit tests for `AdminStatsService` aggregation logic
- [ ] Integration tests for analytics endpoint with date filters
- [ ] E2E tests: Dashboard loads, charts render, export works
- [ ] All tests pass (unit, integration, E2E)

</details>

### Issue #416: ADMIN-01: Add user management CRUD interface

**Status**: CLOSED | **Labels**: type:story, priority:medium, effort:L, sprint:11-12, epic:admin
**Unchecked DoD Items**: 20

<details>
<summary>View 20 unchecked items</summary>

**Other** (16 items):
- [ ] User list table with pagination (20 per page)
- [ ] Search users by email/name
- [ ] Filter by role (Admin/Editor/User)
- [ ] Sort by registration date, last login
- [ ] Create user form (email, password, displayName, role)
- [ ] Edit user modal (update email, role, displayName)
- [ ] Change password functionality
- [ ] Delete user with confirmation dialog
- [ ] Bulk operations: delete multiple users
- [ ] `GET /api/v1/admin/users` - List users with filters (search, role, pagination, sorting)
- [ ] `POST /api/v1/admin/users` - Create new user
- [ ] `PUT /api/v1/admin/users/{id}` - Update user details
- [ ] `DELETE /api/v1/admin/users/{id}` - Delete user
- [ ] All endpoints require admin role authorization
- [ ] Test authorization (non-admin users cannot access)
- [ ] Test edge cases (delete self, last admin, pagination boundaries)

**Testing** (4 items):
- [ ] Unit tests for user service CRUD operations
- [ ] Integration tests for all endpoints with Testcontainers
- [ ] E2E test: create user ÔåÆ edit ÔåÆ delete flow
- [ ] Test validation (invalid email, weak password, duplicate email)

</details>

### Issue #413: EDIT-05: Enhance comments and annotations system with inline annotations and threading

**Status**: CLOSED | **Labels**: effort:M, type:story, priority:medium, epic:editor
**Unchecked DoD Items**: 40

<details>
<summary>View 40 unchecked items</summary>

**Cicd** (1 items):
- [ ] CI/CD pipeline green

**Documentation** (1 items):
- [ ] Documentation updated

**Other** (29 items):
- [ ] Click any rule line to add inline comment
- [ ] Comment bubble indicator displayed next to commented lines
- [ ] Hover line ÔåÆ show comment preview tooltip
- [ ] Click bubble ÔåÆ open comment thread sidebar
- [ ] Reply to comments (nested/threaded structure)
- [ ] Display parent-child comment relationships visually
- [ ] Navigate through comment thread hierarchy
- [ ] Mention users with @username in comments
- [ ] Autocomplete suggestions when typing @
- [ ] Extract and track mentioned users for notifications
- [ ] Mark comments as resolved/unresolved
- [ ] Filter: show only unresolved comments
- [ ] Visual distinction between resolved/unresolved comments
- [ ] Prevent editing resolved comments (or require unresolving first)
- [ ] Comment entity supports line numbers
- [ ] Parent/child relationship for comment threading
- [ ] User mentions tracked in database
- [ ] API endpoints for CRUD operations on threaded comments
- [ ] API endpoint to resolve/unresolve comments
- [ ] Code implemented and functional
- [ ] Code review approved
- [ ] Tested in staging environment
- [ ] No regressions identified
- [ ] All acceptance criteria satisfied
- [ ] UI/UX review completed for comment threading interface
- [ ] Performance tested with large comment threads (50+ comments)
- [ ] Accessibility verified (keyboard navigation, screen readers)
- [ ] Responsive design tested (mobile, tablet, desktop)
- [ ] Database migration tested (up and down)

**Testing** (9 items):
- [ ] Unit tests for comment threading logic
- [ ] Unit tests for mention extraction and validation
- [ ] Integration tests: create comment ÔåÆ reply ÔåÆ resolve flow
- [ ] Integration tests: mention user ÔåÆ verify tracking
- [ ] E2E test: add inline comment ÔåÆ reply ÔåÆ resolve
- [ ] E2E test: mention user ÔåÆ verify autocomplete works
- [ ] Unit tests written and passing (90% coverage target)
- [ ] Integration tests passing
- [ ] E2E tests passing

</details>

### Issue #412: EDIT-04: Improve visual diff viewer for version comparison

**Status**: CLOSED | **Labels**: effort:M, type:story, priority:medium, epic:editor
**Unchecked DoD Items**: 37

<details>
<summary>View 37 unchecked items</summary>

**Cicd** (1 items):
- [ ] CI/CD pipeline green

**Documentation** (1 items):
- [ ] Documentation updated

**Other** (32 items):
- [ ] Side-by-side diff viewer layout (2 columns: old version | new version)
- [ ] Syntax highlighting for JSON using Prism.js or Monaco Editor
- [ ] Color-coded changes with clear visual distinction:
- [ ] Line numbers displayed on both sides
- [ ] Expand/collapse unchanged sections (accordion-style)
- [ ] Diff statistics header showing: `+X added, -Y deleted, ~Z modified`
- [ ] Search functionality within diff content
- [ ] Navigation controls: "Previous Change" and "Next Change" buttons
- [ ] Mobile-responsive: vertical stack on screens < 768px
- [ ] Component properly handles large diffs (virtualization if needed)
- [ ] Maintains current DiffViewer component API compatibility
- [ ] Proper TypeScript types for all props and state
- [ ] Accessible keyboard navigation (Tab, Enter, Arrow keys)
- [ ] ARIA labels for screen readers
- [ ] Tests for edge cases: empty diff, large diff, no changes
- [ ] Tests for expand/collapse functionality
- [ ] Tests for search and navigation features
- [ ] 90% code coverage threshold met
- [ ] Renders diff with 1000+ lines in < 500ms
- [ ] Smooth scrolling and navigation
- [ ] No memory leaks on component unmount
- [ ] Code implemented and functional
- [ ] Code review approved
- [ ] Tested in staging environment
- [ ] No regressions identified
- [ ] All acceptance criteria satisfied
- [ ] UI/UX review completed
- [ ] Performance tested with large diffs (1000+ lines)
- [ ] Accessibility verified (keyboard navigation, screen readers)
- [ ] Responsive design tested on mobile/tablet/desktop
- [ ] Browser compatibility verified (Chrome, Firefox, Safari, Edge)
- [ ] 90% test coverage threshold met (Jest)

**Testing** (3 items):
- [ ] Component unit tests with diff mock data
- [ ] E2E test: navigate to versions page ÔåÆ compare two versions ÔåÆ verify syntax highlighting and color coding
- [ ] Unit tests written and passing

</details>

### Issue #411: EDIT-03: Add rich text editor (TipTap/Slate.js)

**Status**: CLOSED | **Labels**: type:story, priority:medium, effort:L, epic:editor, sprint:9-10
**Unchecked DoD Items**: 30

<details>
<summary>View 30 unchecked items</summary>

**Cicd** (1 items):
- [ ] CI/CD pipeline green

**Documentation** (2 items):
- [ ] Documentation updated (component docs, usage guide)
- [ ] Keyboard shortcuts documented in user guide

**Other** (21 items):
- [ ] Rich text editor component implemented using TipTap
- [ ] Toolbar with formatting options:
- [ ] Toggle button to switch between rich text and JSON view
- [ ] Preserve formatting in JSON serialization (bidirectional conversion)
- [ ] Auto-save functionality on blur (debounced 2 seconds)
- [ ] Undo/redo support with keyboard shortcuts (Ctrl+Z, Ctrl+Shift+Z)
- [ ] Keyboard shortcuts displayed in tooltips for discoverability
- [ ] Mobile-responsive layout with touch-optimized controls
- [ ] Code implemented and functional
- [ ] Code review approved
- [ ] Tested in staging environment
- [ ] No regressions identified
- [ ] All acceptance criteria satisfied
- [ ] UI/UX review completed
- [ ] Performance tested (editor load time < 300ms)
- [ ] Accessibility verified (WCAG 2.1 AA compliance)
- [ ] Responsive design tested (mobile, tablet, desktop)
- [ ] Analytics/tracking implemented (editor usage metrics)
- [ ] Component tests for RuleEditor component
- [ ] Cross-browser testing completed
- [ ] Mobile device testing completed

**Testing** (6 items):
- [ ] Component unit tests with editor mock
- [ ] Integration tests: edit ÔåÆ save ÔåÆ reload ÔåÆ verify persistence
- [ ] E2E test flow: create rule ÔåÆ edit with rich editor ÔåÆ publish ÔåÆ verify
- [ ] Unit tests written and passing (90% coverage threshold)
- [ ] Integration tests for auto-save functionality
- [ ] E2E test for complete edit workflow

</details>

### Issue #410: AI-11: Add response quality scoring and tracking

**Status**: CLOSED | **Labels**: type:tech, effort:M, priority:medium, epic:rag
**Unchecked DoD Items**: 5

<details>
<summary>View 5 unchecked items</summary>

**Manual Verification** (1 items):
- [ ] E2E test: verify dashboard displays quality metrics (ÔÜá´©Å Deferred with dashboard - AI-11.2)

**Other** (3 items):
- [ ] Grafana dashboard showing quality trends over time (ÔÜá´©Å Deferred to AI-11.2)
- [ ] Alert when average confidence drops below 0.6 for 1 hour (ÔÜá´©Å Deferred to AI-11.2)
- [ ] Load test: measure quality metrics under high traffic (ÔÜá´©Å Deferred to performance sprint)

**Testing** (1 items):
- [ ] Integration tests: generate response ÔåÆ verify metrics stored (ÔÜá´©Å Testcontainers setup needed - AI-11.1)

</details>

### Issue #409: AI-09: Add multi-language embeddings (EN, IT, DE, FR, ES)

**Status**: CLOSED | **Labels**: priority:medium, effort:L, type:feature, epic:rag, sprint:7-8
**Unchecked DoD Items**: 33

<details>
<summary>View 33 unchecked items</summary>

**Cicd** (2 items):
- [ ] Support language-specific embedding models (multilingual-e5-large via OpenRouter)
- [ ] CI/CD pipeline green

**Documentation** (7 items):
- [ ] Update `PdfTextExtractionService` to detect document language
- [ ] Update API documentation with language parameter
- [ ] Document supported languages and embedding models
- [ ] Add language support section to user guide
- [ ] Documentation updated
- [ ] API documentation includes language parameters
- [ ] User guide updated with multi-language support section

**Other** (18 items):
- [ ] Implement language detection during PDF ingestion (langdetect or similar library)
- [ ] Store language metadata in Qdrant payload per chunk (`language` field)
- [ ] Add language filter support in RAG search queries
- [ ] Update `EmbeddingService` to use appropriate model per language
- [ ] Update `QdrantService` to store/filter by language metadata
- [ ] Add language selector in upload wizard (`pages/upload.tsx`)
- [ ] Display detected language in PDF list view
- [ ] Allow language filtering in search interface
- [ ] Create test datasets for each language (5 test PDFs minimum)
- [ ] Test language filter in Qdrant queries
- [ ] Code implemented and functional
- [ ] Code review approved
- [ ] Tested in staging environment
- [ ] No regressions identified
- [ ] All acceptance criteria satisfied
- [ ] Language detection tested on 5+ PDFs per language
- [ ] Search quality validated across all languages
- [ ] Performance benchmarked (< 2s embedding time)

**Testing** (6 items):
- [ ] Unit tests for language detection service
- [ ] Unit tests for multilingual embedding generation
- [ ] Integration tests: Upload PDFs in 5 languages and verify search results
- [ ] E2E test: Upload Italian PDF ÔåÆ Ask Italian question ÔåÆ Verify relevant results
- [ ] Unit tests written and passing (90% coverage threshold)
- [ ] E2E test covers full multilingual workflow

</details>

### Issue #408: AI-10: Optimize Redis cache for AI responses

**Status**: CLOSED | **Labels**: type:tech, effort:M, priority:medium, epic:rag, sprint:7-8
**Unchecked DoD Items**: 8

<details>
<summary>View 8 unchecked items</summary>

**Cicd** (5 items):
- [ ] Load test: measure hit rate under realistic traffic ÔÅ│ **POST-DEPLOYMENT** - Follow-up issue AI-10.2
- [ ] Tested in staging environment ÔÅ│ **POST-MERGE**
- [ ] No regressions identified ÔÅ│ **POST-MERGE**
- [ ] Cache hit rate >70% verified in production ÔÅ│ **POST-DEPLOYMENT**
- [ ] P95 cached response time <100ms verified ÔÅ│ **POST-DEPLOYMENT**

**Documentation** (3 items):
- [ ] Cache invalidation when documents updated ÔÜá´©Å **OUT OF SCOPE** - Follow-up issue AI-10.1
- [ ] Cache invalidation verified on document updates ÔÜá´©Å **OUT OF SCOPE** - Follow-up AI-10.1
- [ ] Load test results documented ÔÅ│ **POST-DEPLOYMENT** - Follow-up AI-10.2

</details>

### Issue #407: AI-08: Add page number extraction for citations

**Status**: CLOSED | **Labels**: effort:M, type:story, priority:medium, epic:rag, sprint:7-8
**Unchecked DoD Items**: 9

<details>
<summary>View 9 unchecked items</summary>

**Cicd** (1 items):
- [ ] Display "Page X" in citation UI ÔÅ©´©Å Frontend Phase 2

**Manual Verification** (1 items):
- [ ] E2E test verifying page numbers in UI ÔÅ©´©Å Frontend Phase 2

**Other** (4 items):
- [ ] Handle multi-page chunks (show page range: "Pages 5-6") ÔÅ©´©Å Frontend Phase 2
- [ ] Handle PDFs without page numbers gracefully ÔÅ©´©Å Frontend Phase 2
- [ ] All acceptance criteria satisfied ÔÅ©´©Å Frontend pending (Phase 2)
- [ ] Tested in staging environment ÔÅ©´©Å Phase 2

**Testing** (3 items):
- [ ] Integration tests: upload PDF ÔåÆ verify page numbers in search ÔÅ©´©Å Phase 2
- [ ] E2E test: ask question ÔåÆ verify "Page X" in citation ÔÅ©´©Å Frontend Phase 2
- [ ] Integration tests passing with Testcontainers ÔÅ©´©Å Phase 2

</details>

### Issue #406: AI-12: Add personalized search ranking algorithm

**Status**: CLOSED | **Labels**: type:tech, effort:L, priority:low, epic:rag, sprint:7-8
**Unchecked DoD Items**: 23

<details>
<summary>View 23 unchecked items</summary>

**Cicd** (2 items):
- [ ] Track which citations users found helpful
- [ ] CI/CD pipeline green

**Documentation** (2 items):
- [ ] Document ranking algorithm in code comments
- [ ] Documentation updated

**Other** (16 items):
- [ ] Implement ranking algorithm with weighted factors
- [ ] Store user preferences in database (new table or extend existing)
- [ ] Boost/penalize results based on user feedback
- [ ] Implement A/B testing framework (50/50 split)
- [ ] Measure improvement in user satisfaction metrics
- [ ] Performance test: ranking adds <50ms latency per query
- [ ] Add telemetry for score breakdown (OpenTelemetry metrics)
- [ ] Code implemented and functional
- [ ] Code review approved
- [ ] Tested in staging environment
- [ ] No regressions identified
- [ ] All acceptance criteria satisfied
- [ ] UI/UX review completed (if ranking affects UI)
- [ ] Performance tested and validated (<50ms overhead)
- [ ] A/B test metrics tracked in observability stack
- [ ] OpenTelemetry metrics for ranking factors implemented

**Testing** (3 items):
- [ ] Unit tests for ranking calculation (all factor combinations)
- [ ] Integration tests: compare old vs new ranking results
- [ ] Unit tests written and passing (90% coverage)

</details>

### Issue #404: CHAT-05: Add conversation export (PDF/TXT/Markdown)

**Status**: CLOSED | **Labels**: effort:M, type:story, epic:chat, sprint:5-6, priority:low
**Unchecked DoD Items**: 15

<details>
<summary>View 15 unchecked items</summary>

**Cicd** (1 items):
- [ ] Test with special characters and emojis (ÔÜá´©Å Technical Debt)

**Manual Verification** (1 items):
- [ ] Tested in staging environment (ÔÜá´©Å Manual testing needed)

**Other** (3 items):
- [ ] Test with long conversations (100+ messages) (ÔÜá´©Å Technical Debt)
- [ ] File downloads work across browsers (Chrome, Firefox, Safari, Edge) (ÔÜá´©Å Manual testing needed)
- [ ] Performance tested with large conversations (100+ messages) (ÔÜá´©Å Technical Debt)

**Testing** (10 items):
- [ ] Unit tests: Export service formatting logic for all formats (ÔÜá´©Å Technical Debt)
- [ ] Unit tests: Date range filtering (ÔÜá´©Å Technical Debt)
- [ ] Integration tests: Export chat ÔåÆ verify file content and headers (ÔÜá´©Å Technical Debt)
- [ ] Integration tests: Authentication and authorization (ÔÜá´©Å Technical Debt)
- [ ] Integration tests: Invalid chat ID handling (ÔÜá´©Å Technical Debt)
- [ ] E2E test: Complete flow (click export ÔåÆ select format ÔåÆ download) (ÔÜá´©Å Technical Debt)
- [ ] E2E test: Date range filtering (ÔÜá´©Å Technical Debt)
- [ ] Unit tests written and passing (90%+ coverage) (ÔÜá´©Å Technical Debt - Follow-up needed)
- [ ] Integration tests written and passing (ÔÜá´©Å Technical Debt - Follow-up needed)
- [ ] E2E tests written and passing (ÔÜá´©Å Technical Debt - Follow-up needed)

</details>

### Issue #401: CHAT-02: Add AI-generated follow-up questions

**Status**: CLOSED | **Labels**: effort:M, type:story, priority:medium, epic:chat, sprint:5-6
**Unchecked DoD Items**: 22

<details>
<summary>View 22 unchecked items</summary>

**Cicd** (1 items):
- [ ] CI/CD pipeline green

**Documentation** (1 items):
- [ ] Documentation updated

**Other** (14 items):
- [ ] Generate 3-5 follow-up questions using LLM
- [ ] Questions based on RAG context and conversation history
- [ ] Cache questions with response (avoid regeneration)
- [ ] Track which suggested questions users click (analytics)
- [ ] Display questions as clickable buttons below response
- [ ] Clicking button sends question immediately
- [ ] Questions styled distinctly from regular messages
- [ ] User can dismiss suggestions
- [ ] Code implemented and functional
- [ ] Code review approved
- [ ] Tested in staging environment
- [ ] No regressions identified
- [ ] Performance tested (generation time < 2s)
- [ ] Analytics tracking implemented and verified

**Testing** (6 items):
- [ ] Unit tests for question generation logic
- [ ] Integration tests with LLM mock
- [ ] E2E test: answer appears ÔåÆ suggestions appear ÔåÆ click suggestion
- [ ] Unit tests written and passing (90% coverage)
- [ ] Integration tests passing
- [ ] E2E test covering full user flow

</details>

### Issue #400: CHAT-04: Polish loading states and animations

**Status**: CLOSED | **Labels**: effort:M, type:story, priority:medium, epic:chat, sprint:5-6
**Unchecked DoD Items**: 36

<details>
<summary>View 36 unchecked items</summary>

**Cicd** (2 items):
- [ ] Animated dots for typing indicator (3 dots bouncing)
- [ ] CI/CD pipeline green

**Documentation** (1 items):
- [ ] Documentation updated

**Other** (32 items):
- [ ] Skeleton screens while loading chat history (shimmer effect)
- [ ] Typing indicator when AI is generating ("AI is thinking...")
- [ ] Loading spinner for initial chat load
- [ ] Smooth fade-in for new messages (300ms ease-out)
- [ ] Slide-in animation for user messages (from right)
- [ ] Slide-in animation for AI messages (from left)
- [ ] Smooth scroll to latest message
- [ ] Pulse animation for send button on hover
- [ ] Button hover states (background color change)
- [ ] Input field focus states (border color)
- [ ] Send button disabled state while sending
- [ ] Success feedback on message sent (checkmark icon)
- [ ] Error state with red border and icon
- [ ] Animations run at 60fps
- [ ] No jank during scroll
- [ ] CSS-based animations (no JS for simple transitions)
- [ ] Use `will-change` for animated elements
- [ ] Debounce scroll events
- [ ] Respect `prefers-reduced-motion` media query
- [ ] ARIA live regions for screen readers
- [ ] Focus management after animations
- [ ] Code implemented and functional
- [ ] Code review approved
- [ ] Tested in staging environment
- [ ] No regressions identified
- [ ] All acceptance criteria satisfied
- [ ] UI/UX review completed
- [ ] Performance tested and validated (60fps animations)
- [ ] Accessibility verified (WCAG compliance, reduced-motion support)
- [ ] Responsive design tested (mobile, tablet, desktop)
- [ ] Snapshot tests for all loading states
- [ ] Cross-browser testing (Chrome, Firefox, Safari, Edge)

**Testing** (1 items):
- [ ] Unit tests written and passing (90% coverage threshold)

</details>

### Issue #399: PDF-09: Add pre-upload PDF validation

**Status**: CLOSED | **Labels**: priority:high, effort:M, type:story, epic:pdf, sprint:3-4
**Unchecked DoD Items**: 45

<details>
<summary>View 45 unchecked items</summary>

**Cicd** (1 items):
- [ ] Show validation errors in upload wizard with specific error messages

**Documentation** (5 items):
- [ ] Validate page count: min 1, max 500 pages (using Docnet.Core)
- [ ] Validate PDF version: 1.4+ (compatible with Docnet.Core)
- [ ] XML documentation comments on public methods
- [ ] Configuration options documented in appsettings
- [ ] Error response format documented

**Other** (28 items):
- [ ] Validate file type: application/pdf only (MIME type check)
- [ ] Validate file size: max 100MB (configurable)
- [ ] Validation happens before upload starts (File API preview)
- [ ] Disable upload button while validation in progress
- [ ] Display validation results with clear error messages
- [ ] Validate file type: application/pdf (Content-Type header + magic bytes)
- [ ] Validate file size: max 100MB (configurable in appsettings.json)
- [ ] Return 400 Bad Request with structured error response
- [ ] Backend validates even if client-side passes (prevent API misuse)
- [ ] Add PdfProcessing:MaxFileSizeBytes setting (default: 104857600)
- [ ] Add PdfProcessing:MaxPageCount setting (default: 500)
- [ ] Add PdfProcessing:MinPdfVersion setting (default: "1.4")
- [ ] PdfValidationService implemented with all validation rules
- [ ] Service registered in DI container (Program.cs)
- [ ] Configuration section added to appsettings.json
- [ ] Client-side validation integrated in upload.tsx
- [ ] Server-side endpoint updated with validation
- [ ] Error messages user-friendly and actionable
- [ ] CLAUDE.md updated with validation flow
- [ ] Code review completed and approved
- [ ] No security vulnerabilities introduced
- [ ] Logging includes correlation IDs
- [ ] Error messages don't leak sensitive information
- [ ] Performance tested (validation adds <100ms overhead)
- [ ] Feature flag added if needed
- [ ] Configuration validated in all environments
- [ ] Backwards compatible with existing uploads
- [ ] Monitoring/observability in place

**Testing** (11 items):
- [ ] Unit tests for PdfValidationService covering all validation rules
- [ ] Unit tests for edge cases (corrupted PDF, zero pages, ancient version)
- [ ] Integration tests for upload endpoint with invalid files (400 responses)
- [ ] Integration tests verify structured error response format
- [ ] E2E test for client-side validation error messages
- [ ] E2E test for server-side validation fallback
- [ ] 15+ unit tests written and passing (all validation scenarios)
- [ ] 7+ integration tests written and passing (API endpoints)
- [ ] 4+ E2E tests written and passing (user flows)
- [ ] Code coverage ÔëÑ90% for new code
- [ ] All tests green in CI pipeline

</details>

### Issue #398: PDF-08: Add granular progress tracking for PDF processing

**Status**: CLOSED | **Labels**: priority:high, effort:M, type:story, epic:pdf, sprint:3-4
**Unchecked DoD Items**: 36

<details>
<summary>View 36 unchecked items</summary>

**Cicd** (2 items):
- [ ] Progress updates after each pipeline step in `PdfProcessingService`
- [ ] CI/CD pipeline green

**Documentation** (2 items):
- [ ] Processing status stored in database (`PdfDocument.ProcessingProgress` JSON column)
- [ ] Documentation updated (API docs, component docs)

**Other** (22 items):
- [ ] Progress bar showing 0-100% completion with smooth transitions
- [ ] Step-by-step indicator showing current step with checkmarks for completed steps
- [ ] Real-time progress updates via polling (every 2 seconds) or Server-Sent Events
- [ ] Time estimate per step (e.g., "Extract: ~2 minutes remaining")
- [ ] Total elapsed time display (e.g., "Elapsed: 1m 23s")
- [ ] Cancel button to abort processing with confirmation dialog
- [ ] Progress persists across page refreshes (loaded from DB)
- [ ] UI updates smoothly without flickering
- [ ] Success state with summary: "Processed 45 pages in 2m 34s"
- [ ] Error state shows which step failed
- [ ] New endpoint: `GET /api/v1/pdfs/{pdfId}/progress` returns progress JSON
- [ ] New endpoint: `DELETE /api/v1/pdfs/{pdfId}/processing` cancels processing
- [ ] Incremental progress tracking (pages processed so far)
- [ ] Time estimation based on historical processing data
- [ ] Debounced progress writes to DB (every 5% change or 10 seconds)
- [ ] Code implemented and functional across backend + frontend
- [ ] Code review approved
- [ ] Tested in staging environment with various PDF sizes
- [ ] No regressions identified in existing PDF upload flow
- [ ] Performance validated (no slowdown in processing)
- [ ] UI/UX review completed (smooth animations, clear messaging)
- [ ] Accessibility verified (screen reader support for progress updates)

**Testing** (10 items):
- [ ] Unit tests for progress calculation logic
- [ ] Unit tests for time estimation algorithm
- [ ] Integration tests for progress endpoint (auth, validation, real-time updates)
- [ ] Integration tests for cancellation endpoint
- [ ] E2E test for complete progress flow from upload to completion
- [ ] E2E test for progress persistence across page refresh
- [ ] E2E test for cancellation flow
- [ ] Unit tests written and passing (90%+ coverage)
- [ ] Integration tests passing (auth, real-time updates, cancellation)
- [ ] E2E tests passing (upload flow, progress tracking, page refresh, cancellation)

</details>

### Issue #397: PDF-07: Add PDF preview component with PDF.js

**Status**: CLOSED | **Labels**: priority:high, type:story, epic:pdf, sprint:3-4, effort:L
**Unchecked DoD Items**: 28

<details>
<summary>View 28 unchecked items</summary>

**Cicd** (1 items):
- [ ] CI/CD pipeline green (lint, typecheck, tests)

**Documentation** (1 items):
- [ ] Documentation updated (inline comments, README if needed)

**Other** (23 items):
- [ ] PDF preview component implemented using `react-pdf` library
- [ ] Display first page automatically on file select
- [ ] Zoom controls with 5 preset levels (25%, 50%, 100%, 150%, 200%)
- [ ] Page navigation: prev/next buttons + "Jump to page" input field
- [ ] Thumbnail sidebar showing all pages (lazy loaded with Intersection Observer)
- [ ] Click thumbnail to jump to that page in main preview
- [ ] Responsive design: mobile (single column), desktop (sidebar + preview)
- [ ] Loading states: skeleton screen while rendering pages
- [ ] Error handling: display error message for corrupt PDFs or unsupported versions
- [ ] Accessibility: keyboard navigation (arrow keys, Tab), ARIA labels
- [ ] Component tests with PDF mock (test rendering, navigation, zoom)
- [ ] Component tests with PDF mock data
- [ ] Code review approved
- [ ] Tested in staging environment
- [ ] No regressions identified
- [ ] All acceptance criteria satisfied
- [ ] UI/UX review completed (responsive design verified)
- [ ] Performance tested and validated (meets performance requirements)
- [ ] Accessibility verified (WCAG compliance, keyboard navigation)
- [ ] Cross-browser testing (Chrome, Firefox, Safari, Edge)
- [ ] Mobile responsive testing (iOS Safari, Chrome Android)
- [ ] Error scenarios tested (corrupt PDF, large files, network errors)
- [ ] Memory profiling completed (no leaks, meets memory requirements)

**Testing** (3 items):
- [ ] E2E test: select file ÔåÆ preview loads ÔåÆ navigate pages ÔåÆ zoom ÔåÆ upload
- [ ] Unit tests written and passing (90% coverage)
- [ ] E2E test covering full user journey

</details>

### Issue #396: PDF-06: Improve error handling with user-friendly messages

**Status**: CLOSED | **Labels**: priority:high, effort:M, type:story, epic:pdf, sprint:3-4
**Unchecked DoD Items**: 29

<details>
<summary>View 29 unchecked items</summary>

**Cicd** (2 items):
- [ ] Error categorization with specific messages:
- [ ] CI/CD pipeline green

**Documentation** (1 items):
- [ ] Documentation updated

**Other** (21 items):
- [ ] Error messages in plain language (no technical jargon or stack traces)
- [ ] Retry button for transient errors (network, server 5xx)
- [ ] Error details expandable for advanced users (show technical info)
- [ ] Toast notifications for errors (react-hot-toast or similar)
- [ ] Error state in upload wizard with icon, message, and action buttons
- [ ] Each error tracked with correlation ID (displayed in UI, logged in Seq)
- [ ] Error tracking integrated with existing AuditService
- [ ] Correlation ID extracted from `X-Correlation-Id` response header
- [ ] All errors logged to Seq with context (userId, fileName, fileSize, error type)
- [ ] Code implemented and functional
- [ ] Code review approved
- [ ] Tested in staging environment
- [ ] No regressions identified
- [ ] All acceptance criteria satisfied
- [ ] All four error categories properly handled and tested
- [ ] UI/UX review completed for error messages
- [ ] Error messages reviewed for clarity and tone
- [ ] Correlation ID tracking verified in Seq dashboard
- [ ] Retry logic tested with network simulation
- [ ] Accessibility verified (screen reader compatible error messages)
- [ ] Performance tested (error display doesn't impact upload speed)

**Testing** (5 items):
- [ ] Unit tests for error handling in upload service
- [ ] Integration tests for all error scenarios (400, 500, network failure)
- [ ] E2E tests for error UI display and retry functionality
- [ ] Unit tests written and passing
- [ ] E2E tests cover all error scenarios

</details>

### Issue #395: PDF-05: Add multi-file PDF upload support

**Status**: CLOSED | **Labels**: type:story, epic:pdf, priority:medium, sprint:3-4, effort:L
**Unchecked DoD Items**: 32

<details>
<summary>View 32 unchecked items</summary>

**Cicd** (2 items):
- [ ] Cancel button per file (abort specific upload)
- [ ] CI/CD pipeline green

**Documentation** (1 items):
- [ ] Documentation updated

**Other** (23 items):
- [ ] File picker supports multiple file selection (HTML5 `multiple` attribute)
- [ ] Drag-and-drop area accepts multiple files at once
- [ ] Upload queue UI component showing all files with status
- [ ] Individual progress bar per file (0-100%)
- [ ] Parallel upload with configurable concurrency limit (default: 3)
- [ ] Status indicators per file: Pending, Uploading, Processing, Success, Failed
- [ ] Retry button for failed uploads (retry only that file)
- [ ] Remove button to remove file from queue before upload
- [ ] Aggregate progress: "Uploading 2 of 5 files (40% total)"
- [ ] Summary after completion: "3 succeeded, 1 failed, 1 cancelled"
- [ ] Failed files show error message with retry option
- [ ] Backend supports parallel upload (multiple simultaneous POST requests)
- [ ] Rate limiting adjusted to allow burst uploads (10 files per minute)
- [ ] Code implemented and functional
- [ ] Code review approved
- [ ] Tested in staging environment
- [ ] No regressions identified
- [ ] All acceptance criteria satisfied
- [ ] UI/UX review completed
- [ ] Performance tested with 10+ files
- [ ] Accessibility verified (WCAG compliance)
- [ ] Responsive design tested (mobile drag-drop)
- [ ] OpenTelemetry metrics implemented and validated

**Testing** (6 items):
- [ ] Unit tests for upload queue logic
- [ ] Integration tests with multiple files (test concurrency)
- [ ] E2E test: upload 3 files ÔåÆ verify all processed
- [ ] Coverage maintains 90% threshold
- [ ] Unit tests written and passing
- [ ] E2E test covers full multi-file upload flow

</details>

### Issue #394: TEST-03: Increase frontend test coverage to 90%

**Status**: CLOSED | **Labels**: epic:testing, type:tech, sprint:1-2, effort:M, priority:critical
**Unchecked DoD Items**: 43

<details>
<summary>View 43 unchecked items</summary>

**Cicd** (4 items):
- [ ] Configure CI pipeline to fail on coverage drop
- [ ] Add coverage trend reporting in CI
- [ ] CI pipeline validates coverage on every PR
- [ ] Coverage reports uploaded as CI artifacts

**Documentation** (3 items):
- [ ] Tests are maintainable and well-documented
- [ ] Coverage trends documented (before/after comparison)
- [ ] Testing best practices documented for new developers

**Other** (26 items):
- [ ] `CommentItem.tsx` - Increase from 31% to 90%+
- [ ] `CommentForm.tsx` - Increase from 81% to 90%+
- [ ] `AdminCharts.tsx` - Minor improvements to reach 95%+
- [ ] `api.ts` - Increase from 76% to 90%+
- [ ] `admin.tsx` - Increase from 86% to 90%+
- [ ] `chess.tsx` - Increase from 90% to 92%+
- [ ] `_app.tsx` - Add initial tests
- [ ] Add snapshot tests for all major UI components (consistency regression prevention)
- [ ] Test all loading states (spinners, skeletons, placeholders)
- [ ] Test all error states (network errors, validation errors, API errors)
- [ ] Test all empty states (no data scenarios)
- [ ] Test all user interactions (clicks, inputs, form submissions, keyboard navigation)
- [ ] Update jest.config.js to enforce 90% coverage threshold
- [ ] Update GitHub branch protection to require coverage checks
- [ ] All acceptance criteria satisfied
- [ ] Code follows existing test patterns in `__tests__/` directories
- [ ] No test code duplication (shared utilities extracted)
- [ ] Mock setup is clean and reusable
- [ ] Test coverage report generated and reviewed
- [ ] Complex test scenarios explained with comments
- [ ] Jest config updated with 90% threshold
- [ ] Branch protection rules updated
- [ ] Code review approved by at least 1 team member
- [ ] Coverage report reviewed for accuracy
- [ ] No regressions in existing functionality
- [ ] Manual smoke testing completed on dev environment

**Testing** (10 items):
- [ ] Statement coverage ÔëÑ90% for all directories (`src/pages/`, `src/components/`, `src/lib/`)
- [ ] Branch coverage ÔëÑ85% for all directories
- [ ] Function coverage ÔëÑ85% for all directories
- [ ] Line coverage ÔëÑ90% for all directories
- [ ] Add integration tests for critical user flows:
- [ ] Coverage thresholds met (90% statements, 85% branches/functions)
- [ ] All component tests passing
- [ ] All integration tests passing
- [ ] No flaky tests (consistent pass rate)
- [ ] All tests run successfully in CI environment

</details>

### Issue #393: AUTH-05: Fix inconsistent session timeout behavior

**Status**: CLOSED | **Labels**: priority:high, sprint:1-2, epic:auth, type:bug, effort:S
**Unchecked DoD Items**: 36

<details>
<summary>View 36 unchecked items</summary>

**Cicd** (1 items):
- [ ] CI/CD pipeline green (both API and Web workflows)

**Documentation** (2 items):
- [ ] Documentation updated (`docs/guide/authentication.md`)
- [ ] Root cause identified and documented (add findings to issue comments)

**Other** (26 items):
- [ ] Session timeout configured consistently in `appsettings.json` and applied correctly
- [ ] `SessionAutoRevocationService` reliably revokes sessions after 30 days inactivity
- [ ] `LastSeenAt` timestamp updated on EVERY authenticated API request (verify middleware)
- [ ] Add endpoint `GET /api/v1/auth/session/status` returning: `{ "expiresAt": "ISO8601", "lastSeenAt": "ISO8601", "remainingMinutes": number }`
- [ ] Add endpoint `POST /api/v1/auth/session/extend` to refresh session expiry
- [ ] Implement `useSessionCheck()` React hook for 5-minute polling
- [ ] Hook calls `/api/v1/auth/session/status` every 5 minutes
- [ ] Show warning modal when `remainingMinutes < 5`
- [ ] Modal displays countdown timer and "Stay Logged In" / "Log Out Now" buttons
- [ ] "Stay Logged In" calls `/api/v1/auth/session/extend` and dismisses modal
- [ ] After expiry (`remainingMinutes <= 0`), redirect to `/login?reason=session_expired`
- [ ] Login page displays friendly message: "Your session has expired. Please log in again."
- [ ] Code implemented and functional
- [ ] Code review approved
- [ ] Tested in staging environment
- [ ] No regressions identified
- [ ] Bug reproduced and confirmed in local environment
- [ ] Fix implemented with verified solution
- [ ] Regression prevention tests added (both backend and E2E)
- [ ] Verified in environment where bug occurred
- [ ] Manual testing performed: left session idle for simulated period, verified warning modal appears
- [ ] Verified `LastSeenAt` updates correctly in database after API calls
- [ ] UI/UX review completed (modal design, countdown timer, messaging)
- [ ] Accessibility verified (WCAG compliance: keyboard navigation, screen reader support)
- [ ] Responsive design tested (modal works on mobile/tablet/desktop)
- [ ] Cross-browser testing (Chrome, Firefox, Safari, Edge)

**Testing** (7 items):
- [ ] Unit tests for timeout calculation logic (edge cases: null LastSeenAt, boundary conditions)
- [ ] Integration tests for auto-revocation service (verify 30-day threshold)
- [ ] Unit tests for `useSessionCheck()` hook (with jest fake timers)
- [ ] Unit tests for session warning modal component
- [ ] E2E test (Playwright) for complete expiry warning flow
- [ ] Unit tests written and passing (90% coverage threshold)
- [ ] Integration tests written and passing

</details>

### Issue #392: AUTH-04: Implement password reset flow with email verification

**Status**: CLOSED | **Labels**: priority:high, sprint:1-2, effort:M, epic:auth, type:story
**Unchecked DoD Items**: 25

<details>
<summary>View 25 unchecked items</summary>

**Cicd** (1 items):
- [ ] CI/CD pipeline green

**Documentation** (1 items):
- [ ] Documentation updated

**Other** (20 items):
- [ ] "Forgot Password" link added to login page (index.tsx)
- [ ] POST /api/v1/auth/password-reset/request endpoint (accepts email)
- [ ] Email sent with reset token (use SendGrid or Mailgun)
- [ ] Token stored in DB with 30-minute expiry
- [ ] GET /api/v1/auth/password-reset/verify?token=... endpoint (validates token)
- [ ] Reset password page (pages/reset-password.tsx)
- [ ] PUT /api/v1/auth/password-reset/confirm endpoint (accepts token + new password)
- [ ] Password complexity validation (8+ chars, 1 upper, 1 lower, 1 number)
- [ ] Rate limiting: 3 reset requests per hour per email
- [ ] Success confirmation message and auto-login
- [ ] Code implemented and functional
- [ ] Code review approved
- [ ] Tested in staging environment
- [ ] No regressions identified
- [ ] All acceptance criteria satisfied
- [ ] UI/UX review completed
- [ ] Performance tested and validated
- [ ] Accessibility verified (WCAG compliance)
- [ ] Responsive design tested
- [ ] Email templates tested across providers (Gmail, Outlook, Yahoo)

**Testing** (3 items):
- [ ] Integration tests with email mock
- [ ] E2E test for full flow
- [ ] Unit tests written and passing

</details>

### Issue #390: OPS-06: Optimize CI pipeline to run in <10 minutes

**Status**: CLOSED | **Labels**: epic:testing, priority:high, type:tech, sprint:1-2, effort:M
**Unchecked DoD Items**: 20

<details>
<summary>View 20 unchecked items</summary>

**Cicd** (8 items):
- [ ] Cache pnpm dependencies using `actions/cache@v4`
- [ ] Verify web and API jobs run in parallel (already implemented in ci.yml)
- [ ] Use `dotnet test --no-build` flag where appropriate to avoid redundant builds
- [ ] Achieve average pipeline time <10 minutes over 10 consecutive runs
- [ ] CI/CD pipeline green
- [ ] Tested in real CI environment (not just local)
- [ ] Measured performance over 10 consecutive CI runs
- [ ] Team briefed on new CI behavior

**Documentation** (4 items):
- [ ] Document all optimizations in `docs/ci-optimization.md`
- [ ] Documentation updated (`docs/ci-optimization.md` created)
- [ ] Cache hit rates documented and >80%
- [ ] Optimizations documented with before/after metrics

**Other** (8 items):
- [ ] Cache NuGet packages using `actions/cache@v4`
- [ ] Optimize Testcontainers startup with health check tuning
- [ ] Verify path filters work correctly (tests only run for changed files)
- [ ] Code implemented and functional
- [ ] All acceptance criteria satisfied
- [ ] Code review approved
- [ ] No regressions identified
- [ ] Average duration consistently <10 minutes

</details>

### Issue #380: Fix: Resolve 8 failing auth modal tests in index.test.tsx (framer-motion AnimatePresence)

**Status**: CLOSED | **Labels**: bug, frontend, test
**Unchecked DoD Items**: 22

<details>
<summary>View 22 unchecked items</summary>

**Cicd** (1 items):
- [ ] CI/CD pipeline green (ci-web workflow)

**Documentation** (2 items):
- [ ] Mock approach is documented in `jest.setup.js` with comments
- [ ] Documentation updated (jest.setup.js comments)

**Manual Verification** (1 items):
- [ ] All auth modal user interactions testable via Testing Library queries

**Other** (13 items):
- [ ] Tab switching between Login/Register works in tests
- [ ] Login success flow redirects to `/chat`
- [ ] Registration success flow redirects to `/chat`
- [ ] Error messages display correctly for failed auth attempts
- [ ] Edge cases handled (empty displayName, missing error messages)
- [ ] No console warnings or errors during test execution
- [ ] Test execution time remains reasonable (<5 seconds for index.test.tsx)
- [ ] Code implemented and functional
- [ ] Code review approved
- [ ] No regressions identified in other test suites
- [ ] Jest configuration updated and tested
- [ ] framer-motion mock fully covers AnimatePresence behavior
- [ ] Modal animations work correctly in browser (manual verification)

**Testing** (5 items):
- [ ] All 27 tests in `index.test.tsx` pass (100% success rate)
- [ ] Auth modal form fields are accessible in tests (Email, Password, Display Name)
- [ ] (Optional) 2-3 E2E tests added for critical auth modal flows
- [ ] All unit tests passing (27/27 in index.test.tsx)
- [ ] Test coverage maintained at ÔëÑ90% for affected files

</details>

### Issue #379: Update index.test.tsx to match new landing page design

**Status**: CLOSED | **Labels**: bug, frontend, test
**Unchecked DoD Items**: 8

<details>
<summary>View 8 unchecked items</summary>

**Cicd** (1 items):
- [ ] CI pipeline passes (lint, typecheck, test)

**Other** (5 items):
- [ ] All test assertions updated to match current page structure
- [ ] Tests verify routing to `/chat` instead of `/upload`
- [ ] New UI elements (hero, features, CTAs) are tested
- [ ] No regressions in other tests
- [ ] Code review approved

**Testing** (2 items):
- [ ] All tests pass: `pnpm test index.test.tsx`
- [ ] Code coverage remains ÔëÑ 90%

</details>

### Issue #377: fix(web): Add IntersectionObserver mock for index.test.tsx

**Status**: CLOSED | **Labels**: area/ui, area/testing, kind/bug
**Unchecked DoD Items**: 4

<details>
<summary>View 4 unchecked items</summary>

**Other** (2 items):
- [ ] IntersectionObserver mock added to Jest setup
- [ ] Verify no regressions in other tests

**Testing** (2 items):
- [ ] index.test.tsx passes in CI
- [ ] All web tests pass without errors

</details>

### Issue #370: API-01 - API Foundation and Authentication Infrastructure

**Status**: CLOSED | **Labels**: area/infra, area/auth, kind/feature, area/api, priority: high
**Unchecked DoD Items**: 44

<details>
<summary>View 44 unchecked items</summary>

**Cicd** (2 items):
- [ ] CI/CD pipeline green
- [ ] Staging deployment successful

**Documentation** (7 items):
- [ ] **Security Definitions**: Both ApiKey and Cookie schemes documented in OpenAPI
- [ ] **Migration Guide**: Document new `api_keys` table schema
- [ ] **API Documentation**: Update `docs/api-authentication.md`
- [ ] **Code Comments**: Inline documentation for middleware and services
- [ ] API documentation updated
- [ ] Migration guide written
- [ ] Rollback plan documented

**Manual Verification** (1 items):
- [ ] **OpenAPI/Swagger**: Swagger UI accessible at `/api/docs` in development

**Other** (29 items):
- [ ] **API Key Schema**: `api_keys` table created with EF migration
- [ ] **API Key Entity**: `ApiKeyEntity.cs` with proper relationships
- [ ] **API Key Service**: `ApiKeyAuthenticationService.cs` with key validation
- [ ] **Authentication Middleware**: `ApiKeyAuthenticationMiddleware.cs` validates `X-API-Key` header
- [ ] **Dual Auth Support**: Both API key and cookie authentication work simultaneously
- [ ] **API Versioning**: `/api/v1/*` URL structure implemented
- [ ] **Error Handling**: `ApiExceptionHandlerMiddleware.cs` returns JSON errors for `/api/*` paths
- [ ] **Correlation IDs**: All API responses include `X-Correlation-Id` header
- [ ] **Health Checks**: Unversioned `/health` endpoints remain unchanged
- [ ] **Audit Logging**: API key usage logged to `audit_logs` table
- [ ] **Security Tests**: API key hashes stored (not plaintext), brute force protected
- [ ] **CLAUDE.md**: Update with API authentication details
- [ ] **Performance**: API key validation < 50ms (indexed lookups)
- [ ] **Security**: API keys hashed with bcrypt (cost factor 12)
- [ ] **Observability**: All API requests logged with correlation ID
- [ ] **Backward Compatibility**: Existing cookie auth endpoints unchanged
- [ ] All acceptance criteria met
- [ ] Code reviewed and approved
- [ ] No compiler warnings
- [ ] Follows C# coding standards
- [ ] Manual testing in local environment
- [ ] Tested with both API key and cookie auth
- [ ] CLAUDE.md updated
- [ ] Code comments added
- [ ] Security review completed
- [ ] Performance benchmarks met
- [ ] No new vulnerabilities introduced
- [ ] Migration script tested
- [ ] No regressions in existing features

**Testing** (5 items):
- [ ] **Unit Tests**: `ApiKeyAuthenticationServiceTests.cs` (90%+ coverage)
- [ ] **Integration Tests**: `ApiKeyAuthenticationMiddlewareTests.cs`
- [ ] **E2E Tests**: API documentation accessible, versioned endpoints work
- [ ] Unit tests written and passing (90%+ coverage)
- [ ] Integration tests passing

</details>

### Issue #359: fix(tests): SQLite database schema initialization failures in CI pipeline

**Status**: CLOSED | **Labels**: bug, area/infra, kind/ci, area/db
**Unchecked DoD Items**: 6

<details>
<summary>View 6 unchecked items</summary>

**Cicd** (1 items):
- [ ] CI pipeline runs green for existing tests

**Other** (4 items):
- [ ] SQLite database schema is properly initialized
- [ ] `users` table and all other tables exist before tests run
- [ ] No regression in test execution time
- [ ] Tests remain isolated (no data leakage between tests)

**Testing** (1 items):
- [ ] All 27+ failing tests pass in CI pipeline

</details>

### Issue #357: Refactor: Restructure MCP configuration to align with GUIDA_MCP_Codex.md

**Status**: CLOSED | **Labels**: documentation, refactor, infrastructure
**Unchecked DoD Items**: 27

<details>
<summary>View 27 unchecked items</summary>

**Cicd** (2 items):
- [ ] `tools/mcp-kit/build-all.ps1` successfully builds all custom server images
- [ ] VSCode tasks run successfully (env-check, smoke-test, build custom servers)

**Documentation** (12 items):
- [ ] `.env.example` documents all required secrets for external and custom servers
- [ ] Wrappers in `tools/mcp-kit/wrappers/*.ps1` correctly start MCP containers via STDIO (`docker run -i`)
- [ ] All custom servers have working Dockerfiles
- [ ] All markdown files moved to `docs/mcp/` with appropriate renaming
- [ ] Old `./docker/mcp/` directory removed or marked as deprecated
- [ ] Security documentation (`SECURITY.md`) updated for new structure
- [ ] Docker builds complete without errors
- [ ] `tools/mcp-kit/README.md` created with comprehensive guide
- [ ] `docs/mcp/INDEX.md` created as navigation hub
- [ ] `docs/mcp/GUIDA_MCP_Codex.md` updated with custom server documentation
- [ ] Root `CLAUDE.md` or `README.md` references new MCP structure
- [ ] Migration guide created for developers

**Other** (13 items):
- [ ] `tools/mcp-kit/.env.local` exists and is git-ignored
- [ ] `.codex/mcp.toml` points to repo wrappers using `${workspaceFolder}` (no absolute paths)
- [ ] `.codex/mcp.toml` includes configurations for all MCP servers (external + custom)
- [ ] Wrapper scripts validate required environment variables and fail gracefully
- [ ] `codex mcp list` shows all configured servers (github, github-project-manager, n8n, memory-bank)
- [ ] Each MCP server can be invoked via Codex without errors
- [ ] All custom server source code moved from `./mcp/` to `tools/mcp-kit/servers/`
- [ ] Old `./mcp/` directory removed from repository
- [ ] No orphaned files or directories left behind
- [ ] No secrets committed to git
- [ ] `.gitignore` properly excludes all sensitive files
- [ ] Each wrapper script tested individually with valid credentials
- [ ] STDIO communication works for all servers

</details>

### Issue #323: [Docs] Documentare naming convention BDD-style per test frontend e backend

**Status**: CLOSED | **Labels**: documentation, tests, priority: low, complexity: extra-small
**Unchecked DoD Items**: 8

<details>
<summary>View 8 unchecked items</summary>

**Documentation** (3 items):
- [ ] `README.test.md` creato nella root con sezione "Naming Convention"
- [ ] Link a README da `apps/web/README.md` e `apps/api/tests/README.md`
- [ ] Documentazione merged

**Other** (5 items):
- [ ] Esempi per frontend (Jest/TypeScript)
- [ ] Esempi per backend (xUnit/C#)
- [ ] Common patterns table inclusa
- [ ] Anti-patterns mostrati (cosa evitare)
- [ ] PR template aggiornato con checklist: "Test names follow BDD convention"

</details>

### Issue #322: [Chore] Standardizzare uso di jest.useFakeTimers con try-finally in upload.test.tsx

**Status**: CLOSED | **Labels**: tests, frontend, chore, complexity: small, priority: low
**Unchecked DoD Items**: 6

<details>
<summary>View 6 unchecked items</summary>

**Cicd** (1 items):
- [ ] CI verde

**Documentation** (1 items):
- [ ] Documentazione in `README.test.md`: best practice fake timers

**Other** (4 items):
- [ ] Audit completo: identificati tutti test con `useFakeTimers`
- [ ] Tutti test con fake timers hanno try-finally pattern
- [ ] `afterEach` safety check aggiunto: `jest.useRealTimers()`
- [ ] Test suite eseguita 5 volte consecutivamente senza hang/timeout

</details>

### Issue #321: [Chore] Aggiungere cleanup automatico a QdrantServiceIntegrationTests

**Status**: CLOSED | **Labels**: tests, backend, priority: medium, chore, complexity: small
**Unchecked DoD Items**: 7

<details>
<summary>View 7 unchecked items</summary>

**Cicd** (1 items):
- [ ] CI verde

**Other** (5 items):
- [ ] Tutti i 10 test usano unique `pdfId` con GUID
- [ ] `_createdPdfIds` lista tracked in ogni test
- [ ] `DisposeAsync()` esegue cleanup di tutti pdfId
- [ ] Test eseguiti 10 volte consecutivamente senza failures
- [ ] Verificato che cleanup funziona (search dopo cleanup = 0)

**Testing** (1 items):
- [ ] `IAsyncLifetime` implementato in `QdrantServiceIntegrationTests`

</details>

### Issue #320: [Refactor] Creare MockApiRouter utility per eliminare duplicazione mock setup nei test frontend

**Status**: CLOSED | **Labels**: refactor, tests, frontend, complexity: medium, priority: medium
**Unchecked DoD Items**: 10

<details>
<summary>View 10 unchecked items</summary>

**Cicd** (1 items):
- [ ] CI verde

**Documentation** (1 items):
- [ ] Documentazione API con esempi in `README.test.md`

**Other** (7 items):
- [ ] `MockApiRouter` class implementata in `__tests__/utils/mock-api-router.ts`
- [ ] Support per GET, POST, PUT, DELETE
- [ ] Pattern matching per route parameters (`:id` style)
- [ ] Error messages helpful con lista available routes
- [ ] `toMockImplementation()` helper
- [ ] Almeno 10 test refactorate (pilot in upload.test.tsx)
- [ ] Preset helpers per route comuni

**Testing** (1 items):
- [ ] Unit test per `MockApiRouter` stesso

</details>

### Issue #319: [Refactor] Implementare test isolation pattern con IAsyncLifetime per integration tests

**Status**: CLOSED | **Labels**: refactor, tests, complexity: large, backend, priority: medium
**Unchecked DoD Items**: 9

<details>
<summary>View 9 unchecked items</summary>

**Cicd** (1 items):
- [ ] CI verde

**Documentation** (1 items):
- [ ] Documentazione pattern in `README.test.md`

**Other** (5 items):
- [ ] `InitializeAsync()` e `DisposeAsync()` implementati
- [ ] Helper methods: `CreateTestGameAsync()`, `CreateTestUserAsync()`
- [ ] Almeno 3 test classes refactorate
- [ ] Manual cleanup rimosso da test
- [ ] Test eseguiti 10 volte senza flaky failures

**Testing** (2 items):
- [ ] Base class `IntegrationTestBase` creata con `IAsyncLifetime`
- [ ] Tutti test passano in isolation e in parallel

</details>

### Issue #318: [Refactor] Convertire ChessDatasetTests a data-driven test per error reporting preciso

**Status**: CLOSED | **Labels**: refactor, tests, backend, complexity: medium, priority: medium
**Unchecked DoD Items**: 9

<details>
<summary>View 9 unchecked items</summary>

**Cicd** (1 items):
- [ ] CI verde

**Other** (5 items):
- [ ] Test loop-based convertiti in `[Theory]` con `[MemberData]`
- [ ] `GetAllOpenings()` implementato e ritorna tutti openings da JSON
- [ ] `GetAllTacticsWithExamples()` implementato per tactics
- [ ] Test FIDE rules rimangono `[Fact]` (non data-driven)
- [ ] Error messages includono: nome opening/tactic, ID, campo mancante

**Testing** (3 items):
- [ ] Tutti test passano (stesso numero assertion)
- [ ] Test coverage rimane 100% per dataset loading logic
- [ ] Test runner report mostra granularit├á (es: "45 passed, 2 failed")

</details>

### Issue #317: [Refactor] Suddividere AdminEndpointsIntegrationTests per area funzionale

**Status**: CLOSED | **Labels**: refactor, tests, priority: high, backend, complexity: medium
**Unchecked DoD Items**: 8

<details>
<summary>View 8 unchecked items</summary>

**Other** (4 items):
- [ ] 4 nuovi file test creati
- [ ] Base class `AdminTestFixture.cs` creata con helper comuni
- [ ] Ogni nuovo file ha massimo 300 linee
- [ ] Authorization test convertiti in data-driven `[Theory]`

**Testing** (4 items):
- [ ] File originale `AdminEndpointsIntegrationTests.cs` eliminato
- [ ] Tutti i 13 test originali migrati e passano
- [ ] Test coverage per admin endpoints rimane >= 85%
- [ ] CI verde: integration test suite passa

</details>

### Issue #316: [Refactor] Split upload.test.tsx in file modulari per workflow step

**Status**: CLOSED | **Labels**: refactor, tests, frontend, priority: high, complexity: large
**Unchecked DoD Items**: 10

<details>
<summary>View 10 unchecked items</summary>

**Cicd** (1 items):
- [ ] CI verde: nessuna regressione su test suite esistente

**Documentation** (1 items):
- [ ] README test aggiornato con struttura nuova e convenzioni

**Other** (6 items):
- [ ] File originale `upload.test.tsx` eliminato
- [ ] 4 nuovi file test creati con naming convention chiara
- [ ] Fixture `upload-mocks.ts` creata con almeno 5 mock riutilizzabili
- [ ] Helper `setupUploadMocks()` implementato per ridurre duplicazione
- [ ] Ogni nuovo file test ha massimo 400 linee
- [ ] Codice reviewato e approvato da almeno 2 developer

**Testing** (2 items):
- [ ] Tutti i 29 test originali migrati e passano
- [ ] Test coverage per `upload.tsx` rimane >= 90%

</details>

---

## 📎 Appendix: Category Definitions

**Manual Verification**: Items requiring human testing/validation (e.g., "Dashboard works correctly")
**Testing**: Test coverage, test execution, test passing requirements
**Documentation**: README, guides, documentation updates
**File Creation**: Creating specific files (services, components, configs)
**Service Implementation**: Service class implementations
**CI/CD**: Continuous integration, deployment, build pipeline
**Other**: Miscellaneous or uncategorized items
