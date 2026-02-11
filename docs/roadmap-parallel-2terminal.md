# 🚀 Roadmap Parallela 2-Terminal - MeepleAI

**Planning Date**: 2026-02-11
**Strategy**: Parallelizzazione bilanciata con checkpoint sincronizzazione
**Duration**: 4 settimane (con parallelizzazione)
**Scope**: Epic #1-4 + Gap Analysis issues

---

## 📊 Executive Summary

| Metrica | Valore |
|---------|--------|
| **Total Issues** | 47 issue |
| **Epics** | 4 epic principali |
| **Gap Analysis** | 6 issue critiche |
| **Terminals** | 2 paralleli (A + B) |
| **Checkpoints** | 4 sincronizzazioni |
| **Duration** | 4 settimane (vs 8 sequenziali) |
| **Time Savings** | 50% con parallelizzazione |

---

## 🎯 Allocation Strategy

### Terminal A: Frontend-Heavy Track
**Focus**: UI/UX, componenti, user-facing features
**Developer**: Frontend specialist o fullstack con focus UI

### Terminal B: Backend-Heavy Track
**Focus**: API, integrazioni, processing, infrastructure
**Developer**: Backend specialist o fullstack con focus services

**Bilanciamento**: ~50/50 effort, minimizza dipendenze cross-terminal

---

## 📅 Week 1: Epic #1 (MeepleCard) + Gap Analysis Part 1

### 🖥️ Terminal A - Frontend Focus

#### Epic #1: MeepleCard Enhancements (5 giorni)
**Issues**: #4073, #4076, #4075, #4072, #4080, #4081

- [ ] **#4073**: WCAG 2.1 AA Accessibility (1 giorno)
  - Keyboard navigation, ARIA labels, contrast ratios
  - Components: MeepleCard accessibility audit

- [ ] **#4076**: Mobile Tag Optimization (1 giorno)
  - Responsive tag layout, touch targets
  - Components: TagList responsive behavior

- [ ] **#4075**: Tag System Vertical Layout (1 giorno)
  - Redesign tag positioning
  - Components: TagContainer layout logic

- [ ] **#4072**: Smart Tooltip Positioning (0.5 giorni)
  - Auto-positioning logic (viewport boundaries)
  - Components: TooltipProvider enhancement

- [ ] **#4080**: Context-Aware Tests (1 giorno)
  - Unit tests per tutti i variant contexts
  - Test: MeepleCard.test.tsx expansion

- [ ] **#4081**: Performance Optimization (0.5 giorni)
  - React.memo, useMemo optimization
  - Lazy loading per heavy components

#### Gap Analysis: Wishlist UI (3 giorni)
**Issue**: #4114

- [ ] `/library/wishlist` page implementation
- [ ] WishlistButton component (MeepleCard quick action)
- [ ] WishlistHighlightsWidget (dashboard)
- [ ] API integration with optimistic updates

**Total Terminal A Week 1**: 8 giorni effort (parallelizzabile in 5 giorni)

---

### 💻 Terminal B - Backend Focus

#### Epic #1: MeepleCard Backend (2 giorni)
**Issues**: #4079, #4078, #4077, #4074

- [ ] **#4079**: Agent Type Support (0.5 giorni)
  - Backend DTO per agent entities
  - Endpoint: Enhance game detail response

- [ ] **#4078**: Ownership State Logic (0.5 giorni)
  - Backend ownership validation
  - Endpoint: User library ownership queries

- [ ] **#4077**: Collection Limits Management (0.5 giorni)
  - Backend limits calculation
  - Endpoint: User tier limits API

- [ ] **#4074**: Permission System Integration (0.5 giorni)
  - Backend permission checks
  - Middleware: Authorization enhancements

#### Gap Analysis: Notifications Backend (3 giorni)
**Issue**: #4113 (Backend portion)

- [ ] SSE infrastructure enhancement
- [ ] Notification preferences endpoints
- [ ] Real-time event triggers (upload, processing, errors)
- [ ] Email integration for critical notifications

#### Gap Analysis: Play Records Backend (2 giorni)
**Issue**: #4115 (Backend portion)

- [ ] Players management endpoints
- [ ] Scores tracking endpoints
- [ ] Session workflow (start/complete) logic
- [ ] Validation: cannot complete without scores

**Total Terminal B Week 1**: 7 giorni effort (parallelizzabile in 5 giorni)

---

### 🔄 CHECKPOINT 1 (End Week 1)

**Sincronizzazione richiesta**:
- [ ] Terminal A: MeepleCard UI + Wishlist UI ✅
- [ ] Terminal B: MeepleCard Backend + Notifications Backend ✅
- [ ] Merge: Feature branches → main-dev
- [ ] Test: Integration tests cross-terminal
- [ ] Review: Code review reciproco

