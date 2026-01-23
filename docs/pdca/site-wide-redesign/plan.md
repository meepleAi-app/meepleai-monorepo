# Plan: Site-Wide Dual-Theme Design System

**Issue**: #2965
**Created**: 2026-01-23
**Status**: Planning Complete
**Complexity**: Enterprise (250+ components, 75+ pages)

---

## Hypothesis

**What**: Migrate entire MeepleAI frontend to dual-theme design system with glass morphism (light) and dark professional (dark) aesthetics.

**Why**:
- Current design lacks cohesive theme system
- No dark mode support (user-requested feature)
- Inconsistent styling across pages
- Mockups show significant UX improvement potential

**Approach**:
- **Theme System**: next-themes + CSS variables (hybrid approach)
- **Migration**: Big bang all components at once (consistency)
- **Automation**: Morphllm for pattern-based bulk updates
- **Parallel Execution**: main-dev (admin) + frontend-dev (public) branches
- **Animation**: CSS for simple, Framer Motion for complex orchestrations

---

## Scope Definition

### Quantitative Scope
- **Components**: 250+ visual components requiring theme updates
- **Pages**: 75+ pages across 5 route groups
- **UI Primitives**: 20 shadcn/ui base components
- **Layouts**: 6 global layout wrappers
- **Tests**: 100+ test files to update
- **Stories**: 100+ Storybook stories to update

### Categorical Breakdown

**Critical Path** (35 components):
- Auth pages (Login, Register, OAuth)
- Navigation (TopNav, BottomNav, AdminLayout)
- UI Primitives (Button, Card, Dialog, Input)
- Core features (ChatContent, GameCard, HeroSection)

**High Priority** (80 components):
- Public pages (Dashboard, Games, Library)
- Admin components (Management, Analytics, Users)
- Form controls, Modals, Alerts
- Search, Filters, Catalog components

**Medium Priority** (100 components):
- Data display (Tables, Timelines, Diff viewers)
- Charts and visualizations
- PDF and document components
- Specialized features (State editor, Prompts)

**Low Priority** (35 components):
- Static pages (Privacy, Terms, About)
- Comments system
- Dev-only tools (Wizard, Testing pages)

---

## Design Specifications

### Light Mode: Glass Morphism

**Visual Characteristics**:
- Translucent cards with backdrop blur
- Floating orb background animations
- Subtle gradient overlays
- Warm color palette

**Technical Implementation**:
```css
/* Light Mode Palette */
--bg-primary: linear-gradient(135deg, #fef3e2, #f8f6f0, #fce8d8);
--card-bg: rgba(255, 255, 255, 0.7);
--card-border: rgba(210, 105, 30, 0.15);
--card-backdrop: blur(12px) saturate(180%);
--text-primary: #2d2d2d;
--text-secondary: #666;
--text-tertiary: #999;
--accent-primary: #d2691e;
--accent-gradient: linear-gradient(135deg, #d2691e, #f59e0b);

/* Effects */
--shadow-sm: 0 4px 16px rgba(139, 90, 60, 0.08);
--shadow-md: 0 8px 32px rgba(139, 90, 60, 0.15);
--shadow-lg: 0 12px 48px rgba(139, 90, 60, 0.2);

/* Hover Transform */
transform: translateY(-4px) scale(1.02);
```

**Backgrounds**:
- Floating orb animations (radial gradients)
- Noise texture overlay (SVG)
- Grid pattern (subtle lines)

### Dark Mode: Professional

**Visual Characteristics**:
- Deep backgrounds with contrast
- Glowing accents (amber)
- Sharp borders with warm tones
- Enhanced shadows

