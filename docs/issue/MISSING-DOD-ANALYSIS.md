# Missing DoD Items Analysis

## Summary

- **Issues with missing items**: 17
- **Total missing DoD items**: 37

## Recommendations

### 🟢 Minor - Document for Future Work (17 issues)

These issues have <25% missing implementations - may not need reopening:

- **Issue #319**: [Refactor] Implementare test isolation pattern con IAsyncLifetime per integration tests - 2/9 missing (22.2%)
- **Issue #416**: ADMIN-01: Add user management CRUD interface - 4/20 missing (20.0%)
- **Issue #322**: [Chore] Standardizzare uso di jest.useFakeTimers con try-finally in upload.test.tsx - 1/6 missing (16.7%)
- **Issue #392**: AUTH-04: Implement password reset flow with email verification - 4/25 missing (16.0%)
- **Issue #323**: [Docs] Documentare naming convention BDD-style per test frontend e backend - 1/8 missing (12.5%)
- **Issue #317**: [Refactor] Suddividere AdminEndpointsIntegrationTests per area funzionale - 1/8 missing (12.5%)
- **Issue #476**: CONFIG-01: Backend Foundation - Database schema, service, and API endpoints for dynamic configuration - 6/55 missing (10.9%)
- **Issue #320**: [Refactor] Creare MockApiRouter utility per eliminare duplicazione mock setup nei test frontend - 1/10 missing (10.0%)
- **Issue #398**: PDF-08: Add granular progress tracking for PDF processing - 3/36 missing (8.3%)
- **Issue #393**: AUTH-05: Fix inconsistent session timeout behavior - 3/36 missing (8.3%)
- **Issue #357**: Refactor: Restructure MCP configuration to align with GUIDA_MCP_Codex.md - 2/27 missing (7.4%)
- **Issue #474**: CONFIG-03: Integration - Dynamic AI/LLM configuration - 2/34 missing (5.9%)
- **Issue #475**: CONFIG-04: Integration - Dynamic RAG configuration - 2/35 missing (5.7%)
- **Issue #444**: TEST-05: Close final frontend coverage gap to 90% - 1/18 missing (5.6%)
- **Issue #390**: OPS-06: Optimize CI pipeline to run in <10 minutes - 1/20 missing (5.0%)
- **Issue #511**: AI-11.2: Grafana Dashboard & Prometheus Alerts for Quality Metrics - 1/21 missing (4.8%)
- **Issue #370**: API-01 - API Foundation and Authentication Infrastructure - 2/44 missing (4.5%)

---

## Detailed Missing Items by Issue

### Issue #476: CONFIG-01: Backend Foundation - Database schema, service, and API endpoints for dynamic configuration

**Missing**: 6/55 items (10.9%)

- [ ] Create migration `AddSystemConfigurationsTable` with table `system_configurations`
  - Evidence: Migration not found for table: system_configurations
- [ ] `GET /api/v1/admin/configurations` - List all configurations
  - Evidence: Endpoint not found: GET /api/v1/admin/configurations`
- [ ] `GET /api/v1/admin/configurations/{key}` - Get single configuration
  - Evidence: Endpoint not found: GET /api/v1/admin/configurations/{key}`
- [ ] `POST /api/v1/admin/configurations` - Create new configuration
  - Evidence: Endpoint not found: POST /api/v1/admin/configurations`
- [ ] `PUT /api/v1/admin/configurations/{key}` - Update existing configuration
  - Evidence: Endpoint not found: PUT /api/v1/admin/configurations/{key}`
- [ ] `DELETE /api/v1/admin/configurations/{key}` - Delete configuration
  - Evidence: Endpoint not found: DELETE /api/v1/admin/configurations/{key}`

### Issue #416: ADMIN-01: Add user management CRUD interface

**Missing**: 4/20 items (20.0%)

- [ ] `GET /api/v1/admin/users` - List users with filters (search, role, pagination, sorting)
  - Evidence: Endpoint not found: GET /api/v1/admin/users`
- [ ] `POST /api/v1/admin/users` - Create new user
  - Evidence: Endpoint not found: POST /api/v1/admin/users`
- [ ] `PUT /api/v1/admin/users/{id}` - Update user details
  - Evidence: Endpoint not found: PUT /api/v1/admin/users/{id}`
- [ ] `DELETE /api/v1/admin/users/{id}` - Delete user
  - Evidence: Endpoint not found: DELETE /api/v1/admin/users/{id}`

### Issue #392: AUTH-04: Implement password reset flow with email verification

**Missing**: 4/25 items (16.0%)

- [ ] POST /api/v1/auth/password-reset/request endpoint (accepts email)
  - Evidence: Endpoint not found: POST /api/v1/auth/password-reset/request
- [ ] GET /api/v1/auth/password-reset/verify?token=... endpoint (validates token)
  - Evidence: Endpoint not found: GET /api/v1/auth/password-reset/verify?token=...
- [ ] Reset password page (pages/reset-password.tsx)
  - Evidence: Component not found: password
- [ ] PUT /api/v1/auth/password-reset/confirm endpoint (accepts token + new password)
  - Evidence: Endpoint not found: PUT /api/v1/auth/password-reset/confirm

### Issue #398: PDF-08: Add granular progress tracking for PDF processing

**Missing**: 3/36 items (8.3%)

- [ ] New endpoint: `GET /api/v1/pdfs/{pdfId}/progress` returns progress JSON
  - Evidence: Endpoint not found: GET /api/v1/pdfs/{pdfId}/progress`
