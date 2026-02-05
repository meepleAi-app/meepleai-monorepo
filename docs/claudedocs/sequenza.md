# Sequenza Implementazione Issue - MeepleAI

**Last Updated**: 2026-02-05
**Total Open Issues**: 37
**Active Epics**: 2 (FASE 8 User Library + FASE 9 Advanced)
**Completed Epics**: 17

---

## 🎯 SEQUENZA ISSUE (Numeri in Ordine di Priorità)

### 📋 Lista Rapida - Issue Sequence

```
🔴 FASE 8 - User Private Library (CRITICAL - ACTIVE NOW)
├─ #3475 Epic: User Private Library & Collections Management
├─ #3649: User Collection Dashboard Enhancement (FE) - 5 SP
├─ #3650: Add Game to Collection Wizard (FE) - 5 SP
├─ #3651: UserLibraryEntry PDF Association (BE) - 3 SP
└─ #3653: Private PDF Upload Endpoint (BE) - 3 SP

🟡 FASE 8.1 - Tech Debt
└─ #3531: Fix 115 Pre-Existing Unit Test Failures

✅ FASE 7.5 - Admin SharedGame PDF Workflow ✅ COMPLETED
└─ #3642: Add PDF Upload to SharedGame Wizard ✅ CLOSED

FASE 0 - Frontend-Backend Gap Analysis ✅ COMPLETED
├─ Epic:     #3594 ✅ CLOSED
├─ Admin:    ✅ DONE (3595, 3598 closed)
├─ Editor:   ✅ DONE (3597 closed)
├─ Cleanup:  ✅ DONE (3596, 3599 closed)
└─ Toolkit:  ✅ DONE (3600 closed)

FASE 1 - Critical Path (Parallel Streams) ✅ COMPLETED
├─ Backend:  ✅ DONE (3479, 3483 closed)
├─ Frontend: ✅ DONE (3376, 3375, 3378, 3379 closed via PR #3510)
└─ Tier:     ✅ DONE (3440, 3441, 3442 closed)

FASE 2 - Multi-Agent Foundation ✅ COMPLETED
├─ Backend:  ✅ DONE (3491, 3493, 3492, 3494, 3495 all closed)
├─ Tutor:    ✅ DONE (3496, 3497, 3498, 3499, 3501, 3502 all closed)
└─ Frontend: ✅ DONE (3373, 3374, 3383 all closed)

FASE 3 - Security & Dashboard ✅ COMPLETED
├─ Security: ✅ DONE (3330, 3332, 3339, 3340, 3335, 3333, 3338 all closed)
├─ Dashboard:✅ DONE (3316, 3319, 3317, 3318, 3321, 3322, 3323 all closed)
└─ MeepleCard: ✅ DONE (3326, 3328, 3329, 3334, 3331, 3336 all closed)

FASE 4 - RAG Enhancement ✅ COMPLETED
├─ Navigation: ✅ DONE (3404-3410 closed via PR #3577)
├─ Dashboard:  ✅ DONE (3300-3305 all closed, 303 tests)
└─ Docs:       ✅ DONE (3401, 3399, 3398, 3449, 3450, 3451 all closed)

FASE 5 - Plugin Architecture ✅ COMPLETED
├─ Core:     ✅ DONE (3414, 3415, 3416, 3417 all closed)
├─ Plugins:  ✅ DONE (3418-3424 all closed) - 28 plugins, 143 tests
└─ UI:       ✅ DONE (3425-3431 all closed)

FASE 6 - Visual Strategy Builder ✅ COMPLETED
├─ Setup:    ✅ DONE (3454, 3456, 3457 all closed)
├─ Blocks:   ✅ DONE (3458, 3459, 3460, 3461, 3462 all closed)
└─ Advanced: ✅ DONE (3463, 3464, 3465, 3466, 3467, 3468 all closed)

FASE 7 - Advanced Features ✅ COMPLETED
├─ Admin:    ✅ DONE (3349, 3350, 3380, 3381, 3382, 3384, 3385 all closed)
├─ AI:       ✅ DONE (#3352 AI Feedback, #3354 Invite Links closed)
└─ Sessions: ✅ DONE (#3347 Session Sharing closed)

FASE 9 - Advanced Features (FUTURE)
├─ Sessions: #3341 Epic (remaining toolkit items)
├─ RAG:      #3356 Epic, #3358 (9 variants)
└─ Infra:    #3366 Epic, #2967 Epic (infrastructure)
```

