# Sequenza Risoluzione Issue - MeepleAI

**Data**: 2026-01-26
**Branch attuale**: `frontend-dev`
**Target finale**: Merge in `main-dev` → `main`

---

## Executive Summary

| Categoria | Issue Totali | Test/Fix | Nuove Feature |
|-----------|--------------|----------|---------------|
| Backend | 22 | 8 | 14 |
| Frontend | 24 | 7 | 17 |
| Infra/CI | 12 | 3 | 9 |
| **TOTALE** | 58 | 18 | 40 |

**Principio Guida**: Test/Fix PRIMA delle nuove implementazioni

---

## Legenda Simboli

- `🔴` P0 - Critical (blockers)
- `🟠` P1 - High (security, core features)
- `🟡` P2 - Medium (admin features)
- `🟢` P3 - Low (nice-to-have)
- `✅` Completato
- `🔄` In Progress
- `⏳` Pending
- `🧪` Test
- `🐛` Bug Fix
- `⭐` Nuova Feature
- `⇄` Parallelizzabile

---

## FASE 0: Test Foundation & Bug Fixes (Priorita Massima)

> **Obiettivo**: Zero test failures, coverage collection funzionante

### Sprint 0.1: Backend Bug Fixes (1 settimana)

| # | Issue | Tipo | Branch | Status |
|---|-------|------|--------|--------|
| 1 | #3006 | 🐛 Fix Backend Event Handler Tests (26 failures) | `backend-dev` | ⏳ |
| 2 | #3007 | 🐛 Fix Backend Repository Filter Queries (2 critical) | `backend-dev` | ⏳ |
| 3 | #3008 | 🐛 Fix Backend ShareRequest State Machine (23+ failures) | `backend-dev` | ⏳ |
| 4 | #2991 | 🐛 UserLibraryRepository.GetUserGamesAsync tests | `backend-dev` | ⏳ |

### Sprint 0.2: Frontend Bug Fixes (Parallelizzabile con 0.1)

| # | Issue | Tipo | Branch | Status |
|---|-------|------|--------|--------|
| 5 | #3009 | 🐛 Fix Frontend Coverage Collection (v8 Provider) | `frontend-dev` | ⏳ |

### 🚩 CHECKPOINT 0 - Bug Fix Merge
```bash
# Backend: backend-dev → main-dev
git checkout main-dev && git merge backend-dev --no-ff -m "fix: resolve all backend test failures (#3006, #3007, #3008, #2991)"

# Frontend: frontend-dev → main-dev
git checkout main-dev && git merge frontend-dev --no-ff -m "fix: resolve frontend coverage collection (#3009)"

# Tag checkpoint
git tag -a checkpoint-0-bug-fixes -m "All test failures resolved"
```

---

## FASE 1: Coverage Infrastructure (2 settimane)

> **Obiettivo**: Raggiungere 50% coverage su entrambi i stack

### Sprint 1.1: Coverage Tools Setup

| # | Issue | Tipo | Branch | Dipende Da | Status |
|---|-------|------|--------|------------|--------|
| 6 | #3013 | 🧪 Codecov Integration & CI Gates | `main-dev` | Checkpoint 0 | ⏳ |

### Sprint 1.2: Backend Coverage (⇄ Parallelizzabile)

| # | Issue | Tipo | Branch | Status |
|---|-------|------|--------|--------|
| 7 | #3010 | 🧪 Increase Backend Coverage to 50% | `backend-dev` | ⏳ |
| 8 | #3012 | 🧪 Add Backend E2E Test Suite | `backend-dev` | ⏳ |

### Sprint 1.3: Frontend Coverage (⇄ Parallelizzabile con 1.2)

| # | Issue | Tipo | Branch | Status |
|---|-------|------|--------|--------|
| 9 | #3011 | 🧪 Increase Frontend Coverage to 50% | `frontend-dev` | ⏳ |
| 10 | #2861 | 🧪 [User Dashboard] Component Tests | `frontend-dev` | ⏳ |