**Test Plan Checkpoint 1**:
```bash
# Terminal A
cd apps/web
pnpm test -- MeepleCard
pnpm test -- Wishlist
pnpm test:e2e -- wishlist.spec.ts

# Terminal B
cd apps/api/src/Api
dotnet test --filter "Category=Unit&BoundedContext=UserLibrary"
dotnet test --filter "Category=Unit&BoundedContext=UserNotifications"

# Integration
pnpm test:e2e -- notifications-realtime.spec.ts
```

**Deliverables**:
- ✅ MeepleCard: Accessibility, Mobile, Performance complete
- ✅ Wishlist: Full UI + Backend integration
- ✅ Notifications: Backend SSE ready
- 🔄 Ready for: Notifications UI (Week 2)

---

## 📅 Week 2: Epic #3 (Navbar) + Gap Analysis Part 2

### 🖥️ Terminal A - Frontend Focus

#### Epic #3: Navbar Restructuring (4 giorni)
**Issues**: #4097-4105 (9 issue)

- [ ] **#4097**: Dropdown Grouping Structure (0.5 giorni)
  - Redesign navbar menu hierarchy
  - Components: NavDropdown, NavGroup

- [ ] **#4098**: Mobile Hamburger Menu (1 giorno)
  - Responsive mobile navigation
  - Components: MobileNav, MobileSheet

- [ ] **#4099**: Dynamic Route / (0.5 giorni)
  - Welcome vs Dashboard routing logic
  - Page: Root route conditional rendering

- [ ] **#4100**: Anonymous Catalog Restrictions (0.5 giorni)
  - Show limited content for anonymous users
  - Components: AuthGuard, GuestView

- [ ] **#4101**: Dual CTA (0.5 giorni)
  - Login + Register buttons styling
  - Components: CTAButtons

- [ ] **#4102**: Settings Dropdown (1 giorno)
  - 8 settings sections menu
  - Components: SettingsDropdown

#### Gap Analysis: Notifications UI (Frontend) (2 giorni)
**Issue**: #4113 (Frontend portion)

- [ ] NotificationBell component (header)
- [ ] NotificationDropdown with real-time updates
- [ ] Toast notifications
- [ ] `/settings/notifications` preferences page

**Total Terminal A Week 2**: 6 giorni effort

---

### 💻 Terminal B - Backend Focus

#### Epic #4: PDF Status Tracking (5 giorni)
**Issues**: #4106-4111 (6 issue)

- [ ] **#4106**: 7-State Embedding Pipeline (1.5 giorni)
  - State machine: Uploaded → Queued → Processing → Chunking → Embedding → Indexing → Complete
  - Backend: PDF processing state management

- [ ] **#4107**: Manual Retry + Error Handling (1 giorno)
  - Retry logic per failed uploads
  - Endpoint: POST /pdf/{id}/retry

- [ ] **#4108**: Multi-Location Status UI (Backend) (0.5 giorni)
  - Status API with location context
  - Endpoint: GET /pdf/{id}/status

- [ ] **#4109**: Real-time Updates (SSE) (1.5 giorni)
  - SSE events per PDF processing progress
  - Integration: Existing SSE infrastructure

- [ ] **#4110**: Duration Metrics & ETA (0.5 giorni)
  - Calculate processing time estimates
  - Endpoint: GET /pdf/{id}/metrics

- [ ] **#4111**: Notification Channels (1 giorno)
  - Configure notification delivery (email, push, in-app)
  - Integration: UserNotifications bounded context

#### Gap Analysis: Achievements Backend (1 giorno)
**Issue**: #4117 (Backend portion)

- [ ] Achievement progress calculation
- [ ] Recent achievements endpoint
- [ ] Achievement unlock triggers

**Total Terminal B Week 2**: 6 giorni effort

---

### 🔄 CHECKPOINT 2 (End Week 2)

**Sincronizzazione richiesta**:
- [ ] Terminal A: Navbar complete + Notifications UI ✅
- [ ] Terminal B: PDF Status complete + Achievements Backend ✅
- [ ] Merge: Feature branches → main-dev
- [ ] Test: E2E navbar flows + PDF status tracking
- [ ] Deploy: Staging environment update

**Test Plan Checkpoint 2**:
```bash
# Terminal A
pnpm test:e2e -- navbar-restructuring.spec.ts
pnpm test:e2e -- notifications-ui.spec.ts

# Terminal B
dotnet test --filter "BoundedContext=DocumentProcessing"
dotnet test --filter "Category=Integration&Feature=PDFStatus"

# Integration
pnpm test:e2e -- pdf-upload-with-notifications.spec.ts
```

