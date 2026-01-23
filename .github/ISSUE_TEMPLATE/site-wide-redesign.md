**Epic**: Design System & UX Enhancement

## Description

Complete site-wide migration to dual-theme design system with glass morphism (light mode) and dark professional (dark mode) aesthetics. This migration affects **250+ components** and **75+ pages** across the entire MeepleAI frontend.

**Design Assets**:
- Light mode mockup: `docs/design-proposals/meepleai-style/admin-dashboard-glass.html`
- Dark mode mockup: `docs/design-proposals/meepleai-style/admin-dashboard-dark.html`
- Original design: `docs/design-proposals/meepleai-style/admin-dashboard-v2.html`

**Theme Approach**: Hybrid (next-themes + CSS variables) with toggle in user profile dropdown

---

## Scope

### Component Inventory (250+ components)

**Critical Path** (35 components):
- Auth pages: LoginForm, RegisterForm, OAuthButtons
- Navigation: TopNav, BottomNav, AdminLayout, AdminSidebar
- UI Primitives: Button, Card, Dialog, Input, Badge, Alert
- Core features: ChatContent, MessageList, GameCard, HeroSection

**High Priority** (80 components):
- Public pages: Dashboard, Games, Library, Sessions
- Admin pages: Management, Analytics, Users, Infrastructure
- Forms: Form controls, validation, switches
- Modals: All dialog and sheet components
- Search & Filters: Game catalog, library filters

**Medium Priority** (100 components):
- Data display: Tables, Timelines, Accordions
- Charts: ChartsSection, APIRequestsChart, MetricsChart
- PDF & Docs: Viewers, upload forms, document lists
- State Management: GameStateEditor, BoardStateEditor
- Specialized: Prompts, Diff viewers, Versioning

**Low Priority** (35 components):
- Static pages: Privacy, Terms, FAQ
- Comments system
- Accessibility utilities
- Dev tools: Wizard, Testing pages

---

## Design System Specifications

### Light Mode: Glass Morphism

**Palette**:
```css
--bg-primary: linear-gradient(135deg, #fef3e2, #f8f6f0, #fce8d8);
--card-bg: rgba(255, 255, 255, 0.7);
--card-border: rgba(210, 105, 30, 0.15);
--card-backdrop: blur(12px) saturate(180%);
--text-primary: #2d2d2d;
--text-secondary: #666;
--accent-primary: #d2691e;
--accent-gradient: linear-gradient(135deg, #d2691e, #f59e0b);
```

**Visual Effects**:
- Translucent cards with `backdrop-filter: blur(12px)`
- Floating orb animations (radial gradients)
- Noise texture overlay (SVG)
- Gradient text on titles
- Smooth hover transitions with scale

**Card Hover**:
```css
transition: all 0.4s cubic-bezier(0.4, 0, 0.2, 1);
hover: {
  transform: translateY(-4px) scale(1.02);
  box-shadow: 0 12px 32px rgba(139, 90, 60, 0.15);
  border-color: rgba(210, 105, 30, 0.3);
}
```

### Dark Mode: Professional

**Palette**:
```css
--bg-primary: #1a1a1a;
--card-bg: #2d2d2d;
--card-hover: #3a3a3a;
--card-border: rgba(210, 105, 30, 0.2);
--text-primary: #e8e4d8;
--text-secondary: #999;
--accent-primary: #fbbf24; /* Amber for visibility */
--accent-secondary: #d2691e;
```

**Visual Effects**:
- Deep backgrounds with contrast
- Glowing status indicators
- Text shadows on key metrics
- Enhanced border contrast
- Gradient buttons with dramatic shadows

**Card Hover**:
```css
hover: {
  transform: translateY(-3px);
  box-shadow: 0 8px 24px rgba(210, 105, 30, 0.3);
  border-color: #d2691e;
}
```

### Mobile Optimizations

**Performance Constraints** (<768px viewports):
```css
@media (max-width: 768px) {
  /* Disable expensive effects */
  backdrop-filter: none;           /* Remove blur */
  background: solid colors;         /* No gradients */
  box-shadow: simplified;           /* Reduce blur radius */
  animation: none;                  /* No floating orbs */
}
```

**Targets**:
- Mobile FCP: <1.5s
- Mobile TTI: <2.5s
- Reduced motion support: `prefers-reduced-motion: reduce`