### 🚩 CHECKPOINT 1 - Coverage Foundation Merge
```bash
# Backend coverage work
git checkout main-dev && git merge backend-dev --no-ff -m "test: increase backend coverage to 50% (#3010, #3012)"

# Frontend coverage work
git checkout main-dev && git merge frontend-dev --no-ff -m "test: increase frontend coverage to 50% (#3011, #2861)"

# Verify Codecov reports pass CI gates
# Tag checkpoint
git tag -a checkpoint-1-coverage-foundation -m "50% coverage achieved on both stacks"
```

---

## FASE 2: CQRS Critical Fixes - Backend Only (3-4 settimane)

> **Obiettivo**: Risolvere violazioni CQRS critiche in SharedGameCatalog e Authentication

### Sprint 2.1: SharedGameCatalog Core (Backend)

| # | Issue | Tipo | Priorita | Effort | Status |
|---|-------|------|----------|--------|--------|
| 11 | #3067 | 🔴⭐ SharedGameCatalog Core CQRS Handlers (1/2) | P0 | 3-4d | ⏳ |
| 12 | #3068 | 🔴⭐ SharedGameCatalog Share Flow Handlers (2/2) | P0 | 5-6d | ⏳ |

### Sprint 2.2: Authentication CQRS (Backend)

| # | Issue | Tipo | Priorita | Effort | Status |
|---|-------|------|----------|--------|--------|
| 13 | #3069 | 🟠⭐ Authentication - Migrate to Full CQRS | P1 | 8-10d | ⏳ |

### 🚩 CHECKPOINT 2 - CQRS Compliance Merge
```bash
# CQRS fixes are backend-only, no frontend dependency
git checkout main-dev && git merge backend-dev --no-ff -m "feat: implement CQRS handlers for SharedGameCatalog and Authentication (#3067, #3068, #3069)"

git tag -a checkpoint-2-cqrs-compliance -m "CQRS pattern fully compliant"
```

---

## FASE 3: Security Features (3 settimane)

> **Obiettivo**: Session limits e Email verification

### Sprint 3.1: Session Limits

| # | Issue | Tipo | Branch | Priorita | Dipende Da |
|---|-------|------|--------|----------|------------|
| 14 | #3070 | 🟠⭐ Session Limits Backend | `backend-dev` | P1 | Checkpoint 2 |
| 15 | #3075 | 🟠⭐ Session Quota UI Frontend | `frontend-dev` | P1 | #3070 |

**Sequenza**:
```
#3070 (backend) ──→ PR ──→ merge backend-dev
                          ↓
                    #3075 (frontend) ──→ PR ──→ merge frontend-dev
```

### Sprint 3.2: Email Verification (⇄ Parallelizzabile dopo 3070)

| # | Issue | Tipo | Branch | Priorita | Dipende Da |
|---|-------|------|--------|----------|------------|
| 16 | #3071 | 🟠⭐ Email Verification Backend | `backend-dev` | P1 | Checkpoint 2 |
| 17 | #3076 | 🟠⭐ Email Verification Frontend | `frontend-dev` | P1 | #3071 |

### 🚩 CHECKPOINT 3 - Security Features Merge
```bash
# Backend security features
git checkout main-dev && git merge backend-dev --no-ff -m "feat: implement session limits and email verification backend (#3070, #3071)"

# Frontend security features (after backend APIs available)
git checkout main-dev && git merge frontend-dev --no-ff -m "feat: implement session quota UI and email verification flow (#3075, #3076)"

git tag -a checkpoint-3-security-features -m "Session limits and email verification complete"
```

---

## FASE 4: Admin Features (2-3 settimane)

> **Obiettivo**: PDF limits, Feature flags, 2FA UI

### Sprint 4.1: PDF Limits

| # | Issue | Tipo | Branch | Priorita | Dipende Da |
|---|-------|------|--------|----------|------------|
| 18 | #3072 | 🟡⭐ PDF Upload Limits Admin API | `backend-dev` | P2 | Checkpoint 3 |
| 19 | #3078 | 🟡⭐ PDF Limits Admin UI | `frontend-dev` | P2 | #3072 |