**Deliverables**:
- ✅ Navbar: Complete restructuring + mobile
- ✅ Notifications: Full system (Backend + Frontend)
- ✅ PDF Status: 7-state tracking + SSE
- 🔄 Ready for: Agent System (Week 3)

---

## 📅 Week 3: Epic #2 (Agent System) Part 1

### 🖥️ Terminal A - Frontend Focus

#### Epic #2: Agent System - UI Components (5 giorni)
**Issues**: #4085, #4087, #4090, #4091, #4092, #4093

- [ ] **#4085**: Chat UI Base Component (1.5 giorni)
  - AgentChatInterface base component
  - Message list, input, streaming display
  - Components: ChatMessage, ChatInput, StreamingIndicator

- [ ] **#4087**: Chat History Page (1 giorno)
  - `/agents/history` page with timeline + filters
  - Components: ChatHistoryList, ChatFilters

- [ ] **#4090**: Agent List Page (1 giorno)
  - `/agents` page with agent catalog
  - Components: AgentGrid, AgentCard

- [ ] **#4091**: Dashboard Widget Your Agents (0.5 giorni)
  - Dashboard widget: recent agent interactions
  - Component: YourAgentsWidget

- [ ] **#4092**: Game Page Agent Section (0.5 giorni)
  - Add agent section to `/games/[id]`
  - Component: GameAgentSection

- [ ] **#4093**: Strategy Builder UI (0.5 giorni)
  - Visual strategy builder interface
  - Component: StrategyBuilder

#### Gap Analysis: Achievements UI (1.5 giorni)
**Issue**: #4117 (Frontend portion)

- [ ] `/profile/achievements` page
- [ ] AchievementCard components
- [ ] Dashboard widget integration

**Total Terminal A Week 3**: 6.5 giorni effort

---

### 💻 Terminal B - Backend Focus

#### Epic #2: Agent System - Backend (5 giorni)
**Issues**: #4082, #4083, #4084, #4086, #4088, #4094, #4095, #4096

- [ ] **#4082**: Backend Multi-Agent per Game (1.5 giorni)
  - Multi-agent routing infrastructure
  - Endpoint: POST /agents/multi-agent/query

- [ ] **#4083**: Strategy System (Base/Config/Custom) (1 giorno)
  - Strategy persistence and retrieval
  - Endpoint: GET/POST/PUT /strategies

- [ ] **#4084**: Semi-Auto Creation Flow (1 giorno)
  - Agent creation wizard backend
  - Endpoint: POST /agents/semi-auto/create

- [ ] **#4086**: Chat Persistence (Hybrid Sync) (1 giorno)
  - Chat history storage (DB + cache)
  - Endpoint: GET/POST /chat-sessions

- [ ] **#4088**: Resume Chat (All Methods) (0.5 giorni)
  - Resume logic: by ID, by game, by context
  - Endpoint: POST /chat-sessions/{id}/resume

- [ ] **#4094**: POC Strategy Implementation (Backend only) (1 giorno)
  - Default POC strategy backend
  - Integration: RAG pipeline

- [ ] **#4095**: Tier Limit Enforcement (0.5 giorni)
  - Agent usage limits per tier
  - Middleware: AgentRateLimiter

- [ ] **#4096**: KB Integration (0.5 giorni)
  - KnowledgeBase context for agents
  - Integration: Existing RAG system

#### Gap Analysis: 2FA Backend (1 giorno)
**Issue**: #4116 (Backend portion)

- [ ] 2FA setup/enable/disable endpoints
- [ ] TOTP generation and verification
- [ ] Recovery codes generation

**Total Terminal B Week 3**: 6 giorni effort

---

### 🔄 CHECKPOINT 3 (End Week 3)

**Sincronizzazione richiesta**:
- [ ] Terminal A: Agent UI complete + Achievements UI ✅
- [ ] Terminal B: Agent Backend + 2FA Backend ✅
- [ ] Integration: Agent chat end-to-end flow
- [ ] Merge: Feature branches → main-dev
- [ ] Test: Agent system complete workflows

**Test Plan Checkpoint 3**:
```bash
# Terminal A
pnpm test:e2e -- agent-chat-flow.spec.ts
pnpm test:e2e -- agent-list-and-history.spec.ts
pnpm test:e2e -- achievements-display.spec.ts

# Terminal B
dotnet test --filter "BoundedContext=KnowledgeBase&Feature=Agents"
dotnet test --filter "Feature=TwoFactorAuth"

# Integration
pnpm test:e2e -- agent-multi-agent-query.spec.ts
pnpm test:e2e -- agent-strategy-builder.spec.ts
```

**Deliverables**:
- ✅ Agent System: Chat UI + Backend multi-agent ✅
- ✅ Agent System: History, List, Dashboard widgets ✅
- ✅ Achievements: Full display system ✅
- ✅ 2FA: Backend ready ✅
- 🔄 Ready for: PDF Status UI + 2FA UI (Week 4)

