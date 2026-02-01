# 🗺️ MeepleAI Roadmap - Optimized Sequence

**Ultimo Aggiornamento**: 2026-01-31 16:30
**Priorità Massima**: 🤖 Agent Page Complete MVP
**Status**: Wave-Based Parallelization Strategy

---

## 📊 Executive Summary

**Issue Totali**: 44 aperte (51 originali - 7 completate/duplicate)
**Waves Attive**: 4
**Timeline**: 4-6 settimane (4 core + 2 infra opzionale)

**Progress Overview**:
- ✅ EPIC #3174 EPIC 0-1: Backend + Frontend Typology (100%)
- ✅ EPIC #3167 GST: Game Session Toolkit (100%)
- 🟡 EPIC #3174 EPIC 2: Agent Session Backend (100%) + Frontend (50%)
- 🔴 Agent Page Frontend: 0/18 (NEW PRIORITY)

**Duplicati da Chiudere**: #2930, #2931, #2971

---

## 🎯 WAVE 1: Agent Page MVP (Week 1-2) - 20 ISSUE

**Strategia**: Parallelizzazione massima Frontend (3 streams) + Backend + Testing
**Valore**: Feature principale differenziante, massimo impatto utente
**Efficienza**: 10 issue/week vs 1 issue/week sequenziale

### Frontend Stream A - Foundation (8 issue sequenziali)

**Timeline**: Week 1.1-1.5 (giorni 1-3)
**Dependencies**: Sequential routing → containers → components

1. **#3237** [FRONT-001] Agent Page Base Setup & Routing
   - Route `/agent` con autenticazione
   - Layout base con header + grid
   - Mobile-responsive foundation

2. **#3238** [FRONT-002] Agent Config Sheet Container
   - Shadcn Sheet component
   - Container logic per configurazione
   - State management base

3. **#3239** [FRONT-003] Game/Template/Model Selectors
   - Dropdown giochi da library
   - Template selector (Rules, Strategy, Setup)
   - AI model selector (GPT-4, Claude)

4. **#3240** [FRONT-004] Token Quota & Slot Cards Display
   - Quota visualization (progress bars)
   - Slot cards disponibili/occupati
   - Real-time sync con backend

5. **#3241** [FRONT-005] Contextual Action Bar
   - Actions: Launch, Configure, Close session
   - State-aware buttons (disabled quando appropriato)
   - Keyboard shortcuts

6. **#3242** [FRONT-006] Chat Sheet Container
   - Chat sidebar/sheet component
   - SSE connection logic foundation
   - Message list container

7. **#3254** [FRONT-016] Split View Layout (Desktop)
   - Desktop: Chat + PDF side-by-side
   - Mobile: Tabs/sheets switcher
   - Responsive breakpoints

8. **#3246** [FRONT-010] Slot Management Page
   - `/agent/slots` route
   - Active sessions list
   - Slot release/transfer actions

### Frontend Stream B - Chat Core (5 issue - START dopo #3242)

**Timeline**: Week 1.5-2 (giorni 4-7)
**Dependencies**: Richiede #3242 (Chat container) completo
**Critical Path**: SSE streaming integration ⭐

1. **#3243** [FRONT-007] Message Components & SSE Streaming ⭐ CRITICAL
   - Message bubble components (user, assistant, system)
   - SSE EventSource integration
   - Real-time message streaming
   - Typing indicators

2. **#3244** [FRONT-008] Citations & Confidence Display
   - Citation badges con link PDF
   - Confidence score visualization
   - Source reference popup

3. **#3245** [FRONT-009] Chat Input & SSE Integration ⭐ CRITICAL
   - Input form con markdown support
   - Send message → SSE connection
   - Loading states, error handling
   - Auto-scroll to latest message

4. **#3249** [FRONT-013] Agent Type Switcher & Dynamic Typology
   - Runtime switch tra typologies
   - Dynamic system prompt reload
   - Session context preservation

5. **#3250** [FRONT-014] Agent Settings Drawer (Runtime Config)
   - Temperature, max_tokens sliders
   - Stream toggle (SSE on/off)
   - Reset to defaults button

### Frontend Stream C - Advanced Features (2 issue - PARALLELO con B)

**Timeline**: Week 1.5-2 (giorni 4-7)
**Dependencies**: Indipendente da Stream B, può iniziare dopo Stream A

1. **#3251** [FRONT-015] PDF Viewer Integration & Citation Links ⭐ CRITICAL
   - react-pdf integration
   - Citation click → scroll to PDF page
   - Highlight referenced text
   - Mobile-optimized viewer

2. **#3247** [FRONT-011] Upgrade Flow & Premium CTA
   - Quota reached modal
   - Upgrade CTA design
   - Tier comparison table
   - Payment flow placeholder

