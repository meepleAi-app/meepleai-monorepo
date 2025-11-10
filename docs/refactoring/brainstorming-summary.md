# Brainstorming Session Summary: DDD Refactoring Strategy

**Date**: 2025-11-10
**Participants**: Development Team, Architecture Lead
**Duration**: Full Analysis & Planning Session
**Status**: Planning Complete ✅

---

## Session Objectives ✅

✅ Identificare problematiche di organizzazione codice e test
✅ Analizzare complessità del codebase (file troppo lunghi)
✅ Progettare architettura Domain-Driven Design (DDD)
✅ Creare roadmap di refactoring con 6 fasi
✅ Definire bounded contexts con ubiquitous language
✅ Stimare timeline e risorse necessarie

---

## Key Findings: Complexity Analysis

### Backend C# Hotspots

**Services (Top 5 Longest)**:
1. **RagService.cs** - 995 lines 🔴
   - Multiple responsibilities: vector search, query expansion, RRF fusion, quality tracking
   - **Action**: Split into 5 domain services (200-300 lines each)

2. **PromptEvaluationService.cs** - 869 lines ⚠️
   - 5-metric engine with A/B testing
   - **Action**: Acceptable as-is (single responsibility, complex domain logic)

3. **ConfigurationService.cs** - 814 lines 🔴
   - 14 different operations (CRUD, bulk, import/export, rollback, validation)
   - **Action**: Split into 4 application services (150-250 lines each)

4. **PromptManagementService.cs** - 772 lines ⚠️
   - Version control, activation, caching
   - **Action**: Monitor for future splitting

5. **PdfStorageService.cs** - 740 lines ⚠️
   - Upload, storage, validation, metadata
   - **Action**: Acceptable for Phase 4 (single domain)

**Test Files (Top 5 Longest)**:
1. **PasswordResetServiceTests.cs** - 1,454 lines 🔴
2. **RagServiceTests.cs** - 1,364 lines 🔴
3. **LlmServiceTests.cs** - 1,180 lines 🔴
4. **WebApplicationFactoryFixture.cs** - 1,036 lines 🔴 (shared fixture)
5. **ApiKeyManagementServiceTests.cs** - 989 lines 🔴

**Action**: Split test files into 200-400 line focused suites (Phase 6)

### Frontend TypeScript Hotspots

**Components**:
- **ChatProvider.tsx** - 638 lines 🔴 (manages too many concerns)
- **AccessibleModal.tsx** - 328 lines ⚠️ (complex accessibility logic)
- **CategoryConfigTab.tsx** - 297 lines ⚠️

**Test Files**:
- **TimelineEventList.test.tsx** - 1,123 lines 🔴
- **AccessibleModal.test.tsx** - 718 lines ⚠️
- **TimelineEventItem.test.tsx** - 625 lines ⚠️

**Action**: Feature-based organization + split large test files

### Organizational Problems

1. **Flat Services Directory** ❌
   - 40+ services in single folder
   - No domain-based organization
   - Hard to navigate and find related services

2. **Flat Test Directory** ❌
   - 200+ test files at root level
   - No feature-based grouping
   - Difficult to identify coverage gaps

3. **God Services** ❌
   - Services with 700-1000 lines handling multiple concerns
   - High coupling, low cohesion

4. **God Test Files** ❌
   - Test files with 800-1400 lines
   - Hundreds of test cases in single file

---

## Proposed Solution: Domain-Driven Design (DDD)

### Architecture Transformation

**From (Current): Layered Architecture**
```
Services/          40+ services (flat)
Infrastructure/    All entities mixed
Models/            All DTOs mixed
Tests/             200+ tests (flat)
```

**To (Target): DDD Bounded Contexts**
```
BoundedContexts/
├── Authentication/         Auth domain (OAuth, 2FA, Sessions, API Keys)
├── DocumentProcessing/     PDF domain (Upload, Extraction, Validation)
├── KnowledgeBase/          RAG domain (Embeddings, Vector Search, LLM)
├── GameManagement/         Game domain (Games, RuleSpecs, Versions)
├── SystemConfiguration/    Config domain (Settings, Flags, Prompts)
├── Administration/         Admin domain (Users, Analytics, Alerts)
└── WorkflowIntegration/    Workflow domain (n8n Templates, Webhooks)

Each context:
├── Domain/            Entities, value objects, domain services
├── Application/       Commands, queries, DTOs, application services
├── Infrastructure/    Repositories, external integrations
└── Tests/             Domain, application, integration tests
```

