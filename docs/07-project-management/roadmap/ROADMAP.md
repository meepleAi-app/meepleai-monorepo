# 🗺️ MeepleAI Master Roadmap

**Ultimo Aggiornamento**: 2025-11-23 (Post-Triage)
**Stato Progetto**: Alpha (DDD 99% completo, Triage 100% completo)
**Issue Aperte**: 159 (122 MVP prioritizzate, 37 deferred)
**Timeline to MVP**: 10-12 settimane (976h effort, 2 FTE)
**Target Launch**: Fine Febbraio - Inizio Marzo 2026

---

## 📋 Executive Summary

MeepleAI è un assistente AI per regolamenti di giochi da tavolo, con focus italiano e target accuracy >95%.

**Stack**: ASP.NET 9, Next.js 16, React 19, PostgreSQL, Qdrant, Redis, OpenRouter, n8n
**Architettura**: 7 Bounded Contexts (DDD/CQRS), 99% migrazione completata
**Fase Attuale**: Alpha - Post-Triage, ready for Sprint 1 execution

### 🎯 Prioritizzazione Completata ✅

**TRIAGE COMPLETE** (2025-11-23): 159 issue categorizzate in 4h

**Distribuzione Reale (Post-Triage):**
- **P0 (CRITICAL)** - 4 issue (2.5%): Test infrastructure critical blockers - ~32h
- **P1 (HIGH)** - 28 issue (17.6%): Security, Backend API, Test foundation, BGAI core - ~224h
- **P2 (MEDIUM)** - 46 issue (28.9%): BGAI features, E2E tests, Frontend refactor - ~368h
- **P3 (LOW)** - 44 issue (27.7%): HyperDX, Infrastructure, Documentation - ~352h
- **DEFERRED** - 37 issue (23.3%): Admin console, Frontend epics phase 2-6 - Post-MVP

**MVP Scope**: **122 issue**, **~976h effort** = **10-12 settimane @ 2 FTE**

---

## 🚀 Sequenza di Esecuzione

### 📍 P0: CRITICAL BLOCKERS (1-2 giorni, 32h)

**MUST COMPLETE FIRST** - Blocking everything else

**Status**: ✅ 4 issue identificate (triage completo)

**Issue P0 (4 issue, ~32h):**
- **#1729** - [P0] Missing test timeouts can cause indefinite hangs
- **#1728** - [P0] Barrier resource leak in concurrent race condition test  
- **#1727** - [P0] DbContext disposal test creates invalid test scenario
- **#1726** - [P0] Additional critical test infrastructure issue

**Focus**: Test infrastructure stability and reliability

**Completion Gate**: 100% P0 closed, test suite stable, zero resource leaks

---

### 📍 P1: HIGH PRIORITY FOUNDATION (3-4 settimane, 224h)

**Dipendenze**: P0 al 100%
**Focus**: Security, Backend API, Test infrastructure, BGAI core features

**Status**: ✅ 28 issue identificate (triage completo)

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

### Critical Path Timeline (Updated Post-Triage)

```
Week 1      │ ✅ G0: TRIAGE COMPLETE
            │ └─ 159 issue categorizzate (4h vs 3 giorni stimati)
            ↓ 
Week 2-3    │ P0: Critical Blockers (4 issue, 32h)
            │ └─ Test infrastructure stability fixes
            ↓ GATE: All P0 closed, test suite stable
            ↓
Week 4-7    │ P1: High Priority Foundation (28 issue, 224h)
            │ ├─ Security audit initiation (#576) 🔥 EXTERNAL DEPENDENCY
            │ ├─ Backend API Core (#1006, #1007)
            │ ├─ Test Infrastructure (#1502-1504, #1662, #1678)
            │ └─ BGAI Core Features (19 issues)
            ↓ GATE: Frontend ≥90%, Backend API complete, SSE operational
            ↓
Week 8-12   │ P2: MVP Features (46 issue, 368h)
            │ ├─ BGAI Features & Dataset
            │ ├─ E2E Testing Expansion
            │ ├─ Frontend Refactoring
            │ └─ Test Suite Improvements
            ↓ GATE: All MVP features complete, 150 Q&A ready
            ↓
Week 13-15  │ P3: Polish & Infrastructure (44 issue, 352h)
            │ ├─ HyperDX Observability
            │ ├─ Infrastructure & DevOps
            │ ├─ Documentation & Cleanup
            │ └─ Admin Console Optionals
            ↓ GATE: Production ready
            ↓
Week 16     │ 🚀 PRODUCTION LAUNCH
            │ Target: Fine Febbraio - Inizio Marzo 2026
```

