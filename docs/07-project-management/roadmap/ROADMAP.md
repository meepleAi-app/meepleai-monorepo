# 🗺️ MeepleAI Master Roadmap

**Ultimo Aggiornamento**: 2025-11-23
**Stato Progetto**: Alpha (DDD 99% completo, Triage completato, P0 completati)
**Issue Aperte**: 156 (119 MVP prioritizzate, 37 deferred)
**Timeline to MVP**: 9-11 settimane (~944h effort rimanente, 2 FTE)
**Target Launch**: Fine Febbraio - Inizio Marzo 2026

---

## 📋 Executive Summary

MeepleAI è un assistente AI per regolamenti di giochi da tavolo, con focus italiano e target accuracy >95%.

**Stack**: ASP.NET 9, Next.js 16, React 19, PostgreSQL, Qdrant, Redis, OpenRouter, n8n
**Architettura**: 7 Bounded Contexts (DDD/CQRS), 99% migrazione completata
**Fase Attuale**: Alpha - P0 completati, avvio P1 execution

### 🎯 Status Aggiornato ✅

**TRIAGE COMPLETE** (2025-11-23): 159 issue categorizzate
**P0 COMPLETE** (2025-11-23): 3/3 test infrastructure critical blockers risolti

**Distribuzione Attuale (Post-P0):**
- **P0 (CRITICAL)** - ✅ 3 issue completate: #1729 (test timeouts), #1728 (barrier leak), #1727 (DbContext disposal)
- **P1 (HIGH)** - 28 issue (23.5%): Security, Backend API, Test foundation, BGAI core - ~224h
- **P2 (MEDIUM)** - 46 issue (38.7%): BGAI features, E2E tests, Frontend refactor - ~368h
- **P3 (LOW)** - 45 issue (37.8%): HyperDX, Infrastructure, Documentation, Test improvements - ~352h
- **DEFERRED** - 37 issue: Admin console, Frontend epics phase 2-6 - Post-MVP

**MVP Scope Rimanente**: **119 issue**, **~944h effort** = **9-11 settimane @ 2 FTE**

---

## 🚀 Sequenza di Esecuzione

### 📍 P0: CRITICAL BLOCKERS - ✅ COMPLETATI

**Status**: ✅ **COMPLETED** (2025-11-23)

**Issue P0 Completate (3 issue, ~24h):**
- ✅ **#1729** - Fix test timeouts (commit: f049695)
- ✅ **#1728** - Fix barrier resource leak (commit: dea289d)
- ✅ **#1727** - Fix DbContext disposal test (commit: 43c3afa)

**Risultati Raggiunti**:
- ✅ Test suite stabile e privo di resource leak
- ✅ Zero indefinite hangs nei test
- ✅ Tutti i test infrastructure issues risolti
- ✅ Foundation solida per coverage improvements

**Completion Gate**: ✅ PASSED - Test suite stable, zero resource leaks

---

### 📍 P1: HIGH PRIORITY FOUNDATION (3-4 settimane, 224h)

**Dipendenze**: ✅ P0 completati
**Focus**: Security, Backend API, Test infrastructure, BGAI core features
**Status**: 🚀 **READY TO START** (blockers rimossi)

#### 🛡️ Security & Production Readiness (2 issue, ~16h)
- **#575** - [P1] AUTH-08: Admin Override for 2FA Locked-Out Users
- **#576** - [P1] SEC-05: Security Penetration Testing ⚠️ **START NOW** (24-40h external lead time)

#### ⚙️ Backend API Core (2 issue, ~16h)
- **#1006** - [P1] Backend API integration (/api/v1/board-game-ai/ask) 🔥 **BLOCKS 40+ frontend issues**
- **#1007** - [P1] Streaming SSE support for real-time responses

