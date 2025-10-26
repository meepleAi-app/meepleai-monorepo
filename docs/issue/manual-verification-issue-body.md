# MANUAL-VERIFICATION: DoD Items Requiring Human Validation

## Overview

This issue consolidates **82 manual verification items** from 40 closed issues that require human validation.

These items cannot be automatically verified and need manual testing, visual inspection, or QA validation.

## Priority
**Medium** - Non-blocking but important for quality assurance

## Assignment
**QA Team** / **Product Owner**

## Acceptance Criteria

- [ ] All manual verification items below have been tested/validated
- [ ] Issues with failed validations documented in new issues
- [ ] This tracking issue closed when all verifications complete

---

## Manual Verification Items by Issue

### Issue #511: AI-11.2: Grafana Dashboard & Prometheus Alerts for Quality Metrics

**Manual items to verify**: 5

- [ ] Dashboard shows quality trends over time (1h, 6h, 24h, 7d, 30d)
  - Note: Requires manual testing/verification
- [ ] Dashboard accessible at `http://localhost:3001/d/quality-metrics`
  - Note: Requires manual testing/verification
- [ ] Dashboard renders correctly with sample data
  - Note: Requires manual testing/verification
- [ ] Alerts trigger correctly with test scenarios (manual verification)
  - Note: Requires manual testing/verification
- [ ] Alert notifications visible in Prometheus UI
  - Note: Requires manual testing/verification

### Issue #485: TEST-02-P5: CI Integration + Documentation + Final 90% Validation

**Manual items to verify**: 1

- [ ] Coverage badge added to README (optional - deferred)
  - Note: Documentation needs manual review

### Issue #484: TEST-02-P4: LlmService + RagService + Infrastructure tests

**Manual items to verify**: 3

- [ ] Infrastructure layer: ÔëÑ90% coverage (from ~65%)
  - Note: Coverage verification required: 65%
- [ ] Documentation updated
  - Note: Documentation needs manual review
- [ ] Coverage validated (all services ÔëÑ90%)
  - Note: Coverage verification required: 90%

### Issue #482: TEST-02-P2: ChatExportService + Formatters comprehensive tests

**Manual items to verify**: 1

- [ ] Documentation updated
  - Note: Documentation needs manual review

### Issue #476: CONFIG-01: Backend Foundation - Database schema, service, and API endpoints for dynamic configuration

**Manual items to verify**: 2

- [ ] Documentation updated (inline comments, XML docs)
  - Note: Documentation needs manual review
- [ ] Test coverage ÔëÑ 90% for new code
  - Note: Coverage verification required: 90%

### Issue #475: CONFIG-04: Integration - Dynamic RAG configuration

**Manual items to verify**: 5

- [ ] Test coverage maintained at 90%+
  - Note: Coverage verification required: 90%
- [ ] Re-indexing guide documented with step-by-step instructions
  - Note: Documentation needs manual review
- [ ] Migration guide for changing chunking/vector parameters
  - Note: Documentation needs manual review
- [ ] API documentation includes configuration endpoints
  - Note: Documentation needs manual review
- [ ] Re-indexing guide documented and tested manually
  - Note: Requires manual testing/verification

### Issue #474: CONFIG-03: Integration - Dynamic AI/LLM configuration

**Manual items to verify**: 4

- [ ] Code coverage maintained at 90%+
  - Note: Coverage verification required: 90%
- [ ] Update existing AI documentation with configuration references
  - Note: Documentation needs manual review
- [ ] Include migration guide from appsettings.json to DB config
  - Note: Documentation needs manual review
- [ ] Existing AI documentation updated
  - Note: Documentation needs manual review

### Issue #472: CONFIG-02: Integration - Dynamic rate limiting configuration

**Manual items to verify**: 1

- [ ] Configuration entries include descriptive documentation
  - Note: Documentation needs manual review

### Issue #469: AI-07.2: Implement Adaptive Semantic Chunking for PDF Processing

**Manual items to verify**: 1

- [ ] Documentation updated
  - Note: Documentation needs manual review

### Issue #468: AI-07.1: Implement Advanced Prompt Engineering for RAG Responses

**Manual items to verify**: 1

- [ ] Documentation updated
  - Note: Documentation needs manual review

### Issue #463: Fix pre-existing test failures (admin-cache and chat.test.tsx)

**Manual items to verify**: 1

- [ ] Documentation of testing patterns and best practices
  - Note: Documentation needs manual review

### Issue #444: TEST-05: Close final frontend coverage gap to 90%

**Manual items to verify**: 8

- [ ] Overall statements coverage 90% ÔÜá´©Å **In Progress** (AccessibleModal: 98.3%)
  - Note: Coverage verification required: 90%
