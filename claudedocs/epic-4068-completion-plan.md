# Epic #4068 - Completion Plan

**Generated**: 2026-02-13
**Current Status**: 45% complete (weighted)
**Remaining Effort**: 15-23 days → **9-12 days with parallelization**
**Target**: 2-2.5 weeks (aggressive) | 3 weeks (conservative)

---

## 🎯 Strategic Approach

### Principle: **Quick Wins First → Unblock Critical Path → Quality Gate**

1. **Week 1**: Complete partials + unblock critical path
2. **Week 2**: Implement blocking issues + integration
3. **Week 3**: Testing, validation, merge, close

---

## 📅 Sprint Plan (3 Sprints × 5 giorni)

### Sprint 1: Foundation & Quick Wins (Giorni 1-5)

#### Day 1: Quick Wins (6-8h total)
**🎯 Goal**: Complete 3 partial issues for momentum

**Morning (4h)**:
- [ ] **#4177 - DB Migration** (1-2h)
  - Create `AddUserTierRole` migration
  - Add tier/role columns to User table
  - Create indexes
  - Test migration up/down

- [ ] **#4182 - Tag Integration** (2-3h)
  - Add `tags`, `maxVisibleTags`, `showTagStrip` to MeepleCardProps
  - Import TagStrip in meeple-card.tsx
  - Add conditional rendering logic
  - Test grid variant integration

**Afternoon (4h)**:
- [ ] **#4184 - AgentStatusBadge** (4h)
  - Create AgentStatusBadge component (Active/Idle/Training/Error)
  - Enhance AgentStatsDisplay with capabilities tags
  - Add model info display
  - Write unit tests
  - Integrate in agent cards

**Deliverables**: 3 issues → 100% (from 89%, 0%, 38%)

---

#### Day 2-3: Gate Components & Tests (12-16h)
**🎯 Goal**: Complete #4178 to unblock #4179

**Day 2 Morning (4h)**: Gate Components
- [ ] Create `PermissionGate.tsx` (40 lines)
  ```tsx
  export function PermissionGate({ feature, children, fallback }: PermissionGateProps) {
    const { canAccess } = usePermissions();
    return canAccess(feature) ? <>{children}</> : <>{fallback}</>;
  }
  ```
- [ ] Create `TierGate.tsx` (35 lines)
- [ ] Create `RoleGate.tsx` (35 lines)
- [ ] Add `isActive()` utility to usePermissions

**Day 2 Afternoon (4h)**: Gate Tests
- [ ] permission-gate.test.tsx (100 lines)
- [ ] tier-gate.test.tsx (80 lines)
- [ ] role-gate.test.tsx (80 lines)
- [ ] Integration tests with MeepleCard

**Day 3 Morning (4h)**: Hook Tests
- [ ] usePermissions.test.tsx (150 lines)
  - Hook throws outside provider
  - Cache behavior (no redundant API calls)
  - Auto-refresh on tier/role change
  - Loading states

