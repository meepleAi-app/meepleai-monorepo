# Sequenza Risoluzione Issue - MeepleAI

**Data Creazione**: 2026-01-29
**Focus**: UX per gestione libreria, agenti AI e chat real-time
**Branch attuale**: `frontend-dev`
**Target finale**: Merge in `main-dev` → `main`

---

## Executive Summary

| Categoria | Issue Totali | Priorità Alta | Completabili Q1 |
|-----------|--------------|---------------|-----------------|
| **GST Real-Time UX** | 8 | 7 (P0) | 8 |
| **Library & Catalog UX** | 3 | 2 (P1) | 3 |
| **Admin & Editor UX** | 3 | 1 (P1) | 3 |
| **Session & AI UX** | 3 | 2 (P1-P2) | 2 |
| **Testing & Quality** | 4 | 2 (P0-P1) | 2 |
| **Component Library** | 6 | 0 (P2-P3) | 3 |
| **TOTALE** | **27** | **14** | **21** |

**Focus Strategico**: Game Session Toolkit = UX killer feature per collaborative play in tempo reale

> **PRIORITÀ UX**:
> - 🎯 **FASE 1 (GST)**: Real-time collaborative scorekeeper - La feature più richiesta
> - 📚 **FASE 2**: Library management UX - Completare navigazione e ricerca
> - 👥 **FASE 3**: Admin/Editor workflows - Bulk operations e moderazione
> - 🤖 **FASE 4**: AI & Session UX - Quota tracking e analytics
> - 🧪 **FASE 5**: Testing coverage - Validazione qualità
> - 🎨 **FASE 6**: Component library - Foundation design system

---

## Legenda Simboli

- `🔴` P0 - Critical (blockers, killer features)
- `🟠` P1 - High (core UX, security)
- `🟡` P2 - Medium (admin features, polish)
- `🟢` P3 - Low (nice-to-have, analytics)
- `⏳` Pending
- `🔄` In Progress
- `✅` Completato
- `🧪` Test
- `🐛` Bug Fix
- `⭐` Nuova Feature
- `⇄` Parallelizzabile
- `🎯` UX Critical
- `🤖` AI/Agent Feature
- `📊` Real-Time/SSE

---

## FASE 1: Game Session Toolkit (GST) - Real-Time UX 🔴 CRITICAL

> **Obiettivo**: Collaborative scorekeeper con SSE real-time per sessioni multiplayer
> **Epic**: #3167
> **Impact**: Killer feature per UX - gestione punteggi condivisa in tempo reale
> **Tempo stimato**: 3-4 settimane (8 issue)

### Sprint 1.1: Backend Foundation (⇄ Parallelizzabile)

| # | Issue | Tipo | Priorità | Dependencies | Status |
|---|-------|------|----------|--------------|--------|
| 1 | #3160 | 🔴⭐ GST-001: Bounded Context & DB Schema | P0 | - | ⏳ |
| 2 | #3161 | 🔴⭐ GST-002: CQRS Commands & Queries | P0 | #3160 | ⏳ |
| 3 | #3162 | 🔴📊 GST-003: SSE Infrastructure | P0 | #3161 | ⏳ |

**Key Features**:
- Domain: `Session`, `Participant`, `ScoreEntry`, `GameEvent`
- Commands: `CreateSession`, `JoinSession`, `UpdateScore`, `EndSession`
- Queries: `GetActiveSession`, `GetSessionHistory`, `GetParticipantScores`
- Real-time: SSE endpoint `/api/v1/game-session-toolkit/{id}/events`

### Sprint 1.2: Frontend Integration (Sequential after 1.1)

| # | Issue | Tipo | Priorità | Dependencies | Status |
|---|-------|------|----------|--------------|--------|
| 4 | #3163 | 🔴🎯 GST-004: Generic Toolkit Routes | P0 | #3162 | ⏳ |
| 5 | #3164 | 🟠🎯 GST-005: Game-Specific Integration | P1 | #3163 | ⏳ |
| 6 | #3165 | 🟠📚 GST-006: Session History & Library | P1 | #3164 | ⏳ |

