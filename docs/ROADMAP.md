# MeepleAI Roadmap 2026

**Last Updated**: 2026-02-02
**Planning Horizon**: Q1-Q3 2026
**Active Epics**: 12
**Total Story Points**: ~240 SP

---

## 🎯 Executive Summary

### Strategic Focus

**Q1 2026 (Feb-Apr)**: Foundation & Critical User Flows
- Complete Admin & User critical journeys (85% → 100%)
- Security baseline & quota enforcement
- Core UI component system (MeepleCard)
- Architecture refactoring (Tier-Strategy-Model)

**Q2 2026 (May-Jul)**: Advanced Features & Scalability
- Plugin-based RAG architecture
- Multi-Agent AI system (Tutor, Arbitro, Decisore)
- Infrastructure observability & load testing
- UX enhancements (Navigation, Mobile)

**Q3 2026+ (Aug+)**: Innovation & Expansion
- Visual RAG strategy builder
- Advanced RAG strategies (9 variants)
- Game session advanced toolkit
- AI collaboration features

---

### Progress Snapshot (Feb 2, 2026)

**Flussi Critici Status**:
- **FLUSSO 1 ADMIN**: ✅ 100% (6/6 issues complete)
- **FLUSSO 2 USER**: 🔴 71% (5/7 issues complete)
- **Overall**: 🟡 85% (11/13 issues complete)

**Active Development**:
- In Progress: 2 critical epics (23 SP remaining)
- Blocked: 2 epics (80+ SP) waiting for #3434
- Ready to Start: 4 epics (~75 SP)

