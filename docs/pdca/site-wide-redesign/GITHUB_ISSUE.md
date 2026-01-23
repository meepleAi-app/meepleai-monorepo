# GitHub Issue - Copy this to create the issue manually

**Title**: `[Epic] Site-Wide Dual-Theme Design System (Glass Light + Dark Professional)`

**Labels**: `area/ui`, `frontend`, `kind/enhancement`, `priority: high`, `complexity: xl`, `epic`

---

## Description

Complete site-wide migration to dual-theme design system with **glass morphism** (light mode) and **dark professional** (dark mode) aesthetics. This migration affects **250+ components** and **75+ pages** across the entire MeepleAI frontend.

**Design Assets**:
- Light mode: `docs/design-proposals/meepleai-style/admin-dashboard-glass.html`
- Dark mode: `docs/design-proposals/meepleai-style/admin-dashboard-dark.html`
- Original: `docs/design-proposals/meepleai-style/admin-dashboard-v2.html`

**Strategy**: Big bang migration with Wave-based implementation, parallel execution on `main-dev` (admin) + `frontend-dev` (public) branches.

---

## Scope Overview

**Component Inventory**: 250+ visual components requiring theme updates
**Page Coverage**: 75+ pages across 5 route groups (auth, public, chat, admin, misc)
**Implementation Waves**: 9 waves (Foundation → Testing)
**Estimated Duration**: 12-14 working days with parallelization
**Automation**: Morphllm for 95% time savings on pattern replacements

---

## Design System Specifications

### Light Mode: Glass Morphism

**Visual**: Translucent cards, backdrop blur, floating orbs, warm gradients

**Palette**:
```css
--bg-primary: linear-gradient(135deg, #fef3e2, #f8f6f0, #fce8d8)
--card-bg: rgba(255, 255, 255, 0.7)
--card-backdrop: blur(12px) saturate(180%)
--text-primary: #2d2d2d
--accent: #d2691e
```

### Dark Mode: Professional

**Visual**: Deep backgrounds, glowing accents, enhanced shadows, high contrast

**Palette**:
```css
--bg-primary: #1a1a1a
--card-bg: #2d2d2d
--text-primary: #e8e4d8
--accent: #fbbf24 (amber for visibility)
```

### Mobile Optimizations

Disable expensive effects on <768px:
- No `backdrop-filter` (solid backgrounds)
- Static backgrounds (no floating animations)
- Simplified shadows
- Reduced blur radius

---

## Implementation Waves

### ✅ Wave 1: Foundation (1-2 days) - BOTH BRANCHES
**Critical Infrastructure Setup**

**Tasks**:
- Install `next-themes` dependency
- Create `theme-variables.css` with all design tokens
- Create `ThemeProvider.tsx` wrapper component
- Create `ThemeToggle.tsx` component (user profile dropdown)
- Update root `layout.tsx` with provider
- Configure `tailwind.config.js` for dark mode

**Deliverables**:
- [ ] Theme toggle works globally
- [ ] Theme persists (localStorage)
- [ ] CSS variables accessible everywhere
- [ ] No visual regressions

---

### ✅ Wave 2: UI Primitives (2-3 days) - BOTH BRANCHES
**Foundation Components + Morphllm Automation**

**Components** (20 total):
- Primitives: Button, Input, Textarea, Checkbox, Radio, Toggle, Label, Slider
- Data Display: Card, Badge, Table, Accordion, Avatar
- Feedback: Alert, AlertDialog, Progress, Skeleton, Sonner

**Morphllm Patterns**:
- Card backgrounds: `bg-card/90 backdrop-blur-glass dark:bg-[#2d2d2d]`
- Text colors: `text-muted-foreground dark:text-[#999]`
- Icon backgrounds: `bg-[#fef3e2] dark:bg-[rgba(210,105,30,0.15)]`

**Deliverables**:
- [ ] All 20 primitives support dark/light
- [ ] Storybook stories show both variants
- [ ] Visual regression tests pass

---

### ✅ Wave 3: Global Layouts (1-2 days) - BOTH BRANCHES
**Navigation Shell & Layout Wrappers**

**Components** (10 total):
- TopNav, BottomNav, AdminSidebar
- PublicLayout, AuthLayout, ChatLayout, AdminLayout
- PublicHeader, PublicFooter, CommandPalette

**Effects**:
- TopNav glass: `backdrop-filter: blur(16px)` (light)
- TopNav dark: `backdrop-filter: blur(10px)` with dark background
- Theme toggle in TopNav user dropdown

**Deliverables**:
- [ ] Navigation themed correctly
- [ ] Layout wrappers apply backgrounds
- [ ] Theme toggle accessible

---

### ✅ Wave 4: Admin Components (3-4 days) - MAIN-DEV BRANCH
**Admin Pages & Dashboard**

**Components** (40+ admin):
- Dashboard: MetricsGrid, KPICard, StatCard, ActivityFeed, QuickActionsPanel
- Management: SystemStatus, ServiceHealthMatrix
- Charts: ChartsSection, APIRequestsChart
- Tables: Users, Games, Alerts
- Forms & Modals: Config forms, bulk actions

**Deliverables**:
- [ ] All 29 admin pages themed
- [ ] Charts render in both modes
- [ ] Admin tests passing

---

### ✅ Wave 5: Public Pages (3-4 days) - FRONTEND-DEV BRANCH
**User-Facing Pages**

**Pages** (25+ public):
- Landing: Hero, Features, CTA sections
- Auth: Login, Register, Reset Password
- Dashboard: User dashboard, QuickActions
- Games: Catalog, Detail, Search
- Library: Collections, Filters
- Static: FAQ, About, Contact