---

## 📅 Week 4: Epic #4 (PDF Status) + Gap Analysis Part 3

### 🖥️ Terminal A - Frontend Focus

#### Epic #4: PDF Status Tracking - UI (3 giorni)
**Issues**: #4108 (UI portion), #4109 (UI portion), #4110 (UI portion)

- [ ] **#4108**: Multi-Location Status UI (1 giorno)
  - Status badges in: upload page, library, processing queue
  - Components: PDFStatusBadge, ProcessingProgress

- [ ] **#4109**: Real-time Updates UI (1 giorno)
  - SSE connection for live progress
  - Hook: usePDFStatusEvents

- [ ] **#4110**: Duration Metrics Display (1 giorno)
  - ETA display, progress percentage
  - Component: ProcessingMetrics

#### Gap Analysis: 2FA UI (2 giorni)
**Issue**: #4116 (Frontend portion)

- [ ] `/settings/security` page
- [ ] TwoFactorSetup wizard
- [ ] QR code display + recovery codes

#### Gap Analysis: Play Records UI (2 giorni)
**Issue**: #4115 (Frontend portion)

- [ ] AddPlayerDialog, ScoreTracker
- [ ] Session start/complete buttons
- [ ] Timer display

**Total Terminal A Week 4**: 7 giorni effort

---

### 💻 Terminal B - Backend Focus

#### Epic #3: Navbar Backend Support (1 giorno)
**Issues**: #4104, #4105 (Backend portions)

- [ ] **#4104**: Notifications Page endpoint (0.5 giorni)
  - GET /notifications/by-type
  - Grouping and filtering logic

- [ ] **#4105**: Notifications Config endpoint (0.5 giorni)
  - GET/PUT /notifications/config
  - Per-type notification settings

#### Gap Analysis: Admin Bulk Operations (4 giorni)
**Issue**: #4118

- [ ] Bulk password reset endpoint
- [ ] Bulk role change endpoint
- [ ] CSV import/export endpoints
- [ ] Background job processing for large batches
- [ ] Progress tracking API

#### Epic #1: MeepleCard Backend Finalization (1 giorno)
**Issue**: #4074 (completion)

- [ ] Permission caching optimization
- [ ] Batch permission checks endpoint

**Total Terminal B Week 4**: 6 giorni effort

---

### 🔄 CHECKPOINT 4 (End Week 4) - FINAL

**Sincronizzazione finale**:
- [ ] Terminal A: PDF Status UI + 2FA UI + Play Records UI ✅
- [ ] Terminal B: Navbar Backend + Bulk Ops + MeepleCard finalization ✅
- [ ] Merge: All feature branches → main-dev
- [ ] Test: Complete E2E test suite (all 47 issue)
- [ ] Deploy: Production-ready build