**Key UI Components**:
- `/game-session/{id}` - Active session view con real-time updates
- `<SessionHeader>` - Game info, participants, timer
- `<ParticipantCard>` - Player scores, avatars, status
- `<ScoreEntryForm>` - Quick score input con SSE sync
- Integration con UserLibrary per session history

### Sprint 1.3: Testing & QA

| # | Issue | Tipo | Priorità | Dependencies | Status |
|---|-------|------|----------|--------------|--------|
| 7 | #3166 | 🔴🧪 GST-007: MVP Testing & QA | P0 | #3165 | ⏳ |

**Test Coverage**:
- Backend: Unit tests per tutti i handlers (target 90%+)
- Frontend: Component tests + E2E flow completo
- Real-time: SSE connection stability, reconnection logic
- Performance: Load testing con 4+ concurrent players

### 🚩 CHECKPOINT 1 - GST MVP Launch
```bash
# Backend merge
git checkout main-dev && git merge backend-dev --no-ff -m "feat(GST): complete Game Session Toolkit backend (#3160-#3162)"

# Frontend merge
git checkout main-dev && git merge frontend-dev --no-ff -m "feat(GST): complete Game Session Toolkit UI (#3163-#3165)"

# Testing validation
git checkout main-dev && git merge testing --no-ff -m "test(GST): MVP testing coverage (#3166)"

git tag -a gst-v1.0-mvp -m "Game Session Toolkit MVP - Real-time collaborative scorekeeper"
```

**Success Criteria**:
- ✅ 4+ players possono gestire punteggi in tempo reale
- ✅ SSE updates < 500ms latency
- ✅ Session persistence e recovery
- ✅ 90%+ backend coverage, 85%+ frontend coverage
- ✅ E2E test completo login → create session → play → end

---

## FASE 2: User Library & Catalog UX 🟠 HIGH PRIORITY

> **Obiettivo**: Completare navigazione libreria personale e catalogo condiviso
> **Tempo stimato**: 2 settimane (3 issue + 1 bug fix)

### Sprint 2.1: Library Navigation

| # | Issue | Tipo | Priorità | Dependencies | Status |
|---|-------|------|----------|--------------|--------|
| 8 | #2866 | 🟠🎯 Library Page with Search & Filters | P1 | - | ⏳ OPEN |
| 9 | #2867 | 🟠🎯 Game Cards (Grid + List Views) | P1 | #2866 | ⏳ OPEN |

**Key Features**:
- `/library` - Dashboard con search bar, filters sidebar
- Grid/List toggle - Responsive layout con game cards
- Real-time search - Debounced con loading states
- Filters: Complexity, Players, Duration, Tags, Status
- Sorting: Name, Added Date, Rating, Play Count

### Sprint 2.2: Catalog Enhancements

| # | Issue | Tipo | Priorità | Dependencies | Status |
|---|-------|------|----------|--------------|--------|
| 10 | #3120 | 🟡⭐ Private Games & Catalog Proposal | P2 | #2867 | ⏳ OPEN |

**Key Features**:
- Private games toggle in UserLibrary
- Proposal workflow per aggiungere giochi al catalogo condiviso
- Moderation UI per editors (approve/reject proposals)

### Sprint 2.3: Critical Bug Fixes

| # | Issue | Tipo | Priorità | Dependencies | Status |
|---|-------|------|----------|--------------|--------|
| 11 | #3095 | 🔴🐛 Fix Dashboard Route `/giochi` → `/games` | P0 | - | ⏳ OPEN |

**Impact**: RecentGamesSection usa URL incorretti, broken navigation

### 🚩 CHECKPOINT 2 - Library UX Complete
```bash
# Frontend merge
git checkout main-dev && git merge frontend-dev --no-ff -m "feat(library): complete library navigation and catalog UX (#2866, #2867, #3120)"

# Bug fix merge
git checkout main-dev && git merge hotfix/3095 --no-ff -m "fix(dashboard): correct game routes (#3095)"

git tag -a library-ux-v1.0 -m "User Library UX complete with search, filters, and catalog proposals"
```

