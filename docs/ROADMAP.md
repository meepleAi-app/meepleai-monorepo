# 🗺️ MeepleAI Roadmap - Tutte le Issue Aperte

**Ultimo Aggiornamento**: 2026-01-30 18:30
**Issue Totali Aperte**: 57 (8 completate oggi)
**Priorità Massima**: 🤖 AI Agent System - Chat con Agenti
**Progress**: EPIC 0 ✅ | EPIC 1 Backend ✅ | EPIC 2 In Progress (25% complete)

---

## 🎯 Priorità Massima: Sistema AI Agent (5 settimane)

### Epic #3174: AI Agent System - RAG Integration with Chat UI

**Status**: EPIC 0 ✅ | EPIC 1 Backend ✅ | EPIC 2 In Progress
**Timeline**: 5 settimane (23.5 giorni dev) - Week 1-2 COMPLETATE ✅
**Issue Totali**: 18 (7 completate, 11 rimanenti)

#### ✅ Week 1: EPIC 0 - RAG Prerequisites (COMPLETATO 2026-01-30)

- [x] #3172 [RAG-001] PDF Processing E2E Validation ✅
- [x] #3173 [RAG-002] Agent Endpoints Smoke Test ✅

**Obiettivo**: ✅ Validare infrastruttura RAG esistente prima di procedere - COMPLETATO

---

#### ✅ Week 2-3: EPIC 1 - Agent Typology Management (Backend COMPLETATO 2026-01-30)

**Backend** (4 issues) - ✅ COMPLETATO:

- [x] #3175 AGT-001: AgentTypology Domain Model & Migration ✅
- [x] #3176 AGT-002: Typology CRUD Commands (Admin) ✅
- [x] #3177 AGT-003: Editor Proposal Commands ✅
- [x] #3178 AGT-004: Typology Query Handlers ✅

**Frontend** (4 issues) - 🟡 IN PROGRESS:

- [x] #3179 AGT-005: Admin Typologies List Page
- [x] #3180 AGT-006: Create/Edit Typology Form
- [ ] #3181 AGT-007: Typology Approval Queue
- [ ] #3182 AGT-008: Editor Proposal Form & Test Sandbox

**Deliverable**: Backend API completo ✅ | Frontend UI in sviluppo (Week 3 target)

---

#### 🟡 Week 3-4: EPIC 2 - Session-Based Agent (Chat UI) - IN PROGRESS

**Backend** (3 issues) - 🟡 33% Complete:

- [x] #3183 AGT-009: AgentSession Entity & Migration ✅ (Completato 2026-01-30)
- [x] #3184 AGT-010: Session Agent Commands (Launch, Chat, UpdateState)
- [ ] #3189 AGT-015: GST Integration - Agent State Sync with Game Events

**Frontend** (4 issues) - 🔴 Waiting on GST Backend:

- [ ] #3185 AGT-011: Game Card 'Ask Agent' Button ⭐
- [ ] #3186 AGT-012: Agent Configuration Modal ⭐
- [ ] #3187 AGT-013: Agent Chat Sidebar Component (SSE Streaming) ⭐⭐⭐
- [ ] #3188 AGT-014: Agent State Management (Zustand Store)

**Deliverable**: Backend session management avviato ✅ | UI chat in attesa GST foundation
**Blocker**: GST-001 to GST-003 prerequisiti per AGT-011 to AGT-014

---

#### 🔴 Week 5: EPIC 3 - Testing & Quality Assurance

- [ ] #3190 AGT-016: Frontend Agent Components Tests
- [ ] #3191 AGT-017: Agent E2E Test Flows (4 Scenarios)
- [ ] #3192 AGT-018: RAG Quality Validation (20 Sample Questions)

**Success Metrics**:

- > 90% response accuracy
- <5s E2E latency
- <3% hallucination rate
- > 85% frontend coverage, >90% backend coverage

---

## 🎮 Priorità Alta: Game Session Toolkit

### Epic #3167: Game Session Toolkit - Collaborative Scorekeeper

**Issue Totali**: 7
**Dipendenze**: Bloccante per EPIC 2 dell'AI Agent System

- [ ] #3160 GST-001: Backend - Bounded Context & Database Schema
- [ ] #3161 GST-002: Backend - CQRS Commands & Queries
- [ ] #3162 GST-003: Backend - Real-Time SSE Infrastructure
- [ ] #3163 GST-004: Frontend - Generic Toolkit Routes & Integration
- [ ] #3164 GST-005: Frontend - Game-Specific Toolkit Integration
- [ ] #3165 GST-006: Session History & UserLibrary Integration
- [ ] #3166 GST-007: MVP Testing & Quality Assurance

