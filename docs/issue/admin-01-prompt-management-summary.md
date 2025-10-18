# ADMIN-01: Admin-Configurable Prompt Management System

**Status**: Design Complete - Ready for Implementation
**Priority**: High
**Complexity**: High
**Estimated Effort**: 5-6 weeks (200-240 hours)
**Created**: 2025-10-18

---

## Executive Summary

This issue tracks the implementation of an admin-configurable prompt management system for MeepleAI. The system will replace hardcoded LLM prompts in 4 core services with a database-driven, versioned, and auditable solution that enables non-technical administrators to modify prompts without code deployments.

**Key Benefits**:
- **Faster Iteration**: Update prompts in minutes (not hours via deployment)
- **Version Control**: Full history with rollback capability
- **Quality Assurance**: Comprehensive testing framework prevents regressions
- **Zero Downtime**: Redis-cached retrieval with instant updates
- **Auditability**: Complete change tracking

---

## Problem Statement

### Current State (Pain Points)

1. **Hardcoded Prompts**: System prompts embedded in 4 service files
   - `RagService.cs` (line 111): Q&A agent prompt
   - `StreamingQaService.cs` (line 162): Streaming Q&A prompt
   - `ChessAgentService.cs` (line 137): Chess agent prompt
   - `SetupGuideService.cs` (line 108): Setup guide prompt

2. **Slow Updates**: Prompt changes require:
   - Code modification → PR review → CI/CD → Deployment (2-4 hours minimum)
   - Risky for urgent fixes (hallucination issues, quality improvements)

3. **No Version History**: Cannot rollback to previous prompts
   - Lost institutional knowledge (which prompts worked best)
   - Difficult to A/B test prompt variations

4. **No Testing Framework**: Quality regressions discovered in production
   - No automated way to test prompt changes before deployment
   - Risk of introducing hallucinations or incorrect answers

### Desired State

1. **Database-Driven Prompts**: All prompts stored in PostgreSQL
   - Admin UI for CRUD operations
   - Redis caching for < 10ms retrieval
   - Version history with full audit trail

2. **Fast Updates**: Activate new prompt version in < 30 seconds
   - No code deployment required
   - Instant cache invalidation

3. **Quality Assurance**: Comprehensive testing framework
   - Run 50+ test queries before activation
   - Metrics: accuracy, hallucination rate, confidence, latency
   - A/B comparison for data-driven decisions

4. **Zero Downtime**: Feature flag-based gradual rollout
   - Fallback to hardcoded prompts if issues
   - Rollback in < 5 minutes

---

## Architecture Overview

**Selected Approach**: Redis-Cached Service Layer

```
Request → PromptTemplateService.GetActivePromptAsync("qa-system-prompt")
          ↓
          Redis Cache (key: "prompt:qa-system-prompt:active")
          ↓ (cache miss)
          PostgreSQL → PromptVersion (active=true)
          ↓
          Write to Redis (TTL: 1 hour)
          ↓
          Return prompt → LLM API
```

**Key Components**:
1. **Backend**: `IPromptTemplateService` with Redis caching + EF Core
2. **Admin API**: 10 endpoints under `/api/v1/admin/prompts/*` (admin-only)
3. **Admin UI**: 7 Next.js pages with Monaco editor
4. **Testing Framework**: `IPromptEvaluationService` with test datasets
5. **Monitoring**: OpenTelemetry metrics + Grafana dashboards

**Technology Stack**:
- Backend: ASP.NET Core 9.0, EF Core, Redis, PostgreSQL
- Frontend: Next.js 14, React, Monaco Editor, shadcn/ui
- Testing: xUnit, Moq, Testcontainers, Jest, Playwright
- Observability: Prometheus, Grafana, Seq, Jaeger

---

## Implementation Phases

### Phase 1: Core Infrastructure (Week 1-2) - 80 hours

**Deliverables**:
- `IPromptTemplateService` interface (12 methods)
- `PromptTemplateService` implementation with Redis caching
- 10 admin API endpoints (CRUD + activation + audit)
- 25+ unit tests + 10+ integration tests
- Configuration + Swagger documentation

**Critical Path**:
1. Implement `GetActivePromptAsync` with cache-first strategy
2. Implement `ActivateVersionAsync` with transaction + cache invalidation
3. Test concurrent activation scenarios