- [ ] Branches coverage 85% ÔÜá´©Å **In Progress**
  - Note: Coverage verification required: 85%
- [ ] Functions coverage 90% ÔÜá´©Å **In Progress**
  - Note: Coverage verification required: 90%
- [ ] Lines coverage 90% ÔÜá´©Å **In Progress**
  - Note: Coverage verification required: 90%
- [ ] Coverage report shows 90% for each metric ÔÜá´©Å **Partial** (1/3 files exceeds target)
  - Note: Coverage verification required: 90%
- [ ] api-enhanced.ts coverage 90% ÔÜá´©Å **80.2%** (timer tests fixed in Phase 2, needs ruleSpecComments tests)
  - Note: Coverage verification required: 90%
- [ ] TimelineEventList.tsx coverage 90% ÔÜá´©Å **81.81%** (+33.33%, 8% from target)
  - Note: Coverage verification required: 90%
- [ ] useSessionCheck.ts coverage 95% (2 skipped tests documented) ÔÅ©´©Å **Deferred to Phase 3**
  - Note: Coverage verification required: 95%

### Issue #427: N8N-05: Error handling and retry logic for workflows

**Manual items to verify**: 2

- [ ] Admin dashboard displays workflow errors
  - Note: Requires manual testing/verification
- [ ] Documentation includes error handling guide
  - Note: Documentation needs manual review

### Issue #426: TEST-04: Load testing framework with k6 or Gatling

**Manual items to verify**: 1

- [ ] Documentation includes load testing guide
  - Note: Documentation needs manual review

### Issue #423: PERF-03: Response caching optimization with intelligent invalidation

**Manual items to verify**: 1

- [ ] Documentation updated with cache strategy
  - Note: Documentation needs manual review

### Issue #419: ADMIN-02: Analytics dashboard with key metrics and charts

**Manual items to verify**: 3

- [ ] Dashboard displays key metrics: total users, active sessions, API requests/day, PDF uploads, chat messages
  - Note: Requires manual testing/verification
- [ ] Dashboard displays 10+ key metrics with charts
  - Note: Requires manual testing/verification
- [ ] Documentation updated with dashboard guide
  - Note: Documentation needs manual review

### Issue #413: EDIT-05: Enhance comments and annotations system with inline annotations and threading

**Manual items to verify**: 1

- [ ] Documentation updated
  - Note: Documentation needs manual review

### Issue #412: EDIT-04: Improve visual diff viewer for version comparison

**Manual items to verify**: 1

- [ ] Documentation updated
  - Note: Documentation needs manual review

### Issue #411: EDIT-03: Add rich text editor (TipTap/Slate.js)

**Manual items to verify**: 2

- [ ] Documentation updated (component docs, usage guide)
  - Note: Documentation needs manual review
- [ ] Keyboard shortcuts documented in user guide
  - Note: Documentation needs manual review

### Issue #410: AI-11: Add response quality scoring and tracking

**Manual items to verify**: 1

- [ ] E2E test: verify dashboard displays quality metrics (ÔÜá´©Å Deferred with dashboard - AI-11.2)
  - Note: Requires manual testing/verification

### Issue #409: AI-09: Add multi-language embeddings (EN, IT, DE, FR, ES)

**Manual items to verify**: 5

- [ ] Update API documentation with language parameter
  - Note: Documentation needs manual review
- [ ] Add language support section to user guide
  - Note: Documentation needs manual review
- [ ] Documentation updated
  - Note: Documentation needs manual review
- [ ] API documentation includes language parameters
  - Note: Documentation needs manual review
- [ ] User guide updated with multi-language support section
  - Note: Documentation needs manual review

### Issue #407: AI-08: Add page number extraction for citations

**Manual items to verify**: 1

- [ ] E2E test verifying page numbers in UI ÔÅ©´©Å Frontend Phase 2
  - Note: Requires manual testing/verification

### Issue #406: AI-12: Add personalized search ranking algorithm

**Manual items to verify**: 1

- [ ] Documentation updated
  - Note: Documentation needs manual review

### Issue #404: CHAT-05: Add conversation export (PDF/TXT/Markdown)

**Manual items to verify**: 1

- [ ] Tested in staging environment (ÔÜá´©Å Manual testing needed)
  - Note: Requires manual testing/verification

### Issue #401: CHAT-02: Add AI-generated follow-up questions

**Manual items to verify**: 1

- [ ] Documentation updated
  - Note: Documentation needs manual review

### Issue #400: CHAT-04: Polish loading states and animations

**Manual items to verify**: 1

- [ ] Documentation updated
  - Note: Documentation needs manual review

### Issue #399: PDF-09: Add pre-upload PDF validation