**Recent Achievements**:
- ✅ Dashboard Hub foundation (6 components)
- ✅ SSE Infrastructure (#3324)
- ✅ Admin Game Publication workflow (#3480, #3481, #3488)
- ✅ UserLibrary PDF association (#3489)
- ✅ Tier-Strategy-Model architecture (62% complete)

---

## 🎯 LISTA SOMMARIO - Issue Aperte da Chiudere

**Priorità**: Flussi critici User & Admin - Rimangono 7 issue (29 SP)

### ✅ COMPLETATO - Foundation & Admin Flow (100%)
- ✅ #3324 SSE Infrastructure (5 SP, Backend)
- ✅ #3370 usePdfProcessingProgress hook (2 SP, Frontend)
- ✅ #3480 Admin Wizard - Publish to Shared Library (3 SP, Frontend)
- ✅ #3481 SharedGameCatalog Publication Workflow (5 SP, Backend)
- ✅ #3488 Game Approval Status UI (2 SP, Frontend)
- ✅ #3489 UserLibraryEntry PDF Association (3 SP, Backend)

**Totale completato**: 6 issue, 20 SP | **FLUSSO 1 ADMIN**: 100% ✅

---

### 🔴 PRIORITÀ 1 - User Collection Flow (3 issue, 13 SP)

**Parallelizzazione**: 2 frontend + 1 backend concorrenti

| # | Issue | Area | SP | Tipo | Dipendenze |
|:---:|-------|------|:--:|------|------------|
| 1 | x#3476 User Collection Dashboard | Frontend | 5 | Dashboard UI | Nessuna |
| 2 | #3477 Add Game to Collection Wizard | Frontend | 5 | Wizard multi-step | Nessuna |
| 3 | #3479 Private PDF Upload Endpoint | Backend | 3 | API endpoint | Nessuna |

**Obiettivo**: User può gestire collezione personale e caricare PDF privati
**Timeline**: ~5-7 giorni con parallelizzazione
**Blocca**: Chat History (#3483, #3484)

---

### 🟡 PRIORITÀ 2 - Agent Creation Foundation (2 issue, 8 SP)

**Parallelizzazione**: 2 frontend concorrenti

| # | Issue | Area | SP | Tipo | Dipendenze |
|:---:|-------|------|:--:|------|------------|
| 4 | #3376 Agent Creation Wizard | Frontend | 5 | Wizard UI | Nessuna |
| 5 | #3375 Agent Session Launch | Frontend | 3 | API Integration | Nessuna |

**Obiettivo**: Admin può creare agenti e lanciare sessioni di test
**Timeline**: ~5-7 giorni con parallelizzazione
**Blocca**: Agent Testing (#3378, #3379)

---

### 🟢 PRIORITÀ 3 - Chat History Integration (2 issue, 8 SP)

**Parallelizzazione**: Backend + Frontend concorrenti
**Dipendenze**: Richiede #3476, #3477, #3479 completate

| # | Issue | Area | SP | Tipo | Dipendenze |
|:---:|-------|------|:--:|------|------------|
| 6 | #3483 Chat Session Persistence Service | Backend | 5 | Service + DB | #3476, #3477 |
| 7 | #3484 Chat History Integration | Frontend | 3 | UI Component | #3483 |

**Obiettivo**: Chat utente salvate automaticamente con history persistente
**Timeline**: ~4-6 giorni con parallelizzazione
**Completa**: FLUSSO 2 USER al 100%

---

## 📊 Statistiche Sequenza

### Progress Overview
| **Fase** | **Issue** | **SP** | **Status** | **Timeline** |
|----------|-----------|--------|------------|--------------|
| Week 1 - Foundation | 2 | 7 | ✅ Completato | - |
| Stream A - Admin Flow | 4 | 13 | ✅ Completato | - |
| **Stream B - User Collection** | **3** | **13** | 🔴 In Progress | ~5-7 giorni |
| **Stream C - Agent Creation** | **2** | **8** | 🟡 Ready | ~5-7 giorni |
| **Week 3 - Chat History** | **2** | **8** | 🟢 Waiting | ~4-6 giorni |
| **TOTALE RIMANENTE** | **7** | **29** | **71% completato** | **~2-3 settimane** |

### Parallelizzazione Strategy
**Week Current - Parallel Execution**:
- Team Frontend User: #3476 (Dashboard) + #3477 (Wizard) → ~5-7 giorni
- Team Frontend Agent: #3376 (Wizard) + #3375 (Launch) → ~5-7 giorni
- Team Backend: #3479 (Private PDF Endpoint) → ~3 giorni

**Efficiency**: 3 stream paralleli → ~7 giorni (vs ~21 giorni sequential)
**Time Saved**: ~14 giorni (~2 settimane, ~67% faster)

---

## 📅 Q1 2026 - Foundation & Critical Flows

### February 2026 - Flussi Critici & Architecture

#### Week 1-2 (Feb 2-16): Critical Flows Completion 🔴 ACTIVE

**Epic #3475**: User Private Library & Collections Management (13 SP)
**Epic #3386**: Agent Creation & Testing Flow - Partial (16 SP)

**Parallel Streams** (3 team concorrenti):

**Stream B - User Collection**:
- #3476 User Collection Dashboard (5 SP, Frontend)
- #3477 Add Game to Collection Wizard (5 SP, Frontend)
- #3479 Private PDF Upload Endpoint (3 SP, Backend)

**Stream C - Agent Foundation**:
- #3376 Agent Creation Wizard (5 SP, Frontend)
- #3375 Agent Session Launch (3 SP, Frontend)

**Stream D - Chat History**:
- #3483 Chat Session Persistence Service (5 SP, Backend)
- #3484 Chat History Integration (3 SP, Frontend)
- Dependencies: Requires Stream B complete

**Timeline**: ~7-10 giorni con parallelizzazione
**Milestone**: FLUSSO 2 USER 100% complete ✅

**Business Value**:
- Users manage personal collections
- Private PDF upload for personalized AI
- Chat history persisted and accessible
- Complete end-to-end user journey

---

#### Week 3-4 (Feb 17-28): Architecture Refactoring

**Epic #3434**: Tier-Strategy-Model Architecture (10 SP)

**Remaining Tasks**:
- #3437 StrategyModelMapping configuration (3 SP, Backend)
- #3440 Admin UI for tier-strategy matrix (3 SP, Frontend)
- #3441 Comprehensive test suite (2 SP, Testing)
- #3442 API documentation update (2 SP, Docs)

**Timeline**: 1-2 weeks
**Milestone**: Epic #3434 100% → Unblocks Epic #3413 (80 SP)

**Business Value**:
- Flexible strategy-to-model mapping
- Admin control over AI behavior
- Foundation for plugin-based RAG
- Scalable architecture

**Critical**: BLOCKS 80+ SP of Plugin-Based RAG work

---

### March 2026 - Security & Components

#### Week 1-2 (Mar 1-14): Security Baseline

**Epic #3327**: Security & Quota Enforcement (27 SP)

**Priority 1 - Security Basics** (13 SP):
- #3330 Session Limits Enforcement (5 SP)
- #3332 Email Verification (5 SP)
- #3339 Account Lockout (3 SP)

**Priority 2 - Resource Control** (8 SP):
- #3335 Feature Flags for Tier Access (5 SP)
- #3333 PDF Upload Limits Configuration (3 SP)

**Priority 3 - Monitoring** (8 SP):
- #3340 Login Device Tracking (3 SP)
- #3338 AI Token Usage Tracking (5 SP)

**Timeline**: 3-4 weeks
**Milestone**: Production-ready security ✅

**Business Value**:
- Security hardening for production
- Abuse prevention
- Resource quota management
- Cost control

---

#### Week 3-4 (Mar 15-31): Component System

**Epic #3325**: MeepleCard - Universal Card System (15 SP)

**Phases**:
1. Core component (#3326)
2. Testing & Storybook (#3328, #3329)
3. Integration (#3334)
4. Migration from GameCard (#3331)
5. Documentation (#3336)

**Timeline**: 2-3 weeks
**Milestone**: Unified card system ✅

**Business Value**:
- Consistent UI/UX
- WCAG 2.1 Level AA compliance
- Reduced maintenance
- Design system foundation

---

### April 2026 - UX & Infrastructure

#### Week 1 (Apr 1-7): Navigation Redesign

**Epic #3403**: RAG Dashboard Navigation (12 SP)

**Components**:
- #3404 useScrollSpy hook
- #3405 DashboardSidebar (desktop)
- #3406 DashboardNav (mobile)
- #3407 SectionGroup wrapper
- #3408 ProgressIndicator
- #3409 Integration
- #3410 Tests

**Timeline**: 1-2 weeks
**Milestone**: Enhanced RAG Dashboard UX ✅

---

#### Week 2 (Apr 8-14): Infrastructure

**Epic #3366**: Infrastructure Enhancements (8 SP)

**Deliverables**:
- #3367 Log Aggregation System (5 SP)
- #3368 k6 Load Testing Infrastructure (3 SP)

**Timeline**: 1-2 weeks
**Milestone**: Production observability ✅

---

#### Week 3-4 (Apr 15-30): Game Session Toolkit

**Epic #3341**: Game Session Phase 2 (21 SP)

**Tools**:
- #3342 Dice Roller (3 SP)
- #3344 Private Notes (3 SP)
- #3345 Timer/Coin/Wheel (2 SP)
- #3343 Card Deck System (5 SP)
- #3347 Session Sharing (3 SP)
- #3346 Offline-First PWA (5 SP)

**Timeline**: 3 weeks
**Milestone**: Complete game session toolkit ✅

---

## 📅 Q2 2026 - Advanced Features & Architecture

### May-June 2026 - Plugin-Based RAG

#### Epic #3413: Plugin-Based RAG Pipeline (80 SP, 8-10 weeks)
**Dependencies**: ⚠️ **BLOCKED** until Epic #3434 complete

**Phase 1: Backend Foundation** (4 weeks, 45 SP)
- Plugin contracts and interfaces
- DAG orchestrator engine
- Pipeline definition schema
- Plugin registry service
- 7 plugin categories:
  - Routing (query classification)
  - Cache (semantic + exact)
  - Retrieval (vector, hybrid, multi-source)
  - Evaluation (CRAG, confidence)
  - Generation (LLM integration)
  - Validation (quality assurance)
  - Transform/Filter (data processing)

**Phase 2: Frontend Builder** (3 weeks, 30 SP)
- Visual pipeline builder (ReactFlow)
- Plugin palette with search
- Node configuration panels
- Edge configuration panels
- Pipeline preview and testing

**Phase 3: Testing & Docs** (1 week, 5 SP)
- Plugin testing framework
- Documentation and guides

**Timeline**: 8-10 weeks (earliest start: Early May)
**Milestone**: Configurable RAG pipelines ✅

**Business Value**:
- Admin configures RAG without deployments
- Rapid AI experimentation
- Foundation for visual tools
- Competitive differentiation

---

### July 2026 - Multi-Agent System

#### Epic #3490: Multi-Agent Game AI System (TBD SP)
**Dependencies**: Requires Plugin RAG foundation

**Agents & Components**:
- #3491 Context Engineering Framework
- #3492 Hybrid Search (Keyword + Vector + Reranking)
- #3493 PostgreSQL Schema Extensions
- #3494 Redis 3-Tier Cache Layer
- #3495 LangGraph Orchestrator Foundation
- #3496 Intent Classification System
- #3497 Multi-Turn Dialogue State Machine
- #3498 Conversation Memory - Temporal RAG
- #3499 Tutor Agent REST API Endpoint
- #3501 Beta Testing & Feedback
- #3502 Hybrid Search Integration

**Scope**: 11 issues, high complexity
**Timeline**: 6-8 weeks
**Milestone**: Multi-Agent AI operational ✅

---

#### Epic #3348: Advanced Features (22 SP)

**Admin Tools**:
- #3349 User Impersonation (3 SP)
- #3350 Batch Approval (2 SP)

**AI Enhancements**:
- #3351 Voice-to-Text (3 SP)
- #3352 Feedback System (3 SP)
- #3353 Similar Games RAG (5 SP)

**Collaboration**:
- #3354 Session Invite Links (3 SP)
- #3355 Version History (3 SP)

**Timeline**: 3 weeks

---

## 📅 Q3 2026+ - Innovation & Expansion

### August+ 2026 - Visual Strategy Builder

#### Epic #3412: Custom Strategy Builder
**Dependencies**: ⚠️ Requires Epic #3413 complete

**Scope**: Visual drag-drop RAG strategy designer
- 23 building blocks (Tier 1-4)
- ReactFlow canvas infrastructure
- Block palette with categorization
- Connection validation
- Parameter configuration
- Strategy validation engine
- Live testing with SSE
- Save/load/export strategies
- Strategy templates library

**Timeline**: 4-6 weeks (earliest start: July 2026)

**Business Value**:
- Non-technical users design RAG strategies
- Visual experimentation
- Shareable templates
- Democratized AI configuration

---

### September+ 2026 - Advanced RAG

#### Epic #3356: Advanced RAG Strategies (24 SP)

**Simple Variants** (9 SP):
- #3357 Sentence Window RAG (3 SP)
- #3360 Step-Back Prompting (3 SP)
- #3361 Query Expansion (3 SP)

**Complex Variants** (15 SP):
- #3358 Iterative RAG (5 SP)
- #3359 Multi-Agent RAG (5 SP)
- #3363 RAG-Fusion (5 SP)

**Cleanup**: Close duplicates #3362, #3364, #3365

**Timeline**: 3-4 weeks

**Business Value**:
- Best-in-class RAG performance
- Specialized strategies per use case
- Research-backed methodologies
- Competitive differentiation

---

## 🔗 Critical Path & Dependencies

### Blocker Chain (CRITICAL)

```
Epic #3434: Tier-Strategy-Model (In Progress, 62%)
  │
  └─► UNBLOCKS → Epic #3413: Plugin-Based RAG (80 SP)
                   │
                   └─► UNBLOCKS → Epic #3412: Visual Strategy Builder
                                   │
                                   └─► ENABLES → Epic #3490: Multi-Agent System
```

**Impact Analysis**:
- Epic #3413 blocked: 80 SP (~8-10 weeks)
- Epic #3412 blocked: TBD SP (~4-6 weeks)
- Total blocked: 100+ SP (~3-4 months)

**Mitigation**:
- Prioritize #3434 completion (target: Feb 16, 2026)
- Allocate senior developer
- Weekly progress tracking
- Parallel work on independent epics (#3327, #3325, #3403)

---

### Parallel Execution Opportunities

#### Q1 Independent Streams
```
Stream 1: Flussi Critici (#3475, #3386) → 29 SP
Stream 2: Architecture (#3434) → 10 SP
Stream 3: Security (#3327) → 27 SP [After Flussi]
Stream 4: MeepleCard (#3325) → 15 SP [After Security]

Total Q1: ~81 SP
Strategy: Stream 1 || Stream 2, then Stream 3, then Stream 4
```

#### Q2 Sequential Constraints
```
#3434 Complete → #3413 Start (MANDATORY)
#3413 Complete → #3412 Start (MANDATORY)
#3413 Complete → #3490 Start (MANDATORY)
#3348 Independent (can run parallel)
```

---

## 🎯 Milestone Definitions

### M1: Flussi Critici Complete (Target: Feb 16, 2026)

**Completion Criteria**:
- ✅ FLUSSO 1 ADMIN: 100% (already achieved)
- ✅ FLUSSO 2 USER: 100% (7 issues to close)
- ✅ E2E tests passing
- ✅ Performance targets met

**Remaining Work**: 7 issues, 29 SP

**Business Impact**:
- Core platform value delivered
- Admin can publish to shared catalog
- Users manage collections + private PDFs
- Chat history persisted

---

### M2: Security Baseline (Target: Mar 15, 2026)

**Completion Criteria**:
- ✅ Epic #3327 complete (27 SP)
- ✅ Session limits enforced
- ✅ Email verification active
- ✅ Account lockout implemented
- ✅ Feature flags operational

**Business Impact**:
- Production-ready security
- Abuse prevention
- Resource quota management
- Cost control

---

### M3: Component System Unified (Target: Apr 1, 2026)

**Completion Criteria**:
- ✅ Epic #3325 complete (15 SP)
- ✅ MeepleCard implemented
- ✅ All dashboards migrated
- ✅ Accessibility verified

**Business Impact**:
- Unified UI/UX design system
- WCAG 2.1 Level AA compliance
- Improved maintainability
- Professional interface

---

### M4: Plugin Architecture Live (Target: Jun 30, 2026)

**Completion Criteria**:
- ✅ Epic #3434 complete (prerequisite)
- ✅ Epic #3413 complete (80 SP)
- ✅ 7 plugin categories operational
- ✅ Visual pipeline builder functional
- ✅ Admin can configure RAG

**Business Impact**:
- Configurable RAG without code changes
- Rapid AI experimentation
- Admin-controlled AI behavior
- Foundation for visual tools

**Dependencies**: BLOCKED until Feb 16, 2026

---

### M5: Multi-Agent System (Target: Aug 31, 2026)

**Completion Criteria**:
- ✅ Epic #3490 complete (11 issues)
- ✅ 3 agents operational (Tutor, Arbitro, Decisore)
- ✅ LangGraph orchestration working
- ✅ Hybrid search integrated
- ✅ Conversation memory functional

**Business Impact**:
- Advanced AI conversational capabilities
- Multi-agent orchestration
- Enhanced user experience
- Competitive AI differentiation

---

## 📊 Epic Status Dashboard

### 🔴 Critical Priority (3 epics, ~52 SP)

| Epic | Status | Issues Open | SP | Timeline | Blockers |
|------|--------|-------------|----|---------:|----------|
| #3475 User Private Library | 🔴 In Progress | 3 | 13 | ~1 week | None |
| #3386 Agent Creation (Partial) | 🟡 Ready | 4 | 16 | ~2 weeks | None |
| #3434 Tier-Strategy-Model | 🔵 In Progress | 3 | 10 | 1-2 weeks | None |

---

### 🟡 High Priority (4 epics, ~75 SP)

| Epic | Status | Issues Open | SP | Timeline | Blockers |
|------|--------|-------------|----|---------:|----------|
| #3327 Security & Quota | ⏸️ Not Started | 7 | 27 | 3-4 weeks | None |
| #3325 MeepleCard | ⏸️ Ready | 6 | 15 | 2-3 weeks | None |
| #3403 Navigation Redesign | ⏸️ Not Started | 7 | 12 | 1-2 weeks | None |
| #3366 Infrastructure | ⏸️ Not Started | 2 | 8 | 1-2 weeks | None |

---

### 🟢 Medium Priority (3 epics, ~65 SP)

| Epic | Status | Issues Open | SP | Timeline | Blockers |
|------|--------|-------------|----|---------:|----------|
| #3341 Game Session Phase 2 | ⏸️ Future | 6 | 21 | 3 weeks | None |
| #3348 Advanced Features | ⏸️ Future | 7 | 22 | 3 weeks | None |
| #3356 Advanced RAG Strategies | ⏸️ Future | 6 | 24 | 3-4 weeks | None |

---

### ⏸️ Blocked Epics (2 epics, 80+ SP)

| Epic | Status | Dependencies | SP | Timeline | Est. Start |
|------|--------|--------------|----|---------:|------------|
| #3413 Plugin-Based RAG | ⚠️ BLOCKED | Epic #3434 | 80 | 8-10 weeks | May 2026 |
| #3412 Visual Strategy Builder | ⚠️ BLOCKED | Epic #3413 | TBD | 4-6 weeks | Jul 2026 |

---

### 🚀 Future Epics (1 epic)

| Epic | Dependencies | Timeline |
|------|--------------|----------|
| #3490 Multi-Agent System | Epic #3413 | Aug-Sep 2026 |

---

## 📈 Resource Planning

### Development Capacity

**Team Composition**:
- Backend: 1-2 developers
- Frontend: 1-2 developers
- DevOps: 0.5 developer
- QA: Distributed

**Sprint Velocity**:
- Target: 20-25 SP/week (2 developers)
- Realistic: 15-20 SP/week (with overhead)
- Conservative: 12-15 SP/week (with blockers)

**Q1 Capacity Allocation**:
- Flussi Critici: 40% (highest ROI)
- Architecture: 25% (unblock future)
- Security: 20% (production readiness)
- Components/UX: 15% (quality)

---

## ⚠️ Risks & Mitigation

### Technical Risks

#### R1: Epic #3434 Delays Plugin Work
**Probability**: Medium
**Impact**: High (blocks 80+ SP)
**Mitigation**:
- Senior developer allocation
- Target: Feb 16, 2026
- Weekly progress tracking
- Incremental testing

---

#### R2: Plugin Architecture Scope Creep
**Probability**: High
**Impact**: High (80 SP could grow)
**Mitigation**:
- Strict scope definition
- MVP-first per plugin type
- Phase-based delivery
- Regular scope reviews

---

#### R3: Parallel Stream Dependencies
**Probability**: Medium
**Impact**: Medium (blocked work)
**Mitigation**:
- Clear dependency mapping
- Daily standup coordination
- Feature flags for partial integration
- Rollback strategies

---

#### R4: Testing Coverage Gaps
**Probability**: Medium
**Impact**: Medium (quality issues)
**Mitigation**:
- Mandatory 85% frontend, 90% backend coverage
- Code review gates
- E2E tests for critical flows
- Testcontainers for integration

---

### Resource Risks

#### R1: Developer Availability
**Probability**: Medium
**Impact**: High (timeline delays)
**Mitigation**:
- Buffer time (15-20 SP/week)
- Knowledge sharing
- Documentation-first
- Cross-training

---

#### R2: Underestimated Complexity
**Probability**: High
**Impact**: Medium (timeline pressure)
**Mitigation**:
- Conservative velocity
- Bi-weekly retrospectives
- Regular re-estimation
- Flexible milestones

---

## 🎯 Success Metrics

### Development KPIs

**Velocity Metrics**:
- Sprint velocity: 15-25 SP/week
- Epic completion: 1 per 2-3 weeks
- Issue close rate: 5-8 issues/week
- PR merge time: <2 days average

**Quality Metrics**:
- Test coverage: ≥85% frontend, ≥90% backend
- Code review: 100% PRs reviewed
- Build success: ≥95%
- Deployment frequency: 2-3x/week

---

### Business KPIs

**User Experience**:
- FLUSSO 1: ✅ 100%
- FLUSSO 2: Target 100% by Feb 16
- Dashboard load: <2s
- API response: <500ms (p95)
- Chat latency: <2s

**Platform Capability**:
- RAG strategies: 3 → 12 (by Q3)
- Configurable pipelines: 0 → 50+ (after #3413)
- Concurrent users: 100 → 1,000
- PDF processing: 10/hour → 100/hour

---

## 🚀 Immediate Actions (This Week)

### Week of Feb 2-9, 2026 🔴 ACTIVE

**Parallel Launch - 3 Teams**:

**Team Frontend User**:
- Branch: feature/issue-3476-collection-dashboard
- Task: #3476 User Collection Dashboard (5 SP)
- Timeline: 3-4 giorni

- Branch: feature/issue-3477-collection-wizard
- Task: #3477 Collection Wizard (5 SP)
- Timeline: 3-4 giorni

**Team Frontend Agent**:
- Branch: feature/issue-3376-agent-wizard
- Task: #3376 Agent Creation Wizard (5 SP)
- Timeline: 3-4 giorni

- Branch: feature/issue-3375-agent-launch
- Task: #3375 Agent Session Launch (3 SP)
- Timeline: 2-3 giorni

**Team Backend**:
- Branch: feature/issue-3479-private-pdf-upload
- Task: #3479 Private PDF Upload (3 SP)
- Timeline: 2-3 giorni

**Expected**: ~7 giorni con parallelizzazione

---

### Week of Feb 10-16, 2026

**Focus**: Chat History + Epic #3434 completion

**Team Backend**:
- #3483 Chat Persistence (5 SP)
- #3437 StrategyModelMapping (3 SP)

**Team Frontend**:
- #3484 Chat History UI (3 SP)
- #3440 Admin Config UI (3 SP)

**Team QA**:
- #3441 Test suite (2 SP)
- #3442 Documentation (2 SP)

**Milestones**:
- ✅ FLUSSO 2 USER 100%
- ✅ Epic #3434 100% → Unblocks #3413

---

## 📋 Epic Reference Links

### Active Epics
- [#3475 User Private Library](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3475)
- [#3386 Agent Creation & Testing](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3386)
- [#3434 Tier-Strategy-Model](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3434)
- [#3327 Security & Quota](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3327)
- [#3325 MeepleCard System](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3325)

### Blocked Epics
- [#3413 Plugin-Based RAG](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3413) - ⚠️ BLOCKED
- [#3412 Visual Strategy Builder](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3412) - ⚠️ BLOCKED

### Future Epics
- [#3403 Navigation Redesign](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3403)
- [#3366 Infrastructure](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3366)
- [#3341 Game Session Phase 2](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3341)
- [#3348 Advanced Features](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3348)
- [#3356 Advanced RAG Strategies](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3356)
- [#3490 Multi-Agent System](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3490)

---

## 🎯 Summary Statistics

### Total Scope
- **Epics**: 12 active
- **Issues**: 85+ open
- **Story Points**: ~240 SP
- **Timeline**: Q1-Q3 2026 (6-9 months)

### Q1 2026 Focus
- **Critical Work**: 52 SP (Flussi + Architecture)
- **High Priority**: 75 SP (Security + Components + UX)
- **Total Q1**: ~127 SP
- **Timeline**: 10-12 weeks

### Completion Targets
- **Feb 16**: Flussi Critici 100% + Epic #3434 100%
- **Mar 15**: Security baseline operational
- **Apr 1**: Component system unified
- **Jun 30**: Plugin architecture live
- **Aug 31**: Multi-Agent system deployed

---

## 🔗 Documentation References

### Core Documentation
- **Implementation Details**: `docs/claudedocs/sequenza.md`
- **Architecture Decisions**: `docs/01-architecture/adr/`
- **Developer Guide**: `CLAUDE.md`
- **API Reference**: http://localhost:8080/scalar/v1
- **Testing Guide**: `docs/05-testing/`

### Epic Specifications
- **Flussi Critici Analysis**: `docs/pdca/flussi-critici-analisi/`
- **Agent Architecture**: `docs/02-development/agent-architecture/`

---

**Maintainer**: PM Agent + Product Team
**Review Frequency**: Bi-weekly (tactical), Monthly (strategic), Quarterly (planning)
**Last Review**: 2026-02-02
**Next Review**: 2026-02-16 (Post-Flussi Critici milestone)
