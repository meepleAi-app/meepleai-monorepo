# Admin-Configurable Prompt Management System

## Executive Summary

Implement a database-driven, versioned, and auditable prompt management system that enables administrators to modify LLM prompts without code deployments.

**Key Benefits**:
- ⚡ **Faster Iteration**: Update prompts in minutes (not hours)
- 📚 **Version Control**: Full history with rollback capability
- ✅ **Quality Assurance**: Comprehensive testing framework prevents regressions
- 🚀 **Zero Downtime**: Redis-cached retrieval with instant updates
- 📊 **Auditability**: Complete change tracking

**Estimated Effort**: 5-6 weeks (200-240 hours)

---

## Problem Statement

### Current State (Pain Points)

1. **Hardcoded Prompts**: System prompts embedded in 4 service files:
   - `RagService.cs:111` - Q&A agent prompt
   - `StreamingQaService.cs:162` - Streaming Q&A prompt
   - `ChessAgentService.cs:137` - Chess agent prompt
   - `SetupGuideService.cs:108` - Setup guide prompt

2. **Slow Updates**: Prompt changes require code modification → PR → CI/CD → Deployment (2-4 hours)

3. **No Version History**: Cannot rollback or compare prompt versions

4. **No Testing Framework**: Quality regressions discovered in production

### Desired State

- **Database-Driven Prompts**: All prompts in PostgreSQL with Redis caching
- **Fast Updates**: Activate new prompt in < 30 seconds
- **Quality Assurance**: Test 50+ queries before activation with automated metrics
- **Zero Downtime**: Feature flag-based rollout with instant rollback

---

## Architecture Overview

**Selected Approach**: Redis-Cached Service Layer

```
Request → PromptTemplateService → Redis Cache → PostgreSQL → Return Prompt
```

**Performance Target**: < 10ms prompt retrieval (p95)

**Key Components**:
1. Backend: `IPromptTemplateService` with Redis caching + EF Core
2. Admin API: 10 endpoints under `/api/v1/admin/prompts/*`
3. Admin UI: 7 Next.js pages with Monaco editor
4. Testing Framework: `IPromptEvaluationService` with test datasets
5. Monitoring: Prometheus metrics + Grafana dashboards

---

## Implementation Phases

### Phase 1: Core Infrastructure (Week 1-2) - 80 hours

**Deliverables**:
- [ ] Implement `IPromptTemplateService` interface (12 methods)
- [ ] Implement `PromptTemplateService` with Redis caching
- [ ] Create 10 admin API endpoints (CRUD + activation + audit)
- [ ] Write 25+ unit tests + 10+ integration tests
- [ ] Add configuration + Swagger docs

**Critical Tasks**:
1. `GetActivePromptAsync` with cache-first strategy
2. `ActivateVersionAsync` with transaction + cache invalidation
3. Test concurrent activation scenarios

### Phase 2: Admin UI (Week 2-3) - 80 hours

**Deliverables**:
- [ ] Create 7 admin pages (list, detail, create, edit, versions, compare, audit)
- [ ] Integrate Monaco editor for prompt editing
- [ ] Build side-by-side diff viewer
- [ ] Write 20+ Jest component tests
- [ ] Write 5+ Playwright E2E tests

**Critical Tasks**:
1. Monaco editor integration
2. Version activation workflow
3. Diff viewer for version comparison

### Phase 3: Service Migration (Week 3-4) - 40 hours

**Deliverables**:
- [ ] Refactor 4 services to use `IPromptTemplateService`
- [ ] Implement feature flags for gradual rollout
- [ ] Create seed migration (migrate hardcoded prompts to DB)
- [ ] Update service tests (mock prompt service)

**Critical Tasks**:
1. Create seed script with exact current prompts
2. Refactor RagService (highest priority)
3. Add feature flag checks

### Phase 4: Prompt Testing Framework (Week 4-5) - 80 hours

**Deliverables**:
- [ ] Implement `IPromptEvaluationService`
- [ ] Create 4 test datasets (50+ queries each: Q&A, Chess, Setup, Streaming)
- [ ] Implement 5 evaluation metrics (accuracy, hallucination, confidence, citations, latency)
- [ ] Build admin UI for running tests and viewing reports
- [ ] Add CI/CD integration for automated testing

**Evaluation Metrics**:
1. **Accuracy** (> 80%): Keyword matching + behavior validation
2. **Hallucination Rate** (< 10%): Detects fabricated information
3. **Average Confidence** (> 0.70): RAG search confidence
4. **Citation Correctness** (> 0.80): Page number accuracy
5. **Average Latency** (< 3000ms): End-to-end response time

### Phase 5: Deployment (Week 5-6) - 40 hours