---

## Implementation Waves

### Wave 1: Foundation (1-2 days) - BOTH BRANCHES
**Priority**: 🔴 CRITICAL

**Tasks**:
1. Install dependencies:
   ```bash
   pnpm add next-themes
   pnpm add framer-motion  # If not present
   ```

2. Create theme system:
   - `apps/web/src/styles/theme-variables.css` - All CSS design tokens
   - `apps/web/src/providers/ThemeProvider.tsx` - next-themes wrapper
   - `apps/web/src/components/layout/ThemeToggle.tsx` - Toggle component

3. Update global files:
   - `apps/web/src/styles/globals.css` - Import theme variables
   - `apps/web/src/app/layout.tsx` - Wrap with ThemeProvider
   - `apps/web/tailwind.config.js` - Theme-aware configuration

**Acceptance Criteria**:
- [ ] Theme toggle works globally
- [ ] Theme persists (localStorage)
- [ ] CSS variables accessible in all components
- [ ] No visual regressions on existing pages
- [ ] Dark/light switch animates smoothly

---

### Wave 2: UI Primitives (2-3 days) - BOTH BRANCHES
**Priority**: 🔴 CRITICAL
**Automation**: Morphllm for pattern replacements

**Components** (20 total):
**Primitives**: Button, Input, Textarea, Checkbox, Radio, Toggle, Label, Slider, ScrollArea, ToggleGroup
**Data Display**: Card, Badge, Table, Accordion, Avatar
**Feedback**: Alert, AlertDialog, Progress, Skeleton, Sonner

**Morphllm Pattern Example**:
```typescript
// Pattern 1: Card backgrounds
FROM: className="bg-white border-gray-200"
TO: className="bg-card/90 backdrop-blur-glass border-border dark:bg-[#2d2d2d] dark:border-[rgba(210,105,30,0.2)]"

// Pattern 2: Text colors
FROM: text-gray-600
TO: text-muted-foreground dark:text-[#999]

// Pattern 3: Icon backgrounds
FROM: bg-gray-100
TO: bg-[#fef3e2] dark:bg-[rgba(210,105,30,0.15)]
```

**Acceptance Criteria**:
- [ ] All 20 primitives support dark/light modes
- [ ] CVA variants use theme tokens
- [ ] Hover states themed correctly
- [ ] Focus indicators visible in both modes
- [ ] Storybook stories show both variants
- [ ] Visual regression tests pass

---

### Wave 3: Global Layouts (1-2 days) - BOTH BRANCHES
**Priority**: 🔴 CRITICAL

**Components** (10 total):
- **Navigation**: TopNav, BottomNav, AdminSidebar
- **Layouts**: PublicLayout, AuthLayout, ChatLayout, AdminLayout
- **Shell**: PublicHeader, PublicFooter, CommandPalette

**Glass Effects** (TopNav light mode):
```css
background: rgba(255, 255, 255, 0.8);
backdrop-filter: blur(16px);
border-bottom: 1px solid rgba(210, 105, 30, 0.2);
box-shadow: 0 4px 24px rgba(139, 90, 60, 0.08);
```

**Dark Effects** (TopNav dark mode):
```css
background: rgba(45, 45, 45, 0.95);
backdrop-filter: blur(10px);
border-bottom: 1px solid rgba(210, 105, 30, 0.2);
box-shadow: 0 4px 12px rgba(0, 0, 0, 0.5);
```

**Acceptance Criteria**:
- [ ] TopNav glass effect works in light mode
- [ ] BottomNav mobile themed correctly
- [ ] AdminSidebar dark mode functional
- [ ] Theme toggle accessible in user profile
- [ ] All layout wrappers apply correct backgrounds
- [ ] No layout shifts during theme switching

---

### Wave 4: Admin Components (3-4 days) - MAIN-DEV BRANCH
**Priority**: 🟡 HIGH
**Branch**: `main-dev`

**Components** (40+ admin-specific):
**Dashboard**: DashboardHeader, MetricsGrid, KPICard, StatCard, ActivityFeed, QuickActionsPanel, AlertsBanner
**Management**: AdminHeader, SystemStatus, ServiceStatusCard, ServiceHealthMatrix
**Charts**: ChartsSection, APIRequestsChart, AIUsageDonut, MetricsChart
**Tables**: AiModelsTable, AlertRuleList, UsersTable
**Forms**: GameForm, AlertRuleForm, CategoryConfigTab, FeatureFlagsTab
**Modals**: ModelConfigModal, SetPrimaryModelDialog, BulkActionBar