- [ ] New endpoint: `DELETE /api/v1/pdfs/{pdfId}/processing` cancels processing
  - Evidence: Endpoint not found: DELETE /api/v1/pdfs/{pdfId}/processing`
- [ ] Progress updates after each pipeline step in `PdfProcessingService`
  - Evidence: Service not found: PdfProcessingService

### Issue #393: AUTH-05: Fix inconsistent session timeout behavior

**Missing**: 3/36 items (8.3%)

- [ ] Add endpoint `GET /api/v1/auth/session/status` returning: `{ "expiresAt": "ISO8601", "lastSeenAt": "ISO8601", "remainingMinutes": number }`
  - Evidence: Endpoint not found: GET /api/v1/auth/session/status`
- [ ] Add endpoint `POST /api/v1/auth/session/extend` to refresh session expiry
  - Evidence: Endpoint not found: POST /api/v1/auth/session/extend`
- [ ] Documentation updated (`docs/guide/authentication.md`)
  - Evidence: File not found: docs/guide/authentication.md

### Issue #475: CONFIG-04: Integration - Dynamic RAG configuration

**Missing**: 2/35 items (5.7%)

- [ ] `docs/guide/rag-configuration.md` created with complete reference
  - Evidence: File not found: docs/guide/rag-configuration.md
- [ ] Documentation updated (`docs/guide/rag-configuration.md`, `CLAUDE.md`)
  - Evidence: File not found: docs/guide/rag-configuration.md

### Issue #474: CONFIG-03: Integration - Dynamic AI/LLM configuration

**Missing**: 2/34 items (5.9%)

- [ ] Create `docs/guide/ai-configuration.md` documenting:
  - Evidence: File not found: docs/guide/ai-configuration.md
- [ ] `docs/guide/ai-configuration.md` created with:
  - Evidence: File not found: docs/guide/ai-configuration.md

### Issue #370: API-01 - API Foundation and Authentication Infrastructure

**Missing**: 2/44 items (4.5%)

- [ ] **Migration Guide**: Document new `api_keys` table schema
  - Evidence: Migration not found for table: api_keys
- [ ] **API Documentation**: Update `docs/api-authentication.md`
  - Evidence: File not found: docs/api-authentication.md

### Issue #357: Refactor: Restructure MCP configuration to align with GUIDA_MCP_Codex.md

**Missing**: 2/27 items (7.4%)

- [ ] `tools/mcp-kit/README.md` created with comprehensive guide
  - Evidence: File not found: tools/mcp-kit/README.md
- [ ] `docs/mcp/GUIDA_MCP_Codex.md` updated with custom server documentation
  - Evidence: File not found: docs/mcp/GUIDA_MCP_Codex.md

### Issue #319: [Refactor] Implementare test isolation pattern con IAsyncLifetime per integration tests

**Missing**: 2/9 items (22.2%)

- [ ] Helper methods: `CreateTestGameAsync()`, `CreateTestUserAsync()`
  - Evidence: Test not found: CreateTest
- [ ] Documentazione pattern in `README.test.md`
  - Evidence: File not found: README.test.md

### Issue #511: AI-11.2: Grafana Dashboard & Prometheus Alerts for Quality Metrics

**Missing**: 1/21 items (4.8%)

- [ ] Create `infra/prometheus/alerts/quality-alerts.yml`
  - Evidence: File not found: infra/prometheus/alerts/quality-alerts.yml

### Issue #444: TEST-05: Close final frontend coverage gap to 90%

**Missing**: 1/18 items (5.6%)

- [ ] Documentation updated in `docs/issue/test-05-final-coverage-push.md` ÔÅ│ **Pending**
  - Evidence: File not found: docs/issue/test-05-final-coverage-push.md

### Issue #390: OPS-06: Optimize CI pipeline to run in <10 minutes

**Missing**: 1/20 items (5.0%)

- [ ] Documentation updated (`docs/ci-optimization.md` created)
  - Evidence: File not found: docs/ci-optimization.md

### Issue #323: [Docs] Documentare naming convention BDD-style per test frontend e backend

**Missing**: 1/8 items (12.5%)

- [ ] `README.test.md` creato nella root con sezione "Naming Convention"
  - Evidence: File not found: README.test.md

### Issue #322: [Chore] Standardizzare uso di jest.useFakeTimers con try-finally in upload.test.tsx

**Missing**: 1/6 items (16.7%)

- [ ] Documentazione in `README.test.md`: best practice fake timers
  - Evidence: File not found: README.test.md

### Issue #320: [Refactor] Creare MockApiRouter utility per eliminare duplicazione mock setup nei test frontend

**Missing**: 1/10 items (10.0%)

- [ ] Documentazione API con esempi in `README.test.md`
  - Evidence: File not found: README.test.md

### Issue #317: [Refactor] Suddividere AdminEndpointsIntegrationTests per area funzionale

**Missing**: 1/8 items (12.5%)

- [ ] File originale `AdminEndpointsIntegrationTests.cs` eliminato
  - Evidence: Test not found: AdminEndpointsIntegrationTest
