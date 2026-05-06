# Desktop Dark Theme Unification — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Unify desktop authenticated pages with mobile's Dark Premium Gaming theme, and prepare CSS variables for future light/dark toggle.

**Architecture:** Replace `--nh-*` warm beige tokens with unified `--bg-*` / `--border-*` / `--text-*` variables defaulting to dark values on `:root`. Move current warm values under `.light` class. Update all shell/layout components to use new tokens instead of hardcoded colors.

**Tech Stack:** CSS custom properties, Tailwind CSS 4, React components (TSX)

---

### Task 1: Add unified design tokens and `.light` class to design-tokens.css

**Files:**
- Modify: `apps/web/src/styles/design-tokens.css:166-178` (--nh-* section)

- [ ] **Step 1: Add unified tokens and `.light` overrides**

In `design-tokens.css`, replace the `--nh-*` block (lines 166-178) with unified tokens + `.light` override. Keep `--nh-*` as aliases for backwards compatibility during migration.

```css
    /* ============================================================================
     * UNIFIED THEME TOKENS
     * Dark mode (default) — matches mobile Premium Gaming theme
     * Light overrides via .light class on <html>
     * ============================================================================ */

    /* Unified tokens (dark = default, consumed by all components) */
    --bg-base: var(--gaming-bg-base);
    --bg-elevated: var(--gaming-bg-elevated);
    --bg-glass: var(--gaming-bg-glass);
    --border-glass: var(--gaming-border-glass);
    --border-glass-hover: var(--gaming-border-glass-hover);
    --text-primary: var(--gaming-text-primary);
    --text-secondary: var(--gaming-text-secondary);
    --text-tertiary: var(--gaming-text-tertiary);
    --text-accent: var(--gaming-text-accent);

    /* Border-radius hierarchy */
    --radius-sheet: 1.25rem;   /* 20px — sheets/drawers top-only */
    --radius-card: 1rem;       /* 16px — cards */
    --radius-btn: 0.75rem;     /* 12px — buttons/inputs */
    --radius-pill: 3.125rem;   /* 50px — chips/pills */

    /* Legacy aliases (backwards compat — remove after full migration) */
    --nh-bg-base: #faf8f5;
    --nh-bg-surface: #fffcf8;
    --nh-bg-surface-end: #f5f0e8;
    --nh-bg-elevated: #fffcf8;
    --nh-border-default: rgba(160,120,60,0.08);
    --nh-text-primary: #1a1a1a;
    --nh-text-secondary: #5a4a35;
    --nh-text-muted: #8a7a65;
```

- [ ] **Step 2: Add `.light` class overrides after the `:root` block**

Add this new block after the closing `}` of `:root` in design-tokens.css (after all existing `:root` tokens):

```css
  /* Light mode overrides — apply via class="light" on <html> */
  .light {
    --bg-base: #faf8f5;
    --bg-elevated: #fffcf8;
    --bg-glass: rgba(160, 120, 60, 0.04);
    --border-glass: rgba(160, 120, 60, 0.08);
    --border-glass-hover: rgba(160, 120, 60, 0.14);
    --text-primary: #1a1a1a;
    --text-secondary: #5a4a35;
    --text-tertiary: #8a7a65;
    --text-accent: hsl(25, 95%, 38%);
  }
```

- [ ] **Step 3: Verify no syntax errors**

Run: `cd apps/web && pnpm build`
Expected: Build succeeds (CSS is valid)

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/styles/design-tokens.css
git commit -m "feat(theme): add unified dark-first design tokens with .light class"
```

---

### Task 2: Update globals.css body background and texture for dark theme

**Files:**
- Modify: `apps/web/src/styles/globals.css:168-210` (body styles and texture)

- [ ] **Step 1: Update body background and texture overlays**

Replace the body styles in `@layer base` (lines 177-209):

Old:
```css
  body {
    @apply bg-background text-foreground antialiased;
    font-family: var(--font-nunito, var(--font-sans));
    position: relative;
  }

  /* MeepleAI Background Texture System - Issue #2905 */
  /* Subtle Wood/Paper Texture */
  body::before {
    content: '';
    position: fixed;
    inset: 0;
    background-image:
      repeating-linear-gradient(0deg, transparent, transparent 2px, rgba(210, 105, 30, 0.015) 2px, rgba(210, 105, 30, 0.015) 4px),
      repeating-linear-gradient(90deg, transparent, transparent 2px, rgba(139, 90, 60, 0.02) 2px, rgba(139, 90, 60, 0.02) 4px);
    pointer-events: none;
    z-index: 0;
    opacity: 0.6;
  }

  /* Subtle warm gradient overlay */
  body::after {
    content: '';
    position: fixed;
    inset: 0;
    background: radial-gradient(ellipse at 50% 0%, rgba(210, 105, 30, 0.03), transparent 60%);
    pointer-events: none;
    z-index: 0;
  }