**Technical Implementation**:
```css
/* Dark Mode Palette */
--bg-primary: #1a1a1a;
--card-bg: #2d2d2d;
--card-hover: #3a3a3a;
--card-border: rgba(210, 105, 30, 0.2);
--text-primary: #e8e4d8;
--text-secondary: #999;
--text-tertiary: #666;
--accent-primary: #fbbf24; /* Amber for high visibility */
--accent-secondary: #d2691e;

/* Effects */
--shadow-sm: 0 4px 12px rgba(0, 0, 0, 0.3);
--shadow-md: 0 6px 20px rgba(210, 105, 30, 0.3);
--shadow-lg: 0 8px 32px rgba(210, 105, 30, 0.4);

/* Glow Effects */
box-shadow: 0 0 8px rgba(251, 191, 36, 0.6);
text-shadow: 0 2px 8px rgba(251, 191, 36, 0.3);
```

---

## Expected Outcomes (Quantitative)

### Performance Targets
- **Load Time**: <1s First Contentful Paint (FCP)
- **Interactivity**: <2s Time to Interactive (TTI)
- **Layout Stability**: Cumulative Layout Shift (CLS) <0.1
- **Mobile Performance**: Simplified effects (no blur on <768px)

### Quality Targets
- **Test Coverage**: Maintain 85%+ unit test coverage
- **Visual Regression**: 0 unintended visual changes in Chromatic
- **Accessibility**: WCAG AA compliance (100% critical paths)
- **Browser Support**: Modern browsers only (no Safari <15)

### Code Quality
- **Consistency**: 100% Tailwind CSS usage (no inline styles)
- **Maintainability**: CSS variables for all theme values
- **Reusability**: Pattern-based components via CVA
- **Documentation**: All components have Storybook stories

### User Impact
- **UX Improvement**: Dark mode reduces eye strain
- **Aesthetic Quality**: Glass morphism modern look
- **Performance**: Mobile users get optimized experience
- **Accessibility**: Improved contrast and focus states

---

## Implementation Waves

### Wave 1: Foundation (Critical Infrastructure)
**Timeline**: 1-2 days
**Branch**: Both (main-dev + frontend-dev)

**Tasks**:
1. Install dependencies: `next-themes`, `framer-motion` (if not present)
2. Create `apps/web/src/styles/theme-variables.css` with all design tokens
3. Update `apps/web/src/styles/globals.css` to import theme variables
4. Create `apps/web/src/providers/ThemeProvider.tsx` (next-themes wrapper)
5. Update root `apps/web/src/app/layout.tsx` with ThemeProvider
6. Create `apps/web/src/components/layout/ThemeToggle.tsx` component
7. Update `tailwind.config.js` with theme-aware configuration

**Deliverables**:
- [ ] Theme system functional (toggle works)
- [ ] CSS variables accessible in all components
- [ ] Dark/light mode switches globally
- [ ] No visual regressions on existing pages

---

### Wave 2: UI Primitives (Foundation Components)
**Timeline**: 2-3 days
**Branch**: Both
**Automation**: Morphllm bulk pattern updates

**Components** (20 total):
- **Primitives**: Button, Input, Textarea, Checkbox, Radio, Toggle, Label, Slider, ScrollArea, ToggleGroup
- **Data Display**: Card, Badge, Table, Accordion, Avatar
- **Feedback**: Alert, AlertDialog, Progress, Skeleton, Sonner

**Pattern** (Morphllm automation):
```tsx
// FROM (current)
className="bg-white border-gray-200 text-gray-900"

// TO (themed)
className="bg-card border-border text-foreground dark:bg-[#2d2d2d] dark:border-[rgba(210,105,30,0.2)]"
```

**Deliverables**:
- [ ] All 20 primitives support dark/light
- [ ] CVA variants updated with theme tokens
- [ ] Storybook stories show both modes
- [ ] Visual regression tests pass

---

### Wave 3: Global Layouts (Navigation Shell)
**Timeline**: 1-2 days
**Branch**: Both

**Components** (10 total):
- TopNav, BottomNav, AdminSidebar
- PublicLayout, AuthLayout, ChatLayout, AdminLayout
- PublicHeader, PublicFooter, CommandPalette

**Glass Effects** (light mode):
```css
/* TopNav glass effect */
background: rgba(255, 255, 255, 0.8);
backdrop-filter: blur(16px);
border-bottom: 1px solid rgba(210, 105, 30, 0.2);
box-shadow: 0 4px 24px rgba(139, 90, 60, 0.08);
```

