# Frontend Refactor Implementation Roadmap

**Date**: 2025-11-13
**Duration**: 3-4 weeks (1 developer)
**Status**: Ready for execution

---

## 🎯 Executive Summary

This roadmap transforms the MeepleAI frontend from functional to excellent through systematic refactoring and enhancement. Based on the comprehensive UI analysis, we've created 15 GitHub issues organized in 3 sprints, each building on the previous work.

**Key Deliverables**:
- Design system with complete token system ✅ (already created)
- 15 GitHub issues with detailed implementation plans ✅ (templates ready)
- Automated issue creation script ✅ (ready to run)
- Comprehensive documentation ✅ (this document + 4 others)

**Expected Impact**:
- 75% reduction in largest file size (1564 → ~400 lines)
- 95% reduction in inline styles (200+ → ~10)
- 80% reduction in unnecessary re-renders
- 50% improvement in mobile UX score (6/10 → 9/10)
- WCAG 2.1 AA compliance achieved

---

## 📋 Sprint Overview

### Sprint 1: Critical Foundation (5 days)

**Goal**: Fix blocking issues that prevent scaling and maintainability

| Issue | Title | Effort | Priority |
|-------|-------|--------|----------|
| #01 | Unify Login Flow | 4h | Critical |
| #02 | Refactor Upload Page | 2d | Critical |
| #03 | Fix ChatProvider Complexity | 1.5d | Critical |
| #04 | Standardize Styling Approach | 1d | Critical |

**Rationale**: These issues block all other work. The upload page and ChatProvider are too complex to modify safely, and inconsistent styling makes any UI work difficult.

---

### Sprint 2: Important Improvements (3.5 days)

**Goal**: Enhance UX, performance, and accessibility

| Issue | Title | Effort | Priority |
|-------|-------|--------|----------|
| #05 | Mobile Improvements | 1d | High |
| #06 | Performance Optimization | 1d | High |
| #07 | Accessibility Audit | 0.5d | High |
| #08 | Error Handling Unified | 0.5d | Medium |
| #09 | Loading States Unified | 0.5d | Medium |

**Rationale**: With clean foundation from Sprint 1, we can now add polish and ensure the app works well for all users and devices.

---

### Sprint 3: Polish & Features (7 days)

**Goal**: Developer experience and advanced features

| Issue | Title | Effort | Priority |
|-------|-------|--------|----------|
| #10 | Storybook Setup | 2d | Medium |
| #11 | Component Tests | 1d | Medium |
| #12 | Landing Page Polish | 0.5d | Medium |
| #13 | Keyboard Shortcuts | 1d | Low |
| #14 | Advanced Search | 1.5d | Low |
| #15 | Theme Customization | 1d | Low |

**Rationale**: These are nice-to-have improvements that polish the experience and improve developer workflow. Can be deprioritized if timeline is tight.

---

## 🗂️ Issue Details

### Sprint 1: Critical Foundation

#### Issue #01: Unify Login Flow

**File**: `.github-issues-templates/sprint-1-critical/01-unify-login-flow.md`

**Problem**:
- Auth logic duplicated in 3 places (login page, landing page modal, chat page)
- Inconsistent error handling
- Code duplication makes bug fixes error-prone

**Solution**:
```tsx
// Extract to shared component
<AuthModal
  mode="login"
  onSuccess={(user) => router.push('/chat')}
  onSwitchMode={() => setMode('register')}
/>
```

**Key Changes**:
- Create `components/auth/AuthModal.tsx` (150 lines)
- Create `components/auth/AuthForm.tsx` (80 lines)
- Update login page to use AuthModal
- Update landing page to use AuthModal
- Remove duplicate auth logic (250 lines eliminated)

**Testing**:
- Login from all 3 locations
- Register flow
- OAuth flow (Google/Discord)
- Error states
- 2FA flow

**Effort**: 4 hours
**Files Changed**: 5
**Lines Added/Removed**: +230 / -250

---

#### Issue #02: Refactor Upload Page

**File**: `.github-issues-templates/sprint-1-critical/02-refactor-upload-page.md`

**Problem**:
- 1564 lines in single file
- 20+ useState variables
- Mixed concerns (wizard, validation, upload, game selection)
- Hard to test, modify, or debug

