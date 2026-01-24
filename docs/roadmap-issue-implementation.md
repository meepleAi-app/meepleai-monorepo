# MeepleAI Implementation Roadmap - 68 Open Issues

**Generated**: 2026-01-24
**Priority**: Site-Wide Dual-Theme Design System (#2965)
**Total Issues**: 68 open (13 closed since last update)
**Strategy**: Parallel execution on `main-dev` (backend/infra/admin) + `frontend-dev` (public/user features)
**Duration**: ~55 working days with parallelization

---

## 📊 Issue Categorization

### Epic Issues (3 major initiatives)
- **#2965**: Site-Wide Dual-Theme Design System ⭐ **PRIORITY #1**
- **#2823**: Game Library Detail Page V1
- **#2759**: Frontend Test Coverage 69% → 85%

### Foundation (Prerequisites - 7 issues)
- **#2924**: Storybook Setup and Foundation
- **#2930, #2925**: Extract Reusable Components from Admin Dashboard (duplicates)
- **#2931, #2926**: Design System Documentation (duplicates)
- **#2803**: Consolidate ActivityTimeline and ActivityFeed components

### Backend Features - main-dev (19 issues)
**Editor Dashboard** (1): #2893
**User Management** (1): #2886
**Game Detail Page** (8): #2824-#2831
**Infrastructure** (4): #2809, #2807, #2703, #2737
**Testing** (4): #2841, #2928, #2927, #2843
**Quality** (1): #2852

### Frontend Features - frontend-dev (34 issues)
**Editor Dashboard** (4): #2894, #2895, #2896, #2897
**User Management** (4): #2887, #2888, #2890, #2891
**Profile/Settings** (3): #2881, #2882, #2883
**Shared Catalog** (5): #2873, #2874, #2875, #2876, #2877
**Personal Library** (5): #2866, #2867, #2868, #2869, #2870
**User Dashboard** (6): #2857, #2858, #2859, #2860, #2861, #2862
**Game Detail Page** (9): #2832-#2840
**Quality/Testing** (3): #2842, #2852, #2929
**Misc** (3): #2745, #2746, #2838

---

## 🎯 Implementation Phases (with Synchronization Points)

### **PHASE 0: Foundation Setup** (3-4 days, SEQUENTIAL)

**Goal**: Prepare infrastructure for #2965 Design System
**Branch**: Create from both `main-dev` and `frontend-dev`

**Issues** (7):
1. ✅ **#2924**: Storybook Setup and Foundation
   - Install Storybook 8
   - Configure for Next.js 14 App Router
   - Setup dark mode addon
   - Deliverable: Storybook running with 0 stories

2. ✅ **#2930**: Extract Reusable Components from Admin Dashboard
   - Audit existing components
   - Extract 20+ primitives to `/components/ui`
   - Create initial Storybook stories
   - Deliverable: Component library foundation

3. ✅ **#2931**: Design System Documentation - Typography, Colors, Spacing
   - Document design tokens
   - Create Figma/design asset references
   - CSS variable naming conventions
   - Deliverable: Design system documentation

4. ✅ **#2803**: Consolidate ActivityTimeline and ActivityFeed components
   - Merge duplicate implementations
   - Update all references
   - Add Storybook stories
   - Deliverable: Single unified component

**Deliverables**:
- [ ] Storybook operational with component library
- [ ] Design system documentation complete
- [ ] No duplicate components
- [ ] Ready for #2965 Wave 1

---

### **PHASE 1: Site-Wide Dual-Theme Design System** (18 days, PARALLEL)

**Priority**: ⭐ **EPIC #2965** - Site-Wide Dual-Theme Design System
**Branches**:
- `main-dev` → `feature/issue-2965-site-wide-redesign`
- `frontend-dev` → `feature/issue-2965-site-wide-redesign`

#### **Wave 1-3: Foundation & Global Layouts** (5-7 days, BOTH BRANCHES)

**Issues**: #2965 Waves 1-3

**Wave 1: Foundation** (1-2 days)
- Install `next-themes` dependency
- Create `theme-variables.css` with design tokens
- Create `ThemeProvider.tsx` + `ThemeToggle.tsx`
- Update root `layout.tsx`
- Configure `tailwind.config.js`

**Wave 2: UI Primitives** (2-3 days)
- Update 20 foundation components (Button, Input, Card, etc.)
- Apply Morphllm patterns for theme tokens
- Create Storybook stories (light + dark variants)
- Visual regression tests

**Wave 3: Global Layouts** (1-2 days)
- TopNav, BottomNav, AdminSidebar
- Layout wrappers (PublicLayout, AuthLayout, etc.)
- Theme toggle in TopNav user dropdown

**Deliverables**:
- [ ] Theme toggle works globally
- [ ] All primitives support dual-theme
- [ ] Navigation themed correctly
- [ ] No visual regressions

**🔄 SYNC POINT 1** (after Wave 3 complete)
```bash
# Merge foundation to integration branch
git checkout -b integration/phase1-design-system
git merge feature/issue-2965-site-wide-redesign (from main-dev)
git merge feature/issue-2965-site-wide-redesign (from frontend-dev)
# Test: Theme switching works across all foundation components
```

---

#### **Wave 4-7: Parallel Feature Implementation** (10-12 days, PARALLEL)

##### **main-dev Branch**: Admin Components (Wave 4)

**Issues**: #2965 Wave 4 + Backend Features

**Wave 4: Admin Components** (3-4 days)
- 29 admin pages themed
- Dashboard: MetricsGrid, KPICard, StatCard, ActivityFeed
- Management: SystemStatus, ServiceHealthMatrix
- Charts: ChartsSection, APIRequestsChart
- Tables: Users, Games, Alerts

**Parallel Backend Features** (starting after Wave 3):

**Editor Dashboard** (1 issue, 1 day):
- #2893: BulkApproveGamesCommand and BulkRejectGamesCommand

**User Management** (1 issue, 0.5 days):
- #2886: SuspendUserCommand and UnsuspendUserCommand

**Deliverables**:
- [ ] All admin pages themed
- [ ] 2 backend commands complete
- [ ] Admin tests passing
- [ ] API endpoints registered

---

##### **frontend-dev Branch**: Public Pages + Chat (Wave 5-6)

**Issues**: #2965 Waves 5-6 + Frontend Features

**Wave 5: Public Pages** (3-4 days)
- 25+ public pages themed
- Landing: Hero, Features, CTA sections
- Auth: Login, Register, Reset Password
- Dashboard: User dashboard, QuickActions
- Games: Catalog, Detail, Search
- Library: Collections, Filters
- Static: FAQ, About, Contact

**Wave 6: Chat Interface** (2-3 days)
- 15 chat components themed
- ChatContent, MessageList, Message, MessageInput
- GameSelector, FollowUpQuestions
- ShareChatModal, ContextChip
- Message bubbles high contrast
- Code syntax highlighting themed

**Parallel Frontend Features** (starting after Wave 3):

**Editor Dashboard** (4 issues, 3 days):
- #2894: Editor Dashboard Page with Stats and Queue
- #2895: Approval Queue Items with Priority Indicators
- #2896: Bulk Approval UI with Floating Action Bar
- #2897: E2E Tests - Review, Approve, Reject, Bulk

**User Management** (4 issues, 3 days):
- #2887: User Management Table with TanStack Table
- #2888: Bulk Selection with Floating Action Bar
- #2890: User Detail Modal/Page
- #2891: E2E Tests - Search, Filter, Role Change, Bulk

**Profile/Settings** (3 issues, 2.5 days):
- #2881: Settings Page with 4 Tabs
- #2882: Avatar Upload with Preview and Crop
- #2883: E2E Tests - Profile Update, Password, 2FA

**Shared Catalog** (5 issues, 4 days):
- #2873: Advanced Filter Panel Component
- #2874: Catalog Game Cards with Community Stats
- #2875: Add to Library Overlay with Optimistic UI
- #2876: Pagination Component
- #2877: E2E Tests - Browse and Add to Library

**Personal Library** (5 issues, 4 days):
- #2866: Library Page with Search and Filters
- #2867: Game Cards (Grid + List Views)
- #2868: Bulk Selection Mode with Floating Action Bar
- #2869: Quota Sticky Header Component
- #2870: E2E Tests - Search, Filter, Bulk Operations

**User Dashboard** (6 issues, 4 days):
- #2857: LibraryQuotaWidget Component
- #2858: ActiveSessionsPanel Component
- #2859: Dashboard API Integration with TanStack Query
- #2860: Responsive Navigation (Top Nav + Bottom Nav)
- #2861: Component Tests - All Widgets
- #2862: E2E Tests - User Journey

**Deliverables**:
- [ ] Public pages themed
- [ ] Chat interface themed
- [ ] 27 frontend components complete
- [ ] E2E tests passing
- [ ] Public tests passing

---

#### **Wave 7: Feature Components** (4-5 days, PARALLEL)

**Remaining specialized components across both branches**

**main-dev** (25 components):
- Share requests, Wizard, Bulk export, Alert rules
- Misc admin features

**frontend-dev** (30 components):
- Library (10), Sessions (12), PDF (8), Misc

**Morphllm**: Bulk pattern updates for ~55 similar components

**Deliverables**:
- [ ] 55+ components themed
- [ ] All modals updated
- [ ] Feature tests passing

---

**🔄 SYNC POINT 2** (after Wave 7 complete)
```bash
# Create integration branch for polish and testing
git checkout -b integration/phase1-complete
git merge feature/issue-2965-site-wide-redesign (from main-dev)
git merge feature/issue-2965-site-wide-redesign (from frontend-dev)
# Resolve conflicts
# Test: Full site-wide theme switching works
```

---

#### **Wave 8-9: Polish & Testing** (3-5 days, INTEGRATION BRANCH)

**Branch**: `integration/phase1-complete`

**Wave 8: Polish & Effects** (1-2 days)
- Glass effects (desktop): backdrop-filter, floating orbs, gradient text
- Dark enhancements: glows, text shadows, gradients
- Mobile optimizations: disable blur, static backgrounds
- Framer Motion: staggered reveals, page transitions

**Wave 9: Testing** (2-3 days)
- Unit: Update 100+ test files, maintain 85% coverage
- Visual: Chromatic regression (light + dark variants)
- E2E: Playwright theme switching test
- Accessibility: axe-core WCAG AA validation
- Performance: Lighthouse CI budgets

**Deliverables**:
- [ ] Effects performant
- [ ] Mobile optimized
- [ ] All tests passing
- [ ] Chromatic approved
- [ ] Accessibility compliant
- [ ] Lighthouse >90

**🎉 PHASE 1 COMPLETE**: Site-Wide Dual-Theme Design System fully deployed

---

### **PHASE 2: Game Library Detail Page Epic** (15 days, PARALLEL)

**Epic**: #2823 - Game Library Detail Page V1
**Dependencies**: #2965 complete (uses themed components)

#### **Backend Implementation - main-dev** (8 days)

**Domain Layer** (2 days):
- #2824: Create UserGame Domain Entities & Value Objects
  - UserGame entity with states (Owned, Borrowed, Loaned)
  - GameChecklistItem, GameSession value objects
  - Repository interfaces

**Infrastructure Layer** (2 days):
- #2825: Implement UserGame Repository & Infrastructure
  - EF Core configuration
  - PostgreSQL schema migration
  - Repository implementation

**Application Layer - Queries** (2 days):
- #2826: Implement GetGameDetailQuery with Handler
  - Game detail with user context
  - Ownership status, play history
  - Optimized with EF Core includes

- #2827: Implement GetGameChecklistQuery
  - Setup checklist items
  - Adaptive based on game state
  - Performance optimized

**Application Layer - Commands** (2 days):
- #2828: Implement UpdateGameStateCommand
  - State transitions (Owned → Loaned, etc.)
  - Validation rules
  - Audit trail

- #2829: Implement RecordGameSessionCommand (Adaptive)
  - Record play sessions
  - Adaptive wizard vs quick record
  - Statistics update

- #2830: Implement SendLoanReminderCommand
  - Loan reminder automation
  - Email/notification integration

**Endpoints** (1 day):
- #2831: Register API Endpoints for Game Detail
  - CQRS endpoint registration
  - Swagger documentation
  - Validation pipeline

**Deliverables**:
- [ ] UserGame domain complete
- [ ] 3 queries + 3 commands implemented
- [ ] API endpoints registered
- [ ] Backend tests passing

---

#### **Frontend Implementation - frontend-dev** (10 days)

**State Management** (1 day):
- #2832: Create Zustand Store for Game Detail State
  - Game detail state
  - Checklist state
  - Session recording state
  - Optimistic updates

**Core Components** (3 days):
- #2833: Implement Core Game Detail Components
  - GameHeader with ownership badge
  - GameStats panel
  - PlayHistoryTimeline
  - QuickActions panel

**Interactive Features** (2 days):
- #2834: Adaptive Registration Modal
  - Wizard mode (full setup)
  - Quick mode (ownership only)
  - Form validation

- #2835: Setup Checklist Drawer with Wizard Toggle
  - Checklist component
  - Wizard toggle
  - Progress tracking

- #2836: Sticky Bottom Action Bar
  - Context-aware actions
  - Sticky positioning
  - Mobile optimization

**Integration** (2 days):
- #2837: Game Detail Page Integration
  - Full page assembly
  - API integration
  - Loading states
  - Error handling

**Polish & Testing** (2 days):
- #2838: Responsive Design & Visual Polish
  - Mobile optimization
  - Tablet breakpoints
  - Desktop enhancements
  - Theme integration

- #2839: Context-Aware AI Chat Initialization
  - Chat integration
  - Game context passing
  - Quick questions

- #2840: Quick Regolamento View Integration
  - PDF viewer integration
  - Quick rules access
  - Mobile-friendly

**Deliverables**:
- [ ] Zustand store complete
- [ ] 9 components implemented
- [ ] Full page integration
- [ ] Responsive & polished
- [ ] AI chat integrated

**🔄 SYNC POINT 3**: Game Detail Page Epic Complete
```bash
git checkout integration/phase2-game-detail
git merge feature/issue-2823-game-detail (from main-dev)
git merge feature/issue-2823-game-detail (from frontend-dev)
# E2E test: Full game detail workflow
```

---

### **PHASE 3: Infrastructure & Quality** (12 days, PARALLEL)

#### **Infrastructure - main-dev** (6 days)

**Backend Infrastructure** (4 issues, 4 days):
- #2703: S3-compatible object storage for PDF uploads and backups
  - MinIO integration
  - Upload/download service
  - Backup automation

- #2809: SystemConfiguration Rate Limit Entities
  - Rate limiting configuration
  - Redis-backed rate limiter
  - Admin configuration UI

- #2807: Badge Management Commands & Background Jobs
  - Achievement/badge system
  - Background job processing
  - Gamification logic

- #2737: API - Admin Review Lock Endpoints
  - Optimistic locking for reviews
  - Concurrent review prevention
  - Lock timeout mechanism

**Performance Testing** (2 issues, 2 days):
- #2928: Load Testing Setup with k6 for All APIs
  - k6 test scripts
  - CI integration
  - Performance baselines

- #2927: Performance Testing Infrastructure with Lighthouse CI
  - Lighthouse CI setup
  - Performance budgets
  - Automated reporting

**Deliverables**:
- [ ] Infrastructure services deployed
- [ ] Performance testing operational
- [ ] Baselines established

---

#### **Quality & Testing - frontend-dev** (8 days)

**Component Testing** (1 issue, 3 days):
- #2842: Frontend Component & Integration Tests
  - Component tests with Vitest
  - Integration tests
  - Coverage target 85%

**Visual Regression** (1 issue, 2 days):
- #2852: Visual Regression Testing Setup - All Pages (Chromatic)
  - Chromatic integration
  - Snapshot baselines (light + dark)
  - CI pipeline

**E2E Testing** (1 issue, 3 days):
- #2843: E2E User Journey Tests (Playwright)
  - Critical user journeys
  - Authentication flows
  - Feature workflows

**Accessibility** (1 issue, 2 days):
- #2929: Accessibility Audit & WCAG 2.1 AA Compliance for All Pages
  - axe-core integration
  - Manual audit
  - Remediation
  - WCAG AA compliance

**Deliverables**:
- [ ] 85% frontend coverage
- [ ] Visual regression operational
- [ ] E2E suite complete
- [ ] WCAG AA compliant

---

#### **Epic: Frontend Test Coverage** (ongoing)

**Epic**: #2759 - Frontend Test Coverage 69% → 85% (Q1 2026)

**Strategy**: Incremental coverage improvement throughout all phases
- Phase 1: +5% (74%)
- Phase 2: +6% (80%)
- Phase 3: +5% (85%)

**Tracking**: Weekly coverage reports, blockers identified

---

### **PHASE 4: Remaining Features** (15 days, PARALLEL)

**Admin Features - main-dev** (3 days):
- #2745: Frontend - Admin Review Interface
- #2746: Frontend - Contributor Display su SharedGame

**UI Polish - frontend-dev** (2 days):
- Finalize all themed components
- Address user feedback
- Performance optimizations

**Deliverables**:
- [ ] All 81 issues complete
- [ ] No critical bugs
- [ ] Performance targets met

---

## 📅 Timeline Summary

| Phase | Duration | Start | End | Issues |
|-------|----------|-------|-----|--------|
| **Phase 0**: Foundation | 4 days | Day 1 | Day 4 | 7 |
| **Phase 1**: Design System | 16 days | Day 5 | Day 20 | 1 epic + 36 features |
| **Phase 2**: Game Detail Epic | 15 days | Day 21 | Day 35 | 1 epic (17 issues) |
| **Phase 3**: Infrastructure & Quality | 12 days | Day 36 | Day 47 | 10 |
| **Phase 4**: Remaining Features | 8 days | Day 48 | Day 55 | 4 |
| **TOTAL** | **55 days** | 2026-01-24 | **2026-03-31** | **68 issues** |

---

## 🔄 Synchronization Points

**SYNC 1** (Day 9): After Wave 3 - Foundation Complete
- Test: Theme toggle works globally
- Test: All primitives dual-themed
- Decision: Proceed to parallel feature development

**SYNC 2** (Day 20): After Wave 7 - Features Complete
- Test: Full site-wide theming
- Test: No visual regressions
- Decision: Proceed to polish & testing

**SYNC 3** (Day 35): After Game Detail Epic
- Test: Full game detail workflow
- Test: Backend + frontend integration
- Decision: Proceed to infrastructure

**SYNC 4** (Day 47): Before Final Features
- Test: All infrastructure deployed
- Test: Quality metrics met
- Decision: Final feature implementation

**SYNC 5** (Day 55): Production Ready
- Test: All 68 issues complete
- Test: E2E suite 100% passing
- Decision: Deploy to production

---

## 🎯 Success Criteria

### Functional
- [ ] All 68 issues resolved
- [ ] Theme toggle works on 75+ pages
- [ ] All features integrated and tested
- [ ] No critical bugs

### Visual
- [ ] Dual-theme consistent site-wide
- [ ] Glass effects performant (desktop)
- [ ] Dark mode polished
- [ ] Mobile optimized

### Performance
- [ ] FCP <1s desktop, <1.5s mobile
- [ ] Lighthouse score >90
- [ ] CLS <0.1
- [ ] TTI <2s

### Quality
- [ ] 85% frontend test coverage
- [ ] 90% backend test coverage
- [ ] 0 axe-core violations
- [ ] WCAG AA compliant
- [ ] E2E suite 100% passing
- [ ] Visual regression approved

---

## 📝 Branch Strategy

```bash
# Main branches
main              # Production
main-dev          # Development backend/infra/admin
frontend-dev      # Development public/user features

# Phase branches
feature/issue-2965-site-wide-redesign  # Phase 1 (both main-dev + frontend-dev)
feature/issue-2823-game-detail         # Phase 2 (both main-dev + frontend-dev)
integration/phase1-design-system       # Integration testing
integration/phase2-game-detail         # Integration testing

# Feature branches (as needed)
feature/issue-XXXX-description
```

**Merge Flow**:
1. Feature branches → main-dev or frontend-dev
2. At sync points → integration branch
3. Integration testing complete → merge to main-dev + frontend-dev
4. Final QA → merge to main (production)

---

## 🚨 Risk Mitigation

### Scope Creep (250+ components)
- **Mitigation**: Wave-based boundaries, Morphllm automation, feature flags

### Merge Conflicts (parallel branches)
- **Mitigation**: Clear ownership, daily sync, integration branches

### Performance (backdrop-filter)
- **Mitigation**: Mobile detection, simplified effects, Lighthouse budgets

### Visual Regressions
- **Mitigation**: Chromatic on 100+ stories, Playwright screenshots, rollback plan

### Test Coverage
- **Mitigation**: Incremental coverage improvements, blockers tracking

---

## 📊 Progress Tracking

**GitHub Project Board**: https://github.com/DegrassiAaron/meepleai-monorepo/projects/X

**Columns**:
- 📋 Backlog (68 issues)
- 🏗️ Foundation (Phase 0)
- 🎨 Design System (Phase 1)
- 📖 Game Detail (Phase 2)
- 🔧 Infrastructure (Phase 3)
- ✨ Final Features (Phase 4)
- ✅ Done (13 completed)

**Weekly Checkpoints**:
- Monday: Review progress, adjust priorities
- Wednesday: Sync point check-in
- Friday: Coverage report, blocker resolution

---

## 📚 Documentation

**PDCA Tracking**:
- `docs/pdca/site-wide-redesign/` - Phase 1
- `docs/pdca/game-detail-epic/` - Phase 2
- `docs/pdca/infrastructure/` - Phase 3

**Technical Docs**:
- Design system: `docs/design-system/`
- Component library: Storybook
- API reference: Scalar UI

**Memories** (Serena MCP):
- `session/roadmap-implementation` - Overall progress
- `plan/phase-1/design-system` - Phase 1 plan
- `plan/phase-2/game-detail` - Phase 2 plan
- `execution/phase-X/progress` - Daily progress logs

---

**Created by**: PM Agent
**Last Updated**: 2026-01-24
**Next Review**: Weekly checkpoints
**Completed Issues**: 13 (2892, 2884-2885, 2878-2880, 2871-2872, 2863-2865, 2855-2856)
