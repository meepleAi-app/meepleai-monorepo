# 🗺️ MeepleAI Master Roadmap 2025-2026
**Main Current Roadmap Document - REFACTORING & CLEANUP PRIORITY**

**Ultimo Aggiornamento**: 2025-11-20 (Riorganizzato con focus Refactoring)
**Issue Aperte**: 131 (GitHub sync 2025-11-20)
**Issue Completate (Nov 2025)**: 160+ (analisi git log)
**Progress Complessivo**: 🟢 **~60%** Phase 1A Complete
**Timeline Rimanente**: 10-12 settimane (Dic 2025 - Feb 2026)
**Owner**: Engineering Team

> **🔧 NUOVA PRIORITÀ**: Questa roadmap è stata riorganizzata per prioritizzare **refactoring, pulizia e aggiornamento del codice** come prerequisiti per il completamento MVP. Security e code quality sono ora TIER 0.

---

## 📋 Executive Summary

### 🎯 NUOVO FOCUS - REFACTORING & CLEANUP FIRST!

**Strategia Aggiornata (Nov 2025)**:
1. ✅ **TIER 0**: Security Critical + Large Refactoring (8 issues) - **PRIORITY 1**
2. ✅ **TIER 1**: High Priority Refactoring & Code Cleanup (9 issues) - **PRIORITY 2**
3. ✅ **TIER 2**: Medium Priority Improvements & Optimizations (10+ issues) - **PRIORITY 3**
4. ✅ **TIER 3**: Testing & Quality Assurance (11 issues) - **PRIORITY 4**
5. ✅ **TIER 4**: MVP Features Month 5-6 (30 issues) - **PRIORITY 5**
6. ✅ **TIER 5**: Security Audit & Final Validation (2 issues) - **PRIORITY 6**
7. 📦 **PHASE 2**: Deferred Post-MVP (~70 issues) - **DEFERRED**

**Rationale**: Un codebase pulito, refactorizzato e sicuro è prerequisito per features stabili e maintenance a lungo termine.

---

## 🚨 TIER 0: CRITICAL - Security & Large Refactoring (8 issues)

**Timeline**: Week 1-2 (2 settimane) - **BLOCKERS ASSOLUTI**
**Effort**: ~80-100 ore di sviluppo
**Status**: 🔴 **0/8 completate** - IMMEDIATE ACTION REQUIRED

### 🔐 Security Critical (4 issues) - Week 1

| # | Issue | Area | Effort | Priority | Note |
|---|-------|------|--------|----------|------|
| **#1455** | GitHub Actions Security Fixes (hardcoded credentials, permissions) | CI/Security | 4-6h | 🔥 CRITICAL | Token exposure risk |
| **#1448** | CORS Whitelist Headers (Replace AllowAnyHeader) | Backend/Security | 3-4h | 🔥 CRITICAL | Production blocker |
| **#1447** | SecurityHeadersMiddleware (7 security headers) | Backend/Security | 4-6h | 🔥 CRITICAL | OWASP compliance |
| **#1432** | Remove Hardcoded Demo Password from Frontend | Frontend/Security | 2-3h | 🔥 CRITICAL | Credential leak |

**Total Security Effort**: ~13-19 ore
**Must Complete By**: Week 1 (5 giorni lavorativi)

### 🔧 Large Refactoring Critical (4 issues) - Week 2

| # | Issue | LOC Impact | Effort | Priority | Benefit |
|---|-------|------------|--------|----------|---------|
| **#1441** | Refactor RagService Exception Handling (995 LOC → 400 LOC) | -595 LOC | 16-20h | 🔥 CRITICAL | 60% complexity reduction |
| **#1440** | Migrate ConfigurationService to CQRS Pattern (805 LOC → Handlers) | -400 LOC | 12-16h | 🔥 CRITICAL | DDD completion (100%) |
| **#1439** | Split AdminEndpoints.cs (2031 LOC → 6 focused files) | -1500 LOC | 20-24h | 🔥 CRITICAL | 75% maintainability gain |
| **#1431** | Eliminate `any` Types from Frontend (21 files) | Type safety | 16-20h | 🔥 CRITICAL | 100% TypeScript strict mode |

**Total Refactoring Effort**: ~64-80 ore
**Total LOC Reduction**: **~2,495 lines removed**
**Must Complete By**: Week 2 (5 giorni lavorativi)

### ✅ TIER 0 Completion Criteria

- [ ] All 4 security issues resolved and deployed
- [ ] Security scan passing (CodeQL, dependency audit)
- [ ] All 4 large refactoring issues completed
- [ ] Build passing with 0 TypeScript `any` types
- [ ] Test coverage maintained ≥90%
- [ ] Documentation updated for refactored services

**🚀 GATE**: TIER 1 cannot start until TIER 0 is 100% complete.

---

## ⚡ TIER 1: HIGH - Refactoring & Code Cleanup (9 issues)

**Timeline**: Week 3-4 (2 settimane)
**Effort**: ~60-80 ore di sviluppo
**Status**: 🟡 **0/9 completate**

### 🔄 Code Splitting & Duplication Removal (3 issues)

| # | Issue | Impact | Effort | Quick Win | Benefit |
|---|-------|--------|--------|-----------|---------|
| **#1443** | Split AuthEndpoints.cs (1077 LOC → 4 files) | -600 LOC | 10-12h | ❌ | Auth maintainability |
| **#1442** | Create ValidationExtensions (399 duplications) | -350 LOC | 8-10h | ❌ | DRY principle |
| **#1444** | Extract RagExceptionHandler Pattern | -200 LOC | 3-4h | ✅ | Error handling consistency |

**Total Effort**: ~21-26 ore
**Total LOC Reduction**: **~1,150 lines removed**

### 🏗️ Infrastructure & DevOps (3 issues)

| # | Issue | Area | Effort | Priority | Benefit |
|---|-------|------|--------|----------|---------|
| **#1456** | GitHub Actions Phase 2: Standardization | CI/CD | 8-10h | HIGH | Faster builds |
| **#1450** | NSwag TypeScript Code Generation | API/Frontend | 12-16h | HIGH | Type-safe API client |
| **#1449** | FluentValidation for Authentication CQRS | Backend | 8-10h | HIGH | Validation consistency |