**Deliverables**:
- [ ] Create Grafana dashboards (cache metrics, prompt usage, quality trends)
- [ ] Write deployment guide + rollback procedures
- [ ] Gradual rollout (1 service per 48 hours)
- [ ] Admin training + documentation
- [ ] Final validation + retrospective

**Rollout Schedule**:
- Week 4 Day 1: Enable RagService → monitor 48h
- Week 4 Day 3: Enable ChessAgentService → monitor 48h
- Week 5 Day 1: Enable SetupGuideService → monitor 48h
- Week 5 Day 3: Enable StreamingQaService → monitor 1 week
- Week 6: Final validation + cleanup

---

## Success Criteria

**Functional**:
- [ ] Admin can create/update/activate prompts via UI
- [ ] All 4 services use DB-driven prompts (no hardcoded)
- [ ] Prompt changes are fully audited
- [ ] Rollback to previous version works

**Performance**:
- [ ] Cache hit rate > 95%
- [ ] Prompt retrieval latency < 10ms (p95)
- [ ] Cache availability 99.9%+

**Quality**:
- [ ] All prompts pass automated tests (100% pass rate)
- [ ] Test coverage > 90% (backend + frontend)
- [ ] No quality regressions detected

**Operational**:
- [ ] Zero downtime during rollout
- [ ] Rollback procedure tested (< 5 min recovery)
- [ ] Admin trained on UI
- [ ] Documentation complete

---

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| **Prompt Quality Regression** | HIGH | CRITICAL | Comprehensive testing framework with pass/fail gates |
| **Redis Unavailability** | LOW | HIGH | Automatic fallback to PostgreSQL |
| **Cache Consistency** | MEDIUM | LOW | Transaction before invalidation + 1h TTL |
| **Feature Flag Bug** | LOW | MEDIUM | Extensive integration tests + gradual rollout |
| **Test Dataset Quality** | MEDIUM | MEDIUM | Domain expert review + continuous refinement |

---

## Documentation

Comprehensive technical documentation created:

1. **Architecture**: `docs/technic/admin-prompt-management-architecture.md` (85 pages)
   - Requirements analysis, 3 architecture options, detailed component design
2. **Testing Framework**: `docs/technic/admin-prompt-testing-framework.md` (40 pages)
   - Test dataset format, evaluation metrics, A/B comparison, CI/CD integration
3. **Implementation Checklist**: `docs/issue/admin-01-prompt-management-implementation-checklist.md` (50+ tasks)
   - Task breakdown with estimates, dependencies, testing requirements
4. **Migration Plan**: `docs/guide/admin-prompt-management-migration-plan.md` (30 pages)
   - Zero-downtime deployment, rollback procedures, monitoring checklists
5. **Summary**: `docs/issue/admin-01-prompt-management-summary.md` (15 pages)
   - Executive summary with links to all related docs

**Total**: ~170 pages of production-ready architectural guidance

---

## Dependencies

**Prerequisites**:
- [x] Database schema migrated (`prompt_templates`, `prompt_versions`, `prompt_audit_logs`)
- [x] DTOs implemented (`PromptManagementDto.cs`)
- [x] Redis infrastructure operational
- [ ] Monaco editor npm package added

**Related Systems**:
- AI-06: RAG Evaluation (leverage existing metric infrastructure)
- OPS-02: OpenTelemetry (add prompt management metrics)
- AUTH-03: Session Management (reuse admin authorization patterns)

---

## Acceptance Criteria

**Code Complete**:
- [ ] All 50+ tasks in checklist marked complete
- [ ] Code review approved by lead engineer
- [ ] Test coverage > 90% (backend + frontend)
- [ ] All CI/CD checks passing

**Deployed to Production**:
- [ ] Feature flag enabled for all 4 services
- [ ] Cache metrics showing > 95% hit rate
- [ ] No incidents requiring rollback
- [ ] Admin successfully updated 3+ prompts

**Documentation Complete**:
- [ ] API documentation in Swagger
- [ ] Admin UI user guide created
- [ ] Deployment runbook validated
- [ ] Retrospective conducted

---

## Next Steps

1. **Review & Approval** (1-2 days):
   - [ ] Architecture review by lead engineer
   - [ ] Security review by security team
   - [ ] Product owner approval

2. **Project Setup** (1 day):
   - [ ] Add to GitHub project board
   - [ ] Assign 1-2 senior developers
   - [ ] Schedule kickoff meeting

3. **Begin Phase 1** (Week 1):
   - [ ] Implement `IPromptTemplateService`
   - [ ] Implement `PromptTemplateService` with Redis
   - [ ] Create admin API endpoints

---

**Related Issues**:
- Related to #433 (OpenTelemetry integration)
