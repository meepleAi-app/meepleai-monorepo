# Sequenza Risoluzione Issue - MeepleAI

**Data**: 2026-01-26
**Ultimo Aggiornamento**: 2026-01-28
**Branch attuale**: `frontend-dev`
**Target finale**: Merge in `main-dev` → `main`

---

## Executive Summary

| Categoria | Issue Totali | Completate | Rimanenti |
|-----------|--------------|------------|-----------|
| Backend | 22 | 14 | 8 |
| Frontend | 24 | 11 | 13 |
| Infra/CI | 12 | 3 | 9 |
| **TOTALE** | 58 | **28** | **30** |

**Principio Guida**: Test/Fix PRIMA delle nuove implementazioni

> **AGGIORNAMENTO 2026-01-28**:
> - FASE 0-4: ✅ COMPLETATE
> - FASE 5 (E2E): 2/7 completate (#3082, #2862)
> - FASE 6 (UI): 3/20 completate (#2866, #2867, #2870)
> - Progresso totale: 28/58 (48%)

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

## ~~FASE 0: Test Foundation & Bug Fixes~~ ✅ COMPLETATA

> **Issue completate**: #3006, #3007, #3008, #2991, #3009
> **Vedi sezione "Issue Completate" per dettagli**

---

## ~~FASE 1: Coverage Infrastructure~~ ✅ COMPLETATA

> **Issue completate**: #3013, #3010, #3012, #3011, #2861
> **Vedi sezione "Issue Completate" per dettagli**

---

## ~~FASE 2: CQRS Critical Fixes~~ ✅ COMPLETATA

> **Issue completate**: #3067, #3068, #3069
> **Vedi sezione "Issue Completate" per dettagli**

---

## ~~FASE 3: Security Features~~ ✅ COMPLETATA

> **Issue completate**: #3070, #3075, #3071, #3076
> **Vedi sezione "Issue Completate" per dettagli**

---

## ~~FASE 4: Admin Features~~ ✅ COMPLETATA

> **Issue completate**: #3072, #3078, #3073, #3079, #3077
> **Vedi sezione "Issue Completate" per dettagli**

---

## FASE 5: E2E Test Suite (In Progress)

> **Obiettivo**: E2E tests per tutti i flussi implementati
> **Stato**: 2/7 completate (29%)

### Sprint 5.1: E2E Tests (⇄ Tutte parallelizzabili)

| # | Issue | Tipo | Branch | Status |
|---|-------|------|--------|--------|
| 1 | #2870 | 🧪 [Personal Library] E2E Tests | `frontend-dev` | ✅ **CLOSED** |
| 2 | #2877 | 🧪 [Shared Catalog] E2E Tests | `frontend-dev` | ⏳ |
| 3 | #2883 | 🧪 [Profile/Settings] E2E Tests | `frontend-dev` | ⏳ |
| 4 | #2891 | 🧪 [User Management] E2E Tests | `frontend-dev` | ⏳ |
| 5 | #2897 | 🧪 [Editor Dashboard] E2E Tests | `frontend-dev` | ⏳ |

### 🚩 CHECKPOINT 5 - E2E Tests Merge
```bash
git checkout main-dev && git merge frontend-dev --no-ff -m "test: add comprehensive E2E test suite (#2877, #2883, #2891, #2897)"

git tag -a checkpoint-5-e2e-tests -m "E2E test suite complete"
```

---

## FASE 6: UI Components & Polish (In Progress)

> **Obiettivo**: Completare component library e UI features
> **Stato**: 3/20 completate (15%)

### Sprint 6.1: User Dashboard Components

| # | Issue | Tipo | Branch | Status |
|---|-------|------|--------|--------|
| 6 | #2857 | ⭐ LibraryQuotaWidget Component | `frontend-dev` | ⏳ |
| 7 | #2858 | ⭐ ActiveSessionsPanel Component | `frontend-dev` | ⏳ |
| 8 | #2859 | ⭐ Dashboard API Integration | `frontend-dev` | ⏳ |
| 9 | #2860 | ⭐ Responsive Navigation | `frontend-dev` | ⏳ |

### Sprint 6.2: Library & Catalog UI (⇄ Parallelizzabile)

| # | Issue | Tipo | Branch | Status |
|---|-------|------|--------|--------|
| 10 | #2868 | ⭐ Bulk Selection Mode | `frontend-dev` | ⏳ |
| 11 | #2869 | ⭐ Quota Sticky Header | `frontend-dev` | ⏳ |
| 12 | #2873 | ⭐ Advanced Filter Panel | `frontend-dev` | ⏳ |
| 13 | #2874 | ⭐ Catalog Game Cards | `frontend-dev` | ⏳ |
| 14 | #2875 | ⭐ Add to Library Overlay | `frontend-dev` | ⏳ |
| 15 | #2876 | ⭐ Pagination Component | `frontend-dev` | ⏳ |

### Sprint 6.3: Profile & Settings

| # | Issue | Tipo | Branch | Status |
|---|-------|------|--------|--------|
| 16 | #2881 | ⭐ Settings Page with 4 Tabs | `frontend-dev` | ⏳ |
| 17 | #2882 | ⭐ Avatar Upload with Crop | `frontend-dev` | ⏳ |

### Sprint 6.4: Admin Components

| # | Issue | Tipo | Branch | Status |
|---|-------|------|--------|--------|
| 18 | #2887 | ⭐ User Management Table | `frontend-dev` | ⏳ |
| 19 | #2888 | ⭐ Bulk Selection Action Bar | `frontend-dev` | ⏳ |
| 20 | #2890 | ⭐ User Detail Modal | `frontend-dev` | ⏳ |
| 21 | #2894 | ⭐ Editor Dashboard Page | `frontend-dev` | ⏳ |
| 22 | #2895 | ⭐ Approval Queue Items | `frontend-dev` | ⏳ |
| 23 | #2896 | ⭐ Bulk Approval UI | `frontend-dev` | ⏳ |

### Backend Support (⇄ Parallelizzabile)

| # | Issue | Tipo | Branch | Status |
|---|-------|------|--------|--------|
| 24 | #2886 | ⭐ SuspendUser/UnsuspendUser Commands | `backend-dev` | ⏳ |
| 25 | #2893 | ⭐ BulkApprove/BulkReject Commands | `backend-dev` | ⏳ |

### 🚩 CHECKPOINT 6 - UI Components Merge
```bash
# Backend commands
git checkout main-dev && git merge backend-dev --no-ff -m "feat: add user management and bulk approval commands (#2886, #2893)"

# Frontend UI components
git checkout main-dev && git merge frontend-dev --no-ff -m "feat: complete UI component library (#2857-#2896)"

git tag -a checkpoint-6-ui-components -m "UI component library complete"
```

---

## FASE 7: Analytics & Final Features (Ultima fase)

> **Obiettivo**: AI tracking, coverage targets, final polish

### Sprint 7.1: AI Usage Tracking

| # | Issue | Tipo | Branch | Priorita | Dipende Da | Status |
|---|-------|------|--------|----------|------------|--------|
| 26 | #3074 | 🟢⭐ AI Token Usage Tracking Backend | `backend-dev` | P3 | Checkpoint 6 | ⏳ |
| 27 | #3080 | 🟢⭐ AI Usage Dashboard Frontend | `frontend-dev` | P3 | #3074 | ⏳ |

### Sprint 7.2: Coverage Targets

| # | Issue | Tipo | Branch | Status |
|---|-------|------|--------|--------|
| 28 | #3025 | 🧪 Reach Backend 90% Coverage | `backend-dev` | ⏳ |
| 29 | #3026 | 🧪 Reach Frontend 85% Coverage | `frontend-dev` | ⏳ |

### Sprint 7.3: Component Library Foundation

| # | Issue | Tipo | Branch | Status |
|---|-------|------|--------|--------|
| 30 | #2924 | ⭐ Storybook Setup | `frontend-dev` | ⏳ |
| 31 | #2925 | ⭐ Extract Reusable Components | `frontend-dev` | ⏳ |
| 32 | #2926 | ⭐ Design System Docs | `frontend-dev` | ⏳ |

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

## Dipendenze Consolidate

```
FASE 5 (E2E Tests) - Nessuna dipendenza esterna
├── #2877 [Shared Catalog] ⇄ parallelizzabile
├── #2883 [Profile/Settings] ⇄ parallelizzabile
├── #2891 [User Management] ⇄ parallelizzabile
└── #2897 [Editor Dashboard] ⇄ parallelizzabile

FASE 6 (UI Components) - Nessuna dipendenza esterna
├── Sprint 6.1: Dashboard (#2857→#2858→#2859→#2860) sequenziale
├── Sprint 6.2: Library (#2868-#2876) ⇄ parallelizzabile
├── Sprint 6.3: Profile (#2881, #2882) ⇄ parallelizzabile
├── Sprint 6.4: Admin (#2887-#2896) ⇄ parallelizzabile
└── Backend: #2886, #2893 ⇄ parallelizzabile con frontend

FASE 7 (Analytics & Final)
├── #3074 (AI Backend) → #3080 (AI Frontend) sequenziale
├── #3025 (Backend 90%) indipendente
├── #3026 (Frontend 85%) indipendente
└── #2924→#2925→#2926 (Storybook) sequenziale
```

---

## Gantt Semplificato (Settimane) - Aggiornato 2026-01-28

```
Settimana:  1   2   3   4   5   6   7   8   9  10  11  12  13  14
            |   |   |   |   |   |   |   |   |   |   |   |   |   |
FASE 0-4    ✅✅✅✅ COMPLETATE
FASE 5              [====]🔄                                      E2E Tests (2/5)
FASE 6                  [=========]🔄                             UI (3/17)
FASE 7                              [===]                         Final

CHECKPOINTS:         CP5     CP6  FINAL
                      ↓       ↓      ↓
main-dev:          [merge] [merge][merge]
main:                                    [release]

📊 Progresso: 28/58 issue completate (48%)
⏱️ Tempo risparmiato: FASE 0-4 completate (28 issue)
```

---

## Issue Completate (28)

### FASE 0 - Bug Fixes (5/5) ✅

| Issue | Titolo | Commit/PR |
|-------|--------|-----------|
| #3006 | Fix Backend Event Handler Tests (26 failures) | backend-dev |
| #3007 | Fix Backend Repository Filter Queries | backend-dev |
| #3008 | Fix Backend ShareRequest State Machine | backend-dev |
| #2991 | UserLibraryRepository.GetUserGamesAsync tests | backend-dev |
| #3009 | Fix Frontend Coverage Collection (v8 Provider) | frontend-dev |

### FASE 1 - Coverage Infrastructure (5/5) ✅

| Issue | Titolo | Commit/PR |
|-------|--------|-----------|
| #3013 | Codecov Integration & CI Gates | main-dev |
| #3010 | Increase Backend Coverage to 50% | backend-dev |
| #3012 | Add Backend E2E Test Suite | backend-dev |
| #3011 | Increase Frontend Coverage to 50% | frontend-dev |
| #2861 | [User Dashboard] Component Tests | frontend-dev |

### FASE 2 - CQRS (3/3) ✅

| Issue | Titolo | Data |
|-------|--------|------|
| #3067 | SharedGameCatalog Core CQRS Handlers (1/2) | 2026-01-27 |
| #3068 | SharedGameCatalog Share Flow Handlers (2/2) | 2026-01-27 |
| #3069 | Authentication - Migrate to Full CQRS | 2026-01-27 |

### FASE 3 - Security Features (4/4) ✅

| Issue | Titolo | Commit/PR |
|-------|--------|-----------|
| #3070 | Session Limits Backend | #3102 |
| #3075 | Session Quota UI Frontend | #3103 |
| #3071 | Email Verification Backend | #3105 |
| #3076 | Email Verification Frontend | #3109 |

### FASE 4 - Admin Features (5/5) ✅

| Issue | Titolo | Commit/PR |
|-------|--------|-----------|
| #3072 | PDF Upload Limits Admin API | #3111 |
| #3078 | PDF Limits Admin UI | #3112 |
| #3073 | Feature Flags Tier-Based Backend | #3113 |
| #3079 | Feature Flags Tier UI | #3114 |
| #3077 | 2FA UI - Convert Placeholders | #3115 |

### FASE 5 - E2E Tests (3/7 parziale)

| Issue | Titolo | Commit/PR |
|-------|--------|-----------|
| #3082 | Implement Missing E2E Test Flows (50 flows) | #3116 |
| #2862 | [User Dashboard] E2E Tests | #3117 |
| #2870 | [Personal Library] E2E Tests | 319cc9f08 |

### FASE 6 - UI Components (2/20 parziale)

| Issue | Titolo | Commit/PR |
|-------|--------|-----------|
| #2866 | Library Page with Search | 319cc9f08 |
| #2867 | Game Cards (Grid + List) | 319cc9f08 |

---

## Issue Rimanenti (30)

### FASE 5 - E2E Tests (4 rimanenti)

| Issue | Titolo | Priorità |
|-------|--------|----------|
| #2877 | [Shared Catalog] E2E Tests | Medium |
| #2883 | [Profile/Settings] E2E Tests | Medium |
| #2891 | [User Management] E2E Tests | Medium |
| #2897 | [Editor Dashboard] E2E Tests | Medium |

### FASE 6 - UI Components (17 rimanenti)

| Issue | Titolo | Sprint |
|-------|--------|--------|
| #2857 | LibraryQuotaWidget Component | 6.1 |
| #2858 | ActiveSessionsPanel Component | 6.1 |
| #2859 | Dashboard API Integration | 6.1 |
| #2860 | Responsive Navigation | 6.1 |
| #2868 | Bulk Selection Mode | 6.2 |
| #2869 | Quota Sticky Header | 6.2 |
| #2873 | Advanced Filter Panel | 6.2 |
| #2874 | Catalog Game Cards | 6.2 |
| #2875 | Add to Library Overlay | 6.2 |
| #2876 | Pagination Component | 6.2 |
| #2881 | Settings Page with 4 Tabs | 6.3 |
| #2882 | Avatar Upload with Crop | 6.3 |
| #2887 | User Management Table | 6.4 |
| #2888 | Bulk Selection Action Bar | 6.4 |
| #2890 | User Detail Modal | 6.4 |
| #2894 | Editor Dashboard Page | 6.4 |
| #2895 | Approval Queue Items | 6.4 |
| #2896 | Bulk Approval UI | 6.4 |
| #2886 | SuspendUser/UnsuspendUser Commands (BE) | 6.4 |
| #2893 | BulkApprove/BulkReject Commands (BE) | 6.4 |

### FASE 7 - Analytics & Final (7 rimanenti)

| Issue | Titolo | Priorità |
|-------|--------|----------|
| #3074 | AI Token Usage Tracking Backend | P3 |
| #3080 | AI Usage Dashboard Frontend | P3 |
| #3025 | Reach Backend 90% Coverage | High |
| #3026 | Reach Frontend 85% Coverage | High |
| #2924 | Storybook Setup | Medium |
| #2925 | Extract Reusable Components | Medium |
| #2926 | Design System Docs | Medium |

---

## Issue Escluse (Bassa Priorita/Optional)

| Issue | Motivo |
|-------|--------|
| #2965 | Dual-Theme Design System - Epic separata |
| #2967 | Zero-Cost CI/CD Infrastructure - Infra separata |
| #2968-#2976 | Oracle Cloud Setup - Infra separata |
| #2927 | Lighthouse CI - Nice-to-have |
| #2928 | k6 Load Testing - Nice-to-have |
| #2929 | WCAG Accessibility Audit - Post-MVP |
| #2852 | Chromatic Visual Regression - Post-MVP |
| #2703 | S3 Object Storage - Infra separata |

---

## Comandi Utili

### Verificare stato issue
```bash
gh issue view <numero> --json state,title,labels
```

### Creare branch per issue
```bash
git checkout frontend-dev
git pull origin frontend-dev
git checkout -b feature/issue-<numero>-<descrizione>
```

### Chiudere issue con PR
```bash
gh pr create --title "feat: <descrizione>" --body "Closes #<numero>"
```

### Verificare checkpoint
```bash
git log --oneline --graph main-dev..frontend-dev
```

---

**Ultimo aggiornamento**: 2026-01-28
**Changelog**:
- v4.0 (2026-01-28): Rimosso issue chiuse (FASE 0-4 complete, parziali 5-6), consolidato dipendenze
- v3.0 (2026-01-27): Sincronizzato con GitHub - FASE 0/1 quasi completate
- v2.0 (2026-01-27): Rimosso FASE 2 (già completata), rinumerato issue
- v1.0 (2026-01-26): Versione iniziale

**Prossima revisione**: Dopo ogni checkpoint