**Test Plan Checkpoint 4 (COMPREHENSIVE)**:
```bash
# ═══════════════════════════════════════════════════════════
# EPIC #1: MeepleCard - Complete Test Suite
# ═══════════════════════════════════════════════════════════

# Frontend Tests
cd apps/web
pnpm test -- MeepleCard --coverage
pnpm test:e2e -- meeple-card-accessibility.spec.ts
pnpm test:e2e -- meeple-card-mobile.spec.ts
pnpm test:e2e -- meeple-card-tooltips.spec.ts
pnpm test:e2e -- meeple-card-agent-type.spec.ts

# Backend Tests
cd apps/api/src/Api
dotnet test --filter "Feature=MeepleCard"

# Visual Regression
pnpm test:visual -- meeple-card-variants

# ═══════════════════════════════════════════════════════════
# EPIC #2: Agent System - Complete Test Suite
# ═══════════════════════════════════════════════════════════

# Frontend Tests
cd apps/web
pnpm test -- Agent --coverage
pnpm test:e2e -- agent-chat-complete-flow.spec.ts
pnpm test:e2e -- agent-history.spec.ts
pnpm test:e2e -- agent-list-catalog.spec.ts
pnpm test:e2e -- agent-strategy-builder.spec.ts

# Backend Tests
cd apps/api/src/Api
dotnet test --filter "BoundedContext=KnowledgeBase&Feature=Agents"
dotnet test --filter "Feature=MultiAgent"
dotnet test --filter "Feature=ChatPersistence"

# Integration Tests
pnpm test:e2e -- agent-multi-agent-orchestration.spec.ts
pnpm test:e2e -- agent-resume-all-methods.spec.ts

# ═══════════════════════════════════════════════════════════
# EPIC #3: Navbar - Complete Test Suite
# ═══════════════════════════════════════════════════════════

# Frontend Tests
cd apps/web
pnpm test -- Navbar --coverage
pnpm test:e2e -- navbar-mobile-menu.spec.ts
pnpm test:e2e -- navbar-dynamic-routing.spec.ts
pnpm test:e2e -- navbar-dropdowns.spec.ts

# Backend Tests
cd apps/api/src/Api
dotnet test --filter "Feature=Notifications"

# Accessibility
pnpm test:e2e -- navbar-keyboard-navigation.spec.ts

# ═══════════════════════════════════════════════════════════
# EPIC #4: PDF Status - Complete Test Suite
# ═══════════════════════════════════════════════════════════

# Frontend Tests
cd apps/web
pnpm test -- PDFStatus --coverage
pnpm test:e2e -- pdf-upload-status-tracking.spec.ts
pnpm test:e2e -- pdf-manual-retry.spec.ts
pnpm test:e2e -- pdf-realtime-updates.spec.ts

# Backend Tests
cd apps/api/src/Api
dotnet test --filter "BoundedContext=DocumentProcessing&Feature=StatusTracking"
dotnet test --filter "Feature=PDFPipeline"

# Integration Tests (SSE)
pnpm test:e2e -- pdf-sse-progress-updates.spec.ts

# ═══════════════════════════════════════════════════════════
# GAP ANALYSIS - Complete Test Suite
# ═══════════════════════════════════════════════════════════

# #4113: Notifications
pnpm test:e2e -- notifications-bell-dropdown.spec.ts
pnpm test:e2e -- notifications-realtime-sse.spec.ts
pnpm test:e2e -- notifications-mark-read.spec.ts

# #4114: Wishlist
pnpm test:e2e -- wishlist-add-remove.spec.ts
pnpm test:e2e -- wishlist-dashboard-widget.spec.ts
pnpm test:e2e -- wishlist-sorting-filtering.spec.ts

# #4115: Play Records
pnpm test:e2e -- play-records-add-players.spec.ts
pnpm test:e2e -- play-records-score-tracking.spec.ts
pnpm test:e2e -- play-records-start-complete.spec.ts

# #4116: 2FA
pnpm test:e2e -- 2fa-setup-flow.spec.ts
pnpm test:e2e -- 2fa-qr-code-scan.spec.ts
pnpm test:e2e -- 2fa-recovery-codes.spec.ts

# #4117: Achievements
pnpm test:e2e -- achievements-display.spec.ts
pnpm test:e2e -- achievements-dashboard-widget.spec.ts

# #4118: Admin Bulk Ops
pnpm test:e2e -- admin-bulk-password-reset.spec.ts
pnpm test:e2e -- admin-bulk-role-change.spec.ts
pnpm test:e2e -- admin-csv-import-export.spec.ts

# ═══════════════════════════════════════════════════════════
# PERFORMANCE TESTS
# ═══════════════════════════════════════════════════════════

cd apps/web
pnpm test:performance -- meeple-card-rendering
pnpm test:performance -- agent-chat-streaming
pnpm test:performance -- notification-updates

cd apps/api/src/Api
dotnet test --filter "Category=Performance"

# ═══════════════════════════════════════════════════════════
# VISUAL REGRESSION TESTS
# ═══════════════════════════════════════════════════════════

pnpm test:visual -- meeple-card-all-variants
pnpm test:visual -- navbar-all-states
pnpm test:visual -- agent-chat-interface
pnpm test:visual -- notifications-dropdown
pnpm test:visual -- achievements-page

# ═══════════════════════════════════════════════════════════
# ACCESSIBILITY TESTS
# ═══════════════════════════════════════════════════════════

pnpm test:a11y -- meeple-card
pnpm test:a11y -- navbar
pnpm test:a11y -- agent-chat
pnpm test:a11y -- notifications

# ═══════════════════════════════════════════════════════════
# SMOKE TESTS (Production-Like Environment)
# ═══════════════════════════════════════════════════════════

# Startup
docker compose -f docker-compose.prod.yml up -d
sleep 30

# Critical Flows
pnpm test:e2e:smoke -- user-registration-login.spec.ts
pnpm test:e2e:smoke -- game-search-add-to-library.spec.ts
pnpm test:e2e:smoke -- pdf-upload-process-chat.spec.ts
pnpm test:e2e:smoke -- agent-query-with-rag.spec.ts
pnpm test:e2e:smoke -- wishlist-add-remove.spec.ts
pnpm test:e2e:smoke -- notifications-realtime.spec.ts

# Health Checks
curl http://localhost:8080/health
curl http://localhost:3000/api/health
```