**Recharts Theming**:
```tsx
// Light mode colors
const lightColors = ['#d2691e', '#f59e0b', '#16a34a', '#3b82f6'];

// Dark mode colors
const darkColors = ['#fbbf24', '#d2691e', '#4ade80', '#60a5fa'];

// Apply based on theme
const theme = useTheme();
const colors = theme === 'dark' ? darkColors : lightColors;
```

**Acceptance Criteria**:
- [ ] All 29 admin pages themed
- [ ] 40+ admin components updated
- [ ] Charts render correctly in both modes
- [ ] Tables have proper row hover states
- [ ] Forms maintain functionality
- [ ] Admin layout sidebar themed
- [ ] All admin tests updated and passing

---

### Wave 5: Public Pages (3-4 days) - FRONTEND-DEV BRANCH
**Priority**: 🟡 HIGH
**Branch**: `frontend-dev`

**Pages** (25+ public-facing):
**Landing**: HeroSection, FeaturesSection, CallToActionSection
**Auth**: LoginForm, RegisterForm, ResetPassword, Welcome
**Dashboard**: QuickActions, RecentGamesSection, ChatHistorySection
**Games**: GameCard, GameGrid, CatalogFilters, GameDetail tabs
**Library**: UserGameCard, LibraryFilters, QuotaStatusBar
**Static**: FAQ, About, How It Works, Contact

**Hero Glass Effect** (landing page):
```tsx
<section className="relative overflow-hidden">
  {/* Floating orbs background */}
  <div className="absolute inset-0 bg-gradient-to-br from-[#fef3e2] via-[#f8f6f0] to-[#fce8d8]">
    <div className="absolute top-[20%] left-[10%] w-[500px] h-[500px] bg-[#d2691e]/8 rounded-full blur-[80px] animate-float" />
    <div className="absolute bottom-[30%] right-[15%] w-[400px] h-[400px] bg-[#f59e0b]/6 rounded-full blur-[60px] animate-float" style={{ animationDelay: '2s' }} />
  </div>

  {/* Glass card content */}
  <div className="relative bg-white/70 backdrop-blur-xl border border-[#d2691e]/15 rounded-2xl p-12">
    {/* Hero content */}
  </div>
</section>
```

**Acceptance Criteria**:
- [ ] Landing page glass hero working
- [ ] Auth pages themed and functional
- [ ] Game catalog filters themed
- [ ] Library components updated
- [ ] Static pages have consistent styling
- [ ] All public page tests passing

---

### Wave 6: Chat Interface (2-3 days) - FRONTEND-DEV BRANCH
**Priority**: 🟡 HIGH
**Branch**: `frontend-dev`

**Components** (15 chat-specific):
**Core**: ChatContent, MessageList, Message, MessageInput, ChatHeader
**Features**: VirtualizedMessageList, GameSelector, FollowUpQuestions, MessageActions
**Utilities**: ShareChatModal, ContextChip, CommentBox, MentionInput, MessageEditForm

**Message Bubble Theming**:
```tsx
// User message (light)
className="bg-primary/10 border border-primary/20 text-foreground"

// User message (dark)
className="dark:bg-[#d2691e]/20 dark:border-[#d2691e]/30 dark:text-[#e8e4d8]"

// AI message (light)
className="bg-accent/10 border border-accent/20"

// AI message (dark)
className="dark:bg-[#fbbf24]/10 dark:border-[#fbbf24]/20"
```

**Code Block Theming**:
- Light: Prism theme "oneLight"
- Dark: Prism theme "oneDark"
- Syntax highlighting colors adapt to theme

**Acceptance Criteria**:
- [ ] Chat interface fully themed
- [ ] Message bubbles high contrast in dark mode
- [ ] Code blocks syntax highlighted correctly
- [ ] Sidebar transitions smooth
- [ ] Virtual scrolling performance maintained
- [ ] Citation links visible in both modes
- [ ] Chat tests updated and passing

---

### Wave 7: Feature Components (4-5 days) - PARALLEL BOTH BRANCHES
**Priority**: 🟢 MEDIUM
**Branches**: Split by feature domain

