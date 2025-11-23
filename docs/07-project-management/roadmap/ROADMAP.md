# 🗺️ MeepleAI Master Roadmap

**Ultimo Aggiornamento**: 2025-11-23
**Stato Progetto**: Alpha (DDD 99% completo, 2,070 righe legacy rimosse)
**Issue Aperte**: 165 (21 prioritizzate, 132 da triaggiare, 12 deferred)
**Timeline to MVP**: 6-8 settimane
**Target Launch**: Fine Gennaio 2026

---

## 📋 Executive Summary

MeepleAI è un assistente AI per regolamenti di giochi da tavolo, con focus italiano e target accuracy >95%.

**Stack**: ASP.NET 9, Next.js 16, React 19, PostgreSQL, Qdrant, Redis, OpenRouter, n8n
**Architettura**: 7 Bounded Contexts (DDD/CQRS), 99% migrazione completata
**Fase Attuale**: Alpha - Frontend coverage 66% → 90% (P0 blocker)

### 🎯 Stato Attuale Prioritizzazione

**⚠️ NOTA IMPORTANTE**: Su 165 issue aperte, solo 21 hanno priorità esplicita nel titolo:

**Priorità Correnti (Issue Tagged):**
- **P0 (CRITICAL)** - 3 issue: Test isolation, resource leaks, test scenarios
- **P1 (HIGH)** - 4 issue: Database tests, assertions, hardcoded values, async operations
- **P2 (MEDIUM)** - 8 issue: Test enhancements, assertions, data builders
- **P3 (LOW)** - 6 issue: Edge cases, security tests, optimizations
- **DEFERRED** - 12 issue: 9 Epic deferred + 3 altre feature post-MVP

**Da Triaggiare**: ~132 issue senza priorità (80% del totale)

### 📋 Priorità Pianificate (Target Post-Triage)

Questa è la distribuzione target una volta completato il triage delle 132 issue:

**P0 (CRITICAL)** - Target: 5-10 issue, 36-80h
**P1 (HIGH)** - Target: 25-35 issue, 180-280h
**P2 (MVP)** - Target: 40-50 issue, 300-450h
**P3 (LAUNCH)** - Target: 20-30 issue, 150-250h
**DEFERRED** - Confermati: 12-15 issue post-MVP

---

## 🚀 Sequenza di Esecuzione

### 📍 P0: CRITICAL BLOCKERS (1-2 giorni)

**MUST COMPLETE FIRST** - Blocking everything else

**Issue Attuali P0 (3 issue, 6-12h):**
- **#1729** - [P0] Missing test timeouts can cause indefinite hangs
- **#1728** - [P0] Barrier resource leak in concurrent race condition test
- **#1727** - [P0] DbContext disposal test creates invalid test scenario

**⚠️ AZIONE RICHIESTA**:
1. Completare le 3 P0 attuali (1-2 giorni)
2. Triaggiare le 132 issue non prioritizzate
3. Identificare altri critical blockers nascosti

**Completion Gate**: 100% delle P0 attuali + triage completato

---

### 📍 P1: HIGH PRIORITY FOUNDATION (variabile, da definire post-triage)

**Dipendenze**: P0 al 100% + triage completato
**Focus**: Testing infrastructure + core quality

**Issue Attuali P1 (4 issue, 8-16h):**
- **#1733** - [P1] InMemoryDatabase used to test real database failures
- **#1732** - [P1] Overly broad error message assertions reduce test value
- **#1731** - [P1] Hardcoded magic numbers cause test fragility
- **#1730** - [P1] Synchronous File.Exists() blocks threads in async tests

**⚠️ PIANIFICAZIONE POST-TRIAGE**:

Una volta completato il triage delle ~132 issue, verranno identificate:
- Test Infrastructure issues (target: 10-15 issue)
- Backend Testing & Quality (target: 8-12 issue)
- E2E Testing Expansion (target: 8-12 issue)

**Estimated P1 Total Post-Triage**: 25-35 issue, 180-280h
**Completion Gate**: Frontend ≥90%, Backend tests pass, E2E suite stable

---

### 📍 P2: MVP FEATURES (da definire post-triage)

**Dipendenze**: P1 al 100% + triage completato
**Focus**: Golden dataset + Italian UI + Feature completion

**Issue Attuali P2 (8 issue, 16-24h):**
- **#1741** - [P2] Missing custom assertions for domain objects
- **#1740** - [P2] No test categories or traits
- **#1739** - [P2] Missing parameterized tests for validation scenarios
- **#1738** - [P2] Missing test data builders
- **#1737** - [P2] Unreliable GC.Collect() in performance tests
- **#1736** - [P2] Missing cancellation token tests
- **#1735** - [P2] Inconsistent assertion style across test suite
- **#1734** - [P2] Significant code duplication in mock setup

