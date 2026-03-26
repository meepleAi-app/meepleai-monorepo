# Sidebar Icons & Navbar Redesign — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace sidebar lucide-react icons with native emoji and redesign the navbar with breadcrumb emoji, search trigger, session status pill, quick action button, and contextual tabs.

**Architecture:** Modify 4 existing components in-place + create 2 new config/component files. The sidebar keeps its existing `NavSection[]` structure, only changing `icon` from `LucideIcon` to `string`. The navbar adds a new `NavContextTabs` component rendered inside `UserTopNav` — `ContextBar` stays unchanged.

**Tech Stack:** Next.js 16 App Router, Tailwind CSS 4, React 19, lucide-react (being removed from sidebar/tabbar)

**Spec:** `docs/superpowers/specs/2026-03-26-sidebar-navbar-redesign-design.md`

---

## File Map

| File | Action | Responsibility |
|------|--------|---------------|
| `apps/web/src/config/navigation-emoji.ts` | **CREATE** | Section emoji lookup for breadcrumb |
| `apps/web/src/config/contextual-tabs.ts` | **CREATE** | Route-to-tabs mapping for navbar row 2 |
| `apps/web/src/components/layout/UserShell/NavContextTabs.tsx` | **CREATE** | Contextual tab bar component |
| `apps/web/src/components/layout/UserShell/HybridSidebar.tsx` | MODIFY | Replace lucide icons with emoji, remove Collezioni |
| `apps/web/src/components/layout/UserShell/UserTabBar.tsx` | MODIFY | Replace lucide icons with emoji, fix active state |
| `apps/web/src/components/layout/UserShell/UserTopNav.tsx` | MODIFY | Add breadcrumb emoji, search trigger, session pill, quick action, render NavContextTabs |
| `apps/web/src/components/layout/UserShell/__tests__/HybridSidebar.test.tsx` | MODIFY | Update for emoji + removal of Collezioni |
| `apps/web/src/styles/design-tokens.css` | MODIFY | Update `--top-bar-height` from 48px to 52px |
| `apps/web/src/lib/stores/navbar-height-store.ts` | **CREATE** | Zustand mini-store for navbar height (sibling communication) |

---

### Task 1: Create navigation-emoji config

**Files:**
- Create: `apps/web/src/config/navigation-emoji.ts`

- [ ] **Step 1: Create the emoji lookup config**

```typescript
// apps/web/src/config/navigation-emoji.ts

export const SECTION_EMOJI: Record<string, string> = {
  '/dashboard': '🏠',
  '/library': '📚',
  '/sessions': '🎲',
  '/chat': '✨',
  '/agents': '🤖',
  '/settings': '⚙️',
};

/** Given a pathname, return the matching section emoji */
export function getSectionEmoji(pathname: string): string {
  for (const [prefix, emoji] of Object.entries(SECTION_EMOJI)) {
    if (pathname.startsWith(prefix)) return emoji;
  }
  return '🏠';
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/config/navigation-emoji.ts
git commit -m "feat(nav): add section emoji lookup config"
```

---

### Task 2: Create contextual-tabs config

**Files:**
- Create: `apps/web/src/config/contextual-tabs.ts`

- [ ] **Step 1: Create the route-to-tabs mapping**

```typescript
// apps/web/src/config/contextual-tabs.ts

export interface ContextualTab {
  label: string;
  href: string;
}

/**
 * Route prefix → contextual tabs shown in navbar row 2.
 * Matching: pathname.startsWith(key).
 * Active tab: exact match of href against pathname + search params.
 */
export const CONTEXTUAL_TABS: Record<string, ContextualTab[]> = {
  '/library': [
    { label: 'Collezione', href: '/library?tab=collection' },
    { label: 'Wishlist', href: '/library?tab=wishlist' },
    { label: 'Documenti', href: '/library?tab=private' },
    { label: 'Cronologia', href: '/library?tab=history' },
  ],
  '/sessions': [
    { label: 'In corso', href: '/sessions?tab=active' },
    { label: 'Completate', href: '/sessions?tab=completed' },
    { label: 'Pianificate', href: '/sessions?tab=planned' },
  ],
  '/chat': [
    { label: 'Thread', href: '/chat' },
    { label: 'Agenti', href: '/agents' },
  ],
  '/dashboard': [
    { label: 'Overview', href: '/dashboard' },
    { label: 'Attivita recente', href: '/dashboard?tab=activity' },
  ],
  '/settings': [
    { label: 'Profilo', href: '/settings?tab=profile' },
    { label: 'Preferenze', href: '/settings?tab=preferences' },
    { label: 'Account', href: '/settings?tab=account' },
  ],
};

/** Find tabs for the current pathname. Returns undefined if none match. */
export function getTabsForPathname(pathname: string): ContextualTab[] | undefined {
  for (const [prefix, tabs] of Object.entries(CONTEXTUAL_TABS)) {
    if (pathname.startsWith(prefix)) return tabs;
  }
  return undefined;
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/config/contextual-tabs.ts
git commit -m "feat(nav): add contextual tabs route config"
```