#### 🧪 Test Infrastructure Foundation (5 issue, ~40h)
- **#1502** - [P1] Extract SSE Mock Helper (frontend)
- **#1503** - [P1] Replace Global Fetch Mocks (frontend)
- **#1504** - [P1] Split Large Test Files (frontend)
- **#1662** - [P1] Fix remaining 13 test failures (Redis/OAuth infrastructure)
- **#1678** - [P1] Fix Test Infrastructure Issues

#### 🎮 BGAI Month 3-6 Core (19 issue, ~152h)
- **#978** - [P1] [BGAI-036] End-to-end testing (Month 3)
- **#983** - [P1] [BGAI-041] Extend PromptEvaluationService (Month 4)
- **#989** - [P1] [BGAI-048] Base components (Month 4)
- **#992-993** - [P1] [BGAI-051-052] Frontend testing (Month 4)
- **#994-995** - [P1] [BGAI-054-055] Build optimization + integration testing (Month 4)
- **#996-1000** - [P1] [BGAI-056-060] Dataset annotation + quality baseline (Month 5)
- **#1001-1009** - [P1] [BGAI-061-069] Components + error handling + E2E (Month 5)
- **#1018-1023** - [P1] [BGAI-080-085] E2E + accuracy + performance + completion (Month 6)
- **#1730-1733** - [P1] Test quality improvements

**Completion Gate**: Frontend ≥90% coverage, Backend API complete, SSE streaming operational, Test suite stable

---

### 📍 P2: MVP FEATURES (4-5 settimane, 368h)

**Dipendenze**: P1 al 100%
**Focus**: BGAI features, E2E testing, Frontend refactoring, Test improvements

**Status**: ✅ 46 issue identificate (triage completo)

#### 🎮 BGAI Features & Components (15 issue, ~120h)
- **#1010-1017** - [P2] [BGAI-070-078] Dataset annotation + PDF viewer + Game catalog + Italian UI
- **#1021-1022** - [P2] [BGAI-083-084] Bug fixes + documentation
- **#1436** - [P2] Fix SWR + Zustand State Duplication

#### 🧪 E2E Testing Expansion (8 issue, ~64h)
- **#1492-1498** - [P2] [E2E-006 to E2E-012] POM migration, timeouts, negative scenarios, stability, visual regression, browser matrix, coverage

#### 🎨 Frontend Refactoring (4 issue, ~32h)
- **#1666** - [P2] Consolidate Duplicate React Components
- **#1667** - [P2] Remove Deprecated Profile Page
- **#1668** - [P2] Update Component Imports to Subdirectory Paths
- **#1675** - [P2] Implement Missing Frontend Backend APIs

#### 🧪 Test Suite Improvements (19 issue, ~152h)
- **#1505-1511** - [P2] [TEST-007 to TEST-013] Backend + Frontend test improvements
- **#1734-1741** - [P2] Test quality improvements (assertions, builders, duplication, style, cancellation, performance, parameterized, categories)

**Completion Gate**: All MVP features complete, 150 Q&A pairs ready, E2E suite comprehensive

---

### 📍 P3: POLISH & INFRASTRUCTURE (2-3 settimane, 352h)

**Dipendenze**: P2 al 100%
**Focus**: Observability, Infrastructure improvements, Documentation, Admin console features

**Status**: ✅ 44 issue identificate (triage completo)

#### 📊 HyperDX Observability Platform (10 issue, ~80h)
- **#1561** - [P3] EPIC: Implement HyperDX Observability Platform
- **#1562-1570** - [P3] HyperDX deployment, configuration, integration, testing, documentation

#### 🏗️ Infrastructure & DevOps (5 issue, ~40h)
- **#701-703** - [P3] Docker resource limits, Compose profiles, Traefik proxy
- **#705-706** - [P3] Infrastructure monitoring, operational runbooks

#### 📝 Documentation & Code Cleanup (4 issue, ~32h)
- **#1679** - [P3] Cleanup Legacy Comments and Deprecation Markers
- **#1680** - [P3] Audit Infrastructure Services
- **#1681** - [P3] Update Legacy Documentation References
- **#1725** - [P3] LLM Token Tracking - Advanced Features