```

New:
```css
  body {
    @apply antialiased;
    font-family: var(--font-nunito, var(--font-sans));
    position: relative;
    background: var(--bg-base);
    color: var(--text-primary);
  }

  /* Background texture — only for light mode */
  .light body::before {
    content: '';
    position: fixed;
    inset: 0;
    background-image:
      repeating-linear-gradient(0deg, transparent, transparent 2px, rgba(210, 105, 30, 0.015) 2px, rgba(210, 105, 30, 0.015) 4px),
      repeating-linear-gradient(90deg, transparent, transparent 2px, rgba(139, 90, 60, 0.02) 2px, rgba(139, 90, 60, 0.02) 4px);
    pointer-events: none;
    z-index: 0;
    opacity: 0.6;
  }

  .light body::after {
    content: '';
    position: fixed;
    inset: 0;
    background: radial-gradient(ellipse at 50% 0%, rgba(210, 105, 30, 0.03), transparent 60%);
    pointer-events: none;
    z-index: 0;
  }
```

- [ ] **Step 2: Verify build**

Run: `cd apps/web && pnpm build`
Expected: Build succeeds

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/styles/globals.css
git commit -m "feat(theme): dark body background with light-mode texture fallback"
```

---

### Task 3: Update DesktopShell main container

**Files:**
- Modify: `apps/web/src/components/layout/UserShell/DesktopShell.tsx:37`

- [ ] **Step 1: Replace bg class**

Old (line 37):
```tsx
    <div className="min-h-dvh flex flex-col bg-[var(--nh-bg-base)]">
```

New:
```tsx
    <div className="min-h-dvh flex flex-col bg-[var(--bg-base)]">
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/components/layout/UserShell/DesktopShell.tsx
git commit -m "feat(theme): DesktopShell uses unified --bg-base token"
```

---

### Task 4: Update TopBar to dark elevated theme

**Files:**
- Modify: `apps/web/src/components/layout/UserShell/TopBar.tsx:36-38`

- [ ] **Step 1: Replace background and border**

Old (lines 34-38):
```tsx
    <header
      data-testid="top-bar"
      className="sticky top-0 z-40 h-16 flex items-center gap-4 px-6 border-b border-[var(--nh-border-default)] backdrop-blur-md"
      style={{ background: 'rgba(255, 252, 248, 0.85)' }}
    >
```

New:
```tsx
    <header
      data-testid="top-bar"
      className="sticky top-0 z-40 h-16 flex items-center gap-4 px-6 border-b border-[var(--border-glass)] backdrop-blur-[16px]"
      style={{ background: 'color-mix(in srgb, var(--bg-elevated) 95%, transparent)' }}
    >
```

- [ ] **Step 2: Verify TopBar renders correctly**

Run: `cd apps/web && pnpm build`
Expected: Build succeeds

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/layout/UserShell/TopBar.tsx
git commit -m "feat(theme): TopBar uses dark elevated background with glass border"
```

---

### Task 5: Update HandRail to unified dark tokens

**Files:**
- Modify: `apps/web/src/components/layout/HandRail/HandRail.tsx:28-30`

- [ ] **Step 1: Replace background and border classes**

Old (lines 28-29 inside cn()):
```tsx
        'bg-[hsl(220,15%,11%)] border-r border-white/5',
```

New:
```tsx
        'bg-[var(--bg-base)]/95 border-r border-[var(--border-glass)]',