**main-dev** (Admin features - 25 components):
- Share requests: ContributorCard, ShareRequestCard, ShareRequestFilters
- Wizard: WizardSteps, SetupModals
- Bulk tools: BulkExport, BulkActionBar
- Management: AlertRules, ApiKeys, N8nTemplates

**frontend-dev** (User features - 30 components):
- Library: UserGameCard, SharedLibraryCard, FavoriteToggle, ShareLibraryModal (10 components)
- Sessions: GameStateViewer, StateEditor, PlayerStateCard, LedgerTimeline (12 components)
- PDF: PdfViewer, PdfPreview, PdfTable, DocumentBadge (8 components)
- Miscellaneous: Badges, Upload, Timeline, Versioning

**Morphllm Bulk Updates**:
```typescript
// Automation scope: ~55 components with similar patterns

Pattern {
  name: "card-hover-effects",
  files: "apps/web/src/components/**/*Card*.tsx",
  transform: {
    from: "hover:shadow-lg",
    to: "hover:-translate-y-1 hover:shadow-[0_8px_32px_rgba(139,90,60,0.15)] dark:hover:shadow-[0_6px_20px_rgba(210,105,30,0.3)]"
  }
}

Pattern {
  name: "gradient-headings",
  files: "apps/web/src/app/**/page.tsx",
  transform: {
    from: "text-3xl font-bold text-gray-900",
    to: "text-3xl font-bold bg-gradient-to-r from-[#d2691e] to-[#f59e0b] bg-clip-text text-transparent dark:from-[#fbbf24] dark:to-[#d2691e]"
  }
}
```

**Acceptance Criteria**:
- [ ] 55+ feature components themed
- [ ] Morphllm automation completed successfully
- [ ] All modals and dialogs updated
- [ ] Form components consistent
- [ ] Specialized components (PDF, Diff) themed
- [ ] All feature tests passing

---

### Wave 8: Polish & Effects (1-2 days) - INTEGRATION BRANCH
**Priority**: 🟢 MEDIUM
**Branch**: Create `integration/site-wide-redesign` (merge main-dev + frontend-dev)

**Tasks**:

1. **Glass Morphism Effects** (light mode):
   - Apply `backdrop-filter: blur(12px)` to all cards (desktop only)
   - Add floating orb animations to key pages (Landing, Dashboard)
   - Implement gradient text on page headings
   - Noise texture overlay on body background

2. **Dark Mode Enhancements**:
   - Glow effects on status indicators (`box-shadow: 0 0 8px rgba(74,222,128,0.6)`)
   - Text shadows on metric values (`text-shadow: 0 2px 8px rgba(251,191,36,0.3)`)
   - Enhanced button gradients
   - Border glow on hover

3. **Mobile Optimizations**:
   ```tsx
   // Conditional effects based on viewport
   const isMobile = useMediaQuery('(max-width: 768px)');

   <Card className={cn(
     'bg-card border-border',
     !isMobile && 'backdrop-blur-glass',  // Glass effect desktop only
     !isMobile && 'bg-card/70',           // Transparent desktop only
     isMobile && 'bg-card'                // Solid mobile
   )} />
   ```

4. **Framer Motion Animations**:
   ```tsx
   // Staggered card reveals
   <motion.div
     initial={{ opacity: 0, y: 20 }}
     animate={{ opacity: 1, y: 0 }}
     transition={{ delay: index * 0.05 }}
   >
     <MetricCard {...props} />
   </motion.div>
   ```

**Acceptance Criteria**:
- [ ] Glass effects performant on desktop
- [ ] Dark mode polished and consistent
- [ ] Mobile simplified effects working
- [ ] Animations smooth (60fps)
- [ ] Lighthouse score >90 performance
- [ ] No jank during theme switching

---

### Wave 9: Testing & Validation (2-3 days) - INTEGRATION BRANCH
**Priority**: 🔴 CRITICAL

**Test Updates** (100+ test files):

1. **Unit Tests** (Vitest):
   ```bash
   cd apps/web
   pnpm test                     # All unit tests
   pnpm test:coverage            # Coverage report (target: 85%+)
   ```

   Update patterns:
   - Mock `ThemeProvider` in all component tests
   - Test light and dark variants render
   - Verify theme prop changes apply correctly