### Backend Stream (2 issue - PARALLELO da Day 1)

**Timeline**: Week 1 (giorni 1-3)
**Dependencies**: Nessuna, può iniziare immediatamente
**Parallelizzabile**: Completo indipendente da frontend

1. **#3252** [BACK-AGT-001] PATCH /api/v1/typologies/{id}
   - Update typology command
   - Validation + authorization
   - Audit trail update
   - Integration tests

2. **#3253** [BACK-AGT-002] PATCH /api/v1/agent-sessions/{id}/config
   - Update runtime config (temperature, max_tokens)
   - Session state validation
   - SSE broadcast config change
   - Integration tests

### Testing Stream (2 issue - PARALLELO durante sviluppo)

**Timeline**: Week 1-2 (continuo)
**Dependencies**: #3248 dipende da frontend base (#3237-#3242)

1. **#3258** [P1] Fix Remaining 11 Backend Test Failures (0.4%) ⭐ CRITICAL
   - **START IMMEDIATELY** (blocca CI/CD)
   - Fix test flakiness
   - Testcontainers stability
   - Coverage validation

2. **#3248** [FRONT-012] E2E Tests & Responsive Validation
   - Playwright E2E scenarios (dopo frontend base):
     - Launch agent session
     - Send message → receive response
     - Citation click → PDF scroll
     - Slot management flow
   - Responsive testing (375px-1920px)
   - Accessibility validation

---

## 🎯 WAVE 2: Foundation & Quality (Week 2-3) - 8 ISSUE

**Strategia**: CQRS compliance + Testing consolidation (parallelo)
**Overlap**: Inizia durante Wave 1 finale (Week 2)

### CQRS Gap Analysis Priority (2 issue - backend)

**Timeline**: Week 2-3 (giorni 8-14)

1. **#3073** [P2] Feature Flags Backend - Tier-Based
   - FeatureFlag entity + migration
   - Tier-based activation logic
   - Admin UI endpoints (CRUD)
   - Integration tests

2. **#3075** [P1] Session Quota UI - Frontend Implementation
   - Dependencies: #3073 (Feature flags)
   - Quota display component
   - Real-time sync con backend
   - Upgrade CTA integration

### Testing & Coverage (3 issue - parallelo)

**Timeline**: Week 2-3 (giorni 8-14)
**Parallelizzabile**: Indipendente da CQRS

1. **#3082** [P1] E2E Test Flows (50 scenarios)
   - Authentication flows (5)
   - Game catalog flows (10)
   - Agent interaction flows (15)
   - Library management flows (10)
   - Admin workflows (10)

2. **#3025** [P2] Backend 90% Coverage Target
   - Focus: Edge cases, error handling
   - Integration tests for new endpoints
   - Validation logic coverage

3. **#3026** [P2] Frontend 85% Coverage Target
   - Component unit tests
   - Hook testing
   - Store testing
   - Utility functions coverage

### Low Priority CQRS (2 issue - opzionale)

**Timeline**: Week 3+ (se tempo disponibile)

1. **#3074** [P3] AI Token Usage Tracking - Backend
   - TokenUsage aggregate
   - Per-user/per-session tracking
   - Analytics queries

2. **#3080** [P3] AI Usage Dashboard - Frontend
   - Dependencies: #3074
   - Charts (token usage trends)
   - Cost estimation
   - Export to CSV

---

## 🎯 WAVE 3: User Library + Design System (Week 3-4) - 6 ISSUE

**Strategia**: Library UX + Storybook foundation (parallelo)
**Value**: Enhanced user experience + development velocity

### User Library Enhancement (3 issue - frontend)

**Timeline**: Week 3 (giorni 15-21)

1. **#2866** [Personal Library] Library Page with Search and Filters
   - `/library` route
   - Search bar + filters (owned, wishlist, played)
   - Pagination
   - Sort options (name, date, plays)

2. **#2867** [Personal Library] Game Cards (Grid + List Views)
   - Grid view (responsive cards)
   - List view (compact table)
   - View toggle component
   - Quick actions (play, wishlist, remove)

3. **#3120** [UserLibrary] Private Games & Catalog Proposal System
   - Private game creation
   - Proposal workflow (submit → review → approve)
   - Integration con shared catalog

### Design System Foundation (3 issue - PARALLELO con Library)

**Timeline**: Week 3-4 (giorni 15-24)

1. **#2924** [Component Library] Storybook Setup and Foundation
   - Storybook 7+ installation
   - Component story templates
   - Theme provider integration
   - Accessibility addon