### Sprint 4.2: Feature Flags (⇄ Parallelizzabile con 4.1)

| # | Issue | Tipo | Branch | Priorita | Dipende Da |
|---|-------|------|--------|----------|------------|
| 20 | #3073 | 🟡⭐ Feature Flags Tier-Based Backend | `backend-dev` | P2 | Checkpoint 3 |
| 21 | #3079 | 🟡⭐ Feature Flags Tier UI | `frontend-dev` | P2 | #3073 |

### Sprint 4.3: 2FA UI (Frontend Only)

| # | Issue | Tipo | Branch | Priorita | Dipende Da |
|---|-------|------|--------|----------|------------|
| 22 | #3077 | 🟡⭐ 2FA UI - Convert Placeholders | `frontend-dev` | P2 | Checkpoint 3 |

### 🚩 CHECKPOINT 4 - Admin Features Merge
```bash
# Backend admin APIs
git checkout main-dev && git merge backend-dev --no-ff -m "feat: add PDF limits and feature flags tier-based APIs (#3072, #3073)"

# Frontend admin UIs
git checkout main-dev && git merge frontend-dev --no-ff -m "feat: add PDF limits UI, feature flags UI, and 2FA components (#3078, #3079, #3077)"

git tag -a checkpoint-4-admin-features -m "Admin features complete"
```

---

## FASE 5: E2E Test Suite (2 settimane)

> **Obiettivo**: E2E tests per tutti i flussi implementati

### Sprint 5.1: E2E Tests (⇄ Tutte parallelizzabili)

| # | Issue | Tipo | Branch | Priorita |
|---|-------|------|--------|----------|
| 23 | #3082 | 🧪 Implement Missing E2E Test Flows (50 flows) | `frontend-dev` | High |
| 24 | #2862 | 🧪 [User Dashboard] E2E Tests | `frontend-dev` | Medium |
| 25 | #2870 | 🧪 [Personal Library] E2E Tests | `frontend-dev` | Medium |
| 26 | #2877 | 🧪 [Shared Catalog] E2E Tests | `frontend-dev` | Medium |
| 27 | #2883 | 🧪 [Profile/Settings] E2E Tests | `frontend-dev` | Medium |
| 28 | #2891 | 🧪 [User Management] E2E Tests | `frontend-dev` | Medium |
| 29 | #2897 | 🧪 [Editor Dashboard] E2E Tests | `frontend-dev` | Medium |

### 🚩 CHECKPOINT 5 - E2E Tests Merge
```bash
git checkout main-dev && git merge frontend-dev --no-ff -m "test: add comprehensive E2E test suite (#3082, #2862, #2870, #2877, #2883, #2891, #2897)"

git tag -a checkpoint-5-e2e-tests -m "E2E test suite complete"
```

---

## FASE 6: UI Components & Polish (3-4 settimane)

> **Obiettivo**: Completare component library e UI features

### Sprint 6.1: User Dashboard Components

| # | Issue | Tipo | Branch |
|---|-------|------|--------|
| 30 | #2857 | ⭐ LibraryQuotaWidget Component | `frontend-dev` |
| 31 | #2858 | ⭐ ActiveSessionsPanel Component | `frontend-dev` |
| 32 | #2859 | ⭐ Dashboard API Integration | `frontend-dev` |
| 33 | #2860 | ⭐ Responsive Navigation | `frontend-dev` |

### Sprint 6.2: Library & Catalog UI (⇄ Parallelizzabile)

| # | Issue | Tipo | Branch |
|---|-------|------|--------|
| 34 | #2866 | ⭐ Library Page with Search | `frontend-dev` |
| 35 | #2867 | ⭐ Game Cards (Grid + List) | `frontend-dev` |
| 36 | #2868 | ⭐ Bulk Selection Mode | `frontend-dev` |
| 37 | #2869 | ⭐ Quota Sticky Header | `frontend-dev` |
| 38 | #2873 | ⭐ Advanced Filter Panel | `frontend-dev` |
| 39 | #2874 | ⭐ Catalog Game Cards | `frontend-dev` |
| 40 | #2875 | ⭐ Add to Library Overlay | `frontend-dev` |
| 41 | #2876 | ⭐ Pagination Component | `frontend-dev` |