2. **Visual Regression** (Chromatic):
   ```bash
   pnpm chromatic --exit-zero-on-changes
   ```

   **Stories to update** (100+):
   - Add `parameters.backgrounds` for light/dark
   - Create separate stories: `Default`, `DarkMode`
   - Test viewports: 375px, 768px, 1024px, 1920px
   - Capture hover, focus, loading states

3. **E2E Tests** (Playwright):
   Create `apps/web/__tests__/e2e/theme-switching.spec.ts`:
   ```typescript
   test('Admin dashboard theme switching', async ({ page }) => {
     await page.goto('/admin');

     // Capture light mode
     await page.screenshot({ path: 'admin-light.png' });

     // Toggle to dark
     await page.click('[data-testid="theme-toggle"]');
     await page.waitForTimeout(300); // Animation

     // Capture dark mode
     await page.screenshot({ path: 'admin-dark.png' });

     // Verify persistence
     await page.reload();
     expect(await page.getAttribute('html', 'class')).toContain('dark');
   });
   ```

4. **Accessibility Tests**:
   ```typescript
   import { injectAxe, checkA11y } from 'axe-playwright';

   test('Dashboard accessibility (light mode)', async ({ page }) => {
     await page.goto('/admin');
     await injectAxe(page);
     await checkA11y(page, null, {
       rules: {
         'color-contrast': { enabled: true, options: { level: 'AA' } },
       },
     });
   });

   test('Dashboard accessibility (dark mode)', async ({ page }) => {
     await page.goto('/admin');
     await page.click('[data-testid="theme-toggle"]');
     await injectAxe(page);
     await checkA11y(page);
   });
   ```

5. **Performance Tests**:
   ```bash
   # Lighthouse CI
   lhci autorun --collect.settings.preset=desktop
   lhci autorun --collect.settings.preset=mobile
   ```

**Acceptance Criteria**:
- [ ] 85%+ unit test coverage maintained
- [ ] All integration tests passing
- [ ] Chromatic visual regression: 0 unintended changes
- [ ] Playwright E2E suite: 100% passing
- [ ] Accessibility tests: WCAG AA compliant
- [ ] Lighthouse performance: >90 (desktop), >80 (mobile)
- [ ] Bundle size increase: <50KB

---

## Morphllm Automation Strategy

### Automated Pattern Replacements

**Pattern 1: Card Backgrounds** (~80 components):
```typescript
{
  name: "card-backgrounds",
  instruction: "Update all Card component backgrounds to use theme tokens with glass effect",
  scope: "apps/web/src/components/**/*.tsx",
  pattern: "background colors and borders on Card components"
}
```

**Pattern 2: Text Colors** (~150 instances):
```typescript
{
  name: "text-colors",
  instruction: "Replace all hardcoded text-gray-* with semantic theme tokens",
  scope: "apps/web/src/components/**/*.tsx",
  pattern: "text-gray-500, text-gray-600, text-gray-900 classes"
}
```

**Pattern 3: Hover States** (~60 components):
```typescript
{
  name: "hover-transforms",
  instruction: "Add consistent hover effects with theme-aware shadows",
  scope: "apps/web/src/components/**/*Card*.tsx",
  pattern: "hover: pseudo-class on interactive cards"
}
```

**Pattern 4: Icon Containers** (~40 components):
```typescript
{
  name: "icon-backgrounds",
  instruction: "Update icon container backgrounds to use MeepleAI cream/dark variants",
  scope: "apps/web/src/components/**/*.tsx",
  pattern: "icon wrapper divs with background classes"
}
```

**Pattern 5: Badge Variants** (~30 components):
```typescript
{
  name: "badge-colors",
  instruction: "Update badge backgrounds to theme-aware accent colors",
  scope: "apps/web/src/components/**/*Badge*.tsx",
  pattern: "badge color classes"
}
```

**Efficiency Gains**:
- Manual updates: ~2 hours per 10 components = 50 hours total
- Morphllm automation: ~30 minutes per pattern = 2.5 hours total
- **Time Saved**: 47.5 hours (95% reduction)

---

## Parallel Branch Strategy

### Branch Structure