### 7 Bounded Contexts Defined

1. **Authentication** - Core domain, high complexity
   - User, Session, ApiKey, OAuthAccount, 2FA, TOTP

2. **DocumentProcessing** - Supporting domain, high complexity
   - PdfDocument, Page, TextChunk, Table, OCR, Extraction

3. **KnowledgeBase** - Core domain, very high complexity
   - Embedding, Vector, Search, RAG, LLM, Citation, Confidence

4. **GameManagement** - Core domain, medium complexity
   - Game, RuleSpec, Version, Diff, Comment, Timeline

5. **SystemConfiguration** - Generic subdomain, medium complexity
   - Configuration, FeatureFlag, PromptTemplate, PromptVersion

6. **Administration** - Supporting domain, medium complexity
   - AdminUser, Alert, AuditLog, Statistic, Role, Permission

7. **WorkflowIntegration** - Integration domain, low complexity
   - WorkflowTemplate, WorkflowExecution, WorkflowError, n8n

---

## Migration Roadmap: 6 Phases (16 weeks)

### Phase 1: Foundation & Shared Kernel (2 weeks)
- Create SharedKernel with base domain classes
- Setup bounded context directory structure
- Zero risk (no existing code modified)

### Phase 2: Authentication Context (3 weeks)
- Proof-of-concept migration
- Dual-run mode (old + new implementations)
- Extensive integration testing

### Phase 3: KnowledgeBase Context (4 weeks)
- **Critical**: Split RagService (995 lines → 5 services)
- Complex RAG domain migration
- Performance validation required

### Phase 4: DocumentProcessing & GameManagement (3 weeks)
- Two contexts in parallel
- Medium complexity domains

### Phase 5: SystemConfiguration & Administration (2 weeks)
- **Critical**: Split ConfigurationService (814 lines → 4 services)
- Configuration and admin domains

### Phase 6: WorkflowIntegration & Test Reorganization (2 weeks)
- Final context migration
- **Critical**: Split all large test files (1000+ lines → 200-400 lines)
- Reorganize tests by bounded context

**Total**: 16 weeks (1 developer full-time) or 10-12 weeks (2 developers)

---

## Benefits & Impact

### Complexity Reduction
- **Services**: 40+ flat services → 7 organized bounded contexts
- **File Sizes**: 700-1000 line services → 200-400 line focused modules
- **Test Files**: 1000-1400 line tests → 200-400 line feature suites
- **Navigation**: Find related code by bounded context instead of searching 40+ files

### Testability Improvements
- **Isolated Tests**: Test domain logic independently from infrastructure
- **Faster Tests**: Unit tests run in-memory, integration tests use Testcontainers
- **Maintainable Tests**: 200-400 line test files easier to read and update
- **Clear Coverage**: Coverage per bounded context visible

### Parallel Development
- **Team Scaling**: Multiple teams can work on different bounded contexts
- **Reduced Conflicts**: Fewer merge conflicts (separate directories)
- **Clear Ownership**: Each team owns specific bounded contexts
- **Independent Deployment**: Future microservices extraction easier

### Maintainability
- **Bounded Contexts**: Clear boundaries reduce coupling
- **Domain Language**: Ubiquitous language in each context
- **Single Responsibility**: Each service has one clear purpose
- **Explicit Dependencies**: Dependencies visible via constructor injection

---

## Risk Mitigation Strategy

### Risk 1: Breaking Changes
**Mitigation**:
- Dual-run mode (old + new implementations active)
- Feature flags to switch between old/new
- Extensive integration tests
- Gradual rollout per bounded context

### Risk 2: Performance Regression
**Mitigation**:
- Performance benchmarks before/after each phase
- Target: <5% regression in P95 latency
- Prometheus metrics for real-time monitoring
- Rollback plan if regression detected