### Phase 2: Admin UI (Week 2-3) - 80 hours

**Deliverables**:
- 7 admin pages (list, detail, create, edit, versions, compare, audit)
- Monaco editor integration for prompt editing
- Side-by-side diff viewer for version comparison
- 20+ Jest component tests
- 5+ Playwright E2E tests

**Critical Path**:
1. Monaco editor for version creation
2. Version activation workflow
3. Diff viewer for A/B comparison

### Phase 3: Service Migration (Week 3-4) - 40 hours

**Deliverables**:
- Refactor 4 services to use `IPromptTemplateService`
- Feature flags for gradual rollout
- Database seed script (migrate hardcoded prompts to DB)
- Updated service tests (mock prompt service)

**Critical Path**:
1. Create seed script with exact current prompts
2. Refactor RagService (highest priority)
3. Add feature flag checks

### Phase 4: Prompt Testing Framework (Week 4-5) - 80 hours

**Deliverables**:
- `IPromptEvaluationService` interface + implementation
- 4 test datasets (50+ queries each)
- Evaluation metrics: accuracy, hallucination rate, confidence, citations, latency
- Admin UI for running tests and viewing reports
- CI/CD integration for automated testing

**Critical Path**:
1. Create test dataset schema
2. Implement metric calculations
3. Build evaluation UI with progress tracking

### Phase 5: Deployment & Monitoring (Week 5-6) - 40 hours

**Deliverables**:
- OpenTelemetry metrics + Grafana dashboards
- Alert rules (cache hit rate, latency)
- Runbook documentation
- Admin training materials
- Gradual rollout plan (service-by-service)

**Critical Path**:
1. Configure monitoring dashboards
2. Test rollback procedures
3. Execute gradual rollout

---

## Related Documentation

1. **Architecture Design** (MUST READ):
   - `docs/technic/admin-prompt-management-architecture.md`
   - Complete system design with options analysis
   - Service interfaces and implementation details
   - Risk assessment and mitigation strategies

2. **Prompt Testing Framework** (MUST READ):
   - `docs/technic/admin-prompt-testing-framework.md`
   - Test dataset format and schema
   - Evaluation metrics definitions
   - A/B comparison methodology

3. **Implementation Checklist** (TASK TRACKER):
   - `docs/issue/admin-01-prompt-management-implementation-checklist.md`
   - 50+ tasks with complexity estimates
   - Acceptance criteria for each task
   - Testing requirements

4. **Migration Plan** (DEPLOYMENT GUIDE):
   - `docs/guide/admin-prompt-management-migration-plan.md`
   - Zero-downtime rollout strategy
   - Rollback procedures (< 5 minute recovery)
   - Monitoring and validation

---

## Success Criteria

### Functional Requirements (All must pass)

- [ ] Admin can create, edit, activate, and delete prompt templates via UI
- [ ] All 4 services (Q&A, Chess, Setup, Streaming Q&A) use database prompts
- [ ] Prompt activation completes in < 30 seconds (cache invalidation)
- [ ] Version history shows all changes with user attribution
- [ ] Audit log tracks all modifications
- [ ] Rollback to previous version works (< 5 minutes)

### Performance Requirements (All must pass)

- [ ] Prompt retrieval latency < 10ms (p95) with Redis cache
- [ ] Cache hit rate > 95% in production
- [ ] No increase in end-to-end Q&A latency
- [ ] Database load unchanged (prompts cached, not queried per request)

### Quality Requirements (All must pass)

- [ ] 90%+ code coverage for backend services
- [ ] 90%+ component coverage for frontend
- [ ] All 4 prompts have passing test results (accuracy > 80%, hallucination < 10%)
- [ ] Prompt testing framework operational (can run 50+ queries in < 5 minutes)
- [ ] Zero undetected quality regressions in production

### Operational Requirements (All must pass)

- [ ] Zero downtime during migration
- [ ] Monitoring dashboards deployed (cache hit rate, latency, activation frequency)
- [ ] Alert rules configured (cache miss rate, latency spikes)
- [ ] Runbook documentation complete
- [ ] Admin training completed (recorded session)
- [ ] On-call team briefed on rollback procedures