#### 🎨 Admin Console Optional Features (25 issue, ~200h)
- **#874-889** - [P3] FASE 1-2: Dashboard + Infrastructure monitoring (15 issues)
- **#1742-1747** - [P3] Test edge cases, security tests, optimizations (6 issues)
- **#1674, #1676-1677** - [P3] Backend cleanup (TODO comments, backward compat, obsolete models)

**Completion Gate**: Observability operational, Infrastructure documented, Code cleanup complete

**Note**: Admin console features are optional for MVP - can be deferred if timeline pressure

---

### 🚫 DEFERRED (Post-MVP, 37 issue)

**Scope**: Admin console advanced features, Frontend epics phase 2-6, Infrastructure advanced

**Categories**:
- **Admin Console** (30 issue): Reporting, bulk operations, API key management, enhanced monitoring
- **Frontend Epics** (6 issue): React 19 optimization, App Router migration, Advanced features, Design polish, Performance & Accessibility
- **Infrastructure** (1 issue): Infisical secret rotation POC

**Rationale**: These features are valuable but not required for MVP launch. Can be implemented post-launch based on user feedback.

---

## 📊 EXECUTION DASHBOARD

### Critical Path Timeline (Aggiornato - Current State)

```
Week 0 (NOW) │ ✅ G0: TRIAGE COMPLETE + G1: P0 COMPLETE
             │ ├─ 159 issue categorizzate (100%)
             │ ├─ P0 risolti: #1729, #1728, #1727
             │ └─ Coverage: Frontend 90.03%, Backend 90%+
             ↓ ✅ GATES PASSED: Test suite stable, foundation ready
             ↓
Week 1-4     │ 🚀 P1: High Priority Foundation (28 issue, 224h)
             │ ├─ Week 1: Security audit start (#576) 🔥 CRITICAL
             │ ├─ Week 1-2: Backend API Core (#1006, #1007) 🔥 BLOCKER
             │ ├─ Week 2-3: Test Infrastructure (#1502-1504, #1662, #1678)
             │ └─ Week 3-4: BGAI Core Features (19 issues) - parallel
             ↓ GATE G2: Backend API complete, SSE operational, E2E stable
             ↓
Week 5-9     │ P2: MVP Features (46 issue, 368h)
             │ ├─ BGAI Features & Dataset (15 issues)
             │ ├─ E2E Testing Expansion (8 issues)
             │ ├─ Frontend Refactoring (4 issues)
             │ └─ Test Suite Improvements (19 issues)
             ↓ GATE G3: All MVP features complete, 150 Q&A ready
             ↓
Week 10-13   │ P3: Polish & Infrastructure (45 issue, 352h)
             │ ├─ HyperDX Observability (10 issues)
             │ ├─ Infrastructure & DevOps (5 issues)
             │ ├─ Documentation & Cleanup (4 issues)
             │ └─ Admin Console Optional + Test improvements (26 issues)
             ↓ GATE G4: Security approved, Production ready
             ↓
Week 14-15   │ 🚀 FINAL PREP & LAUNCH
             │ ├─ Documentation complete
             │ ├─ Monitoring operational
             │ ├─ Rollback plan tested
             │ └─ Smoke tests passed
             ↓ GATE G5: Launch Ready
             ↓
Week 15      │ 🎉 PRODUCTION LAUNCH
             │ Target: Fine Febbraio - Inizio Marzo 2026
```