---

### Task 3: Create navbar height store and update design token

**Files:**
- Create: `apps/web/src/lib/stores/navbar-height-store.ts`
- Modify: `apps/web/src/styles/design-tokens.css`

CSS custom properties only inherit to DOM descendants, not siblings. Since `HybridSidebar` is a sibling of `UserTopNav` in `UserShellClient`, we use a Zustand mini-store for cross-component communication.

- [ ] **Step 1: Create the navbar height store**

```typescript
// apps/web/src/lib/stores/navbar-height-store.ts
import { create } from 'zustand';

interface NavbarHeightState {
  /** Current navbar height in pixels (52 without tabs, 88 with tabs) */
  height: 52;
  setHeight: (height: number) => void;
}

export const useNavbarHeightStore = create<NavbarHeightState>(set => ({
  height: 52,
  setHeight: (height: number) => set({ height }),
}));
```

- [ ] **Step 2: Update design-tokens.css**

In `apps/web/src/styles/design-tokens.css`, find `--top-bar-height: 48px;` and change to `--top-bar-height: 52px;`. This updates consumers (`LiveSessionLayout`, `CardRack`, `QuickView`) that use this token for their top offset calculations.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/lib/stores/navbar-height-store.ts apps/web/src/styles/design-tokens.css
git commit -m "feat(nav): add navbar height store, update --top-bar-height to 52px"
```

---

### Task 4: Replace HybridSidebar icons with emoji (was Task 3)

**Files:**
- Modify: `apps/web/src/components/layout/UserShell/HybridSidebar.tsx`
- Modify: `apps/web/src/components/layout/UserShell/__tests__/HybridSidebar.test.tsx`

- [ ] **Step 1: Update HybridSidebar.tsx**

Changes:
1. Remove the entire lucide-react import block (lines 14-26) and the `import type { LucideIcon }` (line 32).
2. Change `NavItem.icon` type from `LucideIcon` to `string`.
3. Replace icon values in `NAV_SECTIONS` with emoji strings:
   - `House` → `'🏠'`, `BookOpen` → `'📚'`, `Heart` → `'💝'`, `Dice5` → `'🎲'`
   - `MessageCircle` → `'✨'`, `FileText` → `'📜'`, `Bot` → `'🤖'`
4. Remove the entire 3rd section "Collezioni" from `NAV_SECTIONS` array (lines 85-92).
5. Change `BOTTOM_ITEMS` icon: `Settings` → `'⚙️'`.
6. In `SidebarLink`:
   - Remove line 120: `const Icon = item.icon;`
   - Replace ONLY the inner `<Icon className="w-5 h-5" />` at line 136. Keep the outer container `<span className="flex items-center justify-center w-[34px] h-[34px] shrink-0">` — it provides the touch target sizing.
   - The inner replacement:
     ```tsx
     <span className="text-xl leading-none" role="img" aria-label={item.label}>
       {item.icon}
     </span>
     ```
   - Result: `<span className="...w-[34px] h-[34px]..."><span role="img">{emoji}</span></span>`
7. Update the docstring (line 8): remove mention of "Three sections: Navigazione, AI Assistant, Collezioni", replace with "Two sections: Navigazione, AI Assistant".
8. Update the `<nav>` element to use navbar height store (CSS vars don't work across siblings):
   - Add import: `import { useNavbarHeightStore } from '@/lib/stores/navbar-height-store';`
   - In component body: `const navbarHeight = useNavbarHeightStore(s => s.height);`
   - Line 170: change `'fixed top-12 left-0 z-30'` → `'fixed left-0 z-30'` and add `style={{ top: navbarHeight }}`
   - Line 171: change `'h-[calc(100dvh-48px)]'` → add to style: `height: \`calc(100dvh - ${navbarHeight}px)\``

- [ ] **Step 2: Update HybridSidebar.test.tsx**