**Dark Effects** (dark mode):
```css
/* TopNav dark effect */
background: rgba(45, 45, 45, 0.95);
backdrop-filter: blur(10px);
border-bottom: 1px solid rgba(210, 105, 30, 0.2);
box-shadow: 0 4px 12px rgba(0, 0, 0, 0.5);
```

**Deliverables**:
- [ ] Navigation renders correctly in both themes
- [ ] Theme toggle accessible in TopNav user menu
- [ ] Layout wrappers apply correct backgrounds
- [ ] Mobile navigation themed properly

---

### Wave 4: Admin Pages (main-dev branch)
**Timeline**: 3-4 days
**Branch**: main-dev
**Components**: 40+ admin-specific

**Implementation Order**:
1. **Dashboard Core** (Day 1):
   - DashboardHeader, MetricsGrid, KPICard, StatCard
   - ActivityFeed, QuickActionsPanel, AlertsBanner

2. **Management Pages** (Day 2):
   - Users, Configuration, Sessions
   - SystemStatus, ServiceHealthMatrix

3. **Analytics & Charts** (Day 3):
   - ChartsSection, APIRequestsChart, AIUsageDonut
   - Infrastructure, Reports pages

4. **Specialized Admin** (Day 4):
   - Prompts, API Keys, Alerts, Share Requests
   - Wizard, Testing pages

**Morphllm Automation**:
- All admin `Card` components use same pattern
- Batch update `StatCard` variants
- Bulk replace color classes

**Deliverables**:
- [ ] All 29 admin pages themed
- [ ] 40+ admin components updated
- [ ] Admin layout glass/dark effects working
- [ ] Charts render correctly in both modes

---

### Wave 5: Public Pages (frontend-dev branch)
**Timeline**: 3-4 days
**Branch**: frontend-dev
**Components**: 50+ public-facing

**Implementation Order**:
1. **Landing & Auth** (Day 1):
   - HeroSection, FeaturesSection
   - LoginForm, RegisterForm, OAuthButtons

2. **Dashboard & Games** (Day 2):
   - User dashboard, QuickActions
   - GameCard, GameGrid, CatalogFilters

3. **Library & Sessions** (Day 3):
   - UserGameCard, LibraryFilters
   - SessionSetupModal, GameStateViewer

4. **Static Pages** (Day 4):
   - FAQ, How It Works, About
   - Privacy, Terms, Cookies

**Deliverables**:
- [ ] All 18 main public pages themed
- [ ] Landing page glass hero effect working
- [ ] Auth pages dark mode functional
- [ ] Games catalog responsive theming

---

### Wave 6: Chat Interface (frontend-dev branch)
**Timeline**: 2-3 days
**Branch**: frontend-dev
**Components**: 15 chat-specific

**Implementation Order**:
1. **Core Chat** (Day 1):
   - ChatContent, MessageList, Message
   - MessageInput, ChatHeader

2. **Chat Features** (Day 2):
   - VirtualizedMessageList, GameSelector
   - FollowUpQuestions, MessageActions

3. **Chat Utilities** (Day 3):
   - ShareChatModal, ContextChip
   - CommentBox, MentionInput

**Special Considerations**:
- Message bubbles need high contrast in dark mode
- Code blocks in messages (syntax highlighting theme)
- Citation links visibility
- Typing indicator theming

**Deliverables**:
- [ ] Chat interface fully themed
- [ ] Message bubbles readable in both modes
- [ ] Sidebar dark/light transitions smooth
- [ ] Virtual scrolling performance maintained

---

### Wave 7: Feature Components (Both branches parallel)
**Timeline**: 4-5 days
**Branches**: Both (split by feature domain)

**main-dev** (Admin features):
- Share requests components
- Wizard steps
- Bulk export tools
- Alert rules management

**frontend-dev** (User features):
- Library components (10+)
- Session components (12+)
- Game state editor (8+)
- PDF viewers (8+)