2. **#2925** [Component Library] Extract Reusable Components
   - **CHIUDI #2930 (duplicato)**
   - Extract da admin dashboard:
     - DataTable generic
     - FormField wrappers
     - Modal/Dialog patterns
     - Button variants
   - Document props + usage

3. **#2926** [Component Library] Design System Documentation
   - **CHIUDI #2931 (duplicato)**
   - Typography scale docs
   - Color palette (light + dark)
   - Spacing system
   - Component guidelines

---

## 🎯 WAVE 4: Infrastructure - BASSA PRIORITÀ (Week 5-6) - 10 ISSUE

**Strategia**: Cost optimization + performance (opzionale)
**Note**: Posticipabile, non bloccante per features

### Oracle Cloud Zero-Cost CI/CD (6 issue - infra)

**Timeline**: Week 5-6 (se richiesto)
**Epic**: #2967

1. **#2968** [Week 1.1] Oracle Cloud Setup & VM Provisioning
   - Oracle Cloud account
   - VM ARM64 provisioning
   - Network configuration
   - SSH access

2. **#2969** [Week 1.2] GitHub Actions Runner Installation
   - Runner installation script
   - Auto-start configuration
   - Security hardening

3. **#2970** [Week 1.3] Workflow Migration to Self-Hosted
   - **CHIUDI #2971 (duplicato)**
   - Migrate backend-ci workflow
   - Migrate frontend-ci workflow
   - Migrate e2e-tests workflow

4. **#2972** [Week 2.1] Performance Monitoring & Reliability
   - Uptime monitoring
   - Build time metrics
   - Failure rate tracking

5. **#2973** [Week 2.2] Cost Validation on GitHub Billing
   - Verify $0 Actions cost
   - Document savings ($120/mo → $0)

6. **#2974-#2976** [Optional] Monitoring & Maintenance
   - #2974: Prometheus + Grafana setup
   - #2975: Troubleshooting docs
   - #2976: Maintenance automation

### Performance Testing Infrastructure (4 issue - opzionale)

**Timeline**: Week 6+ (bassa priorità)

1. **#2927** [Infrastructure] Lighthouse CI Performance Testing
   - Lighthouse CI setup
   - Performance budgets
   - Automated reports
   - PR integration

2. **#2928** [Infrastructure] k6 Load Testing for APIs
   - k6 installation
   - Load test scenarios (auth, catalog, agent)
   - Performance baselines
   - Grafana dashboards

3. **#2929** [Quality] Accessibility Audit (WCAG 2.1 AA)
   - Automated scan (axe, pa11y)
   - Manual audit per page
   - Remediation tasks
   - Compliance documentation

4. **#2703** [Infra] S3-Compatible Object Storage
   - MinIO/Supabase Storage integration
   - PDF upload migration
   - Backup strategy
   - Cost analysis

---

## 📅 Timeline Consolidato

### Week 1-2: WAVE 1 Agent Page (20 issue)

