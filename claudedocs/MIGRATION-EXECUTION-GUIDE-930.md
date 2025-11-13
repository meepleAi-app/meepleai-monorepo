# Component Migration Execution Guide - Issue #930

**Status**: Ready for Execution
**Approach**: Incremental commits every 3-5 components
**Estimated Time**: 84-99 hours total
**Created**: 2025-11-12

## 📋 Prerequisites Completed

✅ Comprehensive audit completed (`component-migration-audit-930.md`)
✅ Tracking spreadsheet created (`component-migration-tracking-930.csv`)
✅ All shadcn/ui components installed (Sonner, Avatar, Badge, Table, Skeleton)
✅ ThemeSwitcher P1 bug documented (Issue #1035)

## 🎯 Execution Strategy

**Commit Frequency**: Every 3-5 components
**Branch**: `frontend-3-design-tokens-migration` (current)
**Testing**: After each commit group
**Documentation**: Update tracking CSV as you go

## 📦 Phase 1: Accessible Components (CRITICAL - 3 components, ~16 hours)

### Commit 1: Migrate AccessibleButton

**File**: `src/components/accessible/AccessibleButton.tsx`

**Changes**:
1. Import shadcn Button and utilities:
```typescript
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import { Loader2 } from 'lucide-react';
```

2. Add variant and size mapping:
```typescript
const variantMap = {
  primary: 'default' as const,
  secondary: 'secondary' as const,
  danger: 'destructive' as const,
  ghost: 'ghost' as const,
};

const sizeMap = {
  sm: 'sm' as const,
  md: 'default' as const,
  lg: 'lg' as const,
};
```

3. Replace the `<button>` element with shadcn `<Button>`:
```typescript
<Button
  ref={ref}
  variant={shadcnVariant}
  size={shadcnSize}
  className={cn(
    'transition-all duration-200',
    'focus-visible:ring-2 focus-visible:ring-offset-2',
    pressedClasses,
    className
  )}
  disabled={disabled || isLoading}
  {...ariaAttributes}
  {...props}
>
  {isLoading ? (
    <>
      <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
      <span>{loadingText}</span>
    </>
  ) : (
    children
  )}
</Button>
```

4. **PRESERVE**: All ARIA attributes, loading state screen reader announcement, validation logic

**Test**:
```bash
cd apps/web && pnpm test -- AccessibleButton
```

**Commit**:
```bash
git add src/components/accessible/AccessibleButton.tsx
git commit -m "feat(frontend-5): Migrate AccessibleButton to shadcn Button

- Wrap shadcn Button with accessibility features
- Preserve all WCAG 2.1 AA compliance (ARIA, focus, loading states)
- Map custom variants (primary/secondary/danger/ghost) to shadcn
- Use Loader2 icon for loading states
- Maintain icon-only button support with validation

Issue #930"
```

---

### Commit 2: Migrate AccessibleFormInput

**File**: `src/components/accessible/AccessibleFormInput.tsx`

**Changes**:
1. Import shadcn Input:
```typescript
import { Input } from '@/components/ui/input';
import { cn } from '@/lib/utils';
```

2. Replace `<input>` with `<Input>`:
```typescript
<Input
  ref={ref}
  id={inputId}
  type={type}
  className={cn(
    'transition-colors',
    error && 'border-red-500 focus-visible:ring-red-500',
    className
  )}
  aria-describedby={descriptors}
  aria-invalid={!!error}
  aria-required={required}
  {...props}
/>
```

3. **PRESERVE**: Label wrapper, error messages, hint text, ARIA relationships

**Test**:
```bash
pnpm test -- AccessibleFormInput
```

**Commit**:
```bash
git add src/components/accessible/AccessibleFormInput.tsx
git commit -m "feat(frontend-5): Migrate AccessibleFormInput to shadcn Input

- Wrap shadcn Input with accessibility features
- Preserve WCAG 2.1 AA compliance (labels, errors, hints, ARIA)
- Maintain error state styling
- Keep validation and required field handling

Issue #930"
```

---

### Commit 3: Migrate AccessibleModal (HIGH RISK - TEST THOROUGHLY)

**File**: `src/components/accessible/AccessibleModal.tsx`

**Changes**:
1. Import shadcn Dialog components:
```typescript
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/dialog';
import { cn } from '@/lib/utils';
```

2. Replace custom modal with Dialog:
```typescript
<Dialog open={isOpen} onOpenChange={(open) => !open && onClose()}>
  <DialogContent
    className={cn(
      'sm:max-w-[425px]',
      size === 'sm' && 'sm:max-w-[350px]',
      size === 'md' && 'sm:max-w-[500px]',
      size === 'lg' && 'sm:max-w-[700px]',
      size === 'xl' && 'sm:max-w-[900px]',
      size === 'full' && 'sm:max-w-[95vw]',
      className
    )}
    aria-labelledby={titleId}
    aria-describedby={descriptionId}
  >
    <DialogHeader>
      <DialogTitle id={titleId}>{title}</DialogTitle>
      {description && (
        <DialogDescription id={descriptionId}>
          {description}
        </DialogDescription>
      )}
    </DialogHeader>

    {children}

    {showFooter && (
      <DialogFooter>
        {/* footer content */}
      </DialogFooter>
    )}
  </DialogContent>
</Dialog>
```

3. **CRITICAL PRESERVATIONS**:
   - Focus trap (Dialog handles automatically)
   - Escape key handling
   - Click outside to close (configurable)
   - Scroll lock
   - ARIA labels and descriptions
   - Keyboard navigation

**Test** (COMPREHENSIVE):
```bash
pnpm test -- AccessibleModal
pnpm test -- accessible/
```

**Manual Testing Required**:
- Test focus trap with Tab/Shift+Tab
- Test Escape key closes modal
- Test click outside behavior
- Test screen reader announcements
- Test keyboard navigation through all interactive elements
- Test nested modals if applicable

**Commit**:
```bash
git add src/components/accessible/AccessibleModal.tsx
git commit -m "feat(frontend-5): Migrate AccessibleModal to shadcn Dialog (CRITICAL)

- Wrap shadcn Dialog with full accessibility features
- PRESERVE WCAG 2.1 AA compliance (focus trap, ARIA, keyboard nav)
- Maintain size variants (sm/md/lg/xl/full)
- Keep escape key and click-outside handling
- Preserve scroll lock and portal rendering

⚠️ CRITICAL: Tested focus management and screen reader compatibility

Issue #930"
```

---

## 📦 Phase 2: Button and Card Class Replacements (12 components, ~18 hours)

### Commit 4-5: Replace .btn-* classes across pages

**Files to Update**:
1. `src/pages/index.tsx`
2. `src/pages/board-game-ai.tsx`
3. `src/pages/reset-password.tsx`
4. `src/pages/admin/bulk-export.tsx`
5. `src/components/loading/LoadingButton.tsx`

**Pattern**:
```tsx
// BEFORE:
<button className="btn-primary">Submit</button>

// AFTER:
import { Button } from '@/components/ui/button';
<Button>Submit</Button>

// BEFORE:
<button className="btn-secondary">Cancel</button>

// AFTER:
<Button variant="secondary">Cancel</Button>

// BEFORE:
<button className="btn-secondary-accessible">Save</button>

// AFTER:
<Button variant="outline">Save</Button>
```

**Test**:
```bash
pnpm test -- pages/index.test
pnpm test -- pages/board-game-ai.test
# etc. for each file
```

**Commit** (after completing all 5 files):
```bash
git add src/pages/ src/components/loading/
git commit -m "feat(frontend-5): Replace btn-* classes with shadcn Button

Files updated:
- pages/index.tsx (btn-primary, btn-secondary-accessible)
- pages/board-game-ai.tsx (btn-primary, btn-secondary)
- pages/reset-password.tsx (button classes)
- pages/admin/bulk-export.tsx (button classes)
- components/loading/LoadingButton.tsx (use shadcn Button internally)

All button functionality preserved, improved theming support

Issue #930"
```

---

### Commit 6: Replace .card class with shadcn Card

**Files to Update**:
1. `src/pages/index.tsx`
2. `src/pages/board-game-ai.tsx`
3. `src/pages/reset-password.tsx`
4. `src/components/AdminCharts.tsx`
5. `src/components/PromptVersionCard.tsx`

**Pattern**:
```tsx
// BEFORE:
<div className="card p-6">
  <h3>Title</h3>
  <p>Content</p>
</div>

// AFTER:
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card';
<Card>
  <CardHeader>
    <CardTitle>Title</CardTitle>
  </CardHeader>
  <CardContent>
    <p>Content</p>
  </CardContent>
</Card>
```

**Test**:
```bash
pnpm test -- pages/
pnpm test -- AdminCharts
pnpm test -- PromptVersionCard
```

**Commit**:
```bash
git add src/pages/ src/components/AdminCharts.tsx src/components/PromptVersionCard.tsx
git commit -m "feat(frontend-5): Replace .card class with shadcn Card

Files updated:
- pages/index.tsx (multiple card instances)
- pages/board-game-ai.tsx (card containers)
- pages/reset-password.tsx (form card)
- components/AdminCharts.tsx (stat cards)
- components/PromptVersionCard.tsx (full Card component structure)

Improved semantic structure with CardHeader/CardContent/CardFooter

Issue #930"
```

---

## 📦 Phase 3: Modal Components (4 components, ~9 hours)

### Commit 7: Migrate Modal Components

**Files**:
1. `src/components/ErrorModal.tsx`
2. `src/components/SessionWarningModal.tsx`
3. `src/components/BggSearchModal.tsx`
4. `src/components/ExportChatModal.tsx`

**Pattern** (using ErrorModal as example):
```tsx
// BEFORE: Custom modal using AccessibleModal
<AccessibleModal isOpen={isOpen} onClose={onClose} title="Error">
  <div>Error content</div>
  <button onClick={onClose}>Close</button>
</AccessibleModal>

// AFTER: Direct shadcn Dialog usage
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';

<Dialog open={isOpen} onOpenChange={onClose}>
  <DialogContent>
    <DialogHeader>
      <DialogTitle>Error</DialogTitle>
    </DialogHeader>
    <div>Error content</div>
    <DialogFooter>
      <Button variant="outline" onClick={onClose}>Close</Button>
    </DialogFooter>
  </DialogContent>
</Dialog>
```

**Note**: Since AccessibleModal now wraps Dialog, these components can still use AccessibleModal if preferred for consistent API. Update only if simplification needed.

**Test**:
```bash
pnpm test -- ErrorModal
pnpm test -- SessionWarningModal
pnpm test -- BggSearchModal
pnpm test -- ExportChatModal
```

**Commit**:
```bash
git add src/components/ErrorModal.tsx src/components/SessionWarningModal.tsx src/components/BggSearchModal.tsx src/components/ExportChatModal.tsx
git commit -m "feat(frontend-5): Migrate modal components to shadcn Dialog

Modals migrated:
- ErrorModal (error display dialog)
- SessionWarningModal (session expiry warning)
- BggSearchModal (board game search with form)
- ExportChatModal (chat export options)

All modals now use shadcn Dialog with consistent styling
Accessibility preserved through Dialog's built-in features

Issue #930"
```

---

## 📦 Phase 4: Chat Components (4 components, ~9 hours)

### Commit 8: Migrate Chat Components

**Files**:
1. `src/components/chat/MessageInput.tsx` (Input + Button)
2. `src/components/chat/MessageEditForm.tsx` (Input + Buttons)
3. `src/components/chat/GameSelector.tsx` (Select)
4. `src/components/chat/AgentSelector.tsx` (Select)
5. `src/components/chat/MessageActions.tsx` (Buttons + potential DropdownMenu)

**Pattern for Selects**:
```tsx
// BEFORE:
<select value={selected} onChange={handleChange}>
  <option value="1">Game 1</option>
  <option value="2">Game 2</option>
</select>

// AFTER:
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';

<Select value={selected} onValueChange={handleChange}>
  <SelectTrigger>
    <SelectValue placeholder="Select game" />
  </SelectTrigger>
  <SelectContent>
    <SelectItem value="1">Game 1</SelectItem>
    <SelectItem value="2">Game 2</SelectItem>
  </SelectContent>
</Select>
```

**Test**:
```bash
pnpm test -- chat/
```

**Commit**:
```bash
git add src/components/chat/
git commit -m "feat(frontend-5): Migrate chat components to shadcn UI

Components migrated:
- MessageInput (shadcn Input + Button)
- MessageEditForm (form with Input + Buttons)
- GameSelector (shadcn Select)
- AgentSelector (shadcn Select)
- MessageActions (Button + DropdownMenu for context menu)

Improved keyboard navigation and accessibility in chat interface

Issue #930"
```

---

## 📦 Phase 5: Admin & Toast (3 components, ~13 hours)

### Commit 9: Migrate Admin Components

**Files**:
1. `src/components/admin/CategoryConfigTab.tsx`
2. `src/components/admin/FeatureFlagsTab.tsx`

**Changes**: Replace form inputs and buttons with shadcn components

**Test**:
```bash
pnpm test -- admin/
```

**Commit**:
```bash
git add src/components/admin/
git commit -m "feat(frontend-5): Migrate admin components to shadcn UI

Components migrated:
- CategoryConfigTab (Input, Button, form controls)
- FeatureFlagsTab (Input, Button, toggles)

Improved form validation and accessibility in admin interface

Issue #930"
```

---

### Commit 10: Replace react-hot-toast with Sonner (BREAKING CHANGE)

**Files to Update**:
1. `src/components/Toast.tsx` (complete rewrite)
2. `src/pages/_app.tsx` (add Toaster component)
3. `src/pages/admin/configuration.tsx` (update toast calls)
4. `src/components/admin/CategoryConfigTab.tsx` (update toast calls)
5. `src/components/admin/FeatureFlagsTab.tsx` (update toast calls)
6. Remove `react-hot-toast` from package.json

**New Toast.tsx**:
```tsx
import { toast as sonnerToast } from 'sonner';

export const toast = {
  success: (message: string) => sonnerToast.success(message),
  error: (message: string) => sonnerToast.error(message),
  info: (message: string) => sonnerToast.info(message),
  warning: (message: string) => sonnerToast.warning(message),
  loading: (message: string) => sonnerToast.loading(message),
};

export default toast;
```

**Add to _app.tsx**:
```tsx
import { Toaster } from '@/components/ui/sonner';

// Inside component return:
<>
  <Component {...pageProps} />
  <Toaster />
</>
```

**Update all toast() calls**:
```tsx
// BEFORE:
import toast from 'react-hot-toast';
toast.success('Saved!');

// AFTER:
import { toast } from '@/components/Toast';
toast.success('Saved!');
// OR direct:
import { toast } from 'sonner';
toast.success('Saved!');
```

**Remove dependency**:
```bash
pnpm remove react-hot-toast
```

**Test**:
```bash
pnpm test -- Toast
pnpm test -- admin/
```

**Commit**:
```bash
git add src/components/Toast.tsx src/pages/_app.tsx src/pages/admin/ src/components/admin/ package.json
git commit -m "feat(frontend-5): Replace react-hot-toast with Sonner

BREAKING CHANGE: Replaced toast notification library

Changes:
- Installed Sonner (shadcn recommended toast library)
- Replaced Toast.tsx with Sonner wrapper
- Added Toaster component to _app.tsx
- Updated all toast() calls across admin components
- Removed react-hot-toast dependency

Benefits:
- Better theming support (light/dark auto-detection)
- Improved accessibility
- Better animations and positioning
- Consistent with shadcn design system

Issue #930"
```

---

## 📦 Phase 6: Upload, Timeline, Diff Components (10 components, ~22 hours)

### Commit 11-13: Migrate Remaining Components

**Commit 11 - Upload Components**:
```bash
# Files: MultiFileUpload.tsx, UploadQueue.tsx, UploadQueueItem.tsx, UploadSummary.tsx
git commit -m "feat(frontend-5): Migrate upload components to shadcn UI

Issue #930"
```

**Commit 12 - Timeline Components**:
```bash
# Files: Timeline.tsx, TimelineFilters.tsx, TimelineEventItem.tsx, VersionTimelineFilters.tsx
git commit -m "feat(frontend-5): Migrate timeline components to shadcn UI

Issue #930"
```

**Commit 13 - Diff Components**:
```bash
# Files: DiffSearchInput.tsx, DiffNavigationControls.tsx, DiffToolbar.tsx, DiffViewModeToggle.tsx
git commit -m "feat(frontend-5): Migrate diff components to shadcn UI

Issue #930"
```

---

## ✅ Phase 7: Testing & Validation

### Test Suite Execution

```bash
# Unit tests
cd apps/web
pnpm test

# Coverage check (must be ≥90%)
pnpm test -- --coverage

# Type checking
pnpm typecheck

# Linting
pnpm lint

# Build verification
pnpm build
```

### Manual Testing Checklist

- [ ] Test all button variants (primary, secondary, danger, ghost)
- [ ] Test all button sizes (sm, md, lg, icon)
- [ ] Test loading states
- [ ] Test disabled states
- [ ] Test card layouts on all pages
- [ ] Test all modals (open, close, escape, click outside)
- [ ] Test form inputs (validation, errors, hints)
- [ ] Test selects (keyboard navigation, search if applicable)
- [ ] Test toast notifications (success, error, warning, info)
- [ ] Test chat interface (message input, selectors, actions)
- [ ] Test admin pages (forms, tables, configuration)
- [ ] Test upload flows
- [ ] Test timeline filters and navigation
- [ ] Test diff viewer controls

### Accessibility Testing

```bash
# Install axe-core if not already
pnpm add -D @axe-core/react

# Run accessibility tests
pnpm test -- .a11y.test
```

**Manual A11y Checks**:
- [ ] Keyboard navigation (Tab, Shift+Tab, Enter, Space, Escape)
- [ ] Screen reader announcements (NVDA/JAWS/VoiceOver)
- [ ] Focus indicators visible and WCAG 2.1 AA compliant (2px minimum)
- [ ] Color contrast ratios meet WCAG AA (4.5:1 for text)
- [ ] ARIA labels and descriptions present
- [ ] Form labels properly associated
- [ ] Error messages announced
- [ ] Loading states announced
- [ ] Modal focus trap works
- [ ] Skip links functional

### Visual Regression Testing

```bash
# Playwright visual tests
pnpm playwright test --update-snapshots  # Update baselines
pnpm playwright test  # Run visual regression
```

**Pages to Screenshot**:
- Home page
- Chat interface
- Upload page
- Admin dashboard
- Admin configuration
- Login/registration
- All modals (open state)

---

## 📝 Phase 8: Documentation & PR

### Update Documentation

**Files to Update**:
1. `CLAUDE.md` - Add shadcn/ui migration notes
2. `claudedocs/component-migration-tracking-930.csv` - Mark all as "Complete"
3. `docs/frontend/component-library.md` - Document new patterns
4. `README.md` - Update component documentation links

### Create Pull Request

**PR Title**:
```
feat(frontend-5): Migrate 38 components to shadcn/ui (#930)
```

**PR Description Template**:
```markdown
## 🎯 Issue
Closes #930

## 📋 Summary
Systematic migration of 38 components across 56 files to shadcn/ui component library.

## ✅ Components Migrated

### High Priority (Week 4)
- [x] AccessibleButton → shadcn Button wrapper
- [x] AccessibleFormInput → shadcn Input wrapper
- [x] AccessibleModal → shadcn Dialog wrapper
- [x] Button class replacements (btn-primary, btn-secondary, etc.)
- [x] Card class replacements
- [x] Modal components (ErrorModal, SessionWarningModal, BggSearchModal, ExportChatModal)

### Medium Priority (Week 5)
- [x] Chat components (MessageInput, MessageActions, GameSelector, AgentSelector)
- [x] Admin components (CategoryConfigTab, FeatureFlagsTab)
- [x] Toast system (react-hot-toast → Sonner)
- [x] Upload components (MultiFileUpload, UploadQueue, UploadQueueItem, UploadSummary)
- [x] Timeline components (Timeline, TimelineFilters, TimelineEventItem)
- [x] Diff components (DiffSearchInput, DiffNavigationControls, DiffToolbar)

## 🧪 Testing

### Test Coverage
- Unit tests: ✅ 90.03% (maintained)
- Accessibility tests: ✅ All passing
- Visual regression: ✅ No regressions
- E2E tests: ✅ All flows validated

### Manual Testing
- [x] All button variants and states
- [x] All card layouts
- [x] All modals (keyboard nav, focus trap, escape)
- [x] All form inputs (validation, errors)
- [x] All selects (keyboard, search)
- [x] Toast notifications
- [x] Chat interface
- [x] Admin pages
- [x] Upload flows
- [x] Timeline navigation
- [x] Diff viewer

### Accessibility (WCAG 2.1 AA)
- [x] Keyboard navigation verified
- [x] Screen reader tested (NVDA)
- [x] Focus indicators compliant
- [x] Color contrast ratios verified
- [x] ARIA labels validated
- [x] Form associations checked
- [x] Loading states announced

## 📊 Impact

### Files Changed
- 56 files modified
- 38 components migrated
- 1 dependency removed (react-hot-toast)
- 6 shadcn components installed (Sonner, Avatar, Badge, Table, Skeleton + existing)

### Benefits
- ✅ Consistent design system
- ✅ Improved theming support (light/dark/system)
- ✅ Better accessibility out of the box
- ✅ Reduced custom CSS maintenance
- ✅ Modern component patterns
- ✅ Type-safe component props

### Breaking Changes
- ⚠️ Toast API changed (react-hot-toast → Sonner)
  - Migration: Import from `@/components/Toast` or `sonner` directly
  - All existing toast calls updated in this PR

## 🔍 Code Review Focus Areas

1. **Accessibility**: Verify WCAG 2.1 AA compliance maintained in AccessibleButton, AccessibleFormInput, AccessibleModal
2. **Toast Migration**: Verify all toast() calls updated correctly
3. **Visual Consistency**: Check button/card styling matches design system
4. **Keyboard Navigation**: Test modal focus traps and form navigation
5. **Test Coverage**: Verify 90%+ coverage maintained

## 📸 Screenshots

_Add screenshots of key components before/after_

## 📚 Documentation
- [x] CLAUDE.md updated
- [x] Component tracking sheet updated
- [x] Migration guide created
- [x] Component library docs updated

## ⚠️ Related Issues
- #1035 - ThemeSwitcher P1 bug (separate issue, not in scope)

## 🚀 Deployment Notes
- No database migrations required
- No environment variable changes
- No breaking API changes
- Frontend-only changes
```

### Commit PR Creation

```bash
git push origin frontend-3-design-tokens-migration
gh pr create --title "feat(frontend-5): Migrate 38 components to shadcn/ui (#930)" \
  --body-file pr-description.md \
  --label "enhancement,frontend"
```

---

## 📊 Phase 9: Code Review & Merge

### Self-Review Checklist

- [ ] All commits have descriptive messages
- [ ] No console.log or debug code left
- [ ] No commented-out code
- [ ] All tests passing
- [ ] Coverage ≥90%
- [ ] TypeScript strict mode passing
- [ ] ESLint warnings addressed
- [ ] No accessibility regressions
- [ ] Documentation updated
- [ ] Tracking sheet updated

### Address Review Feedback

1. Make requested changes
2. Run tests
3. Push updates
4. Request re-review

### Merge Strategy

```bash
# After approval
git checkout frontend-3-design-tokens-migration
git pull origin frontend-3-design-tokens-migration
gh pr merge --squash --delete-branch
```

---

## 🏁 Phase 10: Close Issue #930

### Update Issue on GitHub

```bash
gh issue comment 930 --body "✅ **Migration Complete**

All 38 components successfully migrated to shadcn/ui.

**Summary**:
- 56 files updated
- 13 commits created
- 90.03% test coverage maintained
- WCAG 2.1 AA compliance verified
- All acceptance criteria met

**PR**: #[PR_NUMBER]
**Tracking**: See \`claudedocs/component-migration-tracking-930.csv\`
**Migration Guide**: See \`claudedocs/MIGRATION-EXECUTION-GUIDE-930.md\`"

gh issue close 930
```

---

## 🔄 Rollback Plan

If issues arise post-deployment:

### Option 1: Revert PR
```bash
git revert -m 1 <merge-commit-hash>
git push origin main
```

### Option 2: Revert Specific Commits
```bash
git revert <commit-hash-1> <commit-hash-2>
git push origin main
```

### Option 3: Emergency Hotfix
1. Create hotfix branch from main
2. Apply targeted fixes
3. Fast-track PR review
4. Deploy immediately

---

## 📈 Success Metrics

### Quantitative
- [ ] 38/38 components migrated (100%)
- [ ] Test coverage ≥90%
- [ ] Zero production errors post-deploy
- [ ] Build time unchanged or improved
- [ ] Bundle size unchanged or reduced

### Qualitative
- [ ] Improved developer experience
- [ ] Consistent design language
- [ ] Better theming support
- [ ] Reduced maintenance overhead
- [ ] Positive code review feedback

---

## 🎓 Lessons Learned

Document here after completion:
- What went well?
- What could be improved?
- Any unexpected challenges?
- Reusable patterns discovered?
- Time estimates accuracy?

---

**Status**: Ready for execution
**Last Updated**: 2025-11-12
**Executed By**: [Your Name]
**Completion Date**: [TBD]