---

## FASE 3: Admin & Editor UX 🟡 MEDIUM PRIORITY

> **Obiettivo**: Bulk operations per moderazione e user management
> **Tempo stimato**: 2 settimane (3 issue)

### Sprint 3.1: Backend Commands (⇄ Parallelizzabile)

| # | Issue | Tipo | Priorità | Dependencies | Status |
|---|-------|------|----------|--------------|--------|
| 12 | #2886 | 🟡⭐ Suspend/Unsuspend User Commands | P2 | - | ⏳ OPEN |
| 13 | #2893 | 🟡⭐ BulkApprove/BulkReject Commands | P2 | - | ⏳ OPEN |

**CQRS Handlers**:
- `SuspendUserCommand`, `UnsuspendUserCommand` - Admin bounded context
- `BulkApproveGamesCommand`, `BulkRejectGamesCommand` - Editor bounded context

### Sprint 3.2: Frontend Bulk UI

| # | Issue | Tipo | Priorità | Dependencies | Status |
|---|-------|------|----------|--------------|--------|
| 14 | #2896 | 🟠🎯 Bulk Approval Floating Action Bar | P1 | #2893 | ⏳ OPEN |

**Key Features**:
- Floating action bar per selezione multipla
- Bulk approve/reject con confirmation dialog
- Optimistic UI updates con rollback on error
- Success/error toasts con detailed feedback

### 🚩 CHECKPOINT 3 - Admin Workflows Complete
```bash
# Backend merge
git checkout main-dev && git merge backend-dev --no-ff -m "feat(admin): add bulk user and game management (#2886, #2893)"

# Frontend merge
git checkout main-dev && git merge frontend-dev --no-ff -m "feat(editor): bulk approval UI with floating action bar (#2896)"

git tag -a admin-workflows-v1.0 -m "Admin and Editor bulk operations complete"
```

---

## FASE 4: Session & AI UX 🟠 HIGH PRIORITY

> **Obiettivo**: AI analytics dashboard e session quota tracking
> **Tempo stimato**: 2 settimane (3 issue, sequenziali)

### Sprint 4.1: Session Quota UI

| # | Issue | Tipo | Priorità | Dependencies | Status |
|---|-------|------|----------|--------------|--------|
| 15 | #3075 | 🟠🎯🤖 Session Quota UI Frontend | P1 | Backend #3070 ✅ | ⏳ OPEN |

**Key Features**:
- Real-time quota display: `5/10 active sessions`
- Warning states: ⚠️ approaching limit, 🚨 quota exceeded
- Upgrade prompt per Free tier users
- Integration con Dashboard widget

### Sprint 4.2: AI Usage Analytics (Sequential)

| # | Issue | Tipo | Priorità | Dependencies | Status |
|---|-------|------|----------|--------------|--------|
| 16 | #3074 | 🟢⭐🤖 AI Token Usage Backend | P3 | - | ⏳ OPEN |
| 17 | #3080 | 🟢🎯🤖 AI Usage Dashboard Frontend | P3 | #3074 | ⏳ OPEN |

**Key Features**:
- Backend: Token tracking per user, model breakdown
- Frontend: `/dashboard/ai-usage` con charts
- Metrics: Total tokens, Cost estimate, Usage by model
- Charts: Daily usage trend, Model distribution, Cost projection

### 🚩 CHECKPOINT 4 - Session & AI UX Complete
```bash
# Frontend merge
git checkout main-dev && git merge frontend-dev --no-ff -m "feat(session): add quota UI and AI analytics dashboard (#3075, #3080)"

# Backend merge (if #3074 needed)
git checkout main-dev && git merge backend-dev --no-ff -m "feat(ai): add token usage tracking (#3074)"

git tag -a ai-session-ux-v1.0 -m "Session quota and AI analytics UI complete"
```

---

## FASE 5: Testing & Quality 🔴 CRITICAL