### Sprint 6.3: Profile & Settings

| # | Issue | Tipo | Branch |
|---|-------|------|--------|
| 42 | #2881 | ⭐ Settings Page with 4 Tabs | `frontend-dev` |
| 43 | #2882 | ⭐ Avatar Upload with Crop | `frontend-dev` |

### Sprint 6.4: Admin Components

| # | Issue | Tipo | Branch |
|---|-------|------|--------|
| 44 | #2887 | ⭐ User Management Table | `frontend-dev` |
| 45 | #2888 | ⭐ Bulk Selection Action Bar | `frontend-dev` |
| 46 | #2890 | ⭐ User Detail Modal | `frontend-dev` |
| 47 | #2894 | ⭐ Editor Dashboard Page | `frontend-dev` |
| 48 | #2895 | ⭐ Approval Queue Items | `frontend-dev` |
| 49 | #2896 | ⭐ Bulk Approval UI | `frontend-dev` |

### Backend Support (⇄ Parallelizzabile)

| # | Issue | Tipo | Branch |
|---|-------|------|--------|
| 50 | #2886 | ⭐ SuspendUser/UnsuspendUser Commands | `backend-dev` |
| 51 | #2893 | ⭐ BulkApprove/BulkReject Commands | `backend-dev` |

### 🚩 CHECKPOINT 6 - UI Components Merge
```bash
# Backend commands
git checkout main-dev && git merge backend-dev --no-ff -m "feat: add user management and bulk approval commands (#2886, #2893)"

# Frontend UI components
git checkout main-dev && git merge frontend-dev --no-ff -m "feat: complete UI component library (#2857-#2896)"

git tag -a checkpoint-6-ui-components -m "UI component library complete"
```

---

## FASE 7: Analytics & Final Features (2 settimane)

> **Obiettivo**: AI tracking, coverage targets, final polish

### Sprint 7.1: AI Usage Tracking

| # | Issue | Tipo | Branch | Priorita | Dipende Da |
|---|-------|------|--------|----------|------------|
| 52 | #3074 | 🟢⭐ AI Token Usage Tracking Backend | `backend-dev` | P3 | Checkpoint 6 |
| 53 | #3080 | 🟢⭐ AI Usage Dashboard Frontend | `frontend-dev` | P3 | #3074 |

### Sprint 7.2: Coverage Targets

| # | Issue | Tipo | Branch |
|---|-------|------|--------|
| 54 | #3025 | 🧪 Reach Backend 90% Coverage | `backend-dev` |
| 55 | #3026 | 🧪 Reach Frontend 85% Coverage | `frontend-dev` |

### Sprint 7.3: Component Library Foundation

| # | Issue | Tipo | Branch |
|---|-------|------|--------|
| 56 | #2924 | ⭐ Storybook Setup | `frontend-dev` |
| 57 | #2925 | ⭐ Extract Reusable Components | `frontend-dev` |
| 58 | #2926 | ⭐ Design System Docs | `frontend-dev` |

### 🚩 CHECKPOINT FINALE - Production Ready Merge
```bash
# Backend final
git checkout main-dev && git merge backend-dev --no-ff -m "feat: add AI usage tracking, reach 90% coverage (#3074, #3025)"

# Frontend final
git checkout main-dev && git merge frontend-dev --no-ff -m "feat: add AI dashboard, reach 85% coverage, Storybook setup (#3080, #3026, #2924, #2925, #2926)"

# Final integration
git checkout main && git merge main-dev --no-ff -m "release: v2.0 - full feature set with comprehensive test coverage"

git tag -a v2.0.0 -m "Production release with full CQRS compliance and 90%/85% coverage"
```

---

## Parallelizzazione Matrix