```

- [ ] **Step 2: Update label text color**

Old (lines 40, 63 — both label spans):
```tsx
                'text-[7.5px] font-[800] tracking-[0.1em] uppercase text-white/28 px-[3px] mt-1',
```

New:
```tsx
                'text-[7.5px] font-[800] tracking-[0.1em] uppercase text-[var(--text-tertiary)] px-[3px] mt-1',
```

Apply the same change to both label spans (lines 40 and 63).

- [ ] **Step 3: Update divider color**

Old (line 55):
```tsx
            <div className="h-px bg-white/5 my-1" />
```

New:
```tsx
            <div className="h-px bg-[var(--border-glass)] my-1" />
```

- [ ] **Step 4: Update HandRail padding**

Old (line 35):
```tsx
      <div className="flex flex-col gap-1 p-[5px] flex-1 overflow-y-auto [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
```

New:
```tsx
      <div className="flex flex-col gap-1 p-2 flex-1 overflow-y-auto [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
```

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/layout/HandRail/HandRail.tsx
git commit -m "feat(theme): HandRail uses unified dark tokens and consistent spacing"
```

---

### Task 6: Update MobileStatusBar and MobileScorebar to gaming tokens

**Files:**
- Modify: `apps/web/src/components/session/MobileStatusBar.tsx:21-23`
- Modify: `apps/web/src/components/session/MobileScorebar.tsx:23`

- [ ] **Step 1: Update MobileStatusBar background/border**

Old (MobileStatusBar line 23):
```tsx
        'bg-card border-b border-border text-sm'
```

New:
```tsx
        'bg-[var(--bg-glass)] border-b border-[var(--border-glass)] text-sm text-[var(--text-primary)]'
```

- [ ] **Step 2: Update MobileStatusBar muted text classes**

Old (line 44):
```tsx
        <span className="text-xs text-muted-foreground truncate">· {currentPlayer}</span>
```

New:
```tsx
        <span className="text-xs text-[var(--text-secondary)] truncate">· {currentPlayer}</span>
```

Old (line 48):
```tsx
        <span className="text-xs text-muted-foreground">Turno {currentTurn}</span>
```

New:
```tsx
        <span className="text-xs text-[var(--text-secondary)]">Turno {currentTurn}</span>
```

- [ ] **Step 3: Update MobileScorebar cards**

Old (MobileScorebar line 23):
```tsx
          className="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-card border border-border shrink-0"
```

New:
```tsx
          className="flex items-center gap-2 px-3 py-1.5 rounded-[var(--radius-btn)] bg-[var(--bg-glass)] border border-[var(--border-glass)] shrink-0"
```

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/session/MobileStatusBar.tsx apps/web/src/components/session/MobileScorebar.tsx
git commit -m "feat(theme): MobileStatusBar and MobileScorebar use unified tokens"
```

---

### Task 7: Update ContextualHandSidebar (desktop) to dark tokens

**Files:**
- Modify: `apps/web/src/components/layout/ContextualHand/ContextualHandSidebar.tsx:43,49,53`

- [ ] **Step 1: Replace sidebar container classes**

Old (lines 42-43 in cn()):
```tsx
        'border-l border-[var(--nh-border-default)] bg-[var(--nh-bg-base)]',
```

New:
```tsx
        'border-l border-[var(--border-glass)] bg-[var(--bg-base)]',
```

- [ ] **Step 2: Replace header border**

Old (line 49):
```tsx
      <div className="flex items-center gap-2 border-b border-[var(--nh-border-default)] px-2 py-2.5">
```

New:
```tsx
      <div className="flex items-center gap-2 border-b border-[var(--border-glass)] px-2 py-2.5">
```

- [ ] **Step 3: Replace header text color**

Old (line 53):
```tsx
            <span className="font-quicksand text-sm font-semibold text-[var(--nh-text-default)]">
```

New:
```tsx
            <span className="font-quicksand text-sm font-semibold text-[var(--text-primary)]">
```

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/layout/ContextualHand/ContextualHandSidebar.tsx
git commit -m "feat(theme): ContextualHandSidebar uses unified dark tokens"
```

---

### Task 8: Update ContextualHandBottomBar (mobile) to unified tokens

**Files:**
- Modify: `apps/web/src/components/layout/ContextualHand/ContextualHandBottomBar.tsx:65-67,110,117,123`