**⚠️ PIANIFICAZIONE POST-TRIAGE**:

Le 132 issue da triaggiare dovrebbero includere:
- Golden Dataset Annotation (target: 8-10 issue)
- Italian UI & Localization (target: 10-15 issue)
- PDF Viewer & Game Catalog (target: 8-12 issue)
- Feature Polish & Completion (target: 12-18 issue)

**Estimated P2 Total Post-Triage**: 40-50 issue, 300-450h
**Completion Gate**: Tutte le features MVP operative, 150 Q&A pairs ready

---

### 📍 P3: SECURITY & LAUNCH PREP (da definire post-triage)

**Dipendenze**: P2 al 100%
**Focus**: Security audit + documentation + final polish

**Issue Attuali P3 (6 issue, 12-18h):**
- **#1747** - [P3] Missing edge case tests
- **#1746** - [P3] Missing security tests
- **#1745** - [P3] Shared container optimization opportunity
- **#1744** - [P3] Parallel test execution disabled
- **#1743** - [P3] Quota incremented before processing completes
- **#1742** - [P3] Potential idempotency issue in background processing

**⚠️ PIANIFICAZIONE POST-TRIAGE**:

Le 132 issue da triaggiare dovrebbero includere:
- Security Audit & Hardening (target: 8-12 issue)
- Documentation & User Guides (target: 8-12 issue)
- Launch Preparation (target: 6-10 issue)

**Estimated P3 Total Post-Triage**: 20-30 issue, 150-250h
**Completion Gate**: Security approved, docs complete, production ready

---

## 📊 EXECUTION DASHBOARD

### Critical Path Timeline

**⚠️ NOTA**: Timeline da rivedere post-triage (Week 1)

```
Week 1      │ 🚨 G0: TRIAGE GATE (165 issue, 16-24h)
            │ ├─ 3 giorni: Review e prioritizzazione 132 issue
            │ ├─ Identificare P0/P1 nascosti
            │ └─ Re-stima effort e timeline
            ↓ GATE: 100% issue triagiate e prioritizzate 🔥
            ↓
Week 2-3    │ P0: Critical Blockers (stima post-triage: 5-10 issue, 36-80h)
            │ └─ 3 attuali + nuovi P0 da triage
            ↓ GATE: All P0 closed
            ↓
Week 4-6    │ P1: High Priority Foundation (stima: 25-35 issue, 180-280h)
            │ ├─ Test Infrastructure
            │ ├─ Backend Testing
            │ └─ E2E Expansion
            ↓ GATE: Frontend ≥90% Coverage, E2E stable
            ↓
Week 7-11   │ P2: MVP Features (stima: 40-50 issue, 300-450h)
            │ ├─ Golden Dataset
            │ ├─ Italian UI
            │ ├─ PDF & Catalog
            │ └─ Feature Polish
            ↓ GATE: All features complete, 150 Q&A ready
            ↓
Week 12-14  │ P3: Security & Launch (stima: 20-30 issue, 150-250h)
            │ ├─ Security Audit
            │ ├─ Documentation
            │ └─ Launch Prep
            ↓ GATE: Security approved
            ↓
Week 15-16  │ 🚀 PRODUCTION LAUNCH (data da confermare post-triage)
```

**⚠️ CRITICAL**: Questa timeline presuppone che il triage non riveli troppi P0/P1 critici nascosti. Se emergeranno 10+ P0 aggiuntivi, la timeline slitterà di 2-4 settimane.

### Resource Allocation

**Stato Attuale (21 issue prioritizzate):**

| Priority | Current Count | Effort | Status |
|----------|--------------|--------|--------|
| **P0** | 3 | 6-12h | ✅ In corso |
| **P1** | 4 | 8-16h | ⏸️ Pending P0 |
| **P2** | 8 | 16-24h | ⏸️ Pending triage |
| **P3** | 6 | 12-18h | ⏸️ Pending triage |
| **Deferred** | 12 | 96-150h | ⚪ Post-MVP |
| **Untagged** | 132 | TBD | ⚠️ **TRIAGE NEEDED** |
| **TOTAL** | **165** | **138-220h + TBD** | |

**Allocazione Target Post-Triage:**

| Type | P0 | P1 | P2 | P3 | Total | Effort |
|------|----|----|----|----|-------|--------|
| **Target MVP Issues** | 5-10 | 25-35 | 40-50 | 20-30 | 90-125 | 666-1,060h |
| **Deferred** | 0 | 0 | 0 | 0 | 12-15 | 96-150h |
| **Out of Scope** | - | - | - | - | 25-40 | - |
| **TOTAL** | **5-10** | **25-35** | **40-50** | **20-30** | **165** | **762-1,210h** |

**Team Size**: 2-3 developers full-time
**Velocity Required**: 10-15 issue/settimana (post-triage)

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