---

## Risk Assessment

### Critical Risks (Require Active Mitigation)

1. **Prompt Quality Regression** (Likelihood: High, Impact: Critical)
   - **Risk**: Admin activates prompt that degrades answer quality
   - **Mitigation**: Comprehensive testing framework with pass/fail gates
   - **Rollback**: < 5 minutes via admin UI or SQL

2. **Redis Unavailability** (Likelihood: Low, Impact: High)
   - **Risk**: Redis outage prevents prompt retrieval
   - **Mitigation**: Fallback to PostgreSQL query (automatic)
   - **Monitoring**: Circuit breaker pattern + health checks

3. **Cache Consistency Issues** (Likelihood: Medium, Impact: Low)
   - **Risk**: Short window (1-2s) where users get stale prompt
   - **Mitigation**: Transaction-based invalidation + 1h TTL
   - **Impact**: Minimal (1-2 second delay acceptable)

### Medium Risks (Monitor Closely)

4. **Performance Degradation** (Likelihood: Low, Impact: Medium)
   - **Risk**: Cache miss rate too high, DB load spike
   - **Mitigation**: Cache warming on startup + monitoring alerts

5. **Unauthorized Modifications** (Likelihood: Low, Impact: Critical)
   - **Risk**: Non-admin user gains access to prompt endpoints
   - **Mitigation**: Role-based authorization + audit logging

---

## Dependencies & Prerequisites

### Infrastructure (All must be available)

- [x] PostgreSQL database with prompt tables (migrated, ready)
- [x] Redis infrastructure (existing, production-ready)
- [ ] OpenRouter API key for prompt evaluation (testing only)
- [ ] Qdrant vector database (existing, for RAG testing)

### Code (All must be implemented)

- [ ] `IPromptTemplateService` interface + implementation
- [ ] 10 admin API endpoints
- [ ] 7 admin UI pages
- [ ] `IPromptEvaluationService` interface + implementation
- [ ] 4 refactored services with feature flags

### Data (All must be created)

- [ ] Database seed script with 4 prompts
- [ ] 4 test datasets (200+ queries total)
- [ ] Test thresholds configured

---

## Acceptance Criteria (Issue Closure)

This issue can be closed when:

1. **All 5 phases completed** (80 + 80 + 40 + 80 + 40 = 320 hours estimated)
2. **Feature flags enabled in production** (gradual rollout successful)
3. **All success criteria met** (functional, performance, quality, operational)
4. **1 week of production monitoring** (no incidents, metrics stable)
5. **Admin training completed** (recorded session, positive feedback)
6. **Runbook reviewed and approved** (on-call team ready)
7. **Post-migration retrospective conducted** (lessons learned documented)

---

## Next Steps

1. **Review & Approval**:
   - [ ] Architecture review (lead engineer)
   - [ ] Security review (security team)
   - [ ] Product owner approval (stakeholder)

2. **Project Setup**:
   - [ ] Create GitHub issue from this document
   - [ ] Add to project board
   - [ ] Assign developers (1-2 engineers)
   - [ ] Schedule kickoff meeting

3. **Begin Implementation**:
   - [ ] Start Phase 1: Core Infrastructure (Week 1)
   - [ ] Daily standups to track progress
   - [ ] Weekly reviews with stakeholders

---

## Questions & Clarifications

### Resolved

- **Q**: Do we need real-time A/B testing (split traffic)?
  - **A**: No, Phase 2 feature. Admin can compare versions before activation.

- **Q**: Should prompts support templating (e.g., `{{gameTitle}}`)?
  - **A**: Yes, simple string interpolation (Phase 2 enhancement).

- **Q**: Maximum prompt size limit?
  - **A**: 16KB (sufficient for complex system prompts).

### Open (Need Decision)

- None currently

---

## Related Issues

- None (this is the foundational issue for prompt management)

---

## Labels

- `enhancement`
- `admin`
- `high-priority`
- `backend`
- `frontend`
- `testing`
- `infrastructure`

---

## Assignees

- TBD (assign during project setup)

---

## Milestone

- **Target**: Q1 2026
- **Sprint**: TBD

---

## Comments

(Add updates, blockers, and progress notes here during implementation)