**Critical Dependencies**:
- Security audit (#576): Must start Week 2-3 (external 24-40h lead time)
- Backend API (#1006, #1007): Blocks 40+ frontend issues
- Test infrastructure (#1502-1504, #1662, #1678): Prerequisite for coverage goals

### Resource Allocation (Post-Triage)

**Actual Distribution:**

| Priority | Count | % MVP | Effort (h) | Weeks @ 2 FTE |
|----------|-------|-------|------------|---------------|
| **P0** | 4 | 3.3% | 32 | 0.4 weeks |
| **P1** | 28 | 23.0% | 224 | 2.8 weeks |
| **P2** | 46 | 37.7% | 368 | 4.6 weeks |
| **P3** | 44 | 36.1% | 352 | 4.4 weeks |
| **MVP TOTAL** | **122** | **100%** | **976h** | **12.2 weeks** |
| **DEFERRED** | 37 | - | Post-MVP | - |
| **GRAND TOTAL** | **159** | - | ~976h MVP | ~12 weeks |

**Team Capacity**:
- 2 FTE developers
- 40h/week effective capacity per developer
- 80h/week total team capacity
- 12-week MVP = 960h available ≈ 976h needed ✅ **FEASIBLE**

---

## 🚨 CRITICAL SUCCESS FACTORS

### 🔥 Top 5 Risks & Mitigations

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| **132 issue non triagiate** | 🔴 CRITICAL | High | Sessione triage urgente 2-3 giorni, team completo |
| **Priorità nascoste in 132 issue** | 🔴 CRITICAL | Medium | Review sistematica, identificare P0/P1 urgenti |
| **Effort underestimato** | 🟡 HIGH | High | Buffer 30%, re-stima post-triage |
| **Security audit failure** | 🔴 CRITICAL | Low | Start early, external auditor, triage security issues |
| **Scope creep durante triage** | 🟡 HIGH | Medium | Strict DEFERRED labels, weekly review |

### 🎯 Quality Gates

| Gate | Criteria | Blocker | Phase |
|------|----------|---------|-------|
| **G0: Triage Complete** | 165 issue triagiate, priorità assegnate | Yes | Week 1 🔥 |
| **G1: P0 Complete** | All P0 closed (3 attuali + nuovi da triage) | Yes | Week 2 |
| **G2: Test Foundation** | P1 complete, E2E stable | Yes | Week 5 |
| **G3: Features Complete** | 150 Q&A, Italian 100%, PDF live | Yes | Week 10 |
| **G4: Security Approved** | Pen test pass, 0 crit/high vulns | Yes | Week 13 |
| **G5: Launch Ready** | Docs, monitoring, rollback plan | Yes | Week 14 |

### 📈 KPI Targets

| KPI | Current | Target | Gate |
|-----|---------|--------|------|
| **Issue Triage Progress** | 21/165 (13%) | 165/165 (100%) | G0 🔥 |
| **P0 Completion** | 0/3 (0%) | 3/3 (100%) | G1 🔥 |
| **Frontend Coverage** | 66% | ≥90% | G1 |
| **Backend Coverage** | 90%+ | ≥90% | ✅ Met |
| **E2E Coverage** | 60% | ≥80% | G2 |
| **Accuracy (Golden)** | 75% | ≥80% | G3 |
| **P95 Latency** | 2.5s | <3s | ✅ Met |
| **Lighthouse Score** | 90+ | ≥90 | ✅ Met |
| **Security Vulns (Crit/High)** | 0 | 0 | G4 |

---

## 🎉 NEXT ACTIONS (Prossime 72h)

### 🔥 URGENTE: Triage Session (Giorni 1-3)

**BLOCCO CRITICO IDENTIFICATO**: 132 issue (80%) senza priorità!

**Day 1** (2025-11-23):
1. 🚨 **TRIAGE SESSION** - Blocco 1: 50 issue (4-6h)
   - Review rapida, assegnare P0/P1/P2/P3/DEFERRED/OUT_OF_SCOPE
   - Focus: identificare P0 nascosti
2. ⏯️ **#1729, #1728, #1727** - Iniziare P0 attuali in parallelo (2-4h)

**Day 2** (2025-11-24):
3. 🚨 **TRIAGE SESSION** - Blocco 2: 50 issue (4-6h)
4. ⏯️ **Completare P0 attuali** (2-4h)

**Day 3** (2025-11-25):
5. 🚨 **TRIAGE SESSION** - Blocco 3: 32 issue residue (2-3h)
6. 📊 **CONSOLIDARE** priorità e re-stimare roadmap (2h)
7. 🔥 **AVVIARE** nuovi P0 identificati dal triage

### Week 2 Actions (Post-Triage)

**Prerequisiti**: G0 Gate completato (triage 100%)

**Obiettivi**:
- Completare tutti i P0 (stimati 5-10 issue post-triage)
- Avviare P1 ad alta priorità
- Re-pianificare timeline con dati reali

**Goal**: Tutti i P0 chiusi, P1 in corso

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

### v15.0 (2025-11-23) - REAL ISSUE STATE UPDATE

**🔥 CRITICAL DISCOVERY**: 132 issue (80%) senza priorità!

**Changes**:
- ✅ Aggiornato conteggio totale: 131 → 165 issue aperte
- ✅ Mappato stato reale prioritizzazione: 21 tagged, 132 untagged, 12 deferred
- ✅ Aggiunto G0 Quality Gate: Triage obbligatorio (Week 1)
- ✅ Aggiornata sezione rischi: triage come rischio #1
- ✅ Riviste stime effort: 762-1,210h totali (vs 704-930h pianificate)
- ✅ Aggiornati KPI: Issue triage progress 13% → 100% target
- ✅ Ridefinite next actions: focus su triage urgente (3 giorni)

**Issue Breakdown Reale**:
- P0: 3 issue (target post-triage: 5-10)
- P1: 4 issue (target: 25-35)
- P2: 8 issue (target: 40-50)
- P3: 6 issue (target: 20-30)
- Deferred: 12 issue (9 Epic + 3 altre)
- **Untagged: 132 issue - AZIONE URGENTE RICHIESTA**

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

**Versione**: 15.0 (Real Issue State Update)
**Owner**: Engineering Team
**Issue Totali**: 165 (21 prioritizzate + 132 da triaggiare + 12 deferred)
**Effort to MVP**: 762-1,210h (6-10 settimane post-triage, 2-3 devs)
**Target Launch**: Fine Gennaio/Inizio Febbraio 2026 (da confermare post-triage)

---

🚀 **"Triage first, then execute with precision"** 🚀