Changes:
1. Update the "renders section labels" test (line 44-49):
   - Remove: `expect(screen.getByText('Collezioni')).toBeInTheDocument();`
   - Keep: `Navigazione` and `AI Assistant` assertions
2. Add import for `within` at the top: `import { render, screen, within } from '@testing-library/react';`
3. Add a new test for emoji rendering:
   ```typescript
   it('renders emoji icons instead of SVG icons', () => {
     render(<HybridSidebar />);
     const dashboardLink = screen.getByLabelText(/dashboard/i);
     expect(within(dashboardLink).getByRole('img')).toBeInTheDocument();
   });
   ```

- [ ] **Step 3: Run tests to verify**

Run: `cd apps/web && pnpm vitest run src/components/layout/UserShell/__tests__/HybridSidebar.test.tsx`
Expected: All tests pass.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/layout/UserShell/HybridSidebar.tsx apps/web/src/components/layout/UserShell/__tests__/HybridSidebar.test.tsx
git commit -m "feat(sidebar): replace lucide icons with native emoji, remove Collezioni section"
```

---

### Task 5: Replace UserTabBar icons with emoji (was Task 4)

**Files:**
- Modify: `apps/web/src/components/layout/UserShell/UserTabBar.tsx`

- [ ] **Step 1: Update UserTabBar.tsx**

Changes:
1. Remove lucide-react imports (line 15): `import { BookOpen, Dice5, House, MessageCircle } from 'lucide-react';`
2. Remove `import type { LucideIcon } from 'lucide-react';` (line 21).
3. Change `TabConfig.icon` type from `LucideIcon` to `string`.
4. Replace icon values in `TABS`:
   - `House` → `'🏠'`, `BookOpen` → `'📚'`, `Dice5` → `'🎲'`, `MessageCircle` → `'✨'`
5. In the render function (lines 104-119), replace the `<Icon>` element with emoji span. Replace:
   ```tsx
   <Icon
     className={cn('transition-all duration-200', isActive ? 'w-6 h-6' : 'w-5 h-5')}
     style={isActive ? { color: tab.colorVar } : undefined}
     fill={isActive ? 'currentColor' : 'none'}
     strokeWidth={isActive ? 2 : 1.5}
   />
   ```
   With:
   ```tsx
   <span
     className={cn(
       'transition-all duration-200 leading-none',
       isActive ? 'text-2xl scale-110 bg-primary/15 rounded-full p-1.5' : 'text-xl'
     )}
     role="img"
     aria-label={tab.label}
   >
     {tab.icon}
   </span>
   ```

- [ ] **Step 2: Run typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: No type errors.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/layout/UserShell/UserTabBar.tsx
git commit -m "feat(tabbar): replace lucide icons with native emoji, update active state"
```

---

### Task 6: Create NavContextTabs component (was Task 5)

**Files:**
- Create: `apps/web/src/components/layout/UserShell/NavContextTabs.tsx`

- [ ] **Step 1: Create the component**

```tsx
// apps/web/src/components/layout/UserShell/NavContextTabs.tsx
'use client';

import Link from 'next/link';
import { usePathname, useSearchParams } from 'next/navigation';

import { getTabsForPathname } from '@/config/contextual-tabs';
import { cn } from '@/lib/utils';

export function NavContextTabs() {
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const tabs = getTabsForPathname(pathname);

  if (!tabs) return null;

  const currentUrl = searchParams.toString()
    ? `${pathname}?${searchParams.toString()}`
    : pathname;

  return (
    <div
      className={cn(
        'h-9 border-b border-border/40',
        'bg-background/90 backdrop-blur-xl',
        'flex items-end px-4 gap-1 overflow-x-auto'
      )}
      role="tablist"
      aria-label="Section tabs"
      data-testid="nav-context-tabs"
    >
      {tabs.map(tab => {
        const isActive = currentUrl === tab.href || pathname === tab.href;
        return (
          <Link
            key={tab.href}
            href={tab.href}
            role="tab"
            aria-selected={isActive}
            className={cn(
              'px-3 py-1.5 text-xs whitespace-nowrap transition-colors duration-200',
              isActive
                ? 'font-semibold text-foreground border-b-2 border-primary -mb-px'
                : 'text-muted-foreground hover:text-foreground'
            )}
          >
            {tab.label}
          </Link>
        );
      })}
    </div>
  );
}

/** Returns true if the current pathname has contextual tabs */
export function useHasContextTabs(): boolean {
  const pathname = usePathname();
  return !!getTabsForPathname(pathname);
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/components/layout/UserShell/NavContextTabs.tsx
git commit -m "feat(nav): add NavContextTabs component for contextual tab bar"
```