**Morphllm Patterns**:
```typescript
// Pattern 1: Card hover effects
FROM: hover:shadow-lg hover:border-gray-300
TO: hover:-translate-y-1 hover:shadow-[0_8px_32px_rgba(139,90,60,0.15)] dark:hover:shadow-[0_6px_20px_rgba(210,105,30,0.3)]

// Pattern 2: Badge variants
FROM: bg-blue-100 text-blue-800
TO: bg-accent/10 text-accent dark:bg-accent/20 dark:text-accent-foreground

// Pattern 3: Icon backgrounds
FROM: bg-gray-100
TO: bg-[#fef3e2] dark:bg-[rgba(210,105,30,0.15)]
```

**Deliverables**:
- [ ] 50+ feature components themed
- [ ] All modals and dialogs updated
- [ ] Form components consistent styling
- [ ] Specialized components (PDF, Diff, Timeline) themed

---

### Wave 8: Polish & Effects
**Timeline**: 1-2 days
**Branch**: Both (merge to integration branch)

**Tasks**:
1. **Glass Morphism Effects** (light mode):
   - Apply backdrop-filter to all cards
   - Add floating orb animations to pages
   - Implement gradient text on headings
   - Noise texture overlays

2. **Dark Mode Enhancements**:
   - Glow effects on status indicators
   - Text shadows on key metrics
   - Enhanced border contrast
   - Gradient buttons with shadow effects

3. **Mobile Optimizations**:
   - Disable `backdrop-filter` on <768px viewports
   - Static backgrounds instead of animated
   - Simplified shadows (reduce blur radius)
   - Remove floating orbs on mobile

4. **Animations** (Framer Motion):
   - Staggered card reveals on page load
   - Page transition animations
   - Modal enter/exit animations
   - Hover orchestrations

**Performance Validation**:
- Lighthouse audit: Target >90 performance score
- Mobile FCP <1s, TTI <2s
- Desktop FCP <0.8s, TTI <1.5s

**Deliverables**:
- [ ] Glass effects applied and performant
- [ ] Dark mode polished and consistent
- [ ] Mobile performance optimized
- [ ] Animations smooth (60fps)

---

### Wave 9: Testing & Validation
**Timeline**: 2-3 days
**Branch**: Both

**Test Updates** (100+ test files):

1. **Unit Tests** (Vitest):
   - Update all component tests
   - Add theme prop testing
   - Verify dark/light variants render
   - Mock ThemeProvider in tests

2. **Integration Tests**:
   - Dashboard flows with theme switching
   - Form submissions in both themes
   - Navigation across themed pages

3. **Visual Regression** (Chromatic):
   - All Storybook stories with light variant
   - All Storybook stories with dark variant
   - Multiple viewports (375px, 768px, 1920px)
   - Focus states, hover states, loading states

4. **E2E Tests** (Playwright):
   - Create `theme-switching.spec.ts`:
     - Navigate to /admin
     - Toggle theme
     - Screenshot comparison
     - Verify persistence
   - Update existing E2E tests for theme support
   - Add accessibility tests (axe-core)

5. **Performance Tests**:
   - Lighthouse CI integration
   - Bundle size analysis
   - Runtime performance profiling

**Deliverables**:
- [ ] All unit tests passing
- [ ] Chromatic approved (0 visual regressions)
- [ ] E2E suite covers theme switching
- [ ] Accessibility audit passed (WCAG AA)
- [ ] Performance budgets met

---

## Technical Architecture

### Theme System Design

**Provider Hierarchy**:
```tsx
// apps/web/src/app/layout.tsx
<html suppressHydrationWarning>
  <body>
    <ThemeProvider
      attribute="class"
      defaultTheme="light"
      enableSystem={false}
      storageKey="meepleai-theme"
    >
      <UIProvider>
        {children}
      </UIProvider>
    </ThemeProvider>
  </body>
</html>
```