```
main (production)
├── main-dev (development trunk)
│   └── feature/issue-XXXX-site-wide-redesign (this feature)
│       ├── Wave 1-3: Foundation (merged to main-dev first)
│       ├── Wave 4: Admin components (main-dev work)
│       └── Merge back after Wave 9
│
└── frontend-dev (frontend trunk)
    └── feature/issue-XXXX-site-wide-redesign (parallel branch)
        ├── Wave 1-3: Foundation (sync with main-dev)
        ├── Wave 5-6: Public + Chat (frontend-dev work)
        └── Merge back after Wave 9
```

### Workflow

**Phase 1: Setup** (Wave 1-3):
```bash
# Both developers work on same foundation
git checkout main-dev
git pull origin main-dev
git checkout -b feature/issue-XXXX-site-wide-redesign

# Complete Wave 1-3 together
# Push foundation work

# Sync to frontend-dev
git checkout frontend-dev
git pull origin frontend-dev
git checkout -b feature/issue-XXXX-site-wide-redesign
git cherry-pick <foundation-commits>
```

**Phase 2: Parallel** (Wave 4-7):
```bash
# Developer A (Admin - main-dev)
git checkout feature/issue-XXXX-site-wide-redesign (from main-dev)
# Work on Wave 4 (admin components)
git add . && git commit -m "feat(admin): Wave 4 - admin components themed"
git push origin feature/issue-XXXX-site-wide-redesign

# Developer B (Public - frontend-dev)
git checkout feature/issue-XXXX-site-wide-redesign (from frontend-dev)
# Work on Wave 5-6 (public + chat components)
git add . && git commit -m "feat(public): Wave 5 - public pages themed"
git push origin feature/issue-XXXX-site-wide-redesign
```

**Phase 3: Integration** (Wave 8-9):
```bash
# Create integration branch
git checkout -b integration/site-wide-redesign

# Merge main-dev work
git merge feature/issue-XXXX-site-wide-redesign (from main-dev)

# Merge frontend-dev work
git merge feature/issue-XXXX-site-wide-redesign (from frontend-dev)

# Resolve conflicts if any
# Complete Wave 8-9 (polish + testing)

# Final merge back to trunks
git checkout main-dev
git merge integration/site-wide-redesign

git checkout frontend-dev
git merge integration/site-wide-redesign
```

**Conflict Prevention**:
- Clear file ownership (admin/ vs public components)
- Foundation waves completed before split
- Daily sync on shared components (ui/primitives)
- Communication on layout changes

---

## Acceptance Criteria

### Functional Requirements
- [ ] Theme toggle works globally across all 75+ pages
- [ ] Theme preference persists across sessions (localStorage)
- [ ] All 250+ components render correctly in light mode
- [ ] All 250+ components render correctly in dark mode
- [ ] No layout shifts during theme switching
- [ ] All forms submit correctly in both themes
- [ ] Navigation functional in both modes
- [ ] Modal/dialog open/close smooth in both themes

### Visual Quality
- [ ] Glass morphism effects visible in light mode (desktop)
- [ ] Backdrop blur working on all cards (desktop)
- [ ] Floating orb animations on landing/dashboard pages
- [ ] Dark mode has polished shadows and glows
- [ ] Consistent MeepleAI color palette across all pages
- [ ] Typography hierarchy clear in both modes
- [ ] Icons and badges visible and themed
- [ ] Charts readable with theme-aware colors
- [ ] Status indicators (green/yellow/red) maintain meaning

### Performance
- [ ] Desktop FCP <1s (both themes)
- [ ] Desktop TTI <2s (both themes)
- [ ] Mobile FCP <1.5s (simplified effects)
- [ ] Mobile TTI <2.5s
- [ ] CLS <0.1 (no layout shifts)
- [ ] Bundle size increase <50KB
- [ ] Lighthouse performance >90 (desktop), >80 (mobile)
- [ ] No runtime performance regressions
- [ ] 60fps animations (no jank)

### Accessibility
- [ ] WCAG AA contrast ratios met in both modes
  - [ ] Text contrast ≥4.5:1
  - [ ] UI element contrast ≥3:1
- [ ] Theme toggle keyboard accessible (Tab, Space, Enter)
- [ ] Focus indicators visible in both modes
- [ ] Focus trap working in modals (both themes)
- [ ] Screen reader announcements correct
- [ ] Color not sole indicator (icons + text labels)
- [ ] `prefers-reduced-motion` support (disable animations)
- [ ] Axe-core tests passing (0 violations)