**Solution**: Extract into focused components

```
upload.tsx (150 lines)
├── UploadWizard.tsx (100 lines) - Wizard logic
├── GameSelector.tsx (80 lines) - Game selection
├── FileUploadZone.tsx (100 lines) - Drag & drop
├── ValidationDisplay.tsx (70 lines) - Validation results
└── UploadProgress.tsx (50 lines) - Progress tracking
```

**Key Changes**:
1. **Create UploadWizard component**:
   ```tsx
   type WizardStep = 'select-game' | 'upload-file' | 'validate' | 'complete'

   const UploadWizard: React.FC = () => {
     const [step, setStep] = useState<WizardStep>('select-game')

     return (
       <WizardContainer>
         <WizardSteps steps={steps} currentStep={step} />
         <WizardContent>
           {step === 'select-game' && <GameSelector onSelect={handleGameSelect} />}
           {step === 'upload-file' && <FileUploadZone onUpload={handleUpload} />}
           {step === 'validate' && <ValidationDisplay results={results} />}
           {step === 'complete' && <UploadComplete game={game} />}
         </WizardContent>
       </WizardContainer>
     )
   }
   ```

2. **Extract GameSelector** with search and filtering
3. **Extract FileUploadZone** with drag & drop
4. **Extract ValidationDisplay** for quality reports
5. **Simplify upload.tsx** to coordinate components

**Testing**:
- Full wizard flow (all 4 steps)
- File validation (PDF only, size limits)
- Upload progress tracking
- Error handling (network, validation)
- Cancel upload
- Multiple files

**Effort**: 2 days
**Files Changed**: 8 (1 refactored + 7 new)
**Lines Added/Removed**: +600 / -1400 (net: -800)

---

#### Issue #03: Fix ChatProvider Complexity

**File**: `.github-issues-templates/sprint-1-critical/03-fix-chatprovider-complexity.md`

**Problem**:
- 639 lines, 35+ functions
- Too many responsibilities (auth, game, chat, UI state)
- Map-based state not serializable
- Hard to debug state changes

**Solution**: Split into 4 focused contexts

```
ChatProvider (639 lines)
  ↓ Split into ↓
├── AuthContext (60 lines) - User, session
├── GameContext (80 lines) - Selected game, play session
├── ChatContext (150 lines) - Messages, threads
└── UiContext (40 lines) - Sidebar, modals, toasts
```

**New State Structure**:
```tsx
// Before (not serializable)
const [messages, setMessages] = useState(new Map())

// After (serializable, normalized)
interface ChatState {
  messages: Record<string, Message>  // id → message
  threads: Record<string, Thread>    // id → thread
  activeThreadId: string | null
}
```

**Key Changes**:
1. **Create AuthContext** (60 lines):
   ```tsx
   interface AuthState {
     user: User | null
     session: Session | null
     isLoading: boolean
   }

   const useAuth = () => {
     const context = useContext(AuthContext)
     if (!context) throw new Error('useAuth must be used within AuthProvider')
     return context
   }
   ```

2. **Create GameContext** (80 lines):
   ```tsx
   interface GameState {
     selectedGame: Game | null
     playSession: PlaySession | null
     recentGames: Game[]
   }
   ```

3. **Create ChatContext** (150 lines) - Core messaging logic

4. **Create UiContext** (40 lines) - UI state only

5. **Update all consumers** to use specific contexts