**CSS Variables Structure**:
```css
/* apps/web/src/styles/theme-variables.css */

:root {
  /* Light mode (glass morphism) */
  --background: 48 40% 97%;  /* #fef3e2 */
  --foreground: 0 0% 18%;     /* #2d2d2d */
  --card: 0 0% 100% / 0.7;    /* rgba(255,255,255,0.7) */
  --card-foreground: 0 0% 18%;
  --primary: 25 72% 47%;      /* #d2691e */
  --primary-foreground: 0 0% 100%;
  /* ... all tokens */
}

.dark {
  /* Dark mode (professional) */
  --background: 0 0% 10%;     /* #1a1a1a */
  --foreground: 48 20% 90%;   /* #e8e4d8 */
  --card: 0 0% 18%;           /* #2d2d2d */
  --card-foreground: 48 20% 90%;
  --primary: 45 96% 56%;      /* #fbbf24 amber */
  --primary-foreground: 0 0% 0%;
  /* ... all tokens */
}
```

**Tailwind Configuration**:
```javascript
// tailwind.config.js
module.exports = {
  darkMode: ['class'],
  theme: {
    extend: {
      colors: {
        background: 'hsl(var(--background))',
        foreground: 'hsl(var(--foreground))',
        card: { DEFAULT: 'hsl(var(--card))', foreground: 'hsl(var(--card-foreground))' },
        primary: { DEFAULT: 'hsl(var(--primary))', foreground: 'hsl(var(--primary-foreground))' },
        // ... all semantic tokens
      },
      backdropBlur: {
        'glass': '12px',
      },
      animation: {
        'float': 'float 20s ease-in-out infinite',
      },
      keyframes: {
        float: {
          '0%, 100%': { transform: 'translateY(0) scale(1)' },
          '50%': { transform: 'translateY(-20px) scale(1.05)' },
        },
      },
    },
  },
};
```

### Theme Toggle Component

**Location**: `apps/web/src/components/layout/ThemeToggle.tsx`

**Features**:
- Sun/Moon icons (lucide-react)
- Smooth transition animation
- Placed in TopNav user dropdown
- Keyboard accessible (Space/Enter)
- Persistent via localStorage

**Implementation**:
```tsx
'use client';

import { Moon, Sun } from 'lucide-react';
import { useTheme } from 'next-themes';
import { Button } from '@/components/ui/primitives/button';

export function ThemeToggle() {
  const { theme, setTheme } = useTheme();

  return (
    <Button
      variant="ghost"
      size="sm"
      onClick={() => setTheme(theme === 'light' ? 'dark' : 'light')}
      aria-label="Toggle theme"
    >
      <Sun className="h-5 w-5 rotate-0 scale-100 transition-all dark:-rotate-90 dark:scale-0" />
      <Moon className="absolute h-5 w-5 rotate-90 scale-0 transition-all dark:rotate-0 dark:scale-100" />
      <span className="sr-only">Toggle theme</span>
    </Button>
  );
}
```

---

## Morphllm Automation Patterns

### Pattern 1: Card Base Classes
```typescript
// Target: All components extending Card
// Files: ~80 components using Card

Pattern: {
  search: 'className="bg-white border-gray-',
  replace: 'className="bg-card/90 backdrop-blur-glass border-border dark:bg-[#2d2d2d] dark:border-[rgba(210,105,30,0.2)]',
  scope: 'apps/web/src/components/**/*.tsx',
}
```

### Pattern 2: Hover Effects
```typescript
// Target: Interactive cards with hover
// Files: ~50 cards with hover states

Pattern: {
  search: 'hover:shadow-lg',
  replace: 'hover:-translate-y-1 hover:shadow-[0_8px_32px_rgba(139,90,60,0.15)] dark:hover:shadow-[0_6px_20px_rgba(210,105,30,0.3)]',
  scope: 'apps/web/src/components/**/Card*.tsx',
}
```

### Pattern 3: Icon Backgrounds
```typescript
// Target: Icon containers
// Files: ~30 components with icon backgrounds

Pattern: {
  search: 'bg-gray-100',
  replace: 'bg-[#fef3e2] dark:bg-[rgba(210,105,30,0.15)]',
  scope: 'apps/web/src/components/**/*Icon*.tsx',
}
```