**Quality Gates**:
- [ ] All unit tests passing (>90% coverage)
- [ ] All integration tests passing (>85% coverage)
- [ ] All E2E tests passing (critical flows)
- [ ] Visual regression: 0 unexpected changes
- [ ] Accessibility: 0 WCAG violations
- [ ] Performance: All pages <1s load
- [ ] Smoke tests: All critical flows green

**Deployment Checklist**:
- [ ] Database migrations applied
- [ ] Environment variables configured
- [ ] Feature flags created (all disabled initially)
- [ ] Monitoring alerts configured
- [ ] Rollback plan documented
- [ ] Stakeholder sign-off obtained

---

## 📊 Effort Distribution Summary

| Terminal | Week 1 | Week 2 | Week 3 | Week 4 | Total |
|----------|--------|--------|--------|--------|-------|
| **Terminal A** | 8d | 6d | 6.5d | 7d | **27.5d** |
| **Terminal B** | 7d | 6d | 6d | 6d | **25d** |
| **Sync/Test** | 0.5d | 0.5d | 0.5d | 2d | **3.5d** |
| **Total** | **15.5d** | **12.5d** | **13d** | **15d** | **56d** |

**Con 2 developer paralleli**: 56 giorni effort → **4 settimane calendario** (~28 giorni)

**Savings vs Sequential**: 56 giorni → 28 giorni = **50% tempo risparmiato**

---

## 🎯 Issue Completion Tracking

### Week 1 (11 issue)
**Terminal A**: #4073, #4076, #4075, #4072, #4080, #4081 (Epic #1) + #4114 (Wishlist)
**Terminal B**: #4079, #4078, #4077, #4074 (Epic #1) + #4113 (Notifications BE) + #4115 (Play Records BE)

### Week 2 (15 issue)
**Terminal A**: #4097-#4102 (Epic #3 Navbar) + #4113 (Notifications UI)
**Terminal B**: #4106-#4111 (Epic #4 PDF) + #4117 (Achievements BE)

### Week 3 (14 issue)
**Terminal A**: #4085, #4087, #4090-#4093 (Epic #2 Agent UI) + #4117 (Achievements UI)
**Terminal B**: #4082-#4084, #4086, #4088, #4094-#4096 (Epic #2 Agent BE) + #4116 (2FA BE)

### Week 4 (7 issue)
**Terminal A**: #4108-#4110 (PDF Status UI) + #4116 (2FA UI) + #4115 (Play Records UI)
**Terminal B**: #4104-#4105 (Navbar BE) + #4118 (Admin Bulk Ops)

**Total**: 47 issue in 4 settimane

---

## 🔄 Checkpoint Sync Protocol

### Pre-Checkpoint (Day Before)
```bash
# Terminal A
git add .
git commit -m "checkpoint: Terminal A Week N progress"
git push origin terminal-a/epic-X

# Terminal B
git add .
git commit -m "checkpoint: Terminal B Week N progress"
git push origin terminal-b/epic-Y
```

### During Checkpoint (2-4 ore)
```yaml
1. Code Review (Cross-Terminal):
   - Terminal A reviews Terminal B code
   - Terminal B reviews Terminal A code
   - Feedback via PR comments

2. Integration Testing:
   - Merge both branches to integration branch
   - Run full test suite
   - Fix integration issues found

3. Merge Strategy:
   - Create PR: terminal-a/epic-X → main-dev
   - Create PR: terminal-b/epic-Y → main-dev
   - Merge after approval

4. Sync Meeting:
   - Discuss blockers and dependencies
   - Adjust Week N+1 allocation if needed
   - Update roadmap with learnings
```

### Post-Checkpoint
```bash
# Both terminals sync to latest main-dev
git checkout main-dev
git pull origin main-dev
git checkout -b terminal-[a|b]/week-[N+1]
```

---

## 🧪 Comprehensive Test Plan

### Test Coverage Targets

| Test Type | Target Coverage | Tool |
|-----------|----------------|------|
| **Unit Tests** | >90% | Vitest (FE) + xUnit (BE) |
| **Integration Tests** | >85% | Vitest + Testcontainers |
| **E2E Tests** | 100% critical flows | Playwright |
| **Visual Regression** | Key pages | Playwright screenshots |
| **Accessibility** | WCAG 2.1 AA | axe-core |
| **Performance** | <1s load time | Lighthouse |

### E2E Test Suite (47 Flows)

#### Epic #1: MeepleCard (6 flows)
```typescript
// apps/web/e2e/epic-1-meeple-card.spec.ts

test('MeepleCard - Accessibility keyboard navigation', async ({ page }) => {
  // Test tab navigation, ARIA labels, screen reader
});

test('MeepleCard - Mobile tag optimization responsive', async ({ page }) => {
  // Test mobile viewport, touch targets, tag wrapping
});

test('MeepleCard - Tooltip smart positioning', async ({ page }) => {
  // Test tooltip at all viewport edges, auto-positioning
});

test('MeepleCard - Agent type variant rendering', async ({ page }) => {
  // Test agent entity type, specific styling, icons
});

test('MeepleCard - Ownership state logic', async ({ page }) => {
  // Test owned, wishlist, not-owned states
});

test('MeepleCard - Performance with 100+ cards', async ({ page }) => {
  // Test rendering performance, virtual scrolling
});
```