### Risk 3: Test Coverage Drops
**Mitigation**:
- Enforce 90% coverage in CI pipeline
- Write tests before migration (TDD approach)
- Test reorganization in final phase

### Risk 4: Team Confusion
**Mitigation**:
- Comprehensive migration guide documents
- Phase-by-phase documentation
- Knowledge transfer sessions
- Clear communication about old vs new code

### Risk 5: Extended Timeline (16 weeks)
**Mitigation**:
- Incremental delivery (value after each phase)
- Parallel migration where possible
- Prioritize high-impact contexts first
- Pause regular feature development during refactoring

---

## Success Metrics

### Quantitative (Measurable)
- ✅ Average service file size: 700 lines → 300 lines
- ✅ Average test file size: 800 lines → 300 lines
- ✅ Test coverage: Maintain 90%+ throughout
- ✅ Performance: <5% regression in P95 latency
- ✅ Build time: CI pipeline <20 minutes
- ✅ Code duplication: Reduced by 30%

### Qualitative (Survey)
- ✅ Developer satisfaction: 8/10 or higher
- ✅ Onboarding time: 50% reduction
- ✅ Code review speed: Faster reviews
- ✅ Bug rate: Fewer production bugs

---

## Key Decisions Made

### Decision 1: DDD over Vertical Slice Architecture
**Rationale**: Project has clear domain boundaries (Authentication, RAG, Games), DDD provides better long-term structure

### Decision 2: 7 Bounded Contexts (Not More)
**Rationale**: Balance between granularity and maintainability. Too many contexts increases cognitive load.

### Decision 3: Split RagService First (Phase 3)
**Rationale**: 995 lines, critical RAG domain, high complexity. Splitting demonstrates biggest value.

### Decision 4: Test Reorganization in Final Phase (Phase 6)
**Rationale**: Avoid disrupting test execution during migration. Reorganize after all contexts migrated.

### Decision 5: Dual-Run Mode with Feature Flags
**Rationale**: Zero-downtime migration. Old and new implementations run side-by-side with feature flag control.

### Decision 6: 16-Week Timeline (Single Developer)
**Rationale**: Conservative estimate accounting for testing, integration, documentation. 2 developers = 10-12 weeks.

---

## Deliverables Created

1. ✅ **DDD Architecture Plan** (`docs/refactoring/ddd-architecture-plan.md`)
   - Complete bounded context definitions
   - Ubiquitous language per context
   - Domain events, aggregates, value objects
   - Migration strategy with 6 phases

2. ✅ **Implementation Checklist** (`docs/refactoring/implementation-checklist.md`)
   - Detailed task breakdown for all 6 phases
   - Week-by-week execution plan
   - Success criteria and rollback procedures

3. ✅ **Complexity Analysis** (Serena Memory: `complexity_analysis.md`)
   - Top 10 longest services and test files
   - Organizational problems identified
   - Refactoring priority recommendations

4. ✅ **Project Overview** (Serena Memory: `project_overview.md`)
   - Tech stack, architecture, domain areas
   - Current structure and key features

5. ✅ **Code Style Conventions** (Serena Memory: `code_style_conventions.md`)
   - C# and TypeScript naming conventions
   - Service, endpoint, and testing patterns
   - IDisposable best practices (CODE-01)

6. ✅ **Suggested Commands** (Serena Memory: `suggested_commands.md`)
   - Build, test, migration, Docker commands
   - Git workflow, utility scripts
   - Windows-specific commands

---

## Next Steps (Immediate Actions)

### 1. Stakeholder Review (This Week)
- [ ] Present DDD architecture plan to stakeholders
- [ ] Review 7 bounded contexts and naming
- [ ] Approve 16-week timeline
- [ ] Allocate resources (1-2 developers)

### 2. Setup Project Tracking (Week 1)
- [ ] Create GitHub Project for refactoring
- [ ] Create issues for all 6 phases
- [ ] Setup phase-specific milestones
- [ ] Configure GitHub labels (bounded contexts, phases)

### 3. Setup Monitoring (Week 1)
- [ ] Prometheus dashboards for performance tracking
- [ ] Grafana alerts for regressions
- [ ] Baseline metrics capture (current performance)