### Pattern 4: Text Colors
```typescript
// Target: Secondary text
// Files: ~100 components with muted text

Pattern: {
  search: 'text-gray-600',
  replace: 'text-muted-foreground dark:text-[#999]',
  scope: 'apps/web/src/components/**/*.tsx',
}
```

### Pattern 5: Gradient Text
```typescript
// Target: Page titles, section headings
// Files: ~20 headings

Pattern: {
  search: 'text-3xl font-bold text-gray-900',
  replace: 'text-3xl font-bold bg-gradient-to-r from-[#d2691e] to-[#f59e0b] bg-clip-text text-transparent dark:from-[#fbbf24] dark:to-[#d2691e]',
  scope: 'apps/web/src/app/**/page.tsx',
}
```

---

## Risks & Mitigation

### Risk 1: Scope Creep (250+ components)
**Probability**: High
**Impact**: Timeline overrun, incomplete migration

**Mitigation**:
- Wave-based approach with clear boundaries
- Automated pattern updates via Morphllm (save 60% time)
- Parallel branch execution (2x speed)
- Feature flags for gradual rollout if needed
- Quality gates: each wave must pass tests before next

### Risk 2: Visual Regressions
**Probability**: Medium
**Impact**: Broken UI, user complaints

**Mitigation**:
- Chromatic visual regression on all 100+ stories
- Manual QA checklist for critical paths
- Playwright E2E with screenshot comparison
- Rollback plan: revert commits if critical issues

### Risk 3: Performance Degradation (backdrop-filter)
**Probability**: Medium (mobile devices)
**Impact**: Slow page loads, poor mobile UX

**Mitigation**:
- Mobile detection: disable blur on <768px
- Static backgrounds as fallback
- Performance budgets enforced in CI
- Lighthouse scores monitored per wave

### Risk 4: Dark Mode Contrast Issues
**Probability**: Medium
**Impact**: Accessibility failures, readability problems

**Mitigation**:
- WCAG AA contrast validation (4.5:1 text, 3:1 UI)
- Axe-core automated testing
- Manual accessibility audit
- User testing with dark mode users

### Risk 5: Merge Conflicts (Parallel Branches)
**Probability**: High
**Impact**: Integration delays, wasted effort

**Mitigation**:
- Clear ownership: main-dev=admin, frontend-dev=public
- Daily sync meetings (if team)
- Shared foundation (Wave 1-3) before split
- Integration branch for final merge
- Comprehensive conflict resolution strategy

### Risk 6: API Endpoints Missing
**Probability**: Known (acknowledged by user)
**Impact**: Non-blocking (mock data acceptable)

**Mitigation**:
- Use mock data from `.stories.tsx` files
- Document missing endpoints in issue comments
- Implement UI fully, backend can catch up later
- No API contract changes

---

## Success Criteria

### Functional Requirements
- [ ] Theme toggle works globally across all pages
- [ ] Theme preference persists (localStorage)
- [ ] All 250+ components render in both light/dark modes
- [ ] No layout shifts during theme switching
- [ ] All forms submit correctly in both themes
- [ ] Navigation functional in both modes

### Visual Quality
- [ ] Glass morphism effects visible in light mode (desktop)
- [ ] Dark mode has professional polish (shadows, glows)
- [ ] Consistent color palette across all pages
- [ ] Typography hierarchy clear in both modes
- [ ] Icons and badges visible in both themes
- [ ] Charts readable with theme-aware colors

### Performance
- [ ] FCP <1s on desktop (both themes)
- [ ] TTI <2s on desktop (both themes)
- [ ] Mobile FCP <1.5s (simplified effects)
- [ ] CLS <0.1 (no layout shifts)
- [ ] Bundle size increase <50KB
- [ ] No runtime performance regressions

### Accessibility
- [ ] WCAG AA contrast ratios met (4.5:1 text, 3:1 UI)
- [ ] Theme toggle keyboard accessible
- [ ] Focus indicators visible in both modes
- [ ] Screen reader announcements correct
- [ ] Color not sole indicator (icons + text)
- [ ] Reduced motion support (prefers-reduced-motion)