**Critical Dependencies (UPDATED)**:
- ✅ ~~P0 blockers~~ - COMPLETATI
- ⚠️ Security audit (#576): **MUST START NOW** (external 24-40h lead time) 🔥
- 🔥 Backend API (#1006, #1007): **Week 1 PRIORITY** (blocks 40+ frontend issues)
- 🎯 Test infrastructure (#1502-1504, #1662, #1678): Week 2-3 (enables E2E expansion)

### Resource Allocation (Aggiornato Post-P0)

**Distribuzione Attuale:**

| Priority | Count | Status | Effort Rimanente (h) | Weeks @ 2 FTE |
|----------|-------|--------|---------------------|---------------|
| **P0** | 3 | ✅ **COMPLETATO** | 0h | 0 weeks |
| **P1** | 28 | 🚀 In Progress | 224h | 2.8 weeks |
| **P2** | 46 | ⏳ Pending | 368h | 4.6 weeks |
| **P3** | 45 | ⏳ Pending | 352h | 4.4 weeks |
| **MVP RIMANENTE** | **119** | - | **944h** | **11.8 weeks** |
| **DEFERRED** | 37 | ⏳ Post-MVP | - | - |
| **COMPLETATO** | 3 (P0) | ✅ Done | 32h (spent) | - |
| **GRAND TOTAL** | **159** | - | 944h rimanente | ~10-11 weeks |

**Team Capacity**:
- 2 FTE developers
- 40h/week effective capacity per developer
- 80h/week total team capacity
- 11-week rimanenti = 880h disponibili vs 944h needed
- **Gap**: 64h (~1 settimana) → Richiede piccolo buffer o overtime
- **Feasibility**: ✅ **ACHIEVABLE** con disciplina execution

---

## 🚨 CRITICAL SUCCESS FACTORS

### 🔥 Top 5 Risks & Mitigations (Aggiornato Post-Triage)

| Risk | Impact | Probability | Mitigation | Status |
|------|--------|-------------|------------|--------|
| **Security audit failure** | 🔴 CRITICAL | Medium | Avviare SUBITO (#576), external auditor, 24-40h lead time | ⚠️ **ACTION REQUIRED** |
| **Backend API delay** | 🔴 HIGH | Medium | #1006 priorità assoluta, blocca 40+ frontend issues | 🎯 Week 1 target |
| **Effort underestimato** | 🟡 MEDIUM | Medium | Buffer 20% mantenuto, re-stima continua | ✅ Monitoring |
| **E2E coverage gap** | 🟡 MEDIUM | Medium | P2 comprehensive E2E expansion (#1492-1498) | 🎯 Weeks 8-12 |
| **Scope creep** | 🟡 MEDIUM | Low | Strict DEFERRED policy, 37 issue già deferred | ✅ Controllato |

**Rischi Eliminati**:
- ✅ ~~Issue non triagiate~~ - Completato (159/159)
- ✅ ~~Priorità nascoste~~ - Identificate e categorizzate
- ✅ ~~P0 blockers~~ - Tutti risolti (#1729, #1728, #1727)

### 🎯 Quality Gates (Aggiornato)

| Gate | Criteria | Blocker | Status | Completato |
|------|----------|---------|--------|------------|
| **G0: Triage Complete** | 159 issue triagiate, priorità assegnate | Yes | ✅ **PASSED** | 2025-11-23 |
| **G1: P0 Complete** | All P0 closed, test suite stable | Yes | ✅ **PASSED** | 2025-11-23 |
| **G1b: Coverage Foundation** | Frontend ≥90%, Backend ≥90% | Yes | ✅ **PASSED** | Pre-existing |
| **G2: Test Foundation** | P1 complete, E2E stable, Backend API live | Yes | 🎯 **IN PROGRESS** | Target: Week 4-5 |
| **G3: Features Complete** | 150 Q&A, Italian 100%, PDF live | Yes | ⏳ **PENDING** | Target: Week 10 |
| **G4: Security Approved** | Pen test pass, 0 crit/high vulns | Yes | ⏳ **PENDING** | Target: Week 13 |
| **G5: Launch Ready** | Docs, monitoring, rollback plan | Yes | ⏳ **PENDING** | Target: Week 15 |

### 📈 KPI Status

| KPI | Current | Target | Status |
|-----|---------|--------|--------|
| **Issue Triage Progress** | 159/159 (100%) | 100% | ✅ **ACHIEVED** |
| **P0 Completion** | 3/3 (100%) | 100% | ✅ **ACHIEVED** |
| **Frontend Coverage** | 90.03% | ≥90% | ✅ **ACHIEVED** |
| **Backend Coverage** | 90%+ | ≥90% | ✅ **ACHIEVED** |
| **Test Count** | 4,225 total | N/A | ✅ **STRONG** |
| **E2E Coverage** | ~60% | ≥80% | 🟡 In Progress (G2) |
| **Accuracy (Golden)** | ~75% | ≥80% | 🟡 In Progress (G3) |
| **P95 Latency** | <3s | <3s | ✅ **ACHIEVED** |
| **Lighthouse Score** | 90+ | ≥90 | ✅ **ACHIEVED** |
| **Security Vulns (Crit/High)** | 0 | 0 | ✅ **MAINTAINED** |

---

## 🎉 NEXT ACTIONS (Immediate - Week 1)

### ✅ COMPLETATO: Gate G0 & G1

**G0 - Triage**: ✅ 159/159 issue categorizzate (100%)
**G1 - P0 Critical**: ✅ 3/3 issue risolte (#1729, #1728, #1727)
**Coverage Gates**: ✅ Frontend 90.03%, Backend 90%+

### 🚀 AVVIO P1: High Priority Foundation (Settimana Corrente)

**Obiettivi Immediati** (Prossimi 3-5 giorni):

#### 1. 🛡️ Security (START NOW - Critical Path)
- **#576** - Avviare Security Penetration Testing
  - **URGENTE**: External auditor richiede 24-40h lead time
  - **Action**: Contattare auditor, schedulare testing
  - **Blocca**: Launch (Gate G4)

#### 2. ⚙️ Backend API Core (Unblocks 40+ Frontend Issues)
- **#1006** - Implementare Backend API integration (/api/v1/board-game-ai/ask)
  - **Priorità**: CRITICAL (blocca frontend BGAI)
  - **Effort**: ~8h
- **#1007** - Aggiungere Streaming SSE support
  - **Priorità**: HIGH (MVP feature)
  - **Effort**: ~8h

#### 3. 🧪 Test Infrastructure Foundation
- **#1662** - Fix remaining 13 test failures (Redis/OAuth)
- **#1678** - Fix Test Infrastructure Issues
- **#1502-1504** - Test refactoring (SSE mock, split files, global mocks)
- **Effort totale**: ~40h
- **Obiettivo**: Coverage ≥90% consolidata, test suite scalabile

#### 4. 🎮 BGAI Core - Parallel Track
- Avviare prime 5-8 issue BGAI Month 3-4 (#978, #983, #989, etc.)
- Focus: End-to-end testing, base components
- Resource: 1 dev parallel su BGAI mentre altro dev su API core

**Goal Week 1**: Backend API operativo, Security audit schedulato, Test foundation stabile

---

## 📦 DEFERRED: POST-MVP (12+ Issue)

**Status**: ⚪ Non prioritarie per MVP
**Timeline**: 2026 Q2-Q3

**Issue Confermate DEFERRED (12 issue, ~96-150h):**
- 9 Epic issues deferred (frontend phases, admin console, infrastructure)
- 3+ additional feature enhancements

**Epic Principali Deferred:**
- Epic #935: Performance & Accessibility (Phase 6)
- Epic #934: Design Polish (Phase 5)
- Epic #933: App Router Migration (Phase 3)
- Epic #932: Advanced Features (Phase 4)
- Epic #931: React 19 Optimization (Phase 2)
- Epic #926: Foundation & Quick Wins (Phase 1)
- Epic #915: FASE 4 Advanced Features
- Epic #903: FASE 3 Enhanced Management
- Epic #890: FASE 2 Infrastructure Monitoring

**Rationale**: MVP-first, these enhance existing features but not required for launch

---

## 📝 CHANGELOG

### v16.0 (2025-11-23) - CONSOLIDATED ROADMAP POST-COMPLETION

**🎉 CONSOLIDAMENTO**: Aggiornamento con stato effettivo post-P0 completion

**Major Changes**:
- ✅ **P0 Status**: Aggiornato da "planned" a "COMPLETED" (3/3 issues)
  - #1729 (test timeouts), #1728 (barrier leak), #1727 (DbContext) risolti
- ✅ **Coverage**: Aggiornato a valori reali (Frontend 90.03%, Backend 90%+)
- ✅ **KPIs**: Riflettono achievements reali (5/10 targets già raggiunti)
- ✅ **Quality Gates**: G0 e G1 marcati come PASSED con date
- ✅ **Timeline**: Aggiornato a "Week 0 (NOW)" con P0 completed
- ✅ **Risks**: Rimossi rischi risolti (triage, P0), focus su security audit
- ✅ **Next Actions**: Da "triage planning" a "P1 execution ready"
- ✅ **Resource Allocation**: Aggiornato effort rimanente (944h vs 976h)

**Issue Status Update**:
- **P0**: 3 issue → ✅ COMPLETATE (0h rimanenti)
- **P1**: 28 issue → 🚀 READY TO START (224h)
- **P2**: 46 issue → ⏳ Pending (368h)
- **P3**: 45 issue → ⏳ Pending (352h) [+1 da test improvements]
- **Deferred**: 37 issue → Post-MVP
- **Total MVP Rimanente**: 119 issue, 944h (~10-11 settimane)

**Documentation Quality**:
- ✅ Eliminati riferimenti obsoleti a "132 untagged issues"
- ✅ Consolidate informazioni da CLAUDE.md, git commits, e test reports
- ✅ Timeline realistico basato su actual progress
- ✅ Critical dependencies chiaramente identificate (security audit, backend API)

**Next Focus**: P1 High Priority Foundation (Security, Backend API, Test Infrastructure, BGAI Core)

---

### v15.0 (2025-11-23) - REAL ISSUE STATE UPDATE

**Changes**:
- ✅ Aggiornato conteggio totale: 131 → 159 issue aperte
- ✅ Mappato stato reale prioritizzazione
- ✅ Aggiunto G0 Quality Gate: Triage obbligatorio
- ✅ Aggiornata sezione rischi
- ✅ Riviste stime effort

### v14.0 (2025-11-23) - CLEANUP & SIMPLIFICATION

**Changes**:
- ✅ Semplificato roadmap da 498 a 280 righe
- ✅ Rimossi 9 file markdown obsoleti/duplicati
- ✅ Consolidata documentazione in struttura chiara
- ✅ Focus su execution invece di planning teorico
- ✅ Mantenuti dettagli tecnici nei file di implementazione

**File Rimossi**:
- Stub files: team_structure.md, org_chart.md, roles_playbook.md, onboarding_guide.md
- Duplicati: bgai-issue-tracking-summary.md, manual-issue-creation-guide.md (x2)
- Obsoleti: board-game-ai-execution-calendar.md, board-game-ai-sprint-overview.md

---

## 📚 DOCUMENT NAVIGATION

**Per dettagli tecnici backend**: `planning/backend-implementation-plan.md`
**Per dettagli tecnici frontend**: `planning/frontend-implementation-plan.md`
**Per quick start**: `planning/QUICK-START.md`
**Per overview generale**: `README.md`

**Documentazione Completa**: Vedi `/docs/INDEX.md` (115 docs, 800+ pagine)

---

**Versione**: 16.0 (Consolidated Post-P0 Completion)
**Owner**: Engineering Team
**Issue Totali**: 159 (3 completate + 119 MVP rimanenti + 37 deferred)
**Effort to MVP**: 944h rimanenti (~10-11 settimane @ 2 FTE)
**Target Launch**: Fine Febbraio - Inizio Marzo 2026
**Last Updated**: 2025-11-23

---

🚀 **"Foundation complete, execution in progress"** 🚀