### Code Quality
- [ ] 100% Tailwind CSS usage (no inline style tags)
- [ ] All colors via CSS variables (no hardcoded hex)
- [ ] Design tokens for spacing, typography, shadows
- [ ] CVA variants for component polymorphism
- [ ] Consistent naming conventions (PascalCase components)
- [ ] No legacy CSS files remaining
- [ ] Storybook stories updated (100+ stories)
- [ ] All console warnings resolved
- [ ] ESLint passing with no theme-related warnings

### Testing
- [ ] Unit test coverage ≥85% maintained
- [ ] All integration tests passing
- [ ] Chromatic visual regression: 0 unintended changes
- [ ] Playwright E2E suite: 100% passing
- [ ] Accessibility suite: 0 axe-core violations
- [ ] Performance budgets met in CI
- [ ] Cross-browser testing (Chrome, Firefox, Edge, Safari ≥15)

### Documentation
- [ ] `docs/pdca/site-wide-redesign/` complete (plan, do, check, act)
- [ ] All Storybook stories show light/dark examples
- [ ] Theme system usage guide created
- [ ] Color palette reference documented
- [ ] Animation guidelines documented
- [ ] Migration notes for future components
- [ ] CLAUDE.md updated with theme patterns
- [ ] README includes theme toggle instructions

---

## Complexity

**XL (Extra Large)** - 100+ hours

**Breakdown**:
- Wave 1 (Foundation): 8-12 hours
- Wave 2 (Primitives): 16-20 hours
- Wave 3 (Layouts): 8-12 hours
- Wave 4 (Admin): 20-24 hours
- Wave 5 (Public): 20-24 hours
- Wave 6 (Chat): 12-16 hours
- Wave 7 (Features): 24-30 hours
- Wave 8 (Polish): 8-12 hours
- Wave 9 (Testing): 16-24 hours

**Total**: 132-174 hours (with parallelization: 100-130 hours)

---

## Labels

- `area/ui` - UI/UX changes
- `frontend` - Frontend codebase
- `kind/enhancement` - Feature enhancement
- `priority: high` - High priority
- `complexity: xl` - Extra large scope
- `epic` - Epic issue (multiple waves)

---

## Related Issues

### Dependencies
- Requires: Storybook setup (#2924)
- Requires: Design tokens documented (#2931)
- Blocks: Future component additions (must follow new theme system)

### Reference
- Design mockups: docs/design-proposals/meepleai-style/
- Current admin dashboard: Issue #2793, #2850
- PDCA documentation: docs/pdca/site-wide-redesign/
- Component inventory: Generated by Explore agent (aaf4445)

---

## Timeline

**Start Date**: 2026-01-23
**Estimated Completion**: 2026-02-10 (18 calendar days, 12-14 working days with parallelization)

**Checkpoints**:
- Day 2: Wave 1 complete (theme system functional)
- Day 5: Wave 2 complete (primitives themed)
- Day 6: Wave 3 complete (layouts themed) - **CHECKPOINT REVIEW**
- Day 10: Wave 4-5 complete (admin + public themed)
- Day 12: Wave 6-7 complete (chat + features themed)
- Day 14: Wave 8 complete (polish done) - **CHECKPOINT REVIEW**
- Day 18: Wave 9 complete (all tests pass) - **FINAL REVIEW**

---

## Success Metrics

### User-Facing
- [ ] Users can toggle dark/light mode from any page
- [ ] Theme preference remembered across sessions
- [ ] Dark mode reduces eye strain (user feedback)
- [ ] Glass effects create modern, premium feel
- [ ] Mobile users experience fast, optimized UI

### Developer-Facing
- [ ] Consistent theme system for all future components
- [ ] Reusable patterns documented and automated
- [ ] Comprehensive test coverage prevents regressions
- [ ] Storybook serves as visual documentation
- [ ] Clear guidelines for theme-aware component development

### Business Impact
- [ ] Improved user satisfaction (dark mode requested feature)
- [ ] Modern aesthetic increases perceived quality
- [ ] Accessibility compliance reduces legal risk
- [ ] Performance optimization retains mobile users
- [ ] Systematic approach enables future theme variations

---

**Issue Type**: Epic
**Estimated Effort**: 100-130 hours (with parallelization)
**Risk Level**: Medium (large scope, but systematic approach mitigates)