### 4. Phase 1 Kickoff (Week 2)
- [ ] Start SharedKernel implementation
- [ ] Create bounded context directory structure
- [ ] Setup MediatR for CQRS
- [ ] First weekly sync meeting

### 5. Documentation & Communication
- [ ] Announce refactoring plan to team
- [ ] Schedule weekly sync meetings (Fridays)
- [ ] Create Slack channel: #ddd-refactoring
- [ ] Share migration guide with team

---

## Open Questions for Discussion

1. **Timeline Adjustment**: Can we allocate 2 developers to reduce timeline to 10-12 weeks?

2. **Feature Freeze**: Should we pause all new feature development during refactoring?

3. **Frontend Parallel Track**: Should frontend refactoring happen in parallel or after backend?

4. **Testing Strategy**: Should we write new tests first (TDD) or migrate tests after code?

5. **Rollout Strategy**: Gradual rollout per bounded context or wait until all contexts done?

---

## Recommendations

### High Priority
1. ✅ **Approve DDD Architecture Plan**: Begin Phase 1 immediately
2. ✅ **Allocate 2 Developers**: Reduce timeline to 10-12 weeks
3. ✅ **Feature Freeze**: Pause new features during refactoring (avoid conflicts)
4. ✅ **TDD Approach**: Write tests before migrating code (maintain coverage)

### Medium Priority
1. ⚠️ **Frontend Refactoring**: Start after Phase 3 (parallel track)
2. ⚠️ **CI Pipeline Update**: Configure test execution per bounded context
3. ⚠️ **Documentation Sprint**: Update all docs after Phase 6

### Low Priority (Future)
1. 🔵 **Microservices Extraction**: After DDD refactoring complete
2. 🔵 **Event-Driven Architecture**: Introduce domain events for cross-context communication
3. 🔵 **CQRS Patterns**: Separate read/write models for complex domains

---

## Session Outcome

✅ **Brainstorming Complete**
✅ **DDD Architecture Designed**
✅ **6-Phase Roadmap Created**
✅ **Implementation Checklist Ready**
✅ **Deliverables Generated**
✅ **Stakeholder Review Scheduled**

**Status**: Ready for Phase 1 kickoff pending stakeholder approval

---

## Appendix: File Size Analysis

### Backend Services (Top 20)
```
995 lines - RagService.cs                    🔴 Split into 5 services
869 lines - PromptEvaluationService.cs       ⚠️ Monitor
814 lines - ConfigurationService.cs          🔴 Split into 4 services
772 lines - PromptManagementService.cs       ⚠️ Monitor
740 lines - PdfStorageService.cs             ⚠️ Acceptable (single domain)
697 lines - LlmService.cs                    ✅ Acceptable
```

### Backend Tests (Top 20)
```
1454 lines - PasswordResetServiceTests.cs    🔴 Split into 4 files
1364 lines - RagServiceTests.cs              🔴 Split by bounded context (6 files)
1180 lines - LlmServiceTests.cs              🔴 Split by scenario (3 files)
1036 lines - WebApplicationFactoryFixture.cs 🔴 Extract to Shared/Fixtures/
 989 lines - ApiKeyManagementServiceTests.cs ⚠️ Split into 3 files
```

### Frontend Components (Top 10)
```
638 lines - chat/ChatProvider.tsx            🔴 Split into 3 components
328 lines - accessible/AccessibleModal.tsx   ⚠️ Monitor
297 lines - admin/CategoryConfigTab.tsx      ⚠️ Acceptable
```

### Frontend Tests (Top 10)
```
1123 lines - timeline/__tests__/TimelineEventList.test.tsx  🔴 Split into 3 files
 718 lines - accessible/__tests__/AccessibleModal.test.tsx  ⚠️ Split into 2 files
 625 lines - timeline/__tests__/TimelineEventItem.test.tsx  ⚠️ Split into 2 files
```

**Legend**:
- 🔴 Critical (>800 lines) - Split immediately
- ⚠️ Warning (500-800 lines) - Monitor or split
- ✅ Acceptable (<500 lines) - No action needed

---

**Document Owner**: Architecture Lead
**Last Updated**: 2025-11-10
**Next Review**: After Phase 1 completion