**Testing**:
- Auth flow (login, logout, session)
- Game selection
- Message sending/receiving
- Thread switching
- Context isolation (changes in one don't affect others)

**Effort**: 1.5 days
**Files Changed**: 15 (4 new contexts + 11 updated consumers)
**Lines Added/Removed**: +400 / -640 (net: -240)

---

#### Issue #04: Standardize Styling Approach

**File**: `.github-issues-templates/sprint-1-critical/04-standardize-styling.md`

**Problem**:
- 200+ inline styles with magic numbers
- 4 different styling approaches:
  - Inline styles: `style={{ padding: 24 }}`
  - Tailwind: `className="p-6"`
  - CSS modules: `styles.container`
  - Styled-components: `const Box = styled.div`
- No consistent spacing/color system
- Hard to maintain, inconsistent appearance

**Solution**: Migrate everything to Tailwind + design tokens

**Migration Map**:
```tsx
// Before
<div style={{
  padding: 24,
  background: '#f8f9fa',
  borderRadius: 8,
  boxShadow: '0 2px 4px rgba(0,0,0,0.1)'
}}>

// After
<div className="p-6 bg-sidebar rounded-md shadow-sm">
```

**Common Patterns**:
| Before | After | Token |
|--------|-------|-------|
| `style={{ padding: 24 }}` | `className="p-6"` | `--space-6` |
| `style={{ gap: 16 }}` | `className="gap-4"` | `--space-4` |
| `style={{ background: '#f8f9fa' }}` | `className="bg-sidebar"` | `--sidebar` |
| `style={{ borderRadius: 8 }}` | `className="rounded-md"` | `--radius-md` |
| `style={{ fontSize: 14 }}` | `className="text-sm"` | `--text-sm` |

**Key Changes**:
1. **Remove all inline styles** (200+ instances)
2. **Convert to Tailwind classes** using design tokens
3. **Remove CSS modules** (migrate to Tailwind)
4. **Remove styled-components** (migrate to Tailwind)
5. **Update documentation** with patterns

**Files to Update** (prioritized by inline style count):
1. `upload.tsx` (50+ inline styles)
2. `index.tsx` (40+ inline styles)
3. `ChatSidebar.tsx` (30+ inline styles)
4. `ChatContent.tsx` (25+ inline styles)
5. `login.tsx` (15+ inline styles)
6. Others (40+ total)

**Testing**:
- Visual regression testing (screenshots before/after)
- Light/dark mode
- Responsive breakpoints
- No layout shifts

**Effort**: 1 day
**Files Changed**: 20+
**Lines Changed**: ~400 (mostly replacements)

---

### Sprint 2: Important Improvements

#### Issue #05: Mobile Improvements

**File**: `.github-issues-templates/sprint-2-important/05-mobile-improvements.md`

**Problems**:
- Sidebar pushes content on mobile
- Touch targets too small (< 44px)
- Horizontal scroll on narrow screens
- No mobile-specific navigation

**Solutions**:
1. **Drawer-style sidebar** (Radix Dialog)
2. **Larger touch targets** (min 44x44px)
3. **Mobile-first responsive** (320px base)
4. **Bottom nav** for key actions

**Key Changes**:
```tsx
// Mobile drawer sidebar
const Sidebar = () => {
  const isMobile = useMediaQuery('(max-width: 768px)')

  if (isMobile) {
    return (
      <Drawer open={open} onOpenChange={setOpen}>
        <DrawerTrigger>Menu</DrawerTrigger>
        <DrawerContent>{/* sidebar content */}</DrawerContent>
      </Drawer>
    )
  }

  return <aside className="w-80">{/* sidebar content */}</aside>
}
```

**Testing**:
- iOS Safari (iPhone 12, 13, 14)
- Android Chrome (various devices)
- Landscape orientation
- Touch interactions

**Effort**: 1 day

---

#### Issue #06: Performance Optimization

**File**: `.github-issues-templates/sprint-2-important/06-performance-optimization.md`

**Problems**:
- Unnecessary re-renders (every keystroke re-renders entire chat)
- Large lists not virtualized
- No memoization on expensive computations
- Bundle size ~450 KB (target: <350 KB)

**Solutions**:
1. **Memoization**: `React.memo`, `useMemo`, `useCallback`
2. **Virtualization**: Use `react-window` for long lists
3. **Code splitting**: Dynamic imports for routes
4. **Bundle optimization**: Analyze and remove unused code

**Key Changes**:
```tsx
// Before: Re-renders on every message
const ChatContent = ({ messages }) => {
  return messages.map(m => <Message key={m.id} {...m} />)
}

// After: Memoized
const ChatContent = React.memo(({ messages }) => {
  return (
    <VirtualList
      height={600}
      itemCount={messages.length}
      itemSize={80}
      itemData={messages}
    >
      {({ index, style, data }) => (
        <Message key={data[index].id} {...data[index]} style={style} />
      )}
    </VirtualList>
  )
})
```

**Metrics**:
- Target: 80% reduction in re-renders
- Target: Bundle size < 350 KB
- Target: First paint < 1.5s

**Effort**: 1 day

---

#### Issue #07: Accessibility Audit

**File**: `.github-issues-templates/sprint-2-important/07-accessibility-audit.md`

**Problems**:
- Missing ARIA labels
- Poor keyboard navigation
- Low contrast ratios
- No screen reader testing

**Solutions**:
1. **ARIA labels** on all interactive elements
2. **Keyboard navigation** (Tab, Enter, Escape)
3. **Focus management** (modals, drawers)
4. **Color contrast** (WCAG AA: 4.5:1)

**Testing Tools**:
- axe DevTools
- NVDA/JAWS screen readers
- Keyboard-only navigation
- Color contrast analyzer

**Effort**: 0.5 day

---

#### Issue #08: Error Handling Unified

**File**: `.github-issues-templates/sprint-2-important/08-error-handling-unified.md`

**Problem**: 5 different error display patterns

**Solution**: Single `ErrorBoundary` + `useError` hook

```tsx
const useError = () => {
  const { toast } = useToast()

  const handleError = (error: Error, context?: string) => {
    logger.error(error, { context })
    toast({
      variant: 'destructive',
      title: 'Error',
      description: getUserFriendlyMessage(error)
    })
  }

  return { handleError }
}
```

**Effort**: 0.5 day

---

#### Issue #09: Loading States Unified

**File**: `.github-issues-templates/sprint-2-important/09-loading-states-unified.md`

**Problem**: Inconsistent loading patterns (spinners, skeletons, text)

**Solution**: Standard loading components

```tsx
<Skeleton variant="text" width={200} />
<Skeleton variant="circular" width={40} height={40} />
<Skeleton variant="rectangular" width="100%" height={200} />
```

**Effort**: 0.5 day

---

### Sprint 3: Polish & Features

#### Issue #10: Storybook Setup

**File**: `.github-issues-templates/sprint-3-nice-to-have/10-storybook-setup.md`

**Goal**: Document all components with interactive examples

**Setup**:
```bash
pnpm add -D @storybook/react @storybook/addon-essentials
npx storybook init
```

**Components to Document**:
- AuthModal, AuthForm
- GameSelector, GameCard
- FileUploadZone
- ChatContent, ChatMessage
- Button, Input, Card (shadcn)

**Effort**: 2 days

---

#### Issue #11: Component Tests

**File**: `.github-issues-templates/sprint-3-nice-to-have/11-component-tests.md`

**Goal**: 95%+ test coverage for new components

**Test Stack**: Jest + React Testing Library

**Components to Test**:
- UploadWizard (state transitions)
- GameSelector (search, filtering)
- AuthModal (form validation)
- All contexts (AuthContext, GameContext, etc.)

**Effort**: 1 day

---

#### Issue #12: Landing Page Polish

**File**: `.github-issues-templates/sprint-3-nice-to-have/12-landing-page-polish.md`

**Improvements**:
- Image optimization (Next.js Image)
- Code splitting (dynamic imports)
- Remove unused CSS
- Improve LCP, FID, CLS

**Effort**: 0.5 day

---

#### Issue #13: Keyboard Shortcuts

**File**: `.github-issues-templates/sprint-3-nice-to-have/13-keyboard-shortcuts.md`

**Shortcuts to Add**:
- `Cmd+K` / `Ctrl+K`: Quick search
- `Cmd+/` / `Ctrl+/`: Show shortcuts
- `Escape`: Close modals/drawers
- `Cmd+Enter`: Send message

**Implementation**:
```tsx
import { useHotkeys } from 'react-hotkeys-hook'

useHotkeys('cmd+k', () => openSearch())
useHotkeys('cmd+/', () => showShortcuts())
```

**Effort**: 1 day

---

#### Issue #14: Advanced Search

**File**: `.github-issues-templates/sprint-3-nice-to-have/14-advanced-search.md**

**Features**:
- Filters (player count, duration, complexity)
- Sort options (name, rating, recency)
- Search history
- Saved searches

**Effort**: 1.5 days

---

#### Issue #15: Theme Customization

**File**: `.github-issues-templates/sprint-3-nice-to-have/15-theme-customization.md**

**Features**:
- Theme picker (light, dark, auto, custom)
- Accent color picker
- Font size adjustment
- Contrast mode

**Effort**: 1 day

---

## 📅 Timeline

### Week 1: Foundation

**Days 1-2** (Sprint 1, Part 1):
- Issue #01: Unify Login Flow (4h)
- Issue #02: Refactor Upload Page - Start (1.5d)

**Day 3** (Sprint 1, Part 2):
- Issue #02: Refactor Upload Page - Complete (0.5d)
- Issue #03: Fix ChatProvider - Start (0.5d)

**Day 4** (Sprint 1, Part 3):
- Issue #03: Fix ChatProvider - Complete (1d)

**Day 5** (Sprint 1, Part 4 + Testing):
- Issue #04: Standardize Styling (1d)
- Testing & cleanup

**Checkpoint**:
- ✅ All files < 500 lines
- ✅ Zero inline styles
- ✅ Tests passing
- ✅ Ready for Sprint 2

---

### Week 2: Enhancement

**Days 1-2** (Sprint 2, Part 1):
- Issue #05: Mobile Improvements (1d)
- Issue #06: Performance Optimization (1d)

**Day 3** (Sprint 2, Part 2):
- Issue #07: Accessibility Audit (0.5d)
- Issue #08: Error Handling (0.5d)

**Day 4** (Sprint 2, Part 3 + Testing):
- Issue #09: Loading States (0.5d)
- Testing & Lighthouse audits (0.5d)

**Day 5** (Buffer + Documentation):
- Fix any regressions
- Update documentation
- Manual testing on real devices

**Checkpoint**:
- ✅ Mobile score 9/10
- ✅ Performance improved
- ✅ A11y compliant
- ✅ Ready for Sprint 3

---

### Week 3-4: Polish

**Week 3, Days 1-2** (Sprint 3, Part 1):
- Issue #10: Storybook Setup (2d)

**Week 3, Day 3** (Sprint 3, Part 2):
- Issue #11: Component Tests (1d)

**Week 3, Days 4-5** (Sprint 3, Part 3):
- Issue #12: Landing Page Polish (0.5d)
- Issue #13: Keyboard Shortcuts (1d)

**Week 4, Days 1-2** (Sprint 3, Part 4):
- Issue #14: Advanced Search (1.5d)

**Week 4, Day 3** (Sprint 3, Part 5):
- Issue #15: Theme Customization (1d)

**Week 4, Days 4-5** (Final Testing):
- Full regression testing
- Performance audits
- Documentation updates
- Prepare release notes

**Final Checkpoint**:
- ✅ All 15 issues complete
- ✅ 95%+ test coverage
- ✅ Storybook deployed
- ✅ Ready for production

---

## 🎯 Success Criteria

### Technical Metrics

| Metric | Baseline | Target | Measurement |
|--------|----------|--------|-------------|
| Largest file | 1564 lines | < 500 lines | LOC counter |
| Inline styles | 200+ | < 10 | `grep "style={{" -r src` |
| ChatProvider | 639 lines | < 250 lines | LOC counter |
| Bundle size | 450 KB | < 350 KB | `next build` output |
| Re-renders | Baseline | -80% | React DevTools Profiler |
| Test coverage | 90% | 95%+ | Jest coverage report |

### UX Metrics

| Metric | Baseline | Target | Measurement |
|--------|----------|--------|-------------|
| Mobile score | 6/10 | 9/10 | Internal review |
| A11y score | 7/10 | 9/10 | axe DevTools |
| Lighthouse | 85 | 95+ | Lighthouse CI |
| First paint | ~2s | < 1.5s | Lighthouse |
| Time to interactive | ~3s | < 2.5s | Lighthouse |

### Quality Gates

**Before Merging Each Sprint**:
- ✅ All tests passing (95%+ coverage)
- ✅ No ESLint warnings
- ✅ No TypeScript errors
- ✅ Lighthouse score ≥ 90
- ✅ Visual regression tests pass
- ✅ Manual testing complete

**Before Sprint 2**:
- ✅ Sprint 1 complete
- ✅ Upload page < 500 lines
- ✅ ChatProvider split into 4 contexts
- ✅ Zero inline styles

**Before Sprint 3**:
- ✅ Sprint 2 complete
- ✅ Mobile responsive
- ✅ Performance improved
- ✅ A11y compliant

**Before Production**:
- ✅ All 3 sprints complete
- ✅ All metrics hit targets
- ✅ Storybook deployed
- ✅ Documentation updated

---

## 🔄 Dependencies

### Sprint 1 → Sprint 2

Sprint 2 issues **depend on** Sprint 1 completion:

- **Issue #05 (Mobile)** needs design tokens from #04
- **Issue #06 (Performance)** benefits from context split in #03
- **Issue #08 (Errors)** uses refactored components from #02

**Critical**: Must complete Sprint 1 before starting Sprint 2

### Sprint 2 → Sprint 3

Sprint 3 is **mostly independent** but benefits from:

- **Issue #10 (Storybook)** documents Sprint 1 components
- **Issue #11 (Tests)** covers Sprint 1 refactor
- **Issue #13 (Shortcuts)** uses patterns from Sprint 2

**Flexible**: Can start Sprint 3 in parallel with Sprint 2 completion

### Within Sprints

**Sprint 1 order** (must be sequential):
1. Issue #01 (Login) - No dependencies
2. Issue #02 (Upload) - No dependencies
3. Issue #03 (ChatProvider) - No dependencies
4. Issue #04 (Styling) - **Depends on #01, #02, #03** (needs clean components)

**Sprint 2 order** (flexible):
- Issues #05-09 can be done in any order

**Sprint 3 order** (flexible):
- Issues #10-15 mostly independent

---

## 🛠️ Getting Started

### 1. Create GitHub Issues

```bash
cd .github-issues-templates

# Preview what will be created
./create-issues.sh --dry-run

# Create all 15 issues
./create-issues.sh

# Or create by sprint
./create-issues.sh --sprint 1  # Only Sprint 1 (4 issues)
./create-issues.sh --sprint 2  # Only Sprint 2 (5 issues)
./create-issues.sh --sprint 3  # Only Sprint 3 (6 issues)
```

### 2. Create Project Board

```bash
# Create board
gh project create \
  --title "Frontend Refactor" \
  --body "MeepleAI frontend refactoring roadmap"

# Add issues to board
gh project item-add <PROJECT_ID> --issue <ISSUE_NUMBER>
```

### 3. Assign Issues

```bash
# Assign Sprint 1 issues to yourself
gh issue list --label "sprint-1" --json number --jq '.[].number' | \
  xargs -I {} gh issue edit {} --add-assignee @me
```

### 4. Start Working

```bash
# Create branch for Issue #01
git checkout -b feat/issue-01-unify-login-flow

# Make changes
# ...

# Commit with conventional commits
git commit -m "feat(auth): extract AuthModal component

- Create shared AuthModal component
- Update login page to use AuthModal
- Remove duplicate auth logic

Closes #1"

# Push and create PR
git push -u origin feat/issue-01-unify-login-flow
gh pr create --title "Unify Login Flow" --body "Closes #1"
```

---

## 📊 Tracking Progress

### View Issues

```bash
# All refactor issues
gh issue list --label "frontend,refactor"

# By sprint
gh issue list --label "sprint-1"
gh issue list --label "sprint-2"
gh issue list --label "sprint-3"

# By priority
gh issue list --label "priority-critical"
gh issue list --label "priority-high"
gh issue list --label "priority-medium"

# In progress
gh issue list --assignee @me --label "frontend"
```

### Update Status

```bash
# Mark issue as in progress
gh issue edit 1 --add-label "in-progress"

# Close issue when done
gh issue close 1 --comment "Completed in PR #123"
```

### Generate Reports

```bash
# Issues by sprint
echo "Sprint 1: $(gh issue list --label sprint-1 --json number --jq 'length')/4"
echo "Sprint 2: $(gh issue list --label sprint-2 --json number --jq 'length')/5"
echo "Sprint 3: $(gh issue list --label sprint-3 --json number --jq 'length')/6"

# Closed vs open
gh issue list --label frontend,refactor --json state --jq 'group_by(.state) | map({state: .[0].state, count: length})'
```

---

## 🧪 Testing Strategy

### Unit Tests

**Requirement**: 95%+ coverage

```bash
# Run tests with coverage
pnpm test --coverage

# Coverage report
open coverage/lcov-report/index.html
```

**Focus Areas**:
- New components (AuthModal, UploadWizard, etc.)
- Context providers (AuthContext, GameContext, etc.)
- Hooks (useAuth, useGame, etc.)
- Utility functions

### Integration Tests

**Playwright E2E tests**:

```bash
# Run E2E tests
pnpm playwright test

# Run specific suite
pnpm playwright test --grep "upload flow"
```

**Key Flows**:
- Login → Select game → Ask question
- Upload PDF → Validate → Complete
- Mobile navigation
- Keyboard shortcuts

### Visual Regression

**Tool**: Percy or Chromatic

```bash
# Take screenshots
pnpm percy snapshot

# Compare with baseline
# (runs in CI)
```

**Pages to Test**:
- Landing page
- Login page
- Chat page (with/without sidebar)
- Upload page (all wizard steps)
- Mobile views

### Performance Testing

**Lighthouse CI**:

```bash
# Run Lighthouse
pnpm lighthouse --output json --output-path ./lighthouse-report.json

# Assert thresholds
# (runs in CI)
```

**Metrics**:
- Performance: ≥ 90
- Accessibility: ≥ 95
- Best Practices: ≥ 95
- SEO: ≥ 90

### Manual Testing Checklist

**Before Each Sprint Close**:

- [ ] Test all changed pages
- [ ] Test in light + dark mode
- [ ] Test on mobile (iOS + Android)
- [ ] Test on different browsers (Chrome, Firefox, Safari)
- [ ] Test keyboard navigation
- [ ] Test with screen reader (NVDA/JAWS)
- [ ] Test error states
- [ ] Test loading states
- [ ] Verify no console errors
- [ ] Verify no layout shifts

---

## 🚨 Risk Management

### High-Risk Changes

| Risk | Impact | Mitigation |
|------|--------|------------|
| **Upload page refactor** breaks upload | High | Extensive testing, feature flag, gradual rollout |
| **ChatProvider split** breaks existing code | High | Comprehensive context tests, update all consumers |
| **Styling migration** causes visual regressions | Medium | Visual regression tests, manual review |
| **Performance changes** slow down app | Medium | Benchmarks, profiling, monitoring |

### Mitigation Strategies

1. **Feature Flags**: Enable new features gradually
   ```tsx
   if (featureFlags.newUploadFlow) {
     return <NewUploadWizard />
   }
   return <LegacyUploadPage />
   ```

2. **Incremental Rollout**: Deploy to staging → 10% users → 50% → 100%

3. **Rollback Plan**: Keep old code for 1 sprint, easy to revert

4. **Monitoring**: Track errors, performance, user feedback

5. **Kill Switch**: Ability to disable new features instantly

### Contingency Plans

**If Sprint 1 takes longer than expected**:
- Prioritize Issue #04 (styling) - Can be done anytime
- Delay Sprint 3 issues (#10-15) - Nice-to-have
- Focus on Sprint 2 critical issues (#05-06)

**If performance issues arise**:
- Profile before optimizing (don't guess)
- Focus on largest impact (virtualization > memoization)
- Consider lazy loading more aggressively

**If testing falls behind**:
- Prioritize integration tests over unit tests
- Use manual testing checklist
- Increase sprint buffer time

---

## 📈 Expected Improvements

### Code Quality

**Before**:
```tsx
// upload.tsx - 1564 lines, 20+ useState
const UploadPage = () => {
  const [step, setStep] = useState('select-game')
  const [game, setGame] = useState(null)
  const [file, setFile] = useState(null)
  const [validation, setValidation] = useState(null)
  // ... 20+ more state variables

  // ... 1500 lines of logic
}
```

**After**:
```tsx
// upload.tsx - 150 lines, clean
const UploadPage = () => {
  return (
    <UploadWizard
      onComplete={(game, file) => router.push('/chat')}
    />
  )
}

// UploadWizard.tsx - 100 lines, focused
// GameSelector.tsx - 80 lines, reusable
// FileUploadZone.tsx - 100 lines, testable
```

**Impact**:
- 75% reduction in file size
- 90% reduction in complexity
- 100% increase in testability
- 80% reduction in bug surface area

### Performance

**Before**:
- Bundle: ~450 KB
- Re-renders: Every keystroke re-renders entire chat
- First paint: ~2s
- Time to interactive: ~3s

**After**:
- Bundle: ~350 KB (22% reduction)
- Re-renders: 80% fewer (memoization + virtualization)
- First paint: <1.5s (25% faster)
- Time to interactive: <2.5s (17% faster)

### UX

**Before**:
- Mobile: Sidebar pushes content, broken layout
- Touch: Small buttons (<44px), hard to tap
- Errors: Inconsistent messages, sometimes silent failures
- Loading: Mix of spinners, text, nothing

**After**:
- Mobile: Drawer sidebar, responsive layout, 320px+ support
- Touch: All targets ≥44px, comfortable interactions
- Errors: Consistent toasts with actionable messages
- Loading: Skeleton screens, progress indicators

### Accessibility

**Before**:
- Missing ARIA labels
- Poor keyboard navigation
- Color contrast issues (5 instances)
- No screen reader testing

**After**:
- Complete ARIA labeling
- Full keyboard navigation (Tab, Enter, Escape)
- WCAG AA compliant (4.5:1 contrast)
- Tested with NVDA/JAWS

---

## 🎓 Learning Resources

### For Team Members

**Before Starting**:
1. Read `docs/04-frontend/design-system.md` (15min)
2. Review `DESIGN-SYSTEM-QUICKSTART.md` (5min)
3. Watch Tailwind CSS crash course (30min)
4. Familiarize with shadcn/ui components (15min)

**During Development**:
- Refer to design token reference
- Check Storybook for component examples (after Sprint 3)
- Use ESLint/Prettier for consistency

### Recommended Reading

- **Component Design**: [https://www.componentdriven.org/](https://www.componentdriven.org/)
- **React Patterns**: [https://www.patterns.dev/posts/react-component-patterns](https://www.patterns.dev/posts/react-component-patterns)
- **Performance**: [https://react.dev/learn/render-and-commit](https://react.dev/learn/render-and-commit)
- **Accessibility**: [https://www.w3.org/WAI/WCAG21/quickref/](https://www.w3.org/WAI/WCAG21/quickref/)

---

## ✅ Definition of Done

### For Each Issue

- [ ] Implementation complete
- [ ] Tests written (95%+ coverage for new code)
- [ ] Manual testing completed
- [ ] Light/dark mode verified
- [ ] Mobile tested (iOS + Android)
- [ ] Accessibility checked (axe DevTools)
- [ ] Performance checked (no regressions)
- [ ] Documentation updated
- [ ] PR reviewed and approved
- [ ] Merged to main branch

### For Each Sprint

- [ ] All issues in sprint complete
- [ ] Full regression testing passed
- [ ] Lighthouse scores meet targets
- [ ] Visual regression tests pass
- [ ] No critical bugs
- [ ] Demo prepared for stakeholders
- [ ] Retrospective completed

### For Overall Roadmap

- [ ] All 15 issues complete
- [ ] All success metrics achieved
- [ ] Storybook deployed
- [ ] Documentation complete
- [ ] Performance benchmarks met
- [ ] No P0/P1 bugs
- [ ] User acceptance testing passed
- [ ] Production deployment successful

---

## 🎉 Celebration Milestones

**Sprint 1 Complete** 🎯:
- Largest file reduced from 1564 → 400 lines
- Zero inline styles
- Clean architecture

**Sprint 2 Complete** 🚀:
- Mobile-first responsive
- Performance optimized
- Accessible

**Sprint 3 Complete** ✨:
- Storybook documentation
- 95%+ test coverage
- Production-ready

**Full Roadmap Complete** 🏆:
- World-class frontend
- Best-in-class DX
- Ready for scale

---

## 📞 Support

**Questions**:
- Check documentation in `docs/04-frontend/improvements/`
- Review design system: `docs/04-frontend/design-system.md`
- Create GitHub issue with `question` label

**Blockers**:
- Tag issue as `blocked` with explanation
- Notify team lead
- Work on non-blocked issues in parallel

**Help Needed**:
- Request code review
- Ask in team channel
- Schedule pairing session

---

**Ready to start?**

```bash
cd .github-issues-templates
./create-issues.sh --sprint 1
```

Let's build something great! 🚀

---

**Version**: 1.0
**Last Updated**: 2025-11-13
**Maintained By**: Engineering Team