---

## 📊 SUMMARY TABLE - All Open Issues

| Fase | Epic | Issues | SP Est. | Area | Parallelizzazione | Status |
|:----:|------|:------:|:-------:|:----:|:-----------------:|:------:|
| **8** | 🔴 User Private Library | 4 | 16 | FE+BE | 2 streams | 🔴 **ACTIVE** |
| **8.1** | Tech Debt | 1 | 5 | Testing | 1 stream | 🟡 Ready |
| **0-7.5** | Completed Phases | 0 | 0 | All | - | ✅ 100% |
| **9** | Advanced Features | 20+ | 80 | All | 4 streams | ⏸️ Future |
| **10** | Infrastructure | 11 | 40 | DevOps | 1 stream | ⏸️ Future |
| **TOTAL** | **3 Active** | **~37** | **~141** | - | - | **~85%** |

---

## 🔴 FASE 8 - User Private Library (CRITICAL - ACTIVE NOW)

**Timeline**: 1 settimana | **SP**: 16 | **Epic**: #3475
**Goal**: User può gestire collezione personale con PDF privati e statistiche

### Gap Analysis

| Component | Status | Notes |
|-----------|:------:|-------|
| UserLibraryEntry Entity | ✅ | PrivatePdfId già implementato |
| PDF Upload Handler | ✅ | UploadPrivatePdfCommand esiste |
| Qdrant Indexing | ⚠️ | Serve `private_rules` collection |
| Collection Dashboard | ⚠️ | Sezione esiste, serve enhancement |
| Add Game Wizard | ⚠️ | Scaffolding esiste, serve completion |
| API Endpoints | ⚠️ | Alcuni esistono, altri da creare |

### Implementation Plan

#### Stream A: Frontend (Parallel)
| Seq | Issue | Titolo | SP | Status | Dipendenze |
|:---:|:-----:|--------|:--:|:------:|:----------:|
| 8.1 | #3649 | User Collection Dashboard Enhancement | 5 | 🔴 START | - |
| 8.2 | #3650 | Add Game to Collection Wizard - Full Completion | 5 | 🔴 START | - |

#### Stream B: Backend (Parallel)
| Seq | Issue | Titolo | SP | Status | Dipendenze |
|:---:|:-----:|--------|:--:|:------:|:----------:|
| 8.3 | #3651 | UserLibraryEntry PDF Association - Full Polish | 3 | 🔴 START | - |
| 8.4 | #3653 | Private PDF Upload Endpoint - Full Integration | 3 | 🟡 NEXT | #3651 |

### Execution Order

```
┌─────────────────────────────────────────────────────────────┐
│  DAY 1-2: PARALLEL START                                    │
├─────────────────────────────────────────────────────────────┤
│  FE Stream:  #3649 Dashboard ──────────────────────────────→│
│  FE Stream:  #3650 Wizard ─────────────────────────────────→│
│  BE Stream:  #3651 PDF Association ────────────────────────→│
├─────────────────────────────────────────────────────────────┤
│  DAY 3-4: INTEGRATION                                       │
├─────────────────────────────────────────────────────────────┤
│  BE Stream:  #3653 PDF Upload (dipende da #3651) ──────────→│
│  FE+BE:      Integration testing ──────────────────────────→│
├─────────────────────────────────────────────────────────────┤
│  DAY 5: VALIDATION                                          │
├─────────────────────────────────────────────────────────────┤
│  E2E:        Complete user journey test ───────────────────→│
│  Docs:       Update sequenza.md, roadmap.md ───────────────→│
└─────────────────────────────────────────────────────────────┘
```