**Nota**: GST-001 (game_sessions table) è prerequisito per AGT-009

---

## 📊 Testing & Quality (Parallelo)

### Epic #3005: Test Coverage & Quality Improvement

**Target**: 90% Backend / 85% Frontend

#### Backend Coverage

- [ ] #3025 [P2] Reach Backend 90% Coverage Target
  - Current: ~85%
  - Gap: ~5% (focus su edge cases e error handling)

#### Frontend Coverage

- [ ] #3026 [P2] Reach Frontend 85% Coverage Target
  - Current: 69%
  - Gap: 16% (focus su components, hooks, utilities)

#### E2E Testing

- [ ] #3082 [Testing] Implement Missing E2E Test Flows (50 flows)
- [ ] #2852 Visual Regression Testing Setup - All Pages (Chromatic)

**Priorità**: Alta (eseguibile in parallelo agli sviluppi features)

---

## 🎨 Design System & Component Library

### Epic #2965: Site-Wide Dual-Theme Design System

**Vision**: Glass Light + Dark Professional themes

- [ ] #2924 [Component Library] Storybook Setup and Foundation
- [ ] #2925 [Component Library] Extract Reusable Components from Admin Dashboard
- [ ] #2926 [Component Library] Design System Documentation
- [ ] #2930 [Duplicate] Extract Reusable Components (da chiudere)
- [ ] #2931 [Duplicate] Design System Documentation (da chiudere)

**Nota**: Issues #2930 e #2931 sono duplicati di #2925 e #2926

---

## 📚 Personal Library & User Features

### User Library Features

- [ ] #2866 [Personal Library] Library Page with Search and Filters
- [ ] #2867 [Personal Library] Game Cards (Grid + List Views)
- [ ] #3120 feat(UserLibrary): Private Games & Catalog Proposal System

**Dipendenze**: GameCard component richiesto per AGT-011 (Agent button)

---

## 🏗️ Infrastructure & DevOps

### Epic #2967: Zero-Cost CI/CD Infrastructure

**Oracle Cloud Free Tier + Self-Hosted Runner**

**Week 1: Setup**

- [ ] #2968 [Week 1.1] Oracle Cloud Setup & VM Provisioning
- [ ] #2969 [Week 1.2] GitHub Actions Runner Installation & Configuration
- [ ] #2970 [Week 1.3] Workflow Migration to Self-Hosted Runner
- [ ] #2971 [Duplicate] Workflow Migration (da chiudere)

**Week 2: Monitoring & Validation**

- [ ] #2972 [Week 2.1] Performance Monitoring & Reliability Check
- [ ] #2973 [Week 2.2] Cost Validation on GitHub Billing

**Optional Enhancements**

- [ ] #2974 [Optional] Setup Monitoring (Prometheus + Grafana)
- [ ] #2975 [Optional] Document Troubleshooting Procedures
- [ ] #2976 [Optional] Create Maintenance Schedule Automation

### Storage & Performance

- [ ] #2703 feat(infra): S3-compatible object storage for PDF uploads
- [ ] #2927 [Infrastructure] Performance Testing (Lighthouse CI)
- [ ] #2928 [Infrastructure] Load Testing Setup with k6

---

## 🔧 Bug Fixes & Refactoring

### High Priority Bugs

- [ ] #3095 fix(dashboard): RecentGamesSection uses incorrect /giochi route instead of /games

### Refactoring

- [ ] #3096 refactor(tests): Convert dashboard tests to data-testid pattern (i18n compliance)

---

## ♿ Accessibility & Quality

- [ ] #2929 [Quality] Accessibility Audit & WCAG 2.1 AA Compliance for All Pages

---

## 📋 Gap Analysis & CQRS Compliance

### Epic #3066: Gap Analysis Resolution - CQRS Compliance

- [ ] #3073 [P2] Feature Flags - Tier-Based Backend
- [ ] #3074 [P3] AI Token Usage Tracking - Backend
- [ ] #3075 [P1] Session Quota UI - Frontend Implementation
- [ ] #3080 [P3] AI Usage Dashboard - Frontend

---

## 📅 Timeline Ottimizzato (10 settimane)

### ✅ Fase 1: Foundation (Week 1-2) - COMPLETATO

**Focus**: Sbloccare sviluppo agent system + GST foundation