> **Obiettivo**: Raggiungere coverage targets e validare tutti i flussi
> **Tempo stimato**: 3 settimane (4 issue)

### Sprint 5.1: E2E Test Coverage

| # | Issue | Tipo | Priorità | Dependencies | Status |
|---|-------|------|----------|--------------|--------|
| 18 | #3082 | 🟠🧪 Implement 50 E2E Test Flows | P1 | - | ⏳ OPEN |

**Test Categories**:
- Authentication: Login, Register, OAuth, 2FA (8 flows)
- Library: Add, Remove, Search, Filter, Bulk ops (12 flows)
- Catalog: Browse, Share, Propose (6 flows)
- Admin: User management, Suspension, Bulk approve (8 flows)
- Session: Create, Join, Score, End (6 flows)
- Profile: Settings, Avatar, Preferences (5 flows)
- Editor: Approval queue, Bulk reject (5 flows)

### Sprint 5.2: Coverage Targets

| # | Issue | Tipo | Priorità | Dependencies | Status |
|---|-------|------|----------|--------------|--------|
| 19 | #3025 | 🟡🧪 Reach Backend 90% Coverage | P2 | - | ⏳ OPEN |
| 20 | #3026 | 🟡🧪 Reach Frontend 85% Coverage | P2 | - | ⏳ OPEN |

**Current vs Target**:
- Backend: ~75% → 90% (focus: event handlers, validators)
- Frontend: ~69% → 85% (focus: components, hooks, stores)

### Sprint 5.3: Epic Test Coverage

| # | Issue | Tipo | Priorità | Dependencies | Status |
|---|-------|------|----------|--------------|--------|
| 21 | #2759 | 🟡🧪 EPIC: Frontend Test Coverage | P2 | #3026 | ⏳ OPEN |

**Scope**: Comprehensive testing epic tracking frontend quality

### 🚩 CHECKPOINT 5 - Quality Gates Met
```bash
# Testing validation
git checkout main-dev && git merge testing --no-ff -m "test: complete E2E coverage and reach quality targets (#3082, #3025, #3026)"

git tag -a quality-v2.0 -m "90% backend / 85% frontend coverage with 50 E2E flows"
```

---

## FASE 6: Component Library & Design System 🟢 LOW PRIORITY (Optional)

> **Obiettivo**: Foundation per design system condiviso
> **Tempo stimato**: 3 settimane (6 issue)
> **Nota**: Può essere posposta post-MVP

### Sprint 6.1: Storybook Foundation

| # | Issue | Tipo | Priorità | Dependencies | Status |
|---|-------|------|----------|--------------|--------|
| 22 | #2924 | 🟢⭐ Storybook Setup & Config | P3 | - | ⏳ OPEN |

### Sprint 6.2: Component Extraction

| # | Issue | Tipo | Priorità | Dependencies | Status |
|---|-------|------|----------|--------------|--------|
| 23 | #2925 | 🟢⭐ Extract Reusable Components | P3 | #2924 | ⏳ OPEN |
| 24 | #2930 | 🟢⭐ Extract from Admin Dashboard | P3 | #2924 | ⏳ OPEN |

### Sprint 6.3: Design System Docs

| # | Issue | Tipo | Priorità | Dependencies | Status |
|---|-------|------|----------|--------------|--------|
| 25 | #2926 | 🟢⭐ Typography, Colors, Spacing | P3 | #2925 | ⏳ OPEN |
| 26 | #2931 | 🟢⭐ Design System Documentation | P3 | #2925 | ⏳ OPEN |

### Sprint 6.4: Accessibility Audit

| # | Issue | Tipo | Priorità | Dependencies | Status |
|---|-------|------|----------|--------------|--------|
| 27 | #2929 | 🟢🧪 WCAG 2.1 AA Compliance | P3 | #2926 | ⏳ OPEN |

### 🚩 CHECKPOINT 6 - Design System Foundation (Optional)
```bash
# Frontend merge
git checkout main-dev && git merge frontend-dev --no-ff -m "feat(design): add Storybook, component library, and WCAG audit (#2924-#2931)"

git tag -a design-system-v1.0 -m "Component library and design system foundation"
```