**Day 3 Afternoon (4h)**: Collection Limits UI (#4183)
- [ ] Create CollectionProgressBar component
- [ ] Create CollectionLimitIndicator component
- [ ] Integrate in collection dashboard
- [ ] Add warning states (>75%, >90%)
- [ ] Unit tests

**Deliverables**: #4178 → 100%, #4183 → 100%

---

#### Day 4-5: Tooltip Positioning System (#4186) (16h)
**🎯 Goal**: Foundation for A11y, unblock #4180

**Day 4 - Core Algorithm (8h)**:
- [ ] **Morning**: `lib/tooltip/positioning.ts` (4h)
  - `calculateOptimalPosition()` implementation (150 lines)
  - Viewport boundary detection
  - Auto-flip logic (top↔bottom, left↔right)
  - Space calculation in 4 directions
  - Clamp to viewport bounds

- [ ] **Afternoon**: Positioning tests (4h)
  - Edge case tests (4 corners) (150 lines)
  - Performance benchmark (<16ms test)
  - Space calculation validation
  - Auto-flip behavior verification

**Day 5 - Hook & Component (8h)**:
- [ ] **Morning**: `hooks/useSmartTooltip.ts` (4h)
  - Position state management (180 lines)
  - Debounced scroll/resize handlers (100ms)
  - IntersectionObserver integration
  - Performance optimization
  - Unit tests

- [ ] **Afternoon**: `components/ui/overlays/smart-tooltip.tsx` (4h)
  - Component wrapper (100 lines)
  - Auto/manual mode toggle
  - Integration with base Tooltip
  - Responsive behavior (desktop/tablet/mobile)
  - Integration tests

**Deliverables**: #4186 → 100% (from 25%)

---

### Sprint 2: Critical Path Integration (Giorni 6-10)

#### Day 6-7: Tooltip Accessibility (#4180) (16h)
**🎯 Goal**: WCAG 2.1 AA compliance
**Depends on**: #4186 ✅

**Day 6 - Keyboard & ARIA (8h)**:
- [ ] **Morning**: Keyboard navigation (4h)
  - Enhance tooltip.tsx with keyboard handlers
  - Tab focus management
  - Enter/Space activation
  - Escape dismissal
  - Focus trap for interactive tooltips
  - Create `useFocusTrap.ts` hook

- [ ] **Afternoon**: ARIA attributes (4h)
  - Add aria-describedby linkage
  - Add role="tooltip"
  - Add aria-live regions
  - Screen reader announcement logic
  - Test with NVDA/VoiceOver (manual)

**Day 7 - Mobile & Contrast (8h)**:
- [ ] **Morning**: Mobile touch support (4h)
  - Tap activation (no hover)
  - Tap-outside dismiss logic
  - Touch-friendly close button (44x44px)
  - Long-press alternative
  - Mobile-specific tests

- [ ] **Afternoon**: WCAG AA compliance (4h)
  - Contrast validation utilities (contrast.ts)
  - Ensure 4.5:1 normal text, 3:1 large text
  - High contrast mode support
  - Focus visible indicators
  - axe-core audit integration

**Day 7 Evening**: A11y Test Suite (4h)
- [ ] `__tests__/a11y/tooltip.a11y.test.tsx` (200 lines)
  - axe-core automated tests
  - Keyboard navigation flow tests
  - Mobile touch tests
  - Contrast ratio validation
  - Focus management edge cases

**Deliverables**: #4180 → 100%, WCAG 2.1 AA certified

---

#### Day 8-9: MeepleCard Permission Integration (#4179) (16h)
**🎯 Goal**: USER-FACING permission system
**Depends on**: #4178 ✅

**Day 8 - Core Integration (8h)**:
- [ ] **Morning**: Props & Hook (4h)
  - Add `permissions?: PermissionOverride` to MeepleCardProps
  - Import usePermissions hook
  - Add permission extraction logic
  - Create effectivePermissions abstraction

- [ ] **Afternoon**: Feature Conditional Rendering (4h)
  - Wrap `showWishlist` with canAccess('wishlist')
  - Wrap `selectable` with canAccess('bulk-select')
  - Wrap `draggable` with canAccess('drag-drop')
  - Add permission checks to all opt-in features

**Day 9 - UI Polish (8h)**:
- [ ] **Morning**: Tier Badge & Prompts (4h)
  - Create TierBadge component (50 lines)
  - Create UpgradePrompt component (80 lines)
  - Integrate tier badge in card
  - Add upgrade prompt modal/toast
  - Style glassmorphic design

- [ ] **Afternoon**: Quick Actions Filtering (4h)
  - Modify meeple-card-quick-actions.tsx
  - Add permission filtering logic
  - Filter adminOnly actions
  - Filter tier-locked actions
  - Show upgrade prompt on locked click

**Day 9 Evening**: Permission Tests (4h)
- [ ] `__tests__/meeple-card-permissions.test.tsx` (300 lines)
  - Free tier: limited features scenario
  - Normal tier: standard features
  - Pro tier: advanced features
  - Enterprise: all features
  - Admin role: admin actions visible
  - Permission override prop testing
  - Upgrade prompt trigger tests

**Deliverables**: #4179 → 100% - **PERMISSION SYSTEM LIVE** 🎉

---

#### Day 10: Tag Variants & Polish (6-8h)
**🎯 Goal**: Complete tag system for all card variants

**Morning (4h)**: TagStrip Variants
- [ ] Add horizontal layout (list variant)
- [ ] Add minimal layout (compact variant)
- [ ] Add featured/hero positioning
- [ ] Responsive behavior refinement

**Afternoon (4h)**: Integration Tests
- [ ] Test all 5 variants (grid/list/compact/featured/hero)
- [ ] Test all entity types with tags
- [ ] Test overflow behavior
- [ ] Test animations (stagger)
- [ ] Visual regression snapshots

**Deliverables**: #4182 → 100%

---

### Sprint 3: Quality Gate & Ship (Giorni 11-15)

#### Day 11-12: E2E Test Suite (12-16h)
**🎯 Goal**: Comprehensive end-to-end coverage

**Permission Flows** (Day 11 - 8h):
- [ ] Free tier user journey (2h)
  - Login as Free
  - Browse games (wishlist visible)
  - Try bulk-select (upgrade prompt shown)
  - Try drag-drop (upgrade prompt shown)

- [ ] Pro tier user journey (2h)
  - Login as Pro
  - All features accessible
  - Collection limits display (475/500)
  - Warning at 95% capacity

- [ ] Admin user journey (2h)
  - Login as Admin
  - Admin-only actions visible
  - Permission management UI
  - User tier assignment

- [ ] Cross-tier upgrade flow (2h)
  - Free → Normal upgrade
  - Feature unlocking validation
  - Limits update verification

**UI Component Flows** (Day 12 - 8h):
- [ ] Tooltip positioning edge cases (3h)
  - Viewport corners (4 scenarios)
  - Scroll behavior
  - Resize behavior
  - Multi-tooltip scenarios

- [ ] Tag system flows (2h)
  - Overflow tooltip interaction
  - Entity-specific tags load
  - Responsive tag visibility

- [ ] Collection limit warnings (2h)
  - Progress bar updates
  - Warning states trigger
  - Upgrade CTA displays

- [ ] Agent metadata display (1h)
  - Status badge changes
  - Stats update
  - Capabilities tags render

**File**: `apps/web/src/__tests__/e2e/epic-4068-flows.spec.ts` (500 lines)

---

#### Day 13: Accessibility Audit & Validation (8h)

**Automated Testing** (Morning - 4h):
- [ ] axe-core audit across all components (2h)
  - Run on MeepleCard (all variants)
  - Run on Tooltip (all states)
  - Run on Gates (all types)
  - Target: 0 violations

- [ ] Lighthouse A11y tests (1h)
  - Run on pages using MeepleCard
  - Target: ≥95 score
  - Fix any issues found

- [ ] Contrast validation (1h)
  - Automated contrast checks
  - Verify 4.5:1 / 3:1 ratios
  - High contrast mode testing

**Manual Testing** (Afternoon - 4h):
- [ ] Screen reader testing (2h)
  - NVDA on Windows
  - VoiceOver on macOS/iOS
  - Verify announcements
  - Test navigation flow

- [ ] Keyboard-only navigation (1h)
  - Tab through all interactive elements
  - Verify focus indicators
  - Test keyboard shortcuts
  - Verify no traps

- [ ] Mobile touch testing (1h)
  - Test on real devices
  - Verify tap targets (44x44px)
  - Test touch gestures
  - Verify dismiss behavior

**Deliverables**: WCAG 2.1 AA certification, A11y test report

---

#### Day 14: Visual Regression & Performance (8h)

**Visual Regression** (Morning - 4h):
- [ ] Setup Chromatic (if not configured) (1h)
- [ ] Create snapshot tests (2h)
  - All variants (grid/list/compact/featured/hero)
  - All entity types (game/player/collection/event/agent)
  - All states (default/hover/selected/dragging)
  - All responsive breakpoints (mobile/tablet/desktop)
- [ ] Review visual diffs (1h)
- [ ] Update baselines

**Performance Benchmarks** (Afternoon - 4h):
- [ ] Tooltip positioning benchmark (1h)
  - Measure calculateOptimalPosition() (<16ms target)
  - Profile under scroll/resize
  - Optimize if needed

- [ ] Card render benchmark (1h)
  - Measure initial render (<100ms target)
  - Test with all features enabled
  - Profile large grids (100+ cards)

- [ ] Permission check cache validation (1h)
  - Verify no redundant API calls
  - Check cache hit rate
  - Measure check latency (<5ms target)

- [ ] Bundle size analysis (1h)
  - Check gzipped size (<15KB target)
  - Tree-shaking verification
  - Code splitting optimization

**Deliverables**: Performance report, visual regression baseline

---

#### Day 15: Final Validation & PR (6-8h)

**Morning - Final Checks** (4h):
- [ ] Run full test suite (1h)
  - Backend: `dotnet test`
  - Frontend: `pnpm test`
  - E2E: `pnpm test:e2e`
  - A11y: axe-core + manual

- [ ] Code quality checks (1h)
  - `pnpm typecheck` - 0 errors
  - `pnpm lint` - 0 errors
  - Code review self-check
  - Remove console.logs/debuggers

- [ ] Documentation review (1h)
  - Verify all 12+ docs accurate
  - Update CHANGELOG
  - Check code examples work
  - Update component API reference

- [ ] GitHub checklist final update (1h)
  - Update ALL checkboxes across 10 issues
  - Verify all AC marked
  - Update epic DoD checklist
  - Add implementation notes

**Afternoon - PR Creation** (4h):
- [ ] Pre-PR validation (1h)
  - Squash/rebase commits if needed
  - Verify clean git history
  - Check no merge conflicts with main-dev
  - Final build test

- [ ] Create PR (1h)
  - Write comprehensive PR description
  - Reference all 10 issues (#4177-#4186)
  - Add screenshots/GIFs
  - List breaking changes (if any)
  - Tag reviewers

- [ ] PR polish (2h)
  - Address CI/CD feedback
  - Fix any failing checks
  - Respond to review comments
  - Update based on feedback

**Deliverables**: PR ready for review → https://github.com/DegrassiAaron/meepleai-monorepo/pulls

---

## 🔀 Parallel Execution Strategy

### Stream A: Tooltip Track (Days 4-7)
```
Day 4: #4186 Positioning algorithm + tests
Day 5: #4186 Hook + component
Day 6: #4180 Keyboard + ARIA
Day 7: #4180 Mobile + contrast + A11y tests
```
**Owner**: Developer A | **Duration**: 4 days | **Deliverable**: Smart accessible tooltips

---

### Stream B: Permission Track (Days 1-3, 8-9)
```
Day 1: #4177 Migration + #4184 AgentStatus
Day 2: #4178 Gates components
Day 3: #4178 Tests + #4183 Collection Limits
[WAIT for Stream A #4178 complete]
Day 8-9: #4179 MeepleCard integration
```
**Owner**: Developer B | **Duration**: 5 days (non-consecutive) | **Deliverable**: Permission system UX

---

### Stream C: Tag Track (Days 1, 10)
```
Day 1: #4182 Basic integration (4h)
[WAIT for integration testing]
Day 10: #4182 All variants + polish (8h)
```
**Owner**: Developer A or B (flexible) | **Duration**: 1.5 days | **Deliverable**: Tag system complete

---

### Stream D: Quality Track (Days 11-14)
```
Day 11-12: E2E tests (all flows)
Day 13: A11y audit + manual testing
Day 14: Visual regression + performance
```
**Owner**: QA + Developer A | **Duration**: 4 days | **Deliverable**: Quality certification

---

## 📋 Daily Standups Checklist

### Day 1 EOD
- ✅ #4177 migration created and tested
- ✅ #4182 tag integration (grid variant working)
- ✅ #4184 AgentStatusBadge component created
- ⏳ #4186 started (positioning algorithm)

### Day 3 EOD
- ✅ #4178 gate components + tests complete (100%)
- ✅ #4183 collection limits UI working
- ⏳ #4186 positioning system (50% done)

### Day 5 EOD
- ✅ #4186 tooltip positioning complete (100%)
- ⏳ #4180 A11y started (keyboard nav)

### Day 7 EOD
- ✅ #4180 tooltip A11y complete (100%)
- ⏳ #4179 MeepleCard integration started

### Day 9 EOD
- ✅ #4179 MeepleCard permission integration complete (100%)
- ✅ #4182 tag integration all variants complete (100%)
- 🎯 **ALL IMPLEMENTATION DONE** - Enter quality gate

### Day 14 EOD
- ✅ All tests passing (unit/integration/E2E)
- ✅ WCAG 2.1 AA certified (0 violations)
- ✅ Visual regression baseline approved
- ✅ Performance benchmarks met
- 🚀 Ready for PR

---

## 🧪 Testing Schedule

### Unit Tests (Continuous - Days 1-10)
**As you implement, write tests immediately**
- Day 1: Migration tests, AgentStatus tests, Tag integration tests
- Day 2-3: Gate tests, usePermissions tests, Collection Limit tests
- Day 4-5: Positioning tests, useSmartTooltip tests
- Day 6-7: A11y utility tests, keyboard handler tests
- Day 8-9: MeepleCard permission tests, quick action filter tests
- Day 10: Tag variant tests

**Target**: 90%+ coverage

---

### Integration Tests (Days 3, 5, 9)
**Test component interactions**
- Day 3: Permission Context + Gates integration
- Day 5: Tooltip + Positioning integration
- Day 9: MeepleCard + Permission + Tags full integration

**Target**: All integration points covered

---

### E2E Tests (Days 11-12)
**User journey validation**
- Permission flows (Free/Normal/Pro/Enterprise)
- Tooltip positioning (viewport edges)
- Tag overflow (click +N → shows hidden)
- Collection limits (progress bars)

**Target**: 20+ E2E scenarios

---

### A11y Tests (Day 13)
**Compliance validation**
- axe-core: 0 violations
- Lighthouse: ≥95 score
- Keyboard-only: full navigation
- Screen reader: proper announcements
- Mobile touch: 44x44px targets

**Target**: WCAG 2.1 AA certified

---

### Visual Regression (Day 14)
**UI consistency**
- Chromatic snapshots (50+ scenarios)
- Responsive breakpoints
- All variants × entities
- Dark/light themes

**Target**: Approved baseline

---

## 🎯 Success Criteria

### Code Quality
- [x] All files follow project conventions
- [ ] TypeScript: 0 errors
- [ ] ESLint: 0 errors
- [ ] Prettier: formatted
- [ ] No console.log/debugger

### Test Coverage
- [ ] Backend: ≥90% coverage
- [ ] Frontend: ≥85% coverage
- [ ] E2E: 20+ scenarios
- [ ] A11y: 0 violations

### Performance
- [ ] Tooltip positioning: <16ms
- [ ] Card render: <100ms
- [ ] Permission check: <5ms (cached)
- [ ] Bundle: <15KB gzipped

### Documentation
- [x] 12+ comprehensive docs (✅ DONE)
- [ ] API reference updated
- [ ] Component examples working
- [ ] Migration guide complete

### Process
- [ ] All 10 issues closed
- [ ] PR approved & merged
- [ ] Epic #4068 closed
- [ ] Changelog updated

---

## 🚀 Deployment Plan

### Pre-Merge Checklist
- [ ] All tests green (unit/integration/E2E)
- [ ] Code review approved (2+ reviewers)
- [ ] No merge conflicts with main-dev
- [ ] Documentation reviewed
- [ ] Breaking changes documented
- [ ] Migration script tested

### Merge Strategy
```bash
# 1. Update from parent
git checkout feature/issue-4177-permission-model
git fetch origin main-dev
git rebase origin/main-dev

# 2. Final validation
pnpm typecheck && pnpm lint && pnpm test
cd apps/api/src/Api && dotnet test

# 3. Create PR
gh pr create \
  --base main-dev \
  --title "feat(epic-4068): Complete MeepleCard Enhancements - Permission System + Smart Tooltips + Tags" \
  --body "$(cat PR_TEMPLATE.md)"

# 4. After approval
git checkout main-dev
git merge --no-ff feature/issue-4177-permission-model
git push origin main-dev

# 5. Cleanup
git branch -D feature/issue-4177-permission-model
git push origin --delete feature/issue-4177-permission-model

# 6. Close issues (automated via PR description)
# Closes #4177, #4178, #4179, #4180, #4181, #4182, #4183, #4184, #4185, #4186
# Closes #4068
```

### Post-Merge
- [ ] Verify deployment successful
- [ ] Monitor error rates (first 24h)
- [ ] Smoke test production
- [ ] Close epic #4068
- [ ] Team announcement
- [ ] Update project docs

---

## 📊 Resource Allocation

### If 1 Developer (Conservative - 3 weeks)
- Week 1: Days 1-5 (Foundation)
- Week 2: Days 6-10 (Integration)
- Week 3: Days 11-15 (Quality)

### If 2 Developers (Aggressive - 2 weeks)
**Dev A**: Tooltip Track (Days 4-7) + Quality (Days 11-14)
**Dev B**: Permission Track (Days 1-3, 8-9) + Tag Track (Days 1, 10)
**Shared**: E2E tests (Days 11-12), PR review (Day 15)

Timeline: 10 working days

---

## 🎯 Risk Mitigation

### Technical Risks

**Risk**: Tooltip positioning performance <16ms
- **Mitigation**: Use memoization, IntersectionObserver, RAF
- **Fallback**: Disable auto-positioning, use fixed placement
- **Detection**: Day 5 benchmark tests

**Risk**: Permission checks slow down card rendering
- **Mitigation**: React Query caching (5min staleTime)
- **Fallback**: Move checks to parent component
- **Detection**: Day 9 performance tests

**Risk**: WCAG AA compliance issues found late
- **Mitigation**: Run axe-core continuously from Day 6
- **Fallback**: Simplify tooltip interactions
- **Detection**: Day 7 automated tests

### Process Risks

**Risk**: Scope creep during implementation
- **Mitigation**: Strict AC adherence, no feature additions
- **Detection**: Daily standups, track LOC vs estimate

**Risk**: Test writing takes longer than expected
- **Mitigation**: Use test templates from docs
- **Fallback**: Reduce E2E scenarios, focus on critical paths
- **Detection**: Day 11 progress check

**Risk**: PR review delays
- **Mitigation**: Request reviewers early (Day 14), parallel review cycles
- **Fallback**: Self-review + automated checks
- **Detection**: Day 15 PR status

---

## 🔧 Development Environment Setup

### Prerequisites
```bash
# 1. Ensure on correct branch
git checkout feature/issue-4177-permission-model
git status # Should show WIP branch

# 2. Install dependencies (if needed)
cd apps/web && pnpm install
cd apps/api/src/Api && dotnet restore

# 3. Start services
cd infra && docker compose up -d postgres qdrant redis

# 4. Run migrations (after #4177 migration created)
cd apps/api/src/Api
dotnet ef database update

# 5. Start dev servers
# Terminal 1: dotnet run (API :8080)
# Terminal 2: pnpm dev (Web :3000)
```

### Testing Setup
```bash
# Install Playwright (if needed)
cd apps/web && pnpm exec playwright install

# Install axe-core
pnpm add -D @axe-core/react jest-axe

# Setup Chromatic (visual regression)
pnpm add -D chromatic
```

---

## 📚 Reference Documentation

### Implementation Guides (Already Created ✅)
- `claudedocs/epic-4068-implementation-guide.md` (1,113 lines)
- `docs/03-api/permission-api-reference.md` (757 lines)
- `docs/frontend/epic-4068-accessibility-checklist.md` (288 lines)
- `docs/frontend/epic-4068-e2e-test-scenarios.md` (889 lines)

### Best Practices (Already Created ✅)
- `docs/10-best-practices/epic-4068-best-practices.md` (1,552 lines)
- `docs/11-advanced/epic-4068-advanced-patterns.md` (1,412 lines)
- `docs/06-performance/epic-4068-optimization-guide.md` (963 lines)

### Troubleshooting (Already Created ✅)
- `docs/08-troubleshooting/epic-4068-common-issues.md` (933 lines)

---

## ✅ Completion Checklist

### Week 1: Foundation
- [ ] Day 1: Quick wins (#4177, #4182, #4184)
- [ ] Day 2-3: Gates & tests (#4178, #4183)
- [ ] Day 4-5: Tooltip positioning (#4186)

### Week 2: Integration
- [ ] Day 6-7: Tooltip A11y (#4180)
- [ ] Day 8-9: MeepleCard permissions (#4179)
- [ ] Day 10: Tag variants polish (#4182)

### Week 3: Quality
- [ ] Day 11-12: E2E test suite (#4185)
- [ ] Day 13: A11y audit (#4185)
- [ ] Day 14: Visual regression & performance (#4185)
- [ ] Day 15: PR & merge

### Epic Closure
- [ ] All 10 issues closed
- [ ] Epic #4068 closed
- [ ] Merged to main-dev
- [ ] Deployed to staging
- [ ] Team notified

---

## 🎉 Expected Outcomes

### User-Facing Features (After Merge)
1. **Tier-Based Access Control**
   - Free users: limited features + upgrade prompts
   - Pro users: advanced features unlocked
   - Enterprise: unlimited resources

2. **Smart Tooltips**
   - Auto-positioning (never cut off)
   - Full keyboard accessibility
   - Mobile-friendly touch interactions

3. **Visual Tag System**
   - Entity-specific tags (game/agent/document)
   - Overflow handling (+N badge)
   - Responsive design (desktop/tablet/mobile)

4. **Collection Management**
   - Progress bars (games, storage quota)
   - Warning states (75%, 90%)
   - Upgrade CTAs

5. **Agent Enhancements**
   - Status badges (Active/Idle/Training/Error)
   - Invocation stats (342, 1.2K, 3.4M)
   - Capabilities tags (RAG/Vision/Code)

### Developer Experience
- Comprehensive 600K+ token documentation
- Reusable gate components (PermissionGate, TierGate, RoleGate)
- Type-safe permission system
- Easy testing with permission overrides

### System Quality
- WCAG 2.1 AA compliant
- 90%+ test coverage
- <100ms render performance
- Production-ready code quality

---

**Total Timeline**: 15 days (3 weeks) sequential → **9-12 days (2-2.5 weeks) parallelized**

**Next Action**: Start Day 1 quick wins? (6-8h to complete 3 issues)
