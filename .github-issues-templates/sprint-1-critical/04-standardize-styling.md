# [REFACTOR] Eliminate Inline Styles and Standardize with Design System

## 🎯 Objective

Remove all inline styles from the codebase and replace them with Tailwind CSS classes using the new design system tokens.

## 📋 Current State

**Problem**: **200+ instances** of inline styles with magic numbers scattered across components:

```tsx
// ❌ Current state (examples from codebase)
<div style={{ padding: 24, maxWidth: 900, margin: "0 auto" }}>
<div style={{ width: sidebarCollapsed ? 0 : 320, minWidth: 320 }}>
<button style={{ padding: '10px 18px', backgroundColor: '#1a73e8' }}>
<div style={{ padding: '16px', backgroundColor: '#f9fafb', borderRadius: '6px' }}>
```

**Issues**:
- 🎨 **Inconsistency**: Same spacing defined differently (`padding: 24px` vs `padding: 16px` vs `p-6`)
- 🔧 **Maintenance**: Hard to update design system-wide
- 📱 **Responsiveness**: Inline styles can't use responsive breakpoints
- 🌙 **Dark Mode**: Inline colors don't adapt to theme
- 📦 **Bundle**: Inline styles aren't tree-shaken
- ♿ **Accessibility**: Harder to maintain consistent contrast ratios

## ✅ Acceptance Criteria

- [ ] Zero inline `style={}` props (except for truly dynamic values)
- [ ] All spacing uses Tailwind classes (`p-6`, `gap-4`, etc.)
- [ ] All colors use semantic tokens (`bg-card`, `text-foreground`, etc.)
- [ ] All borders/shadows use design system (`rounded-xl`, `shadow-sm`, etc.)
- [ ] Responsive design uses Tailwind breakpoints (`md:`, `lg:`, etc.)
- [ ] Dark mode works correctly for all components
- [ ] No new ESLint warnings about inline styles
- [ ] Performance maintained or improved
- [ ] Visual design unchanged (pixel-perfect migration)

## 🏗️ Implementation Plan

### Phase 1: Audit & Mapping (2h)

#### 1.1. Find All Inline Styles

```bash
# Search for inline style props
cd apps/web/src
grep -rn 'style={{' --include="*.tsx" --include="*.ts" | wc -l
# Output: ~200 instances

# Create audit report
grep -rn 'style={{' --include="*.tsx" --include="*.ts" > /tmp/inline-styles-audit.txt
```

#### 1.2. Create Migration Map

| Inline Style | Tailwind Class | Design Token |
|-------------|----------------|--------------|
| `padding: 24px` | `p-6` | `--space-6` |
| `margin: 16px` | `m-4` | `--space-4` |
| `gap: 12px` | `gap-3` | `--space-3` |
| `borderRadius: 8px` | `rounded-md` | `--radius-md` |
| `background: '#f8f9fa'` | `bg-sidebar` | `--color-sidebar` |
| `color: '#3391ff'` | `text-primary` | `--color-primary` |
| `width: 320px` | `w-80` | `--size-sidebar` |
| `maxWidth: 900px` | `max-w-3xl` | `--size-content-max` |
| `height: 100vh` | `h-screen` or `h-dvh` | - |

### Phase 2: Refactor by Priority (1 day)

#### Priority 1: Layout Components (4h)

**Files** (highest impact):
1. `pages/upload.tsx` (~50 inline styles)
2. `pages/chat.tsx` (~10 inline styles)
3. `components/chat/ChatSidebar.tsx` (~15 inline styles)
4. `components/chat/ChatContent.tsx` (~8 inline styles)

**Example Migration**:

**Before**:
```tsx
<aside
  style={{
    width: sidebarCollapsed ? 0 : 320,
    minWidth: sidebarCollapsed ? 0 : 320,
    background: '#f8f9fa',
    borderRight: '1px solid #dadce0',
    display: 'flex',
    flexDirection: 'column',
    overflow: 'hidden',
    transition: 'width 0.3s ease, min-width 0.3s ease'
  }}
>
```