**Total Effort**: ~28-36 ore

### 🎨 Frontend Quality Improvements (3 issues)

| # | Issue | Area | Effort | Priority | Benefit |
|---|-------|------|--------|----------|---------|
| **#1438** | Standardize Immer Patterns in Zustand Slices | State | 6-8h | MEDIUM | State management consistency |
| **#1437** | Extract MotionButton Component (duplication) | UI | 4-6h | MEDIUM | Reusable components |
| **#1435** | Implement Structured Logging with Sentry | Observability | 8-10h | HIGH | Production debugging |

**Total Effort**: ~18-24 ore

### ✅ TIER 1 Completion Criteria

- [ ] All 9 high-priority refactoring issues completed
- [ ] Total LOC reduction: **~1,150+ lines**
- [ ] CI/CD pipeline optimized (Phase 2 standardization)
- [ ] NSwag integration functional with type-safe client
- [ ] FluentValidation integrated in Auth CQRS
- [ ] Sentry logging operational in production

**🚀 GATE**: TIER 2 can start in parallel with TIER 1 completion.

---

## 🔧 TIER 2: MEDIUM - Improvements & Optimizations (10 issues)

**Timeline**: Week 4-5 (overlap with TIER 1)
**Effort**: ~40-60 ore di sviluppo
**Status**: 🟡 **0/10 completate**

### 🚀 Performance & Infrastructure (4 issues)

| # | Issue | Area | Effort | Impact | Note |
|---|-------|------|--------|--------|------|
| **#1457** | GitHub Actions Phase 3: Optimizations | CI/CD | 6-8h | Medium | Build speed +20% |
| **#1454** | Request Deduplication Cache | Frontend/API | 6-8h | Medium | Reduce redundant calls |
| **#1453** | Retry Logic with Exponential Backoff | Frontend | 4-6h | Medium | Resilience improvement |
| **#1452** | Rate Limiting UX with Retry-After Countdown | Frontend/UX | 4-6h | Medium | Better user feedback |

**Total Effort**: ~20-28 ore

### 🧹 Code Cleanup Quick Wins (6 issues)

| # | Issue | Area | Effort | Quick Win | Benefit |
|---|-------|------|--------|-----------|---------|
| **#1451** | Consolidate Streaming Hooks (useChatStreaming + useChatStream) | Frontend | 4-6h | ✅ | Hook consistency |
| **#1446** | Session Validation Middleware | Backend/Auth | 3h | ✅ | Security +15% |
| **#1445** | Query Validation Helper | Backend | 2-3h | ✅ | Input validation DRY |
| **#1436** | Fix SWR + Zustand State Duplication in Chat | Frontend | 6-8h | ❌ | State management fix |
| **#1434** | Replace window.confirm with Custom Dialog | Frontend/UI | 4-6h | ❌ | UX consistency |
| **#1433** | Centralize Magic Numbers into Config Files | Frontend | 4-6h | ❌ | Maintainability |

**Total Effort**: ~23-35 ore

### ✅ TIER 2 Completion Criteria

- [ ] All 10 medium-priority improvements completed
- [ ] GitHub Actions optimization achieving +20% build speed
- [ ] Request deduplication reducing API calls by 30%
- [ ] All streaming hooks consolidated into single pattern
- [ ] Session validation middleware active on all protected routes
- [ ] Magic numbers eliminated from codebase

**🚀 GATE**: TIER 3 can start once TIER 0 + TIER 1 are 100% complete.

---

## ✅ TIER 3: Testing & Quality Assurance (11 issues)