#### Epic #2: Agent System (10 flows)
```typescript
// apps/web/e2e/epic-2-agent-system.spec.ts

test('Agent - Complete chat flow with streaming', async ({ page }) => {
  // User asks question → agent responds → streaming display
});

test('Agent - Chat history persistence', async ({ page }) => {
  // Create chat → refresh → history preserved
});

test('Agent - Resume chat by ID', async ({ page }) => {
  // Resume from history → context loaded → continue chat
});

test('Agent - Multi-agent orchestration', async ({ page }) => {
  // Complex query → multiple agents → combined response
});

test('Agent - Strategy builder custom strategy', async ({ page }) => {
  // Build custom strategy → save → apply to game
});

test('Agent - Agent list and catalog browse', async ({ page }) => {
  // Browse agents → filter by type → view details
});

test('Agent - Dashboard widget your agents', async ({ page }) => {
  // Dashboard shows recent agent interactions
});

test('Agent - Game page agent section', async ({ page }) => {
  // Game detail → agent section → start chat
});

test('Agent - Tier limit enforcement', async ({ page }) => {
  // Free user → exceed limit → upgrade prompt
});

test('Agent - KB integration context loading', async ({ page }) => {
  // Agent query → loads game KB → relevant answers
});
```

#### Epic #3: Navbar (9 flows)
```typescript
// apps/web/e2e/epic-3-navbar.spec.ts

test('Navbar - Mobile hamburger menu complete', async ({ page }) => {
  // Mobile viewport → hamburger → all menu items
});

test('Navbar - Dynamic route anonymous vs authenticated', async ({ page }) => {
  // Anonymous: / → Welcome, Authenticated: / → Dashboard
});

test('Navbar - Anonymous catalog restrictions', async ({ page }) => {
  // Anonymous user → limited catalog access → login prompt
});

test('Navbar - Dual CTA buttons', async ({ page }) => {
  // Login + Register buttons visible, correct routing
});

test('Navbar - Settings dropdown 8 sections', async ({ page }) => {
  // Settings dropdown → all 8 sections present → navigation
});

test('Navbar - Notifications dropdown preview', async ({ page }) => {
  // Bell icon → dropdown → recent notifications
});

test('Navbar - Notifications page 10 types', async ({ page }) => {
  // View all → grouped by type → filtering
});

test('Navbar - Notifications configuration', async ({ page }) => {
  // Settings → notification preferences → save
});

test('Navbar - Dropdown grouping structure', async ({ page }) => {
  // All dropdowns grouped logically → easy navigation
});
```

#### Epic #4: PDF Status (6 flows)
```typescript
// apps/web/e2e/epic-4-pdf-status.spec.ts

test('PDF - 7-state pipeline complete flow', async ({ page }) => {
  // Upload → Queued → Processing → Chunking → Embedding → Indexing → Complete
});

test('PDF - Manual retry after error', async ({ page }) => {
  // Upload fails → error shown → retry button → success
});

test('PDF - Multi-location status display', async ({ page }) => {
  // Status badge in: upload page, library, processing queue
});

test('PDF - Real-time updates SSE', async ({ page }) => {
  // Upload → SSE events → progress bar updates live
});

test('PDF - Duration metrics and ETA', async ({ page }) => {
  // Processing → ETA displayed → updates as progresses
});

test('PDF - Notification channel configuration', async ({ page }) => {
  // Configure: email, push, in-app notifications
});
```