1. ✅ Week 1: RAG Validation (#3172, #3173) ✅ COMPLETATO | GST Backend (#3160, #3161) In Progress
2. ✅ Week 2: Agent Typology Backend (#3175-#3178) ✅ COMPLETATO | GST SSE (#3162) Pending

**Achievement**: 7 issue completate in 1 settimana! EPIC 0 + EPIC 1 Backend Done 🎉

### Fase 2: Core Agent Development (Week 3-4)

**Focus**: Chat UI + Agent Management 3. Week 3: Agent Typology Frontend (#3179-#3182) + Agent Session Backend (#3183-#3184) 4. Week 4: **Chat Sidebar & Agent UI** (#3185-#3188) ⭐⭐⭐

### Fase 3: Testing & Quality (Week 5)

**Focus**: Validazione completa agent system 5. Week 5: E2E Tests + RAG Quality (#3190-#3192)

### Fase 4: Parallel Development (Week 6-8)

**Focus**: Component Library + Personal Library + Infrastructure 6. Week 6: Storybook Setup (#2924) + Library Pages (#2866, #2867) 7. Week 7: Design System (#2925, #2926) + Oracle Cloud (#2968-#2970) 8. Week 8: Testing Coverage (#3025, #3026) + CI/CD Validation (#2972, #2973)

### Fase 5: Polish & Launch (Week 9-10)

**Focus**: Bug fixes + E2E + Accessibility 9. Week 9: E2E Testing (#3082) + Bug Fixes (#3095, #3096) 10. Week 10: Accessibility Audit (#2929) + Final QA

---

## 🎯 Milestone Chiave

### Milestone 1: Agent Chat MVP (Week 5) ⭐⭐⭐

**Deliverable**:

- ✅ RAG pipeline validato
- ✅ 2 agent typologies (Rules Expert, Quick Start)
- ✅ Game card "Ask Agent" button
- ✅ Chat sidebar con SSE streaming
- ✅ Admin UI per gestione tipologie
- ✅ >90% test coverage

**Valore**: Giocatori possono chattare con AI agents per regole, setup, strategie

### Milestone 2: Game Session Toolkit (Week 4)

**Deliverable**:

- ✅ Backend CQRS completo
- ✅ SSE real-time infrastructure
- ✅ Frontend toolkit routes
- ✅ Session history integration

**Valore**: Foundation per collaborative scorekeeper + agent state sync

### Milestone 3: Design System & Component Library (Week 8)

**Deliverable**:

- ✅ Storybook con 20+ components
- ✅ Glass Light + Dark themes
- ✅ Design tokens documentati
- ✅ Reusable components extracted

**Valore**: Sviluppo UI accelerato, consistenza visiva

### Milestone 4: Zero-Cost CI/CD (Week 8)

**Deliverable**:

- ✅ Oracle Cloud runner operativo
- ✅ Workflows migrati
- ✅ $0 monthly cost
- ✅ Monitoring attivo

**Valore**: Costi GitHub Actions eliminati, build più veloci

---

## 📊 Metriche di Successo

### Agent System (Milestone 1)

- **Response Accuracy**: >90%
- **E2E Latency**: <5s
- **Hallucination Rate**: <3%
- **Test Coverage**: Backend >90%, Frontend >85%
- **User Adoption**: >60% players usa agents entro 2 settimane dal launch

### Testing Coverage (Ongoing)

- **Backend**: 85% → 90% (gap: 5%)
- **Frontend**: 69% → 85% (gap: 16%)
- **E2E Flows**: 0 → 50 flows

### Infrastructure (Milestone 4)

- **CI/CD Cost**: $120/month → $0/month
- **Build Time**: Baseline → -20% target
- **Uptime**: >99.5%

---

## 🚨 Blockers & Dipendenze

### Critical Path (Agent System)

```
✅ RAG Validation (#3172, #3173) - COMPLETATO
  ↓
✅ Agent Typology Backend (#3175-#3178) - COMPLETATO
  ↓
🟡 Agent Typology Frontend (#3179-#3182) - IN PROGRESS (Week 3 target)
  ↓
🔴 GST Backend (#3160-#3162) → ✅ Agent Session Backend (#3183-#3184) - AGT-009 Done
  ↓
🔴 Chat UI (#3185-#3188) ⭐⭐⭐ - BLOCKED by GST Backend
  ↓
⚪ E2E Testing (#3190-#3192) - Week 5 target
```

**Current Bottleneck**: GST Backend (#3160-#3162) blocking Chat UI development

### Dipendenze Esterne

- **GameCard Component** (#2867) → Required for AGT-011 (Agent button)
- **game_sessions table** (GST-001 #3160) → Required for AGT-009 (AgentSession)

### Duplicati da Chiudere

- #2930 (duplicato di #2925)
- #2931 (duplicato di #2926)
- #2971 (duplicato di #2970)

---

## 💡 Note Strategiche

### Perché Agent System è Priorità #1?

1. **High User Value**: Differenziatore competitivo principale
2. **90% Infrastructure Ready**: Qdrant, embedding, reranker, ollama già operativi
3. **Clear Scope**: 18 issues, 5 settimane, deliverable ben definito
4. **Foundation for Future**: Agent framework riutilizzabile per Strategy Helper, Tournament Assistant

### Opportunità di Parallelizzazione

- **Week 2-3**: Agent Typology Backend + GST Backend (team diversi)
- **Week 3-4**: Agent Frontend + GST Frontend (può procedere appena GST Backend è ready)
- **Week 6-10**: Component Library + Infrastructure + Testing (completamente paralleli)

### Risk Mitigation

- **RAG Validation First**: EPIC 0 sblocca tutto, eseguire Week 1
- **E2E Tests Early**: Iniziare test setup in parallelo allo sviluppo
- **Incremental Delivery**: Deploy agent system progressivamente (typology → session → chat)

---

## 🔀 Git Workflow Strategy - Parallel Development

### Branch Structure

```
main-dev (development trunk)
├── frontend-dev (UI/UX development)
│   ├── feature/agt-011-agent-button
│   ├── feature/agt-012-config-modal
│   ├── feature/agt-013-chat-sidebar
│   └── feature/component-library
│
└── backend-dev (API/Services development)
    ├── feature/agt-001-typology-model
    ├── feature/agt-009-agent-session
    └── feature/gst-001-session-schema
```

### Merge Points (Sincronizzazione)

#### Merge Point 1: Week 2 End (Agent Typology Backend Ready)

```bash
# Backend completa AGT-001 to AGT-004
backend-dev → main-dev (PR + merge)

# Frontend può iniziare AGT-005 to AGT-008
main-dev → frontend-dev (sync)
```

#### Merge Point 2: Week 3 End (Agent Typology Frontend + GST Backend Ready)

```bash
# Frontend completa AGT-005 to AGT-008
frontend-dev → main-dev (PR + merge)

# Backend completa GST-001 to GST-003, AGT-009, AGT-010
backend-dev → main-dev (PR + merge)

# Sincronizzazione per EPIC 2 development
main-dev → frontend-dev (sync)
main-dev → backend-dev (sync)
```

#### Merge Point 3: Week 4 End (Chat UI Ready)

```bash
# Frontend completa AGT-011 to AGT-014 (Chat Sidebar!)
frontend-dev → main-dev (PR + merge)

# Backend completa AGT-015 (GST Integration)
backend-dev → main-dev (PR + merge)

# Sincronizzazione per E2E testing
main-dev → frontend-dev (sync)
main-dev → backend-dev (sync)
```

#### Merge Point 4: Week 5 End (Testing Complete - MVP LAUNCH)

```bash
# Frontend tests AGT-016, E2E AGT-017
frontend-dev → main-dev (PR + merge)

# Backend validation AGT-018
backend-dev → main-dev (PR + merge)

# Final merge to production
main-dev → main (Release PR)
```

### Parallelization Rules

#### ✅ Può Procedere in Parallelo

- **Week 2**: Backend (AGT-001 to AGT-004) || Frontend (Component Library #2924-#2926)
- **Week 3**: Frontend (AGT-005 to AGT-008) || Backend (GST-001 to GST-003)
- **Week 4**: Frontend (AGT-011 to AGT-014) || Backend (AGT-015 GST Integration)
- **Week 6-8**: Frontend (Library #2866, #2867) || Backend (Feature Flags #3073) || Infra (Oracle Cloud #2968-#2970)

#### ⛔ Richiede Sincronizzazione (Sequential)

- **Week 1**: RAG Validation (#3172, #3173) → **BLOCKER** per tutto
- **Week 2 → Week 3**: Backend Typology deve completare prima che Frontend Typology inizi AGT-005
- **Week 3 → Week 4**: GST Backend deve completare prima che Agent Session Frontend inizi AGT-011
- **Week 5**: E2E tests richiedono sia Frontend che Backend completati

### Daily Sync Protocol

#### Morning Standup (async Slack)

- **Backend Team**: AGT-001 progress 70%, AGT-002 blocked by migration issue
- **Frontend Team**: Component Library 80%, Storybook setup complete
- **Conflicts**: None detected

#### Evening Sync (se necessario)

```bash
# Frontend synca da main-dev ogni sera per includere backend changes
cd apps/web
git checkout frontend-dev
git fetch origin
git merge origin/main-dev

# Risolvi conflicts se presenti
# Pusha changes
git push origin frontend-dev
```

#### Conflict Resolution Strategy

1. **API Contract Changes**: Backend notifica Frontend team → Update OpenAPI spec → Frontend rigenera client
2. **Database Schema Changes**: Backend crea migration → Frontend aggiorna types dopo merge point
3. **Environment Variables**: Backend aggiunge .secret entry → Frontend aggiorna .env.development

### Example Workflow (Week 3-4)

```bash
# === BACKEND TEAM (Week 3) ===
git checkout main-dev && git pull
git checkout -b backend-dev
git checkout -b feature/agt-009-agent-session

# Sviluppo AGT-009: AgentSession entity + migration
# Commit incrementali
git add . && git commit -m "feat(agent): add AgentSession entity"
git add . && git commit -m "feat(agent): add session migration"
git push -u origin feature/agt-009-agent-session

# PR to backend-dev
# Code review + merge
git checkout backend-dev
git merge feature/agt-009-agent-session
git push origin backend-dev

# === FRONTEND TEAM (Week 3 - parallel) ===
git checkout main-dev && git pull
git checkout -b frontend-dev
git checkout -b feature/agt-005-admin-list

# Sviluppo AGT-005: Admin typologies list page
git add . && git commit -m "feat(admin): add typologies list page"
git push -u origin feature/agt-005-admin-list

# PR to frontend-dev
# Code review + merge
git checkout frontend-dev
git merge feature/agt-005-admin-list
git push origin frontend-dev

# === MERGE POINT (End of Week 3) ===
# Backend merge
git checkout main-dev
git merge backend-dev
git push origin main-dev

# Frontend merge
git checkout main-dev
git merge frontend-dev
git push origin main-dev

# Sync per Week 4
git checkout backend-dev && git merge main-dev && git push
git checkout frontend-dev && git merge main-dev && git push

# === WEEK 4 ===
# Frontend ora può usare AgentSession types da backend
# Backend può procedere con AGT-015 (GST Integration)
```

### CI/CD Integration

#### Branch Protection Rules

```yaml
main-dev:
  required_reviews: 1
  required_checks:
    - backend-tests
    - frontend-tests
    - e2e-smoke-tests

frontend-dev:
  required_checks:
    - frontend-tests
    - frontend-lint
    - frontend-typecheck

backend-dev:
  required_checks:
    - backend-tests
    - backend-coverage-90
```

#### Automated Merge Point Validation

```yaml
# .github/workflows/merge-point-validation.yml
on:
  pull_request:
    branches: [main-dev]

jobs:
  validate-merge-point:
    - Check all dependencies merged
    - Run full integration tests
    - Validate API contracts
    - Check migration compatibility
```

---

## 📖 Risorse & Documentazione

### Agent System

- PRD: `docs/prd/ai-agent-system-mvp.md`
- Epic Breakdown: `docs/prd/ai-agent-epic-breakdown.md`
- Summary: `docs/prd/ai-agent-summary.md`
- Visual Roadmap: `docs/prd/ai-agent-visual-roadmap.md`

### Project Docs

- Questo Roadmap: `docs/ROADMAP.md`
- Architecture: `docs/01-architecture/`
- Development Guide: `docs/02-development/`
- Testing Guide: `docs/05-testing/`

---

**🎯 Next Actions** (Priority Order):

1. **🔴 Critical**: Completare GST Backend (#3160-#3162) - BLOCCA Chat UI Development
2. **🟡 High**: Continuare Agent Typology Frontend (#3180-#3182) - 3/4 issues rimanenti
3. **🟢 Medium**: Pianificare AGT-010 Session Commands (dipende da GST-003 SSE)

**🎉 Recent Achievements** (2026-01-30):

- ✅ EPIC 0 RAG Prerequisites: 2/2 issues completate
- ✅ EPIC 1 Backend: 4/4 issues completate
- ✅ EPIC 1 Frontend: 1/4 issue completata (AGT-005)
- ✅ EPIC 2 Backend: 1/3 issue completata (AGT-009)
- **Total**: 8 issue completate in 1 settimana! 🚀

**⚠️ Current Blocker**: GST Backend foundation (game_sessions table + SSE) prerequisito per Chat UI