**Manual items to verify**: 2

- [ ] Code coverage ÔëÑ90% for new code
  - Note: Coverage verification required: 90%
- [ ] XML documentation comments on public methods
  - Note: Documentation needs manual review

### Issue #398: PDF-08: Add granular progress tracking for PDF processing

**Manual items to verify**: 1

- [ ] Documentation updated (API docs, component docs)
  - Note: Documentation needs manual review

### Issue #397: PDF-07: Add PDF preview component with PDF.js

**Manual items to verify**: 1

- [ ] Documentation updated (inline comments, README if needed)
  - Note: Documentation needs manual review

### Issue #396: PDF-06: Improve error handling with user-friendly messages

**Manual items to verify**: 1

- [ ] Documentation updated
  - Note: Documentation needs manual review

### Issue #395: PDF-05: Add multi-file PDF upload support

**Manual items to verify**: 2

- [ ] Coverage maintains 90% threshold
  - Note: Coverage verification required: 90%
- [ ] Documentation updated
  - Note: Documentation needs manual review

### Issue #394: TEST-03: Increase frontend test coverage to 90%

**Manual items to verify**: 5

- [ ] Statement coverage ÔëÑ90% for all directories (`src/pages/`, `src/components/`, `src/lib/`)
  - Note: Coverage verification required: 90%
- [ ] Branch coverage ÔëÑ85% for all directories
  - Note: Coverage verification required: 85%
- [ ] Function coverage ÔëÑ85% for all directories
  - Note: Coverage verification required: 85%
- [ ] Line coverage ÔëÑ90% for all directories
  - Note: Coverage verification required: 90%
- [ ] Coverage thresholds met (90% statements, 85% branches/functions)
  - Note: Coverage verification required: 90%

### Issue #392: AUTH-04: Implement password reset flow with email verification

**Manual items to verify**: 1

- [ ] Documentation updated
  - Note: Documentation needs manual review

### Issue #380: Fix: Resolve 8 failing auth modal tests in index.test.tsx (framer-motion AnimatePresence)

**Manual items to verify**: 3

- [ ] Documentation updated (jest.setup.js comments)
  - Note: Documentation needs manual review
- [ ] Test coverage maintained at ÔëÑ90% for affected files
  - Note: Coverage verification required: 90%
- [ ] All auth modal user interactions testable via Testing Library queries
  - Note: Requires manual testing/verification

### Issue #379: Update index.test.tsx to match new landing page design

**Manual items to verify**: 1

- [ ] Code coverage remains ÔëÑ 90%
  - Note: Coverage verification required: 90%

### Issue #370: API-01 - API Foundation and Authentication Infrastructure

**Manual items to verify**: 5

- [ ] **OpenAPI/Swagger**: Swagger UI accessible at `/api/docs` in development
  - Note: Requires manual testing/verification
- [ ] **E2E Tests**: API documentation accessible, versioned endpoints work
  - Note: Documentation needs manual review
- [ ] **Code Comments**: Inline documentation for middleware and services
  - Note: Documentation needs manual review
- [ ] API documentation updated
  - Note: Documentation needs manual review
- [ ] Migration guide written
  - Note: Documentation needs manual review

### Issue #357: Refactor: Restructure MCP configuration to align with GUIDA_MCP_Codex.md

**Manual items to verify**: 1

- [ ] Migration guide created for developers
  - Note: Documentation needs manual review

### Issue #318: [Refactor] Convertire ChessDatasetTests a data-driven test per error reporting preciso

**Manual items to verify**: 1

- [ ] Test coverage rimane 100% per dataset loading logic
  - Note: Coverage verification required: 100%

### Issue #317: [Refactor] Suddividere AdminEndpointsIntegrationTests per area funzionale

**Manual items to verify**: 1

- [ ] Test coverage per admin endpoints rimane >= 85%
  - Note: Coverage verification required: 85%

### Issue #316: [Refactor] Split upload.test.tsx in file modulari per workflow step

**Manual items to verify**: 2

- [ ] Test coverage per `upload.tsx` rimane >= 90%
  - Note: Coverage verification required: 90%
- [ ] README test aggiornato con struttura nuova e convenzioni
  - Note: Documentation needs manual review

---

## Verification Instructions

For each item above:
1. Perform the manual test/verification described
2. Check the box if validation passes
3. If validation fails:
   - Document the failure in a comment on the original issue
   - Consider reopening the original issue if critical
   - Create new issue if fix requires significant work

## Timeline
Target completion: 2 weeks from creation

## Related
- DoD Verification Report: `docs/issue/dod-verification-report-*.md`
- Automated verification: `tools/verify-dod-implementation.py`