```
FASE    | Backend (backend-dev)          | Frontend (frontend-dev)         | Sync Point
--------|--------------------------------|----------------------------------|------------
0       | #3006,#3007,#3008,#2991        | #3009                           | CHECKPOINT 0
        | ⇄ PARALLEL                     | ⇄ PARALLEL                      |
--------|--------------------------------|----------------------------------|------------
1       | #3010,#3012                    | #3011,#2861                     | CHECKPOINT 1
        | ⇄ PARALLEL                     | ⇄ PARALLEL                      |
--------|--------------------------------|----------------------------------|------------
2       | #3067→#3068→#3069              | (attende backend)               | CHECKPOINT 2
        | SEQUENTIAL                     |                                  |
--------|--------------------------------|----------------------------------|------------
3       | #3070 → #3071                  | #3075 (dopo #3070)              | CHECKPOINT 3
        |         ⇄ PARALLEL             | #3076 (dopo #3071)              |
--------|--------------------------------|----------------------------------|------------
4       | #3072 ⇄ #3073                  | #3078,#3079,#3077               | CHECKPOINT 4
        | PARALLEL                       | ⇄ PARALLEL (dopo BE)            |
--------|--------------------------------|----------------------------------|------------
5       | (manutenzione)                 | #3082,#2862-#2897               | CHECKPOINT 5
        |                                | ⇄ ALL PARALLEL                   |
--------|--------------------------------|----------------------------------|------------
6       | #2886 ⇄ #2893                  | #2857-#2896                     | CHECKPOINT 6
        | PARALLEL                       | ⇄ ALL PARALLEL                   |
--------|--------------------------------|----------------------------------|------------
7       | #3074 → #3025                  | #3080,#3026,#2924-#2926         | FINAL
        | SEQUENTIAL                     | ⇄ PARALLEL                       |
```

---

## Gantt Semplificato (Settimane)

```
Settimana:  1   2   3   4   5   6   7   8   9  10  11  12  13  14  15  16
            |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |
FASE 0      [===]                                                          Bug Fixes
FASE 1          [=======]                                                  Coverage 50%
FASE 2                  [===============]                                  CQRS
FASE 3                                  [===========]                      Security
FASE 4                                              [=======]              Admin
FASE 5                                                      [=====]        E2E Tests
FASE 6                                                          [=========] UI
FASE 7                                                                  [===] Final

CHECKPOINTS: CP0  CP1         CP2         CP3     CP4     CP5         CP6  FINAL
              ↓    ↓           ↓           ↓       ↓       ↓           ↓      ↓
main-dev:    [merge][merge]   [merge]    [merge] [merge] [merge]    [merge][merge]
main:                                                                        [release]
```

---

## Issue Escluse (Bassa Priorita/Optional)

Queste issue sono state escluse dal percorso principale ma possono essere lavorate in parallelo se risorse disponibili:

| # | Issue | Motivo |
|---|-------|--------|
| #2965 | Dual-Theme Design System | Epic separata, non bloccante |
| #2967 | Zero-Cost CI/CD Infrastructure | Infra separata |
| #2968-#2976 | Oracle Cloud Setup | Infra separata |
| #2927 | Lighthouse CI | Nice-to-have |
| #2928 | k6 Load Testing | Nice-to-have |
| #2929 | WCAG Accessibility Audit | Post-MVP |
| #2852 | Chromatic Visual Regression | Post-MVP |
| #2703 | S3 Object Storage | Infra separata |

---

## Comandi Utili

### Verificare stato issue
```bash
gh issue view <numero> --json state,title,labels
```

### Creare branch per issue
```bash
git checkout backend-dev  # o frontend-dev
git pull origin backend-dev
git checkout -b feature/issue-<numero>-<descrizione>
```

### Chiudere issue con PR
```bash
gh pr create --title "feat: <descrizione>" --body "Closes #<numero>"
```

### Verificare checkpoint
```bash
git log --oneline --graph main-dev..backend-dev
git log --oneline --graph main-dev..frontend-dev
```

---

**Ultimo aggiornamento**: 2026-01-26
**Prossima revisione**: Dopo ogni checkpoint