#### Gap Analysis: All 6 Features (16 flows)
```typescript
// apps/web/e2e/gap-analysis.spec.ts

// #4113: Notifications System
test('Notifications - Bell icon with badge count', async ({ page }) => {
  // New notification → badge increments → bell shows count
});

test('Notifications - Dropdown list and mark read', async ({ page }) => {
  // Click bell → dropdown → click notification → mark read + navigate
});

test('Notifications - Mark all as read', async ({ page }) => {
  // Multiple unread → mark all → badge clears
});

test('Notifications - Toast for critical events', async ({ page }) => {
  // Error occurs → toast appears → dismissible
});

// #4114: Wishlist
test('Wishlist - Add game from card', async ({ page }) => {
  // Game card → heart icon → add to wishlist → optimistic UI
});

test('Wishlist - View and manage wishlist page', async ({ page }) => {
  // /library/wishlist → grid view → sorting/filtering
});

test('Wishlist - Dashboard highlights widget', async ({ page }) => {
  // Dashboard → widget shows top 3 wishlist items
});

// #4115: Play Records
test('Play Records - Add players to session', async ({ page }) => {
  // Session detail → add player → search → select → added
});

test('Play Records - Track scores inline edit', async ({ page }) => {
  // Score table → inline edit → auto-save → validated
});

test('Play Records - Start and complete session', async ({ page }) => {
  // Start → timer begins → complete → summary shown
});

// #4116: 2FA
test('2FA - Setup wizard complete flow', async ({ page }) => {
  // Settings → Enable 2FA → scan QR → verify → recovery codes
});

test('2FA - Download recovery codes', async ({ page }) => {
  // Setup complete → download codes (TXT + PDF)
});

test('2FA - Disable 2FA with password', async ({ page }) => {
  // Disable → password prompt → confirm → disabled
});

// #4117: Achievements
test('Achievements - Display all with filtering', async ({ page }) => {
  // /profile/achievements → filter earned/locked → display correct
});

test('Achievements - Dashboard widget recent', async ({ page }) => {
  // Dashboard → widget shows last 3 earned
});

// #4118: Admin Bulk Operations
test('Admin Bulk - Select and reset passwords', async ({ page }) => {
  // Select 5 users → bulk password reset → emails sent
});

test('Admin Bulk - CSV import with validation', async ({ page }) => {
  // Import CSV → validation → errors shown → valid imported
});
```

---

## 🚨 Risk Mitigation

### Cross-Terminal Dependencies

| Dependency | Risk | Mitigation |
|------------|------|------------|
| Agent Backend → Agent UI | Terminal B delay blocks Terminal A Week 3 | Terminal A can mock API responses |
| Notifications Backend → UI | Terminal B Week 1 blocks Terminal A Week 2 | Checkpoint 1 sync mandatory |
| PDF Status Backend → UI | Terminal B Week 2 blocks Terminal A Week 4 | Buffer in schedule |

### Technical Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| SSE scaling issues | Medium | High | Load testing at Checkpoint 2 |
| Mobile UX complexity | Medium | Medium | Early mobile testing |
| Test suite execution time | High | Medium | Parallel test execution |
| Merge conflicts | Medium | Medium | Frequent syncs (daily commits) |

---

## 📈 Success Metrics

### Code Quality
- [ ] Backend tests: >90% coverage
- [ ] Frontend tests: >85% coverage
- [ ] E2E tests: 100% critical flows
- [ ] 0 high-severity security issues
- [ ] 0 accessibility violations (WCAG AA)

### Performance
- [ ] Page load: <1s (95th percentile)
- [ ] API response: <200ms (95th percentile)
- [ ] SSE latency: <500ms
- [ ] Mobile performance score: >90

### User Impact
- [ ] 47 issue closed
- [ ] 4 epic completed
- [ ] UI coverage: 37% → 75%
- [ ] 6 new user-visible features

---

## 🔄 Daily Sync Protocol (Async)

**Prevent merge conflicts e maintain awareness**:

```bash
# Every morning (both terminals)
git checkout main-dev
git pull origin main-dev
git checkout terminal-[a|b]/current-branch
git rebase main-dev

# Every evening (both terminals)
git add .
git commit -m "wip: [feature] - [progress summary]"
git push origin terminal-[a|b]/current-branch
```

**Slack/Communication**:
- Daily standup (async): "Today working on: #XXXX, #YYYY"
- Blockers immediately communicated
- PR reviews: within 4 hours

---

## 📚 Documentation Updates

### Per-Epic Documentation
- [ ] Epic #1: Update `docs/design-system/cards.md`
- [ ] Epic #2: Create `docs/07-frontend/agent-system-guide.md`
- [ ] Epic #3: Update `docs/07-frontend/navbar-specification.md`
- [ ] Epic #4: Create `docs/03-api/pdf-status-tracking.md`

### Gap Analysis Documentation
- [ ] Update `docs/ui-api-gap-analysis.md` with implementation status
- [ ] Create `docs/07-frontend/notifications-system.md`
- [ ] Create `docs/07-frontend/wishlist-system.md`
- [ ] Create `docs/06-security/2fa-user-guide.md`

---

## 🎯 Definition of Done (Roadmap Complete)

- [ ] All 47 issue closed
- [ ] All E2E tests passing
- [ ] Documentation updated
- [ ] Code reviewed and merged
- [ ] Deployed to staging
- [ ] Smoke tests on production-like env passing
- [ ] Stakeholder acceptance

---

**Approved**: Pending review
**Start Date**: 2026-02-12 (suggested)
**End Date**: 2026-03-11 (4 weeks)
**Team**: 2 developers (parallel terminals)