### Code Quality
- [ ] 100% Tailwind CSS (no inline styles)
- [ ] All colors via CSS variables
- [ ] CVA variants for component polymorphism
- [ ] Consistent naming conventions
- [ ] No legacy CSS files remaining
- [ ] Storybook stories updated (100+)

### Testing
- [ ] 85%+ unit test coverage maintained
- [ ] All integration tests passing
- [ ] Chromatic visual regression approved
- [ ] Playwright E2E suite passing
- [ ] Accessibility tests (axe-core) passing
- [ ] Performance budgets met in CI

---

## Timeline Estimate

**Total Duration**: 14-18 days (2.5-3.5 weeks)

**Critical Path**:
```
Wave 1 (Foundation)          → 1-2 days
Wave 2 (Primitives)          → 2-3 days
Wave 3 (Layouts)             → 1-2 days
─────────────────────────────────────────── Checkpoint (Day 6)
Wave 4 (Admin) ┐
               ├─ Parallel → 3-4 days
Wave 5 (Public)┘
Wave 6 (Chat)                → 2-3 days
Wave 7 (Features) - Parallel → 4-5 days
─────────────────────────────────────────── Checkpoint (Day 14)
Wave 8 (Polish)              → 1-2 days
Wave 9 (Testing)             → 2-3 days
─────────────────────────────────────────── Done (Day 18)
```

**Parallelization Gains**:
- Sequential: ~24 days
- Parallel (2 branches): ~18 days
- **Time Saved**: 25% reduction

---

## Resource Requirements

### Dependencies
```json
{
  "next-themes": "^0.2.1",
  "framer-motion": "^10.16.16",
  "class-variance-authority": "^0.7.0",
  "@radix-ui/react-*": "latest",
  "tailwindcss": "^4.0.0",
  "lucide-react": "latest"
}
```

### MCP Servers
- **Morphllm**: Bulk pattern transformations (Wave 2, 4, 5, 7)
- **Sequential**: Complex reasoning for architecture decisions
- **Playwright**: E2E testing and visual validation
- **Context7**: Framework best practices (Tailwind dark mode, next-themes)
- **Serena**: Session persistence and memory management

### Tooling
- **Chromatic**: Visual regression testing
- **Lighthouse CI**: Performance monitoring
- **axe-core**: Accessibility testing
- **Storybook**: Component development and documentation

---

## Next Actions

### Immediate (After Plan Approval)
1. Create GitHub issue with this complete plan
2. Create feature branch: `feature/issue-XXXX-site-wide-redesign`
3. Initialize PDCA documentation structure
4. Setup parallel branch strategy (main-dev + frontend-dev)
5. Begin Wave 1 implementation

### Documentation Tracking
```
docs/pdca/site-wide-redesign/
├── plan.md              ← This file
├── do.md                ← Implementation log (created during Wave 1)
├── check.md             ← Evaluation after each wave
└── act.md               ← Final learnings and patterns
```

### Memory Tracking
```
session/checkpoint       → Progress snapshots every 30min
plan/redesign/waves      → Wave completion status
execution/redesign/log   → Implementation events
learning/patterns/theme  → Reusable theme patterns
```

---

## Assumptions & Constraints

### Assumptions
- Next.js 14 App Router remains current framework
- Tailwind CSS v4 is stable and suitable
- shadcn/ui components continue as UI foundation
- Mock data acceptable for missing API endpoints
- Modern browser targets (no IE11, no Safari <15)

### Constraints
- **No breaking changes** to component APIs (props remain same)
- **UI-only migration** (no backend API changes)
- **Maintain functionality** (theme updates don't alter behavior)
- **Preserve accessibility** (WCAG AA minimum standard)
- **Performance budget**: No >50KB bundle increase

### Out of Scope
- Backend API theme endpoints
- Email template theming
- Print stylesheets
- Third-party iframe content (Grafana dashboards styled externally)
- Legacy browser support (Safari <15, old Chrome)

---

**Plan Status**: ✅ Complete
**Next**: Generate GitHub issue → Begin Wave 1 execution
**Estimated Start**: 2026-01-23
**Estimated Completion**: 2026-02-10