**Deliverables**:
- [ ] Landing glass hero working
- [ ] Auth pages themed
- [ ] Game catalog themed
- [ ] Public tests passing

---

### ✅ Wave 6: Chat Interface (2-3 days) - FRONTEND-DEV BRANCH
**Chat Components**

**Components** (15 chat):
- Core: ChatContent, MessageList, Message, MessageInput
- Features: GameSelector, FollowUpQuestions
- Utilities: ShareChatModal, ContextChip

**Special**: Message bubbles high contrast, code syntax highlighting themed

**Deliverables**:
- [ ] Chat fully themed
- [ ] Message bubbles readable
- [ ] Chat tests passing

---

### ✅ Wave 7: Feature Components (4-5 days) - PARALLEL
**Specialized Features**

**main-dev** (25 components):
- Share requests, Wizard, Bulk export, Alert rules

**frontend-dev** (30 components):
- Library (10), Sessions (12), PDF (8), Misc

**Morphllm**: Bulk pattern updates for ~55 similar components

**Deliverables**:
- [ ] 55+ components themed
- [ ] All modals updated
- [ ] Feature tests passing

---

### ✅ Wave 8: Polish & Effects (1-2 days) - INTEGRATION BRANCH
**Visual Polish & Performance**

**Tasks**:
- Glass effects (desktop): backdrop-filter, floating orbs, gradient text
- Dark enhancements: glows, text shadows, gradients
- Mobile optimizations: disable blur, static backgrounds
- Framer Motion: staggered reveals, page transitions

**Deliverables**:
- [ ] Effects performant
- [ ] Mobile optimized
- [ ] Lighthouse >90

---

### ✅ Wave 9: Testing (2-3 days) - INTEGRATION BRANCH
**Comprehensive Validation**

**Tests**:
- Unit: Update 100+ test files, maintain 85% coverage
- Visual: Chromatic regression (light + dark variants)
- E2E: Playwright theme switching test
- Accessibility: axe-core WCAG AA validation
- Performance: Lighthouse CI budgets

**Deliverables**:
- [ ] All tests passing
- [ ] Chromatic approved
- [ ] E2E suite complete
- [ ] Accessibility compliant
- [ ] Performance budgets met

---

## Morphllm Automation

**Efficiency**: 95% time reduction on pattern-based updates

**Patterns** (5 automated transformations):
1. **Card Backgrounds** (~80 components): Theme tokens + glass effect
2. **Text Colors** (~150 instances): Semantic color replacement
3. **Hover States** (~60 components): Consistent transforms + shadows
4. **Icon Containers** (~40 components): MeepleAI cream/dark backgrounds
5. **Badge Variants** (~30 components): Theme-aware accent colors

**Manual**: 50 hours → **Automated**: 2.5 hours

---

## Parallel Branch Strategy

**Setup**:
```bash
# Foundation (Wave 1-3): Both branches together
git checkout main-dev
git checkout -b feature/issue-XXXX-site-wide-redesign

# After Wave 3, sync to frontend-dev
git checkout frontend-dev
git checkout -b feature/issue-XXXX-site-wide-redesign
git cherry-pick <foundation-commits>
```

**Parallel Execution** (Wave 4-7):
- `main-dev`: Admin components (Developer A)
- `frontend-dev`: Public + Chat (Developer B)

**Integration** (Wave 8-9):
```bash
git checkout -b integration/site-wide-redesign
git merge feature/issue-XXXX-site-wide-redesign (from main-dev)
git merge feature/issue-XXXX-site-wide-redesign (from frontend-dev)
# Resolve conflicts, complete testing
```

---

## Risks & Mitigation

**Scope Creep** (250+ components):
- Mitigation: Wave-based boundaries, Morphllm automation, feature flags

**Visual Regressions**:
- Mitigation: Chromatic on 100+ stories, Playwright screenshots, rollback plan

**Performance** (backdrop-filter):
- Mitigation: Mobile detection, simplified effects, Lighthouse budgets

**Dark Mode Contrast**:
- Mitigation: WCAG AA validation, axe-core tests, manual audit

**Merge Conflicts** (parallel branches):
- Mitigation: Clear ownership, daily sync, integration branch

---

## Success Criteria

### Functional
- [ ] Theme toggle works on all 75+ pages
- [ ] Preference persists across sessions
- [ ] All 250+ components render in both modes
- [ ] No layout shifts during switching

### Visual
- [ ] Glass effects visible (desktop light)
- [ ] Dark mode polished
- [ ] Consistent palette site-wide
- [ ] Charts readable in both themes

### Performance
- [ ] Desktop: FCP <1s, TTI <2s
- [ ] Mobile: FCP <1.5s (simplified)
- [ ] CLS <0.1
- [ ] Bundle +<50KB

### Accessibility
- [ ] WCAG AA contrast (4.5:1 text, 3:1 UI)
- [ ] Toggle keyboard accessible
- [ ] Focus visible both modes
- [ ] 0 axe-core violations

### Testing
- [ ] 85%+ coverage maintained
- [ ] Chromatic approved
- [ ] Playwright 100% passing
- [ ] All tests updated (100+ files)

---

## Documentation

**PDCA**: `docs/pdca/site-wide-redesign/` (plan, do, check, act)
**Component Inventory**: Generated by Explore agent (aaf4445)
**Issue Template**: `.github/ISSUE_TEMPLATE/site-wide-redesign.md`

---

## Timeline

**Duration**: 18 days (12-14 working days with parallelization)
**Start**: 2026-01-23
**Completion**: 2026-02-10

**Checkpoints**:
- Day 6: Foundation complete → Review
- Day 14: Implementation complete → Review
- Day 18: Testing complete → Final review

---

**Complexity**: XL (100-130 hours with parallelization)
**Risk**: Medium (systematic approach mitigates large scope)