- [ ] **Step 1: Replace bottom nav bar tokens**

Old (lines 65-67 in cn()):
```tsx
          'border-t border-[var(--nh-border-default)]',
          'bg-[var(--nh-bg-base)]/95 backdrop-blur-sm',
          'supports-[backdrop-filter]:bg-[var(--nh-bg-base)]/60'
```

New:
```tsx
          'border-t border-[var(--border-glass)]',
          'bg-[var(--bg-base)]/95 backdrop-blur-sm',
          'supports-[backdrop-filter]:bg-[var(--bg-base)]/60'
```

- [ ] **Step 2: Replace sheet tokens**

Old (line 110 in cn()):
```tsx
              'border-t border-[var(--nh-border-default)] bg-[var(--nh-bg-base)]',
```

New:
```tsx
              'border-t border-[var(--border-glass)] bg-[var(--bg-base)]',
```

Old (line 117):
```tsx
            <div className="sticky top-0 z-10 flex items-center justify-center bg-[var(--nh-bg-base)] pt-3 pb-1">
```

New:
```tsx
            <div className="sticky top-0 z-10 flex items-center justify-center bg-[var(--bg-base)] pt-3 pb-1">
```

Old (line 123):
```tsx
              <h3 className="font-quicksand text-sm font-semibold text-[var(--nh-text-default)]">
```

New:
```tsx
              <h3 className="font-quicksand text-sm font-semibold text-[var(--text-primary)]">
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/layout/ContextualHand/ContextualHandBottomBar.tsx
git commit -m "feat(theme): ContextualHandBottomBar uses unified tokens"
```

---

### Task 9: Update CardRack sidebar to dark tokens

**Files:**
- Modify: `apps/web/src/components/layout/CardRack/CardRack.tsx:57-58`

- [ ] **Step 1: Replace sidebar semantic colors**

Old (lines 57-58 in cn()):
```tsx
        'bg-sidebar text-sidebar-foreground',
        'border-r border-sidebar-border',
```

New:
```tsx
        'bg-[var(--bg-base)] text-[var(--text-primary)]',
        'border-r border-[var(--border-glass)]',
```

- [ ] **Step 2: Update divider**

Old (line 77):
```tsx
      <hr className="mx-3 border-sidebar-border" />
```

New:
```tsx
      <hr className="mx-3 border-[var(--border-glass)]" />
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/layout/CardRack/CardRack.tsx
git commit -m "feat(theme): CardRack uses unified dark tokens"
```

---

### Task 10: Update AdminShell to dark theme

**Files:**
- Modify: `apps/web/src/components/layout/AdminShell/AdminShell.tsx:30,42`

- [ ] **Step 1: Replace container and header backgrounds**

Old (line 30):
```tsx
    <div className="flex h-dvh bg-background">
```

New:
```tsx
    <div className="flex h-dvh bg-[var(--bg-base)]">
```

Old (line 42):
```tsx
          className="sticky top-0 z-40 h-[52px] flex items-center gap-3 px-4 border-b bg-background/95 backdrop-blur-md shrink-0"
```

New:
```tsx
          className="sticky top-0 z-40 h-[52px] flex items-center gap-3 px-4 border-b border-[var(--border-glass)] bg-[var(--bg-elevated)]/95 backdrop-blur-[16px] shrink-0"
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/components/layout/AdminShell/AdminShell.tsx
git commit -m "feat(theme): AdminShell uses unified dark tokens"
```

---

### Task 11: Build verification and visual sanity check

**Files:** None (verification only)

- [ ] **Step 1: Run full build**

Run: `cd apps/web && pnpm build`
Expected: Build succeeds with no errors

- [ ] **Step 2: Run typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: No type errors

- [ ] **Step 3: Run lint**

Run: `cd apps/web && pnpm lint`
Expected: No new lint errors

- [ ] **Step 4: Run existing tests**

Run: `cd apps/web && pnpm test --run`
Expected: No regressions (tests that were passing still pass)

- [ ] **Step 5: Commit any fixes if needed, then final commit**

```bash
git add -A
git commit -m "chore(theme): build verification pass — dark theme unification complete"
```