---

## Dipendenze & Sequenzialità

```
FASE 1: Game Session Toolkit (GST) - CRITICAL PATH
├── Sprint 1.1: Backend (parallel) → #3160 → #3161 → #3162
├── Sprint 1.2: Frontend (sequential) → #3163 → #3164 → #3165
└── Sprint 1.3: Testing → #3166

FASE 2: Library UX - CAN START PARALLEL
├── #2866 → #2867 (sequential)
├── #3120 (depends on #2867)
└── #3095 (independent bug fix - START IMMEDIATELY)

FASE 3: Admin UX - PARALLEL READY
├── Backend: #2886 ⇄ #2893 (parallel)
└── Frontend: #2896 (depends on #2893)

FASE 4: Session & AI - SEQUENTIAL
├── #3075 (depends on backend #3070 ✅ already merged)
└── #3074 → #3080 (sequential AI analytics)

FASE 5: Testing - CAN START ANYTIME
├── #3082 (independent - 50 E2E flows)
├── #3025 ⇄ #3026 (parallel coverage targets)
└── #2759 (epic tracker)

FASE 6: Component Library - OPTIONAL POST-MVP
├── #2924 → (#2925 ⇄ #2930) → (#2926 ⇄ #2931) → #2929
└── Può essere posposta senza impatto su delivery MVP
```

---

## Timeline & Gantt (10 settimane per MVP)

```
Settimana:   1    2    3    4    5    6    7    8    9   10
             |    |    |    |    |    |    |    |    |    |
FASE 1 (GST) [========Backend========][=====Frontend=====][T]
FASE 2 (Lib)      [=====#3095====][======#2866-2867======][===#3120===]
FASE 3 (Adm)           [====BE Commands====][====#2896====]
FASE 4 (AI)                  [#3075][=====#3074-3080=====]
FASE 5 (Test)    [===============Ongoing E2E Testing===============]
                                                           [Coverage]
FASE 6 (Opt)                                               [Post-MVP]

CHECKPOINTS:           CP1      CP2   CP3  CP4      CP5
                        ↓        ↓     ↓    ↓        ↓
main-dev:         [====merge===merge=merge=merge===merge====]
                                                            ↓
main:                                                   [RELEASE]

🎯 MVP Target: Week 9-10
📊 Coverage Target: Week 10
🎨 Design System: Post-MVP (Week 11+)
```

---

## Risorse & Parallellismo

### Work Streams (4 parallel tracks)

**Stream A - Critical Path (GST)**:
- Week 1-4: Backend + Frontend GST implementation
- Week 5: GST testing & polish
- **Blocca**: Nessuno - può procedere indipendentemente

**Stream B - Library & Admin UX**:
- Week 2-3: #3095 bug fix (start ASAP)
- Week 3-5: #2866, #2867 library navigation
- Week 4-6: #2886, #2893 backend commands
- Week 6-7: #3120, #2896 catalog proposals & bulk UI
- **Blocca**: Nessuno - parallel a Stream A

**Stream C - AI & Session UX**:
- Week 4-5: #3075 quota UI
- Week 6-8: #3074 → #3080 AI analytics
- **Blocca**: Nessuno - parallel a Stream A/B

**Stream D - Testing (Ongoing)**:
- Week 1-10: #3082 E2E flows (continuous)
- Week 8-10: #3025, #3026 coverage push
- **Supporta**: Tutti gli stream

### Allocation Suggerita

**2 Frontend Devs**:
- Dev 1: Stream A (GST frontend) → Stream B (Library)
- Dev 2: Stream C (AI/Session) → Stream B (Admin) → Stream D (Testing)