### Branch Commands

```bash
# Frontend Stream - Dashboard
git checkout frontend-dev && git pull
git checkout -b feature/issue-3649-collection-dashboard
git config branch.feature/issue-3649-collection-dashboard.parent frontend-dev

# Frontend Stream - Wizard
git checkout frontend-dev && git pull
git checkout -b feature/issue-3650-add-game-wizard
git config branch.feature/issue-3650-add-game-wizard.parent frontend-dev

# Backend Stream - PDF Association
git checkout main-dev && git pull
git checkout -b feature/issue-3651-pdf-association
git config branch.feature/issue-3651-pdf-association.parent main-dev

# Backend Stream - PDF Upload
git checkout main-dev && git pull
git checkout -b feature/issue-3653-pdf-upload-integration
git config branch.feature/issue-3653-pdf-upload-integration.parent main-dev
```

### Definition of Done (Epic #3475)
- [ ] User can view collection dashboard with stats (total games, PDFs, sessions)
- [ ] User can add game via wizard (catalog or custom)
- [ ] User can upload private PDF during wizard
- [ ] Private PDF correctly associated with UserLibraryEntry
- [ ] Private PDF vectors stored in `private_rules` collection
- [ ] SSE progress streaming for PDF processing
- [ ] Quota enforcement (daily/weekly/per-game)
- [ ] E2E test: Complete user journey (add game → upload PDF → view collection)
- [ ] Documentation updated
- [ ] Performance: Collection dashboard loads < 2s

---

## 🟡 FASE 8.1 - Tech Debt (After FASE 8)

**Timeline**: 2-3 giorni | **SP**: 5 | **Issue**: #3531

| Seq | Issue | Titolo | SP | Status |
|:---:|:-----:|--------|:--:|:------:|
| 8.1.1 | #3531 | Fix 115 Pre-Existing Unit Test Failures | 5 | ⏳ Ready |

**Branch**:
```bash
git checkout main-dev && git pull
git checkout -b fix/issue-3531-unit-test-failures
git config branch.fix/issue-3531-unit-test-failures.parent main-dev
```

---

## ✅ FASE 0-7.5 - COMPLETED PHASES

### FASE 7.5 - Admin PDF Workflow ✅ COMPLETED (2026-02-05)
| Seq | Issue | Titolo | SP | Status |
|:---:|:-----:|--------|:--:|:------:|
| 7.5.1 | #3642 | Add PDF Upload to SharedGame Wizard | 5 | ✅ DONE |
| 7.5.2 | #3646 | PDF Upload Step PR | - | ✅ MERGED |

### FASE 7 - Advanced Features ✅ COMPLETED (2026-02-05)
| Seq | Issue | Titolo | SP | Status |
|:---:|:-----:|--------|:--:|:------:|
| 7.1 | #3352 | AI Response Feedback System | 3 | ✅ CLOSED |
| 7.2 | #3354 | Session Invite Links with QR | 3 | ✅ CLOSED |
| 7.3 | #3347 | Session Sharing | 3 | ✅ CLOSED |

### FASE 6 - Visual Strategy Builder ✅ COMPLETED
- 14/14 issues completed
- 23 RAG blocks implemented
- ReactFlow canvas with drag-drop

### FASE 5 - Plugin Architecture ✅ COMPLETED
- 18/18 issues completed
- 28 plugins implemented
- 143 tests passing

### FASE 4 - RAG Enhancement ✅ COMPLETED
- Navigation: PR #3577 merged
- Dashboard: 303 tests passing
- Documentation: Single source of truth

### FASE 3 - Security & Dashboard ✅ COMPLETED
- Security enforcement
- Dashboard AI insights
- MeepleCard universal system