---

### Task 7: Redesign UserTopNav (was Task 6)

**Files:**
- Modify: `apps/web/src/components/layout/UserShell/UserTopNav.tsx`

This is the largest change. The navbar goes from a single 48px row to a two-row layout (52px main + 36px tabs when applicable).

- [ ] **Step 1: Update imports**

Replace `import { Search } from 'lucide-react';` with:
```typescript
import { getSectionEmoji } from '@/config/navigation-emoji';
```

Add import for NavContextTabs:
```typescript
import { NavContextTabs, useHasContextTabs } from './NavContextTabs';
```

- [ ] **Step 2: Update the header structure**

Replace the entire `<header>` JSX. Key changes:

1. **Header wrapper**: Change from single-row `h-12` to a flex column. Use navbar height store to communicate height to sibling `HybridSidebar`:
   ```tsx
   import { useNavbarHeightStore } from '@/lib/stores/navbar-height-store';

   // Inside the component:
   const hasTabs = useHasContextTabs();
   const setNavbarHeight = useNavbarHeightStore(s => s.setHeight);

   useEffect(() => {
     setNavbarHeight(hasTabs ? 88 : 52);
   }, [hasTabs, setNavbarHeight]);
   ```
   Wrap in a `<div>`:
   ```tsx
   <div className="sticky top-0 z-40">
     <header ...>Row 1</header>
     <Suspense><NavContextTabs /></Suspense>
   </div>
   ```
   Move `sticky top-0 z-40` from `<header>` to the outer `<div>` so both rows stick together.

2. **Row 1 (52px)**: Change height from `h-12` (48px) to `h-[52px]`.

3. **Breadcrumb with emoji**: In the non-game-mode center area, replace the current breadcrumb/sectionTitle block. Always show the section emoji + section title. When breadcrumbs have 3+ segments, show full breadcrumb with emoji prefix:
   ```tsx
   const sectionEmoji = getSectionEmoji(pathname);
   ```
   Render:
   ```tsx
   <div className="flex items-center gap-1 text-sm truncate min-w-0">
     <span className="text-base" role="img" aria-hidden="true">{sectionEmoji}</span>
     {showBreadcrumb ? (
       <nav aria-label="Breadcrumb" className="flex items-center gap-1 truncate">
         {breadcrumbs.map((crumb, i) => (
           <span key={crumb.href} className="flex items-center gap-1">
             {i > 0 && <span className="text-muted-foreground/50">›</span>}
             {i === breadcrumbs.length - 1 ? (
               <span className="font-medium text-foreground truncate">{crumb.label}</span>
             ) : (
               <Link href={crumb.href} className="text-muted-foreground hover:text-foreground transition-colors truncate">
                 {crumb.label}
               </Link>
             )}
           </span>
         ))}
       </nav>
     ) : (
       <span className="font-medium text-muted-foreground font-quicksand truncate">{sectionTitle}</span>
     )}
   </div>
   ```

4. **Search trigger**: Replace the existing search button (lines 114-127) with a wider styled button that looks like an input:
   ```tsx
   <button
     type="button"
     className="hidden lg:flex items-center gap-2 px-3 py-1.5 rounded-lg bg-muted/50 border border-border/60 text-muted-foreground text-xs hover:bg-muted/80 transition-colors max-w-[320px] flex-1"
     onClick={() => {
       document.dispatchEvent(new KeyboardEvent('keydown', { key: 'k', metaKey: true }));
     }}
     aria-label="Cerca (⌘K)"
   >
     <span role="img" aria-hidden="true">🔍</span>
     <span className="flex-1 text-left">Cerca giochi, documenti, chat...</span>
     <kbd className="text-[10px] bg-background border border-border px-1.5 py-0.5 rounded ml-auto">⌘K</kbd>
   </button>
   ```

5. **Session status pill**: Add between search and notifications (only when `activeSession` exists):
   ```tsx
   {activeSession && !isGameMode && (
     <Link
       href={`/sessions/live/${activeSession.id}`}
       className="hidden lg:flex items-center gap-1.5 h-7 px-2.5 rounded-full bg-emerald-500/10 border border-emerald-500/20 text-emerald-400 text-[11px] hover:bg-emerald-500/20 transition-colors"
     >
       <span className="w-1.5 h-1.5 rounded-full bg-emerald-400 animate-pulse" />
       Sessione live
     </Link>
   )}
   ```