**Parallelizzazione**:
- Frontend Stream A (8) → giorni 1-3
- Frontend Stream B (5) → giorni 4-7 (dopo A.#3242)
- Frontend Stream C (2) → giorni 4-7 (parallelo B)
- Backend Stream (2) → giorni 1-3 (parallelo A)
- Testing Stream (2) → giorni 1-7 (continuo)

**Efficienza**: 20 issue / 2 settimane = 10 issue/week

### Week 2-3: WAVE 2 Foundation (8 issue - overlap con Wave 1)

**Parallelizzazione**:
- CQRS (2) + Testing (3) + Low Priority (2 opzionale)

**Efficienza**: 8 issue / 1 settimana

### Week 3-4: WAVE 3 Library + Design (6 issue)

**Parallelizzazione**:
- Library (3) + Design System (3)

**Efficienza**: 6 issue / 1 settimana

### Week 5-6: WAVE 4 Infrastructure (10 issue - OPZIONALE)

**Parallelizzazione**:
- Oracle Cloud (6) + Performance (4)

**Efficienza**: 10 issue / 2 settimane (se richiesto)

---

## 🎯 Milestone Chiave

### M1: Agent Page MVP (Week 2) ⭐⭐⭐

**Deliverable**:
- 18 frontend components completati
- 2 backend PATCH endpoints
- E2E tests passanti
- 11 backend test failures fixati

**Valore**: Feature principale differenziante completa

### M2: Foundation Complete (Week 3)

**Deliverable**:
- CQRS gap analysis P1 completo
- 50 E2E test flows
- Backend 90% + Frontend 85% coverage

**Valore**: Production-ready quality assurance

### M3: UX Enhancement (Week 4)

**Deliverable**:
- User library completa
- Storybook + design system foundation
- Component library documentata

**Valore**: Enhanced UX + development velocity

### M4: Infrastructure Optimization (Week 6 - OPZIONALE)

**Deliverable**:
- Zero-cost CI/CD operativo
- Performance testing infrastructure
- Accessibility compliance

**Valore**: Cost optimization + quality gates

---

## 🚨 Blockers & Dipendenze

### Critical Path (Agent Page)

```
✅ EPIC 2 Backend (Session + GST) - DONE
  ↓
🔴 Stream A Frontend (Base UI) - #3237-#3242
  ↓
🔴 Stream B Frontend (Chat SSE) - #3243-#3245 ⭐ CRITICAL
  ↓
🔴 Stream C Frontend (PDF + Polish) - #3251, #3247
  ↓
🔴 E2E Testing - #3248
  ↓
✅ Agent Page MVP Complete
```

**Current Blocker**: Nessuno - pronto per partenza Wave 1

**Dipendenze Interne**:
- Stream B (#3243-#3245) → Richiede Stream A #3242 (Chat container)
- #3248 (E2E tests) → Richiede Stream A base (#3237-#3242)
- #3075 (Session Quota UI) → Richiede #3073 (Feature flags backend)
- #3080 (AI Usage Dashboard) → Richiede #3074 (Token tracking)

**Azioni Immediate**:
1. ✅ Start #3258 (Fix 11 test failures) - CRITICAL per CI/CD
2. ✅ Start Backend Stream (#3252-#3253) - Indipendente
3. ✅ Start Frontend Stream A (#3237-#3242) - Foundation

**Duplicati da Chiudere**:
1. #2930 → Duplicato di #2925 (Extract components)
2. #2931 → Duplicato di #2926 (Design docs)
3. #2971 → Duplicato di #2970 (Workflow migration)

---

## 📊 Metriche di Successo

**Agent Page MVP**:
- 18 frontend components ✅
- 2 backend endpoints ✅
- E2E tests 100% pass rate ✅
- SSE streaming latency <1s ✅
- Mobile responsive 375px-1920px ✅

**Testing Quality**:
- Backend: 85% → 90% coverage
- Frontend: 69% → 85% coverage
- E2E: 0 → 50 flows implemented
- Test failures: 11 → 0

**Infrastructure (opzionale)**:
- CI/CD cost: $120/mo → $0
- Build time: -20% improvement
- Uptime: >99.5%

---

## 💡 Note Strategiche

**Perché Agent Page è Priorità #1?**

1. **Valore Utente**: Feature principale differenziante vs competitori
2. **Foundation Ready**: Backend 100% completo (EPIC 2), pronto per UI
3. **Parallelizzazione**: 3 frontend streams + backend + testing = max efficienza
4. **MVP Clear**: 18 issue ben definite, scope chiaro

**Perché Infrastructure è Bassa Priorità?**

1. **Non Bloccante**: Cost optimization, non feature-critical
2. **Valore Differito**: ROI a lungo termine vs features immediate
3. **Opzionale**: Sistema funziona con GitHub Actions standard
4. **Posticipabile**: Può essere fatto in background dopo MVP

**Parallelizzazione Strategy**:

- **Week 1-2**: Frontend (3 streams) + Backend + Testing = 5 work streams paralleli
- **Week 2-3**: CQRS + Coverage = 2 work streams paralleli
- **Week 3-4**: Library + Design = 2 work streams paralleli
- **Week 5-6**: Infra opzionale (se richiesto)

**Risk Mitigation**:

- Fix #3258 test failures ASAP (blocca CI/CD)
- E2E tests paralleli durante sviluppo (feedback rapido)
- Backend endpoints prima di frontend (no blockers)
- Incremental delivery (Stream A → B → C)

---

## 📖 Risorse & Documentazione

**PRD**: `docs/prd/agent-page-design-spec.md`
**Architecture**: `docs/01-architecture/`
**Development**: `docs/02-development/`
**Testing**: `docs/05-testing/`
**Workflows**: `docs/workflows/git-parallel-development.md`

---

## 🎯 Next Actions (Priority Order)

1. **🔴 IMMEDIATE**: #3258 Fix 11 backend test failures ⭐ CRITICAL
2. **🔴 HIGH**: Start Backend Stream (#3252-#3253) - Parallel da Day 1
3. **🔴 HIGH**: Start Frontend Stream A (#3237-#3242) - Foundation
4. **🟡 MEDIUM**: Chiudi duplicati (#2930, #2931, #2971)
5. **🟡 MEDIUM**: Aggiorna project board con Wave structure

---

**Ultimo Aggiornamento**: 2026-01-31 16:30
**Prossimo Review**: 2026-02-07 (fine Wave 1)