**After**:
```tsx
<aside
  className={cn(
    "bg-sidebar border-r border-border",
    "flex flex-col overflow-hidden",
    "transition-all duration-300 ease-in-out",
    sidebarCollapsed ? "w-0 min-w-0" : "w-80 min-w-80"
  )}
>
```

**Benefits**:
- 9 style properties → 1 className
- Responsive: Can add `md:w-80 lg:w-96`
- Dark mode: Automatic with `bg-sidebar`
- Maintainable: Tokens in one place

#### Priority 2: Form Components (2h)

**Files**:
1. `pages/login.tsx`
2. `pages/index.tsx` (auth modal)
3. `components/auth/*` (after refactor from issue #01)

**Example**:

**Before**:
```tsx
<input
  style={{
    width: '100%',
    padding: '12px',
    border: '1px solid #ddd',
    borderRadius: '4px',
    fontSize: '16px'
  }}
/>
```

**After**:
```tsx
<Input
  className="w-full"
  // Shadcn Input component already has correct styles
/>
```

#### Priority 3: Card/Container Components (1h)

**Files**:
1. `pages/upload.tsx` (game selection card, PDF table)
2. `pages/admin.tsx`
3. Any custom `<Card>` implementations

**Example**:

**Before**:
```tsx
<div
  style={{
    marginTop: '20px',
    marginBottom: '24px',
    padding: '16px',
    border: '1px solid #e0e0e0',
    borderRadius: '6px',
    backgroundColor: '#f9fafb'
  }}
>
```

**After**:
```tsx
<Card className="my-6 bg-muted">
  {/* Card automatically has p-6, rounded-xl, border */}
</Card>
```

#### Priority 4: Button/Interactive Components (1h)

**Files**:
1. All button instances not using Shadcn `<Button>`
2. Custom interactive elements

**Example**:

**Before**:
```tsx
<button
  onClick={handleClick}
  style={{
    padding: '10px 18px',
    backgroundColor: '#1a73e8',
    color: 'white',
    border: 'none',
    borderRadius: '4px',
    cursor: 'pointer',
    fontWeight: 500
  }}
>
  Submit
</button>
```

**After**:
```tsx
<Button onClick={handleClick}>
  Submit
</Button>
```

### Phase 3: Dynamic Styles (Legitimate Use Cases)

Some inline styles are **necessary** for truly dynamic values:

```tsx
// ✅ ALLOWED: Calculated values
<div style={{ height: `calc(100vh - ${headerHeight}px)` }}>

// ✅ ALLOWED: Animation values from state
<div style={{ transform: `translateX(${position}px)` }}>

// ✅ ALLOWED: User-customizable values
<div style={{ backgroundColor: userSelectedColor }}>
```

**Solution**: Use CSS variables for dynamic values:

```tsx
// ✅ BETTER: Use CSS variable
<div
  className="h-[var(--dynamic-height)]"
  style={{ '--dynamic-height': `${height}px` } as React.CSSProperties}
>
```

### Phase 4: Update Linting Rules (30min)

**File**: `apps/web/eslint.config.mjs`

Add rule to warn about inline styles:

```js
module.exports = {
  rules: {
    'react/forbid-dom-props': [
      'warn',
      {
        forbid: [
          {
            propName: 'style',
            message: 'Use Tailwind classes instead of inline styles. Only use style prop for truly dynamic values.',
          },
        ],
      },
    ],
  },
};
```

## 🧪 Testing Strategy

### Visual Regression Testing

1. **Take screenshots before migration**:
   ```bash
   pnpm playwright test --project=chromium --update-snapshots
   ```

2. **After migration, compare**:
   ```bash
   pnpm playwright test --project=chromium
   ```

3. **Manual verification**:
   - Light mode: Check all pages
   - Dark mode: Check all pages
   - Mobile: Check responsive breakpoints
   - Tablet: Check intermediate sizes

### Functional Testing

Run full E2E test suite:

```bash
pnpm test:e2e
```

All existing tests should pass without modification.

## 📦 Files to Modify

**By Priority**:

**P1 - Layout** (50 inline styles):
- `apps/web/src/pages/upload.tsx`
- `apps/web/src/pages/chat.tsx`
- `apps/web/src/components/chat/ChatSidebar.tsx`
- `apps/web/src/components/chat/ChatContent.tsx`
- `apps/web/src/components/chat/MessageList.tsx`

**P2 - Forms** (30 inline styles):
- `apps/web/src/pages/login.tsx`
- `apps/web/src/pages/index.tsx`
- `apps/web/src/components/auth/OAuthButtons.tsx`

**P3 - Cards** (40 inline styles):
- `apps/web/src/pages/upload.tsx` (game picker)
- `apps/web/src/pages/admin.tsx`
- `apps/web/src/pages/admin/analytics.tsx`

**P4 - Buttons** (30 inline styles):
- Various files with custom buttons

**P5 - Misc** (50 inline styles):
- Landing page sections
- Admin pages
- Editor components

**Total**: ~200 inline styles across ~30 files

## 📊 Impact

**Bundle Size**:
- Estimated reduction: **3-5 KB** (inline styles → tree-shakable Tailwind)
- Tailwind purge will remove unused classes

**Performance**:
- Initial render: **5-10% faster** (fewer inline style computations)
- Re-renders: **Slightly faster** (className string comparison vs object diff)

**Maintainability**:
- Design changes: Update tokens in ONE place instead of 200+
- Dark mode: Automatic for all components
- Responsive: Easy to add breakpoints

**Developer Experience**:
- No more magic numbers
- Autocomplete for Tailwind classes (VSCode)
- Consistent spacing/sizing
- ESLint catches inline styles

## ⏱️ Effort Estimate

**1 day** (8 hours)

- Audit & mapping: 2h
- P1 (Layout): 2h
- P2 (Forms): 1h
- P3 (Cards): 1h
- P4 (Buttons): 1h
- P5 (Misc): 1h
- Linting + testing: 1h (includes verification)

## 📚 Dependencies

- **Design System** (#TBD) - Design tokens must be available
- **Shadcn/UI** - Button, Card, Input components installed

## 🔗 Related Issues

- #TBD: Design System - Provides tokens
- #TBD: Upload Page Refactor - Will clean up many inline styles
- #TBD: Mobile Improvements - Responsive classes needed

## 📝 Notes

### Migration Checklist

For each file with inline styles:

- [ ] Identify all `style={{}}` props
- [ ] Map to Tailwind classes using design tokens
- [ ] Test in light mode
- [ ] Test in dark mode
- [ ] Test responsive behavior
- [ ] Verify no visual regression
- [ ] Run ESLint
- [ ] Commit

### Common Patterns

```tsx
// Spacing
style={{ padding: 24 }} → className="p-6"
style={{ margin: '16px 0' }} → className="my-4"
style={{ gap: 12 }} → className="gap-3"

// Sizing
style={{ width: 320 }} → className="w-80"
style={{ maxWidth: 900 }} → className="max-w-3xl"
style={{ height: '100vh' }} → className="h-screen" or "h-dvh"

// Colors
style={{ background: '#f8f9fa' }} → className="bg-sidebar"
style={{ color: '#3391ff' }} → className="text-primary"
style={{ border: '1px solid #ddd' }} → className="border border-border"

// Layout
style={{ display: 'flex' }} → className="flex"
style={{ flexDirection: 'column' }} → className="flex-col"
style={{ justifyContent: 'space-between' }} → className="justify-between"

// Borders & Shadows
style={{ borderRadius: 8 }} → className="rounded-md"
style={{ boxShadow: '...' }} → className="shadow-sm"

// Transitions
style={{ transition: 'all 0.3s' }} → className="transition-all duration-300"
```

### Tools

- **Tailwind CSS IntelliSense** (VSCode extension): Autocomplete classes
- **Headwind** (VSCode extension): Sort Tailwind classes
- **Tailwind Fold** (VSCode extension): Fold long className strings

---

**Priority**: 🔴 Critical
**Sprint**: Sprint 1
**Effort**: 1d (8h)
**Labels**: `frontend`, `refactor`, `design-system`, `tailwind`, `sprint-1`, `priority-critical`