6. **Quick action button**: Add a "+ Nuova" button:
   ```tsx
   <Link
     href="/sessions/new"
     className="hidden lg:flex items-center gap-1 h-7 px-3 rounded-lg bg-gradient-to-r from-emerald-500 to-teal-400 text-[11px] font-bold text-white hover:opacity-90 transition-opacity"
   >
     + Nuova
   </Link>
   ```

7. **Remove MeepleAI logo link** (line 72-74): The breadcrumb with emoji replaces it as the left-side anchor.

- [ ] **Step 3: Add `usePathname` import**

Add `usePathname` to existing next/navigation imports (currently only used in `useNavigation` hook, but now needed directly for `getSectionEmoji`):
```typescript
import { usePathname } from 'next/navigation';
```

- [ ] **Step 4: Run typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: No type errors.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/layout/UserShell/UserTopNav.tsx
git commit -m "feat(navbar): redesign with breadcrumb emoji, search, session pill, quick action, context tabs"
```

---

### Task 8: Wire up dynamic navbar height in UserShellClient (was Task 7)

**Files:**
- Modify: `apps/web/src/components/layout/UserShell/UserShellClient.tsx`

- [ ] **Step 1: Minimal update**

The `UserTopNav` now returns a wrapper `<div>` that includes both the header and `NavContextTabs`. The `ContextBar` component remains rendered separately in `UserShellClient` — no change needed there.

Since `UserTopNav` now renders both rows inside its own wrapper div with the CSS variable, and the layout is flex-based (`flex flex-col h-dvh`), the main content area auto-adjusts. No height change needed in `UserShellClient`.

However, verify the import of `Suspense` wraps the tabs properly. Since `NavContextTabs` uses `useSearchParams` (which requires Suspense), and it's rendered inside `UserTopNav`, wrap the `NavContextTabs` inside `UserTopNav` with `<Suspense>`.

This means the Suspense boundary goes in `UserTopNav.tsx` (Task 6), not here. `UserShellClient.tsx` needs no changes beyond verifying it still works.

- [ ] **Step 2: Verify build**

Run: `cd apps/web && pnpm build`
Expected: Build succeeds.

- [ ] **Step 3: Commit (only if changes were needed)**

```bash
git add apps/web/src/components/layout/UserShell/UserShellClient.tsx
git commit -m "chore(shell): adjust layout for new navbar height"
```

---

### Task 9: Visual verification and final typecheck

- [ ] **Step 1: Run full typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: No errors.

- [ ] **Step 2: Run all layout tests**

Run: `cd apps/web && pnpm vitest run src/components/layout/`
Expected: All tests pass.

- [ ] **Step 3: Run lint**

Run: `cd apps/web && pnpm lint`
Expected: No errors.

- [ ] **Step 4: Final commit if any fixes were needed**

```bash
git add -u
git commit -m "fix(nav): address lint/type issues from navbar redesign"
```

---

### Task 10: Create PR

- [ ] **Step 1: Push and create PR**

```bash
git push -u origin HEAD
```

Create PR to parent branch (detect with `git config branch.$(git branch --show-current).parent` — typically `frontend-dev`):
```bash
gh pr create --base frontend-dev --title "feat(ui): emoji sidebar icons + navbar redesign" --body "$(cat <<'EOF'
## Summary
- Replace lucide-react sidebar icons with native emoji for a more cheerful feel
- Redesign navbar: breadcrumb with emoji, search bar trigger, session status pill, quick action button
- Add contextual tab bar (NavContextTabs) below navbar that changes per route section
- Remove placeholder Collezioni section from sidebar
- Update mobile tab bar with emoji + new active state treatment

## Spec
docs/superpowers/specs/2026-03-26-sidebar-navbar-redesign-design.md

## Test plan
- [ ] Sidebar shows emoji icons on desktop
- [ ] Sidebar hover expands with labels
- [ ] Mobile bottom tab bar shows emoji with active state
- [ ] Navbar shows breadcrumb with section emoji
- [ ] Search trigger opens command palette (⌘K)
- [ ] Session pill appears when a live session exists
- [ ] Context tabs show for /library, /sessions, /chat, /dashboard, /settings
- [ ] Context tabs hidden on routes without mapping
- [ ] Admin shell unchanged
- [ ] Public/auth pages unchanged

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

PR base: parent branch (detect with `git config branch.<current>.parent`).