**2 Backend Devs**:
- Dev 3: Stream A (GST backend) → Stream C (#3074)
- Dev 4: Stream B (Commands #2886, #2893) → Stream D (Coverage)

**1 QA/Test**:
- Stream D: E2E flows, coverage validation, regression testing

---

## Issue Escluse da Questa Roadmap

### Infrastructure (Roadmap separata)

| Issue | Motivo |
|-------|--------|
| #2967-#2976 | Zero-Cost CI/CD - Infrastructure EPIC separata |
| #2703 | S3 Object Storage - Post-MVP infrastructure |

### Testing Infrastructure (Post-MVP)

| Issue | Motivo |
|-------|--------|
| #2927 | Lighthouse CI - Performance monitoring, non-blocking |
| #2928 | k6 Load Testing - Advanced performance, post-MVP |
| #2852 | Chromatic Visual Regression - Nice-to-have, post-MVP |

### Design System (EPIC separata)

| Issue | Motivo |
|-------|--------|
| #2965 | Dual-Theme Design System - Large EPIC, fase 2 |

**Rationale**: Focus su UX funzionale prima, design system e infra dopo MVP

---

## Comandi Utili

### Verificare stato issue
```bash
# Lista issue aperte per label
gh issue list --state open --label "game-session-toolkit" --json number,title,state
gh issue list --state open --label "frontend" --json number,title,labels

# Dettagli issue specifica
gh issue view 3167 --json state,title,labels,body
```

### Branch workflow
```bash
# Creare branch per nuova feature
git checkout frontend-dev && git pull
git checkout -b feature/issue-3160-gst-backend-schema

# Creare branch per bug fix
git checkout main-dev && git pull
git checkout -b hotfix/issue-3095-dashboard-route

# Merge con squash per features
git checkout main-dev
git merge --squash feature/issue-3160-gst-backend-schema
git commit -m "feat(GST): add bounded context and database schema (#3160)"
```

### Testing workflow
```bash
# Backend tests
cd apps/api/src/Api
dotnet test --filter "Category=Unit&BoundedContext=GameSessionToolkit"
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura

# Frontend tests
cd apps/web
pnpm test -- --coverage --run
pnpm test:e2e -- game-session-toolkit
```

### Checkpoint validation
```bash
# Verificare merge readiness
git log --oneline --graph main-dev..frontend-dev | head -20
git diff main-dev..frontend-dev --stat

# Coverage check
cd apps/api/src/Api && dotnet test /p:CollectCoverage=true | grep "Line coverage"
cd apps/web && pnpm test:coverage | grep "All files"
```

---

## Success Metrics - MVP Definition

### Funzionalità Core (Must-Have)

- ✅ **GST Real-Time**: 4+ players possono gestire punteggi live con SSE
- ✅ **Library Navigation**: Search, filters, grid/list views funzionanti
- ✅ **Admin Workflows**: Bulk operations per user e game management
- ✅ **Session Quota**: UI real-time per tracking limiti tier-based
- ✅ **Bug Fixes**: #3095 dashboard route corretta

### Quality Gates (Must-Meet)

- ✅ **Backend Coverage**: ≥ 90%
- ✅ **Frontend Coverage**: ≥ 85%
- ✅ **E2E Tests**: ≥ 40/50 flows (80%)
- ✅ **Performance**: SSE latency < 500ms, page load < 2s
- ✅ **Accessibility**: No critical WCAG violations

### Nice-to-Have (Post-MVP)

- 🎨 Component Library (Storybook)
- 📊 AI Analytics Dashboard
- 🔐 Advanced auth features (oltre 2FA base)
- ♿ Full WCAG 2.1 AA compliance
- 🖼️ Visual regression testing

---

**Ultimo aggiornamento**: 2026-01-29 (v1.0 - Focus UX)

**Prossima revisione**: Dopo Checkpoint 1 (GST MVP)

**Changelog**:
- v1.0 (2026-01-29): Versione iniziale con focus UX
  - Nuova struttura: FASE 1 (GST) come priorità #1
  - Rimosso issue completate (43 closed)
  - Focus su 27 issue aperte: 14 high priority, 21 completabili Q1
  - Gantt timeline: 10 settimane per MVP, post-MVP opzionale
  - Work streams: 4 parallel tracks per massimizzare velocità
  - Success metrics chiari: GST real-time + library UX + quality gates