### FASE 2 - Multi-Agent AI ✅ COMPLETED
- Context engineering framework
- Tutor agent with hybrid search
- Temporal RAG conversation memory

### FASE 1 - Critical Path ✅ COMPLETED
- Agent creation flow (PR #3510)
- Tier-strategy-model architecture

### FASE 0 - FE-BE Gap Analysis ✅ COMPLETED
- Admin sidebar navigation
- Editor navigation
- Component cleanup

---

## ⏸️ FASE 9 - Advanced Features (FUTURE)

**Timeline**: 4-6 settimane | **SP**: 80 | **Parallelizzazione**: 4 streams

### Stream A: Game Session Toolkit (#3341)
| Seq | Issue | Titolo | SP | Status |
|:---:|:-----:|--------|:--:|:------:|
| 9.1 | #3341 | Epic: Game Session Toolkit Phase 2 | - | ⏳ Future |
| - | TBD | Dice Roller, Card Deck, Timer, etc. | 30 | ⏳ Future |

### Stream B: Advanced RAG (#3356)
| Seq | Issue | Titolo | SP | Status |
|:---:|:-----:|--------|:--:|:------:|
| 9.2 | #3356 | Epic: Advanced RAG Strategies (9 variants) | - | ⏳ Future |
| 9.3 | #3358 | Iterative RAG Strategy | 5 | ⏳ Future |

### Stream C: UI Enhancements
| Seq | Issue | Titolo | SP | Status |
|:---:|:-----:|--------|:--:|:------:|
| 9.4 | #3355 | Version History and Comparison UI | 3 | ⏳ Future |
| 9.5 | #3320 | Gamification & Dashboard Features | - | ⏳ Future |
| 9.6 | #3511 | Game Detail Page - Collection View | 5 | ⏳ Future |

**Related Epics**:
- #3490 Multi-Agent AI System (remaining work)
- #3386 Agent Creation & Testing Flow (enhancement)
- #3327 User Flow Gaps - Security (remaining)
- #3453 Visual RAG Strategy Builder (enhancement)
- #3434 Tier-Strategy-Model (enhancement)
- #3413 Plugin-Based RAG (enhancement)
- #3412 Custom Strategy Builder (enhancement)
- #3403 RAG Dashboard Navigation (enhancement)

---

## ⏸️ FASE 10 - Infrastructure & Testing (FUTURE)

**Timeline**: 3-4 settimane | **SP**: 40 | **Parallelizzazione**: 1 stream

### Infrastructure (#3366 + #2967)
| Seq | Issue | Titolo | SP | Dipendenze |
|:---:|:-----:|--------|:--:|:----------:|
| 10.1 | #3367 | Log Aggregation System | 5 | - |
| 10.2 | #3368 | k6 Load Testing | 3 | - |
| 10.3 | #2968 | Oracle Cloud Setup | 3 | - |
| 10.4 | #2969 | GitHub Actions Runner | 3 | #2968 |
| 10.5 | #2970 | Workflow Migration | 5 | #2969 |
| 10.6 | #2972 | Performance Monitoring | 3 | #2970 |
| 10.7 | #2973 | Cost Validation | 2 | #2972 |
| 10.8 | #2974 | Prometheus + Grafana | 5 | #2973 |
| 10.9 | #2975 | Troubleshooting Docs | 2 | - |
| 10.10 | #2976 | Maintenance Automation | 3 | #2974 |

### E2E Testing
| Seq | Issue | Titolo | SP | Dipendenze |
|:---:|:-----:|--------|:--:|:----------:|
| 10.11 | #3082 | Missing E2E Test Flows (50) | 10 | ALL |

### Storage
| Seq | Issue | Titolo | SP | Dipendenze |
|:---:|:-----:|--------|:--:|:----------:|
| 10.12 | #2703 | S3-compatible Object Storage | 5 | - |

**Epic References**:
- #3366 Infrastructure Enhancements
- #2967 Zero-Cost CI/CD Infrastructure

---

## 📈 PARALLELIZATION MATRIX

### Current Week Execution Plan

```
🔴 WEEK CURRENT: FASE 8 - User Private Library (16 SP)
├─ FE Team A:  #3649 Collection Dashboard ─────────────────────┐
├─ FE Team B:  #3650 Add Game Wizard ──────────────────────────┤ PARALLEL
├─ BE Team A:  #3651 PDF Association ──────────────────────────┤
└─ BE Team B:  #3653 PDF Upload (after #3651) ─────────────────┘

🟡 NEXT WEEK: FASE 8.1 - Tech Debt (5 SP)
└─ QA Team:    #3531 Fix Unit Tests ───────────────────────────→

⏸️ FUTURE: FASE 9 - Advanced Features (80 SP)
├─ FE Team:    #3341 Sessions → #3355 Version History ─────────┐
├─ BE Team:    #3356 RAG Strategies → #3358 Iterative ─────────┤ FUTURE
└─ DevOps:     #3366 → #2967 Infrastructure ───────────────────┘
```

---

## 🏗️ EPIC STATUS OVERVIEW

| Epic | Priorità | Status | Open | Closed | Progress |
|------|:--------:|:------:|:----:|:------:|:--------:|
| **#3475 User Private Library** | 🔴 **CRITICAL** | 🔴 **ACTIVE** | 4 | 0 | 0% |
| #3531 Tech Debt | 🟡 High | 🟡 Ready | 1 | 0 | 0% |
| #3642 Admin PDF Workflow | 🔴 Critical | ✅ DONE | 0 | 1 | 100% |
| #3594 FE-BE Gap Analysis | 🔴 Critical | ✅ DONE | 0 | 6 | 100% |
| #3490 Multi-Agent AI | 🔴 Critical | ✅ DONE | 0 | 14 | 100% |
| #3386 Agent Creation | 🔴 Critical | ✅ DONE | 0 | 6 | 100% |
| #3434 Tier-Strategy-Model | 🔴 Critical | ✅ DONE | 0 | 8 | 100% |
| #3413 Plugin Architecture | 🟡 Medium | ✅ DONE | 0 | 18 | 100% |
| #3453 Visual Strategy | 🟡 Medium | ✅ DONE | 0 | 14 | 100% |
| #3403 RAG Navigation | 🟡 Medium | ✅ DONE | 0 | 7 | 100% |
| #3299 RAG Dashboard | 🟡 Medium | ✅ DONE | 0 | 19 | 100% |
| #3327 Security & Quota | 🟡 High | ✅ DONE | 0 | 7 | 100% |
| #3315 AI Insights | 🟡 High | ✅ DONE | 0 | 7 | 100% |
| #3325 MeepleCard | 🟡 Medium | ✅ DONE | 0 | 6 | 100% |
| #3348 Advanced Features | 🟡 Medium | ✅ DONE | 0 | 10 | 100% |
| #3356 Advanced RAG | 🟢 Low | ⏸️ Future | 2 | 0 | 0% |
| #3341 Game Session | 🟢 Medium | ⏸️ Future | 1 | 0 | 0% |
| #3366 Infrastructure | 🟢 Medium | ⏸️ Future | 2 | 0 | 0% |
| #2967 CI/CD Infra | 🟢 Low | ⏸️ Future | 9 | 0 | 0% |

---

## 🎯 IMMEDIATE ACTIONS (This Week)

### 🔴🔴🔴 PRIORITÀ CRITICA - FASE 8 - Start IMMEDIATELY

**Parallel Execution Required**:

1. **#3649 - User Collection Dashboard Enhancement** (FE, 5 SP):
```bash
git checkout frontend-dev && git pull
git checkout -b feature/issue-3649-collection-dashboard
# Implement: Hero stats bar, inline filters, private PDF badge
```

2. **#3650 - Add Game to Collection Wizard** (FE, 5 SP):
```bash
git checkout frontend-dev && git pull
git checkout -b feature/issue-3650-add-game-wizard
# Implement: API integration, catalog+custom games, PDF upload
```

3. **#3651 - UserLibraryEntry PDF Association** (BE, 3 SP):
```bash
git checkout main-dev && git pull
git checkout -b feature/issue-3651-pdf-association
# Implement: private_rules collection, API endpoints, search isolation
```

4. **#3653 - Private PDF Upload Endpoint** (BE, 3 SP - after #3651):
```bash
git checkout main-dev && git pull
git checkout -b feature/issue-3653-pdf-upload-integration
# Implement: SSE progress, quota integration, collection targeting
```

**Obiettivo**: User può gestire collezione → aggiungere giochi → caricare PDF privati → vedere statistiche

---

## 📋 UNCATEGORIZED ISSUES

| Issue | Titolo | Priority | Action |
|:-----:|--------|:--------:|:------:|
| #3120 | Private Games & Catalog Proposal | Low | Evaluate for FASE 9 |

---

## 📊 METRICS & KPI

### Velocity Target
- **Sprint Velocity**: 25-30 SP/week (3 developers)
- **Issue Close Rate**: 8-12 issues/week
- **Epic Completion**: 1-2 epics/month

### Quality Gates
- **Test Coverage**: 85%+ FE, 90%+ BE
- **Code Review**: 100% PRs reviewed
- **Documentation**: Updated before close

### Timeline Summary
| Fase | Timeline | SP | Issues | Status |
|:----:|:--------:|:--:|:------:|:------:|
| **8** | **This Week** | **16** | **4** | 🔴 **ACTIVE** |
| **8.1** | Next Week | 5 | 1 | 🟡 Ready |
| 0-7.5 | Completed | 0 | 0 | ✅ 100% |
| 9 | Week 3-6 | 80 | 20+ | ⏸️ Future |
| 10 | Week 7-10 | 40 | 11 | ⏸️ Future |
| **TOTAL** | **~10 weeks** | **~141** | **~37** | **~85% done** |

---

## ✅ COMPLETED EPICS (All Phases)

| Epic | Issues | Closed | Description |
|------|:------:|:------:|-------------|
| #3642 Admin PDF Workflow | 1 | 2026-02-05 | PDF upload in SharedGame wizard |
| #3348 Advanced Features | 10 | 2026-02-05 | AI feedback, invite links, session sharing |
| #3453 Visual Strategy Builder | 14 | 2026-02-05 | 23 RAG blocks, ReactFlow canvas |
| #3594 FE-BE Gap Analysis | 6 | 2026-02-05 | Admin sidebar, editor nav, cleanup |
| #3585 GameCarousel | 7 | 2026-02-05 | Full carousel integration with sorting |
| #3532 Admin Shared Games | 10 | 2026-02-04 | Admin management with PDF workflow |
| #3413 Plugin Architecture | 18 | 2026-02-05 | 28 plugins, 143 tests |
| #3299 RAG Dashboard | 19 | 2026-02-05 | 303 tests, 6 strategies |
| #3403 RAG Navigation | 7 | 2026-02-04 | Navigation redesign PR #3577 |
| #3490 Multi-Agent AI | 14 | 2026-02-05 | Context engineering + Tutor |
| #3327 Security & Quota | 7 | 2026-02-05 | Security enforcement |
| #3315 AI Insights | 7 | 2026-02-05 | Dashboard AI widgets |
| #3325 MeepleCard | 6 | 2026-02-05 | Universal card system |
| #3512-3516 Game Detail | 5 | 2026-02-04 | Game detail phases 1-5 |
| #3434 Tier-Strategy | 8 | 2026-02-05 | Architecture refactoring |
| #3386 Agent Creation | 6 | 2026-02-05 | Agent creation flow (PR #3510) |

**Total Completed**: 145+ issues across 17 epics

---

**Maintainer**: PM Agent + Claude Code
**Last Updated**: 2026-02-05