**Timeline**: Week 5-7 (3 settimane)
**Effort**: ~80-100 ore di sviluppo
**Status**: 🟡 **1/11 completate** (#1369 PDF citation done)

### 🧪 Frontend Testing (5 issues)

| # | Issue | Target | Effort | Status | Note |
|---|-------|--------|--------|--------|------|
| **#1255** | Frontend Coverage 66% → 90% | 90% coverage | 16-20h | ⏸️ PENDING | Critical quality gate |
| **#1015** | PDF Viewer Tests (Jest + Playwright) | 20 tests | 8-10h | ⏸️ PENDING | Month 6 MVP |
| **#1005** | Jest Tests Q&A Components | 20 tests | 10-12h | ⏸️ PENDING | Month 5 MVP |
| **#992** | Frontend Component Testing (Jest 90%+) | 90% coverage | 12-16h | ⏸️ PENDING | Month 4 MVP |
| **#993** | Responsive Design Testing (320px-1920px) | 5 breakpoints | 8-10h | ⏸️ PENDING | Month 4 MVP |

**Total Effort**: ~54-68 ore

### 🧪 Backend Testing & Quality (6 issues)

| # | Issue | Target | Effort | Status | Note |
|---|-------|--------|--------|--------|------|
| **#1020** | Performance Testing (P95 latency <3s) | P95 <3s | 8-10h | ⏸️ PENDING | Month 6 MVP |
| **#1019** | Accuracy Validation (80%+ on 100 Q&A) | 80% accuracy | 12-16h | ⏸️ PENDING | Month 6 MVP |
| **#1018** | E2E Testing (question → PDF citation) | 10 scenarios | 12-16h | ⏸️ PENDING | Month 6 MVP |
| **#1009** | Month 5 E2E Testing | 8 scenarios | 10-12h | ⏸️ PENDING | Month 5 MVP |
| **#1000** | Run First Accuracy Test (baseline) | Baseline metrics | 4-6h | ⏸️ PENDING | Month 5 MVP |
| **#999** | Quality Test Implementation | 5-metric framework | 8-10h | ⏸️ PENDING | Month 5 MVP |

**Total Effort**: ~54-70 ore

### ✅ TIER 3 Completion Criteria

- [ ] Frontend coverage ≥90% restored and maintained
- [ ] All component tests passing (90%+ coverage)
- [ ] Responsive design validated across 5 breakpoints
- [ ] Performance P95 latency <3s achieved
- [ ] Accuracy validation ≥80% on golden dataset
- [ ] E2E tests passing for all critical user journeys
- [ ] Quality framework operational (5-metric)

**🚀 GATE**: TIER 4 MVP features can start once TIER 3 testing infrastructure is ready.

---

## 🎯 TIER 4: MVP Features Month 5-6 (30 issues)

**Timeline**: Week 6-10 (5 settimane)
**Effort**: ~120-160 ore di sviluppo
**Status**: 🟡 **~3/30 completate** (#1369, #990 partial, #989 partial)

### 📊 Month 4: Quality Framework (6 issues) - 🔄 ~50% Done

| # | Issue | Status | Effort | Note |
|---|-------|--------|--------|------|
| **#989** | Base Components (Button, Card, Input, Form) | 🔄 PARTIAL | 8-10h | Shadcn/UI base done |
| **#992** | Frontend Component Testing (Jest 90%+) | ⏸️ PENDING | 12-16h | See TIER 3 |
| **#993** | Responsive Design Testing (320px-1920px) | ⏸️ PENDING | 8-10h | See TIER 3 |
| **#994** | Frontend Build Optimization | ⏸️ PENDING | 8-10h | Webpack/Next.js tuning |
| **#995** | Month 4 Integration Testing | ⏸️ PENDING | 10-12h | Multi-service tests |

**Total Effort**: ~46-58 ore

### 📈 Month 5: Golden Dataset (10 issues) - 🔴 ~10% Done

| # | Issue | Status | Effort | Note |
|---|-------|--------|--------|------|
| **#996** | Annotation: Terraforming Mars (20 Q&A) | ⏸️ PENDING | 12-16h | High quality required |
| **#997** | Annotation: Wingspan (15 Q&A) | ⏸️ PENDING | 10-12h | High quality required |
| **#998** | Annotation: Azul (15 Q&A) | ⏸️ PENDING | 10-12h | High quality required |
| **#999** | Quality Test Implementation | ⏸️ PENDING | 8-10h | See TIER 3 |
| **#1000** | Run First Accuracy Test (baseline) | ⏸️ PENDING | 4-6h | See TIER 3 |
| **#1001** | QuestionInputForm Component | ⏸️ PENDING | 8-10h | Frontend Q&A UI |
| **#1005** | Jest Tests Q&A Components (20 tests) | ⏸️ PENDING | 10-12h | See TIER 3 |
| **#1009** | Month 5 E2E Testing | ⏸️ PENDING | 10-12h | See TIER 3 |

**Total Effort**: ~72-90 ore

### 🇮🇹 Month 6: Italian UI & Final Polish (14 issues) - 🟡 ~20% Done

| # | Issue | Status | Effort | Note |
|---|-------|--------|--------|------|
| **#1013** | PDF Viewer Integration (react-pdf) | 🔄 PARTIAL | 8-10h | Base in #1369 |
| **#1014** | Citation Click → Jump to Page | ✅ DONE | - | #1369 merged! |
| **#1015** | PDF Viewer Tests (Jest + Playwright) | ⏸️ PENDING | 8-10h | See TIER 3 |
| **#1016** | Complete Italian UI Strings (200+ translations) | 🔄 PARTIAL | 12-16h | i18n done (#990), need translations |
| **#1017** | Game Catalog Page (/board-game-ai/games) | ⏸️ PENDING | 8-10h | UI page implementation |
| **#1018** | E2E Testing (question → PDF citation) | ⏸️ PENDING | 12-16h | See TIER 3 |
| **#1019** | Accuracy Validation (80%+ on 100 Q&A) | ⏸️ PENDING | 12-16h | See TIER 3 |
| **#1020** | Performance Testing (P95 <3s) | ⏸️ PENDING | 8-10h | See TIER 3 |
| **#1021** | Final Bug Fixes and Polish | ⏸️ PENDING | 12-16h | General cleanup |
| **#1022** | Documentation Updates (user guide, README) | ⏸️ PENDING | 8-10h | User-facing docs |
| **#1023** | Phase 1A Completion Checklist | ⏸️ PENDING | 4-6h | Final validation |

**Total Effort**: ~92-120 ore

### ✅ TIER 4 Completion Criteria

- [ ] All Month 4 Quality Framework issues completed
- [ ] Golden dataset annotated (50 Q&A pairs, 3 games)
- [ ] Quality test implementation operational
- [ ] QuestionInputForm component functional
- [ ] PDF viewer fully integrated with citation jump
- [ ] Italian UI 100% translated (200+ strings)
- [ ] Game catalog page live
- [ ] All MVP features tested and documented

**🚀 GATE**: TIER 5 Security Audit requires all TIER 4 features complete.

---

## 🔐 TIER 5: Security Audit & Final Validation (2 issues)

**Timeline**: Week 11-12 (2 settimane)
**Effort**: ~40-60 ore di sviluppo
**Status**: 🔴 **0/2 completate** - **MANDATORY PRE-PRODUCTION**

| # | Issue | Area | Effort | Priority | Note |
|---|-------|------|--------|----------|------|
| **#576** | SEC-05: Security Penetration Testing | Security | 24-40h | 🔥 CRITICAL | External audit required |
| **#575** | AUTH-08: Admin Override for 2FA Locked-Out Users | Backend/Auth | 8-10h | HIGH | Support tool |

### ✅ TIER 5 Completion Criteria

- [ ] Security penetration testing completed by external auditor
- [ ] All OWASP Top 10 vulnerabilities addressed
- [ ] Security report approved (no critical/high findings)
- [ ] Admin 2FA override tool implemented and tested
- [ ] Security documentation updated
- [ ] Production deployment checklist approved

**🚀 GATE**: Production launch blocked until TIER 5 is 100% complete.

---

## 📦 PHASE 2: DEFERRED (Post-MVP) - ~70 Issues

**Timeline**: Month 8-12 (2026 H2)
**Status**: ⚪ **CONSOLIDATE & DEFER** - Focus on MVP first

### Epic #1300: Admin Dashboard v2.0 (~48 issues) - Priority: LOW

**Scope**: Issues #874-922 (Admin console 4 fasi) + #864-869 (Session/Agent management)
**Milestone**: FASE 1-4 (Dashboard, Infrastructure, Management, Advanced)
**Effort**: 12-16 settimane post-MVP
**Decisione**: **DEFER** - Non critico per MVP utenti finali
**Note**: Dashboard base operativo, advanced features deferred

**Categorie**:
- **FASE 1**: Dashboard Overview (11 issues) - optional, priority-low
- **FASE 2**: Infrastructure Monitoring (3 issues) - optional, priority-low
- **FASE 3**: Enhanced Management (13 issues) - deferred, priority-low
- **FASE 4**: Advanced Features (6 issues) - deferred, priority-low
- **Session/Agent Management** (6 issues) - backend improvements

### Epic #1301: Frontend Modernization Extended (6 issues) - Priority: LOW

**Scope**: Issues #926, #931-935 (Frontend advanced patterns)
**Status**: ✅ **MOSTLY COMPLETE** - Next.js 16 + React 19 migration done (FE-IMP-001→009)
**Decisione**: **DEFER** - Core modernization already complete
**Note**: Advanced patterns can wait for post-MVP

### Epic #1302: Infrastructure Hardening v2.0 (5 issues) - Priority: LOW

**Scope**: Issues #701-704, #818 (Docker, backups, Traefik, resource limits)
**Effort**: 4-6 settimane post-MVP
**Decisione**: **DEFER** - 15 Docker services già funzionanti
**Note**: Production infrastructure operational, hardening deferred

**Issues**:
- **#701**: Add resource limits to all Docker services
- **#702**: Implement Docker Compose profiles
- **#703**: Add Traefik reverse proxy layer
- **#704**: Create backup automation scripts
- **#818**: Quarterly security scan review process

---

## 📊 Progress Summary & Metrics

### Overall Progress (131 Open Issues)

| Tier | Issue Totali | Completate | Rimanenti | Progress | Timeline |
|------|--------------|------------|-----------|----------|----------|
| **TIER 0 (Critical)** | 8 | 0 | 8 | 🔴 **0%** | Week 1-2 (2 weeks) |
| **TIER 1 (High)** | 9 | 0 | 9 | 🔴 **0%** | Week 3-4 (2 weeks) |
| **TIER 2 (Medium)** | 10 | 0 | 10 | 🔴 **0%** | Week 4-5 (2 weeks) |
| **TIER 3 (Testing)** | 11 | 1 | 10 | 🟡 **~9%** | Week 5-7 (3 weeks) |
| **TIER 4 (MVP Features)** | 30 | ~3 | ~27 | 🟡 **~10%** | Week 6-10 (5 weeks) |
| **TIER 5 (Security Audit)** | 2 | 0 | 2 | 🔴 **0%** | Week 11-12 (2 weeks) |
| **PHASE 2 (Deferred)** | ~70 | 0 | ~70 | ⚪ **0%** | Post-MVP (2026 H2) |
| **TOTAL** | **131** | **~4** | **~127** | 🟡 **~3%** | 12 weeks to MVP |

### Effort Estimation Summary

| Category | Issues | Effort (hours) | Weeks (40h/week) |
|----------|--------|----------------|------------------|
| **TIER 0: Critical** | 8 | 77-99h | ~2 weeks |
| **TIER 1: High** | 9 | 67-86h | ~2 weeks |
| **TIER 2: Medium** | 10 | 43-63h | ~1.5 weeks |
| **TIER 3: Testing** | 11 | 108-138h | ~3 weeks |
| **TIER 4: MVP Features** | 30 | 210-268h | ~6 weeks |
| **TIER 5: Security** | 2 | 32-50h | ~1 week |
| **TOTAL to MVP** | **70** | **537-704h** | **~15-18 weeks** |

**Note**: Timeline assumes 1-2 developers full-time. With team scaling, can compress to 10-12 weeks.

### 🎯 Key Performance Indicators (KPIs)

#### Code Quality Targets

| Metrica | Target | Current | TIER 0 Goal | Status |
|---------|--------|---------|-------------|--------|
| **LOC Reduction** | -4,000 LOC | Baseline | -2,495 LOC | 🔴 Not started |
| **TypeScript `any` Types** | 0 | 21 files | 0 files | 🔴 Not started |
| **Code Duplication** | <3% | ~5% | ~3% | 🔴 Not started |
| **Frontend Coverage** | ≥90% | 66% | 90% | 🔴 Critical gap |
| **Backend Coverage** | ≥90% | 90%+ | 90%+ | 🟢 Maintained |
| **Security Issues (Critical)** | 0 | 4 | 0 | 🔴 Must fix |
| **Security Issues (High)** | 0 | ~3 | 0 | 🔴 Must fix |

#### Performance Targets

| Metrica | Target MVP | Current | TIER 4 Goal | Status |
|---------|------------|---------|-------------|--------|
| **P95 Latency** | <3s | ~2.5s | <3s | 🟢 Near target |
| **TTFT (Streaming)** | <1s | ~800ms | <1s | 🟢 Achieved |
| **Accuracy (BGAI)** | ≥80% | ~75% | ≥80% | 🟡 Near target |
| **Hallucination Rate** | ≤10% | <5% | ≤10% | 🟢 Achieved |
| **Lighthouse Score** | ≥90 | ✅ | ≥90 | 🟢 Achieved |

---

## 🚀 Critical Path & Dependencies

### Dependency Graph

```
TIER 0 (Security + Large Refactoring)
    │
    ├─ MUST complete before TIER 1
    │
    ▼
TIER 1 (High Priority Refactoring)
    │
    ├─ Can overlap with TIER 2
    │
    ▼
TIER 2 (Medium Priority Improvements)
    │
    └─ TIER 3 can start once TIER 0+1 complete
        │
        ▼
    TIER 3 (Testing Infrastructure)
        │
        ├─ MUST complete before TIER 4 features
        │
        ▼
    TIER 4 (MVP Features)
        │
        └─ MUST complete before TIER 5
            │
            ▼
        TIER 5 (Security Audit)
            │
            └─ REQUIRED for Production Launch
```

### Blocking Issues (Must Resolve First)

1. **#1441** - RagService Refactoring (995 LOC) - **BLOCKS** all RAG feature work
2. **#1440** - ConfigurationService CQRS - **BLOCKS** config management improvements
3. **#1439** - AdminEndpoints Split (2031 LOC) - **BLOCKS** admin feature development
4. **#1431** - Eliminate `any` Types - **BLOCKS** TypeScript strict mode
5. **#1255** - Frontend Coverage 66%→90% - **BLOCKS** confidence in frontend changes
6. **#1447, #1448** - Security Headers + CORS - **BLOCKS** production deployment

### Quick Wins (High Impact, Low Effort)

| # | Issue | Effort | Impact | ROI |
|---|-------|--------|--------|-----|
| **#1444** | Extract RagExceptionHandler Pattern | 3-4h | High | ⭐⭐⭐⭐⭐ |
| **#1446** | Session Validation Middleware | 3h | High | ⭐⭐⭐⭐⭐ |
| **#1445** | Query Validation Helper | 2-3h | Medium | ⭐⭐⭐⭐ |
| **#1432** | Remove Hardcoded Password | 2-3h | High | ⭐⭐⭐⭐⭐ |
| **#1437** | Extract MotionButton Component | 4-6h | Medium | ⭐⭐⭐ |

**Recommendation**: Tackle quick wins first for morale boost and immediate impact.

---

## 🗓️ Detailed Sprint Planning

### 🔥 Sprint 1-2: TIER 0 Critical (Week 1-2)

**Goal**: Eliminate all critical security issues and complete large refactoring

**Week 1: Security Critical** (4 issues)
- Day 1-2: #1455 GitHub Actions security fixes + #1432 Remove hardcoded password
- Day 3-4: #1448 CORS whitelist + #1447 Security headers middleware
- Day 5: Security testing, verification, deployment

**Week 2: Large Refactoring** (4 issues)
- Day 1-2: #1441 Refactor RagService (995 LOC → 400 LOC)
- Day 2-3: #1440 Migrate ConfigurationService to CQRS
- Day 4-5: #1439 Split AdminEndpoints.cs (2031 LOC → 6 files)
- Weekend: #1431 Eliminate `any` types (21 files) - can be parallelized

**Deliverables**:
- ✅ Zero critical security issues
- ✅ ~2,495 LOC removed
- ✅ TypeScript strict mode enabled
- ✅ Security scan passing

---

### ⚡ Sprint 3-4: TIER 1 High Priority (Week 3-4)

**Goal**: Complete high-priority refactoring and infrastructure improvements

**Week 3: Code Splitting** (3 issues)
- Day 1-2: #1443 Split AuthEndpoints.cs (1077 LOC → 4 files)
- Day 3: #1444 Extract RagExceptionHandler Pattern (Quick Win!)
- Day 4-5: #1442 Create ValidationExtensions (399 duplications)

**Week 4: Infrastructure** (6 issues)
- Day 1-2: #1456 GitHub Actions Phase 2 + #1449 FluentValidation
- Day 3-4: #1450 NSwag TypeScript Code Generation
- Day 4-5: #1438 Immer Patterns + #1437 MotionButton + #1435 Sentry Logging

**Deliverables**:
- ✅ ~1,150 LOC removed
- ✅ CI/CD standardized and optimized
- ✅ NSwag type-safe API client operational
- ✅ FluentValidation integrated

---

### 🔧 Sprint 5: TIER 2 Medium Priority (Week 5)

**Goal**: Complete medium-priority improvements and optimizations

**Week 5: Improvements** (10 issues - can parallelize)
- Day 1: #1457 GitHub Actions Phase 3 + #1451 Consolidate Streaming Hooks
- Day 2: #1454 Request Deduplication + #1453 Retry Logic
- Day 3: #1452 Rate Limiting UX + #1446 Session Validation (Quick Win!)
- Day 4: #1445 Query Validation (Quick Win!) + #1436 SWR/Zustand fix
- Day 5: #1434 Replace window.confirm + #1433 Centralize Magic Numbers

**Deliverables**:
- ✅ CI/CD fully optimized
- ✅ Frontend resilience improved
- ✅ All quick wins completed
- ✅ Magic numbers eliminated

---

### ✅ Sprint 6-8: TIER 3 Testing (Week 6-8)

**Goal**: Restore and improve test coverage to ≥90%

**Week 6: Frontend Testing** (5 issues)
- Day 1-3: #1255 Frontend Coverage 66% → 90% (priority!)
- Day 4: #1015 PDF Viewer Tests
- Day 5: #1005 Jest Tests Q&A Components

**Week 7: Backend Testing** (4 issues)
- Day 1-2: #999 Quality Test Implementation + #1000 Run First Accuracy Test
- Day 3-4: #1009 Month 5 E2E Testing
- Day 5: #1018 E2E Testing (question → PDF citation)

**Week 8: Performance & Quality** (2 issues)
- Day 1-2: #1020 Performance Testing (P95 <3s)
- Day 3-4: #1019 Accuracy Validation (80%+)
- Day 5: #992 Frontend Component Testing + #993 Responsive Design Testing

**Deliverables**:
- ✅ Frontend coverage ≥90%
- ✅ All E2E tests passing
- ✅ Performance P95 <3s validated
- ✅ Accuracy ≥80% on golden dataset

---

### 🎯 Sprint 9-13: TIER 4 MVP Features (Week 9-13)

**Goal**: Complete all Month 4-6 MVP features

**Week 9: Month 4 Quality Framework** (6 issues)
- Complete #989, #992, #993, #994, #995

**Week 10-11: Month 5 Golden Dataset** (10 issues)
- Annotate datasets (#996-998)
- Implement Q&A components (#1001, #1005)
- E2E testing (#1009)

**Week 12-13: Month 6 Italian UI** (14 issues)
- Complete Italian translations (#1016)
- PDF viewer finalization (#1013, #1015)
- Game catalog (#1017)
- Final testing (#1018-1020)
- Polish + docs (#1021-1023)

**Deliverables**:
- ✅ All MVP features complete
- ✅ Italian UI 100% translated
- ✅ PDF viewer fully functional
- ✅ Documentation complete

---

### 🔐 Sprint 14-15: TIER 5 Security Audit (Week 14-15)

**Goal**: Pass security audit and complete final validation

**Week 14: Security Penetration Testing**
- #576 SEC-05 external security audit
- Remediate any findings

**Week 15: Final Validation**
- #575 AUTH-08 Admin 2FA Override
- Final smoke tests
- Production deployment preparation

**Deliverables**:
- ✅ Security audit passed (no critical/high findings)
- ✅ Production deployment approved
- ✅ **READY FOR LAUNCH** 🚀

---

## 🚨 Risk Assessment & Mitigation

### 🔴 HIGH RISK (Immediate Attention Required)

#### 1. Frontend Coverage 66% (Target: 90%) - Issue #1255
- **Probabilità**: 90% (already occurred)
- **Impatto**: 🔥🔥🔥🔥 CRITICAL
- **Descrizione**: Coverage drop creates regression risk for all frontend changes
- **Mitigazione**:
  - ✅ Prioritized in TIER 3 Week 6
  - ✅ Block all feature work until resolved
  - ✅ Add coverage gate to CI/CD (fail PR if <90%)
- **Status**: 🔴 **ACTIVE BLOCKER** - Must fix Sprint 6

#### 2. Large Codebase Complexity (3,900+ LOC in 3 files)
- **Probabilità**: 100% (measured)
- **Impatto**: 🔥🔥🔥🔥 HIGH
- **Descrizione**: AdminEndpoints (2031), RagService (995), AuthEndpoints (1077) unmaintainable
- **Mitigazione**:
  - ✅ Prioritized in TIER 0-1
  - ✅ Total reduction: ~3,645 LOC
  - ✅ Split into focused, testable files
- **Status**: 🔴 **ACTIVE** - TIER 0-1 Sprint 1-4

#### 3. Security Vulnerabilities (4 Critical + ~3 High)
- **Probabilità**: 100% (identified)
- **Impatto**: 🔥🔥🔥🔥🔥 CRITICAL
- **Descrizione**: Hardcoded credentials, missing CORS whitelist, no security headers
- **Mitigazione**:
  - ✅ Prioritized in TIER 0 Week 1
  - ✅ Security scan in CI/CD
  - ✅ Mandatory security audit TIER 5
- **Status**: 🔴 **PRODUCTION BLOCKER** - Sprint 1

#### 4. TypeScript `any` Types (21 files)
- **Probabilità**: 100% (measured)
- **Impatto**: 🔥🔥🔥 HIGH
- **Descrizione**: Loss of type safety, runtime errors
- **Mitigazione**:
  - ✅ Prioritized in TIER 0 Week 2
  - ✅ Enable TypeScript strict mode after fix
  - ✅ Add `any` detection to CI/CD
- **Status**: 🔴 **ACTIVE** - Sprint 2

### 🟡 MEDIUM RISK (Monitor Closely)

#### 5. Dataset Annotation Quality (60 Q&A pairs)
- **Probabilità**: 40% (reduced from 35%)
- **Impatto**: 🔥🔥🔥🔥 HIGH
- **Descrizione**: Poor annotation quality affects accuracy validation
- **Mitigazione**:
  - ✅ Rigorous review process
  - ✅ Multiple annotators
  - ✅ Quality scoring rubric
  - ✅ Dedicated in TIER 4 Week 10-11
- **Status**: 🟡 **MONITORING** - Sprint 10-11

#### 6. Accuracy Target <80% at Validation
- **Probabilità**: 25% (reduced from 20%)
- **Impatto**: 🔥🔥🔥🔥 HIGH
- **Descrizione**: MVP launch blocked if accuracy below 80%
- **Mitigazione**:
  - ✅ Early baseline testing (#1000)
  - ✅ Iterative improvements in TIER 4
  - ✅ Multi-model validation already operational
  - ✅ Golden dataset expansion if needed
- **Status**: 🟡 **UNDER CONTROL** - Sprint 11

#### 7. Timeline Slip (15 weeks vs 10 weeks target)
- **Probabilità**: 60%
- **Impatto**: 🔥🔥🔥 MEDIUM
- **Descrizione**: Realistic timeline is 15 weeks, not 10 weeks
- **Mitigazione**:
  - ✅ Honest estimation in roadmap (15-18 weeks)
  - ✅ Parallelize work where possible
  - ✅ Quick wins for morale
  - ✅ Consider team scaling
- **Status**: 🟡 **ACCEPTED** - Honest timeline

### 🟢 LOW RISK (Controlled)

#### 8. Scope Creep Phase 2 (70 deferred issues)
- **Probabilità**: 10% (reduced from 10%)
- **Impatto**: 🔥🔥 LOW
- **Descrizione**: Temptation to add deferred features during MVP work
- **Mitigazione**:
  - ✅ Strict tier prioritization
  - ✅ All Phase 2 clearly labeled "DEFERRED"
  - ✅ Focus on critical path only
- **Status**: 🟢 **CONTROLLED** - Discipline maintained

#### 9. Performance P95 >3s
- **Probabilità**: 10% (reduced from 15%)
- **Impatto**: 🔥🔥🔥 MEDIUM
- **Descrizione**: RAG pipeline latency exceeds target
- **Mitigazione**:
  - ✅ Current P95 ~2.5s (buffer available)
  - ✅ Parallel validation already implemented (#979)
  - ✅ Monitoring in place (Prometheus + Grafana)
  - ✅ Performance testing in TIER 3
- **Status**: 🟢 **LOW RISK** - Near target

---

## 🎯 Success Criteria & MVP Quality Gate

### ✅ MVP Launch Checklist (Phase 1A Completion)

#### **TIER 0: Critical (Mandatory)**
- [ ] All 4 security critical issues resolved (#1455, #1448, #1447, #1432)
- [ ] Security scan passing (0 critical, 0 high vulnerabilities)
- [ ] All 4 large refactoring completed (~2,495 LOC removed)
- [ ] TypeScript strict mode enabled (0 `any` types)
- [ ] Build passing with 0 errors

#### **TIER 1: High Priority (Mandatory)**
- [ ] All 9 high-priority refactoring issues completed
- [ ] ~1,150 LOC removed (total: ~3,645 LOC)
- [ ] NSwag type-safe API client operational
- [ ] FluentValidation integrated in Auth CQRS
- [ ] CI/CD optimized (Phase 2 standardization)

#### **TIER 2: Medium Priority (Mandatory)**
- [ ] All 10 medium-priority improvements completed
- [ ] Streaming hooks consolidated
- [ ] Request deduplication active
- [ ] Session validation middleware deployed
- [ ] Magic numbers eliminated

#### **TIER 3: Testing (Mandatory)**
- [ ] Frontend coverage ≥90% restored (#1255)
- [ ] All component tests passing (90%+ coverage)
- [ ] Responsive design validated (320px-1920px)
- [ ] E2E tests passing (all critical journeys)
- [ ] Quality framework operational (5-metric)

#### **TIER 4: MVP Features (Mandatory)**
- [ ] Golden dataset annotated (50 Q&A pairs minimum)
- [ ] Accuracy ≥80% on golden dataset (#1019)
- [ ] PDF viewer fully functional with citation jump (#1013, #1014)
- [ ] Italian UI 100% translated (200+ strings) (#1016)
- [ ] Game catalog page live (#1017)
- [ ] Performance P95 <3s (#1020)
- [ ] All MVP features documented (#1022)

#### **TIER 5: Security Audit (Mandatory)**
- [ ] Security penetration testing completed (#576)
- [ ] Security audit report approved (no critical/high findings)
- [ ] All OWASP Top 10 vulnerabilities addressed
- [ ] Production deployment checklist approved

### 📊 KPI Targets (Must Achieve)

| Category | KPI | Target | Current | Gate |
|----------|-----|--------|---------|------|
| **Code Quality** | LOC Reduction | -3,645 LOC | Baseline | ✅ TIER 0-1 |
| | TypeScript `any` | 0 types | 21 files | ✅ TIER 0 |
| | Frontend Coverage | ≥90% | 66% | ✅ TIER 3 |
| | Backend Coverage | ≥90% | 90%+ | ✅ Maintained |
| **Security** | Critical Vulns | 0 | 4 | ✅ TIER 0 |
| | High Vulns | 0 | ~3 | ✅ TIER 0-1 |
| | Penetration Test | Pass | Not done | ✅ TIER 5 |
| **Performance** | P95 Latency | <3s | ~2.5s | ✅ TIER 3 |
| | Accuracy (BGAI) | ≥80% | ~75% | ✅ TIER 4 |
| | Hallucination Rate | ≤10% | <5% | ✅ Already met |
| **Features** | Italian UI | 100% | ~50% | ✅ TIER 4 |
| | PDF Viewer | 100% | ~60% | ✅ TIER 4 |
| | E2E Tests | 100% pass | Partial | ✅ TIER 3-4 |

### 🚀 Production Launch Gate

**ALL criteria must be met**:
1. ✅ All TIER 0-5 issues completed (70 issues)
2. ✅ All KPI targets achieved
3. ✅ Security audit passed with no critical/high findings
4. ✅ Documentation complete and reviewed
5. ✅ Production environment tested and validated
6. ✅ Rollback plan documented and rehearsed
7. ✅ On-call rotation established
8. ✅ Monitoring and alerting operational

**Decision**: 🚀 **GO FOR PRODUCTION LAUNCH**

**Target Date**: February 2026 (Week 15-16)

---

## 📚 Documentazione & Riferimenti

### Documentazione Tecnica Aggiornata

| Documento | Path | Scopo | Aggiornamento |
|-----------|------|-------|---------------|
| **CLAUDE.md** | `/CLAUDE.md` | Project overview completo | 2025-11-20 |
| **System Architecture** | `docs/01-architecture/overview/system-architecture.md` | Full system design | Current |
| **API Architecture** | `docs/API-architecture/classi.md` | API class design | #1279 |
| **ADR-001: Hybrid RAG** | `docs/01-architecture/adr/adr-001-hybrid-rag.md` | RAG architecture | Current |
| **ADR-006: Multi-Layer Validation** | `docs/01-architecture/adr/adr-006-*.md` | Validation framework | #1299 |
| **Testing Guide** | `docs/02-development/testing/testing-guide.md` | Test pyramid strategy | Current |
| **Refactoring Guide** | `docs/02-development/refactoring-guide.md` | Refactoring patterns | 🆕 TODO |
| **Security Guide** | `docs/06-security/security-checklist.md` | Security best practices | 🆕 TODO |

### Roadmap Precedenti (📦 ARCHIVED)

Questo documento **v6.0** consolida e sostituisce:
- ✅ `ROADMAP.md` v5.0 (2025-11-18) - Previous consolidated roadmap
- ✅ `visual-roadmap.md` (2025-11-12)
- ✅ `executive-summary-development-roadmap.md` (2025-11-12)
- ✅ `NEXT-30-ISSUES-ROADMAP.md` (2025-11-17)
- ✅ `master-roadmap-2025.md` (2025-11-17)

**Note**: Documenti precedenti archiviati per reference storico.

### Issue Tracking & Labels

**Priority Labels**:
- `priority:critical` (8 issues) - TIER 0
- `priority:high` (9 issues) - TIER 1
- `priority:medium` (10 issues) - TIER 2
- `priority:low` (~70 issues) - PHASE 2 Deferred

**Area Labels**:
- `area:backend` - Backend refactoring/features
- `area:frontend` - Frontend refactoring/features
- `area/security` - Security issues
- `area/auth` - Authentication/authorization
- `area/infra` - Infrastructure/DevOps
- `area/ui` - UI/UX improvements

**Type Labels**:
- `refactor` - Code refactoring (17 issues prioritized)
- `testing` - Testing improvements (11 issues)
- `mvp` - MVP features (30 issues)
- `deferred` - Post-MVP work (~70 issues)
- `kind/ci` - CI/CD improvements

### GitHub Milestones

- **Month 4: Quality Framework** (6 issues) - TIER 4
- **Month 5: Golden Dataset** (10 issues) - TIER 4
- **Month 6: Italian UI** (14 issues) - TIER 4
- **Phase 2** (~70 issues) - Deferred
- **FASE 1-4: Admin Console** (48 issues) - Deferred

---

## 📞 Communication & Escalation

### Project Leadership

- **Technical Lead**: [TBD]
- **Product Owner**: [TBD]
- **QA Lead**: [TBD]
- **Security Lead**: [TBD]

### Communication Cadence

- **Daily**: Standup (blockers, progress, plan)
- **Weekly**: Sprint review + planning
- **Bi-weekly**: Stakeholder demo
- **Monthly**: Retrospective + roadmap review

### Escalation Path

1. **Blocker Identified** → Immediate Slack notification (#meepleai-dev)
2. **Sprint at Risk** → Technical Lead escalation
3. **Timeline Slip >1 week** → Product Owner + stakeholders
4. **Critical Security Issue** → Security Lead + immediate action

### Slack Channels

- `#meepleai-dev` - Development discussions
- `#meepleai-alerts` - CI/CD alerts, monitoring
- `#meepleai-security` - Security issues
- `#meepleai-releases` - Release coordination

---

## 🎉 Conclusion & Next Steps

### Key Takeaways

1. **Refactoring First**: Security and code quality are now TIER 0-1 priorities
2. **Realistic Timeline**: 15-18 weeks to MVP launch (honest estimation)
3. **LOC Reduction**: ~3,645 lines to be removed (30% complexity reduction)
4. **Security Mandatory**: 4 critical + 3 high security issues must be resolved
5. **Coverage Critical**: Frontend coverage 66%→90% is a gate for all work
6. **Deferred Scope**: ~70 issues clearly marked as Post-MVP

### Immediate Actions (This Week)

#### Monday-Tuesday: Security Critical
1. **#1455** - Fix GitHub Actions hardcoded credentials
2. **#1432** - Remove hardcoded demo password from frontend

#### Wednesday-Thursday: Security Critical
3. **#1448** - Implement CORS whitelist headers
4. **#1447** - Add SecurityHeadersMiddleware

#### Friday: Validation
5. Run security scan (CodeQL + dependency audit)
6. Verify all critical issues resolved
7. Plan Week 2 large refactoring sprint

### Success Metrics (15 weeks)

- ✅ **70 issues completed** (TIER 0-5)
- ✅ **~3,645 LOC removed** (30% complexity reduction)
- ✅ **Security audit passed** (0 critical/high findings)
- ✅ **Coverage ≥90%** (frontend + backend)
- ✅ **Accuracy ≥80%** (golden dataset validation)
- ✅ **MVP launched** (February 2026)

### Vision Statement

> **"Clean code, secure foundation, user-centric MVP"**
>
> We prioritize refactoring and security to build a maintainable, scalable, and secure codebase that enables rapid feature development post-MVP. Quality over speed, sustainability over shortcuts.

---

**🚀 Let's ship a robust, maintainable MVP! Andiamo! 🚀**

---

## 📎 Appendice

### Command Reference (Quick Start)

```bash
# Development
cd infra && docker compose up -d               # Start all 15 services
cd apps/api/src/Api && dotnet run              # Backend (8080)
cd apps/web && pnpm dev                        # Frontend (3000)

# Testing
dotnet test                                    # Backend tests
pnpm test                                      # Frontend tests
pnpm test:e2e                                  # E2E tests

# Code Quality
dotnet build                                   # Check build
pnpm typecheck                                 # Check TypeScript
pnpm lint                                      # Check ESLint

# Coverage
./tools/run-backend-coverage.sh --html --open   # Backend coverage
./tools/run-frontend-coverage.sh --open         # Frontend coverage

# Security
dotnet list package --vulnerable               # Check vulnerabilities
pnpm audit --audit-level=high                  # Check npm packages

# Git Workflow
git checkout -b refactor/issue-XXX             # New branch
git commit -m "refactor(area): description"    # Commit
git push -u origin refactor/issue-XXX          # Push
```

### Docker Services (15 services)

| Service | Port | Purpose | Status |
|---------|------|---------|--------|
| **meepleai-postgres** | 5432 | Database | ✅ Operational |
| **meepleai-qdrant** | 6333 | Vector search | ✅ Operational |
| **meepleai-redis** | 6379 | Caching | ✅ Operational |
| **meepleai-api** | 8080 | ASP.NET backend | ✅ Operational |
| **meepleai-web** | 3000 | Next.js frontend | ✅ Operational |
| **meepleai-seq** | 8081 | Logging | ✅ Operational |
| **meepleai-jaeger** | 16686 | Tracing | ✅ Operational |
| **meepleai-prometheus** | 9090 | Metrics | ✅ Operational |
| **meepleai-grafana** | 3001 | Dashboards | ✅ Operational |
| **meepleai-alertmanager** | 9093 | Alerting | ✅ Operational |
| **meepleai-n8n** | 5678 | Workflows | ✅ Operational |
| **meepleai-ollama** | 11434 | Local LLM | ✅ Operational |
| **meepleai-unstructured** | 8001 | PDF extraction | ✅ Operational |
| **meepleai-smoldocling** | 8002 | PDF VLM | ✅ Operational |
| **meepleai-embedding** | 8000 | Embeddings | ✅ Operational |

### Changelog Roadmap

- **v6.0** (2025-11-20): Complete refactoring-first reorganization, 131 open issues analyzed
- **v5.0** (2025-11-18): Consolidamento finale + verifica GitHub commits
- **v4.0** (2025-11-17): Consolidamento 4 roadmap + verifica issues
- **v3.0** (2025-11-17): Master roadmap con analisi 125 issue
- **v2.0** (2025-11-17): Next 30 issues roadmap Phase 1B
- **v1.1** (2025-11-12): Executive summary updates
- **v1.0** (2025-11-12): Visual roadmap baseline

---

**Versione**: 6.0 (Refactoring-First Reorganization)
**Owner**: Engineering Team
**Ultima Revisione**: 2025-11-20
**Prossima Revisione**: Post-TIER 0 completion (Week 3)
**Status**: 🔴 **REFACTORING PRIORITY** - Clean code first, features second
**Open Issues**: 131 (GitHub sync verified 2025-11-20)
**Effort to MVP**: 537-704 hours (15-18 weeks with 1-2 devs)

---

**🎯 Focus: Refactoring → Testing → Features → Security → Launch 🚀**
