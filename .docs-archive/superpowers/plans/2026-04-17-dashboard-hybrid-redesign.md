# Dashboard Hybrid Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign the dashboard from flat grid layout to "Tavolo Ibrido" — entity-colored zones, greeting strip with quick actions, varied layouts per section, and MeepleCard as the core interaction surface. Preserve all existing functionality including ManaPips.

**Architecture:** Replace `GreetingHeader` with `GreetingStrip` (avatar + stats + CTAs). Replace `HubBlock` with `EntityZone` (colored dot + title + count + "Vedi tutti" link). Sessions use horizontal scroll MeepleCard `list` variant. Toolkit uses MeepleCard `compact` variant with toolkit entity color. Cleanup `--nh-*` inline CSS vars → use `--mc-*` or Tailwind semantic tokens.

**Tech Stack:** Next.js 16 App Router, React 19, Tailwind 4, existing MeepleCard component system, existing `next-themes` dark mode, Vitest for testing.

**Branch:** `feature/dashboard-hybrid-redesign` from `feature/manapips-interactive-flow`

**Parent branch:** `feature/manapips-interactive-flow`

---

## File Structure

| Action | File | Responsibility |
|--------|------|----------------|
| Modify | `apps/web/src/app/(authenticated)/dashboard/DashboardClient.tsx` | Main dashboard — replace sub-components, use EntityZone layout |
| Create | `apps/web/src/components/dashboard/GreetingStrip.tsx` | Avatar + name + stats summary + quick action buttons |
| Create | `apps/web/src/components/dashboard/EntityZone.tsx` | Section wrapper with entity-colored dot, title, count, "Vedi tutti" link |
| Modify | `apps/web/src/components/layout/UserShell/TopBarV2.tsx` | Add visible ThemeToggle icon |
| Create | `apps/web/src/__tests__/components/dashboard/GreetingStrip.test.tsx` | Unit tests for GreetingStrip |
| Create | `apps/web/src/__tests__/components/dashboard/EntityZone.test.tsx` | Unit tests for EntityZone |

---

### Task 1: Create GreetingStrip component

**Files:**
- Create: `apps/web/src/components/dashboard/GreetingStrip.tsx`
- Create: `apps/web/src/__tests__/components/dashboard/GreetingStrip.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// apps/web/src/__tests__/components/dashboard/GreetingStrip.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { GreetingStrip } from '@/components/dashboard/GreetingStrip';

describe('GreetingStrip', () => {
  it('renders user display name', () => {
    render(
      <GreetingStrip
        displayName="Aaron"
        stats={{ games: 12, sessions: 8, agents: 4 }}
      />
    );
    expect(screen.getByText(/Ciao, Aaron/)).toBeInTheDocument();
  });

  it('renders stats summary', () => {
    render(
      <GreetingStrip
        displayName="Aaron"
        stats={{ games: 12, sessions: 8, agents: 4 }}
      />
    );
    expect(screen.getByText(/12 giochi/)).toBeInTheDocument();
    expect(screen.getByText(/8 sessioni/)).toBeInTheDocument();
  });

  it('renders quick action buttons', () => {
    render(
      <GreetingStrip
        displayName="Aaron"
        stats={{ games: 0, sessions: 0, agents: 0 }}
      />
    );
    expect(screen.getByRole('link', { name: /Aggiungi gioco/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Nuova sessione/i })).toBeInTheDocument();
  });

  it('renders user initial in avatar', () => {
    render(
      <GreetingStrip
        displayName="Aaron"
        stats={{ games: 0, sessions: 0, agents: 0 }}
      />
    );
    expect(screen.getByTestId('greeting-avatar')).toHaveTextContent('A');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/__tests__/components/dashboard/GreetingStrip.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Write GreetingStrip component**

```tsx
// apps/web/src/components/dashboard/GreetingStrip.tsx
'use client';

import Link from 'next/link';

export interface GreetingStripStats {
  games: number;
  sessions: number;
  agents: number;
}

interface GreetingStripProps {
  displayName: string;
  stats: GreetingStripStats;
}

export function GreetingStrip({ displayName, stats }: GreetingStripProps) {
  const initial = displayName.charAt(0).toUpperCase();
  const statsSummary = [
    stats.games > 0 ? `${stats.games} giochi` : null,
    stats.sessions > 0 ? `${stats.sessions} sessioni` : null,
    stats.agents > 0 ? `${stats.agents} agenti` : null,
  ]
    .filter(Boolean)
    .join(' · ');

  return (
    <div className="flex items-center gap-5 rounded-2xl border border-[hsl(25,40%,85%)] bg-gradient-to-r from-[hsl(25,40%,94%)] to-[hsl(30,35%,91%)] px-7 py-6 dark:border-[hsl(25,30%,25%)] dark:from-[hsl(25,20%,14%)] dark:to-[hsl(30,15%,12%)]">
      {/* Avatar */}
      <div
        data-testid="greeting-avatar"
        className="grid h-13 w-13 shrink-0 place-items-center rounded-xl bg-gradient-to-br from-[hsl(25,95%,45%)] to-[hsl(25,95%,55%)] text-xl font-bold text-white"
      >
        {initial}
      </div>

      {/* Text */}
      <div className="min-w-0">
        <h1 className="font-quicksand text-[22px] font-bold text-foreground">
          Ciao, {displayName} 👋
        </h1>
        <p className="mt-0.5 text-[13px] text-muted-foreground">
          La tua tavola da gioco{statsSummary ? ` · ${statsSummary}` : ''}
        </p>
      </div>

      {/* Quick Actions */}
      <div className="ml-auto flex shrink-0 gap-2 max-md:hidden">
        <Link
          href="/sessions/new"
          className="inline-flex items-center gap-1.5 rounded-full border border-[var(--mc-border)] bg-card/70 px-4 py-2 text-xs font-semibold text-foreground transition-all hover:bg-card hover:shadow-sm"
        >
          📋 Nuova sessione
        </Link>
        <Link
          href="/library?action=add"
          className="inline-flex items-center gap-1.5 rounded-full bg-[hsl(25,95%,45%)] px-4 py-2 text-xs font-semibold text-white shadow-[0_2px_12px_rgba(210,105,30,0.25)] transition-all hover:bg-[hsl(25,95%,38%)]"
        >
          + Aggiungi gioco
        </Link>
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/__tests__/components/dashboard/GreetingStrip.test.tsx`
Expected: PASS — all 4 tests

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/dashboard/GreetingStrip.tsx apps/web/src/__tests__/components/dashboard/GreetingStrip.test.tsx
git commit -m "feat(dashboard): add GreetingStrip component with avatar, stats, and quick actions"
```

---

### Task 2: Create EntityZone component

**Files:**
- Create: `apps/web/src/components/dashboard/EntityZone.tsx`
- Create: `apps/web/src/__tests__/components/dashboard/EntityZone.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// apps/web/src/__tests__/components/dashboard/EntityZone.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { EntityZone } from '@/components/dashboard/EntityZone';

describe('EntityZone', () => {
  it('renders title and count', () => {
    render(
      <EntityZone entity="game" title="Giochi" count={12} viewAllHref="/games">
        <div>children</div>
      </EntityZone>
    );
    expect(screen.getByText('Giochi')).toBeInTheDocument();
    expect(screen.getByText('12')).toBeInTheDocument();
  });

  it('renders entity-colored dot', () => {
    render(
      <EntityZone entity="session" title="Sessioni" count={3}>
        <div>children</div>
      </EntityZone>
    );
    const dot = screen.getByTestId('entity-dot');
    expect(dot).toBeInTheDocument();
  });

  it('renders "Vedi tutti" link when viewAllHref provided', () => {
    render(
      <EntityZone entity="game" title="Giochi" count={12} viewAllHref="/games">
        <div>children</div>
      </EntityZone>
    );
    expect(screen.getByText(/Vedi tutti/)).toBeInTheDocument();
  });

  it('does not render "Vedi tutti" when viewAllHref omitted', () => {
    render(
      <EntityZone entity="toolkit" title="Strumenti" count={4}>
        <div>children</div>
      </EntityZone>
    );
    expect(screen.queryByText(/Vedi tutti/)).not.toBeInTheDocument();
  });

  it('renders children', () => {
    render(
      <EntityZone entity="agent" title="Agenti" count={0}>
        <div data-testid="child">hello</div>
      </EntityZone>
    );
    expect(screen.getByTestId('child')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/__tests__/components/dashboard/EntityZone.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Write EntityZone component**

```tsx
// apps/web/src/components/dashboard/EntityZone.tsx
'use client';

import Link from 'next/link';

import { entityHsl } from '@/components/ui/data-display/meeple-card';
import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';

interface EntityZoneProps {
  entity: MeepleEntityType;
  title: string;
  count: number;
  viewAllHref?: string;
  children: React.ReactNode;
}

export function EntityZone({
  entity,
  title,
  count,
  viewAllHref,
  children,
}: EntityZoneProps) {
  const dotColor = entityHsl(entity);

  return (
    <section className="space-y-3">
      {/* Zone header */}
      <div className="flex items-center gap-2.5">
        <div
          data-testid="entity-dot"
          className="h-2.5 w-2.5 rounded-full"
          style={{ backgroundColor: dotColor }}
        />
        <h3 className="font-quicksand text-[13px] font-bold uppercase tracking-[0.08em] text-muted-foreground">
          {title}
        </h3>
        <span className="text-[11px] tabular-nums text-muted-foreground/60">
          {count}
        </span>
        {viewAllHref && (
          <Link
            href={viewAllHref}
            className="ml-auto text-xs font-semibold text-[hsl(25,95%,45%)] hover:underline"
          >
            Vedi tutti →
          </Link>
        )}
      </div>

      {/* Zone content */}
      {children}
    </section>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/__tests__/components/dashboard/EntityZone.test.tsx`
Expected: PASS — all 5 tests

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/dashboard/EntityZone.tsx apps/web/src/__tests__/components/dashboard/EntityZone.test.tsx
git commit -m "feat(dashboard): add EntityZone component with entity-colored header"
```

---

### Task 3: Rewrite DashboardClient with Hybrid layout

**Files:**
- Modify: `apps/web/src/app/(authenticated)/dashboard/DashboardClient.tsx`

This is the main task. We replace `GreetingHeader` with `GreetingStrip`, `HubBlock` with `EntityZone`, change sessions to horizontal scroll, and toolkit to grid. All existing functionality (search, filter, ManaPips, NewUserGamesBlock, EmptyCTA, OwnershipConfirmDialog) is preserved.

- [ ] **Step 1: Update imports at the top of DashboardClient.tsx**

Replace the file header (lines 1-19) with:

```tsx
'use client';

import { useEffect, useMemo, useState } from 'react';

import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { useAuth } from '@/components/auth/AuthProvider';
import { EntityZone } from '@/components/dashboard/EntityZone';
import { GreetingStrip } from '@/components/dashboard/GreetingStrip';
import { OwnershipConfirmDialog } from '@/components/dialogs/OwnershipConfirmDialog';
import { HubLayout, type FilterChip } from '@/components/layout/HubLayout';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import type { MeepleCardProps, MeepleEntityType } from '@/components/ui/data-display/meeple-card';
import { useActiveSessions } from '@/hooks/queries/useActiveSessions';
import { useAgents } from '@/hooks/queries/useAgents';
import { useBatchGameStatus } from '@/hooks/queries/useBatchGameStatus';
import { useGames } from '@/hooks/queries/useGames';
import { useAddGameToLibrary, useLibrary } from '@/hooks/queries/useLibrary';
import { useMiniNavConfig } from '@/hooks/useMiniNavConfig';
import { useRecentsStore } from '@/stores/use-recents';
```

- [ ] **Step 2: Remove old GreetingHeader and HubBlock — replace with nothing**

Delete the `GreetingHeader` function (lines 65-76) and `HubBlock` function (lines 78-90). These are replaced by `GreetingStrip` and `EntityZone` imported above.

- [ ] **Step 3: Replace ToolkitCarousel with ToolkitGrid**

Replace the `ToolkitCarousel` function (lines 389-421) with:

```tsx
function ToolkitGrid() {
  return (
    <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
      {TOOLKIT_TOOLS.map(tool => (
        <Link
          key={tool.id}
          href={`/toolkit?tool=${tool.id}`}
          className="flex flex-col items-center gap-1.5 rounded-xl border border-[hsl(142,30%,88%)] bg-[hsl(142,30%,96%)] p-4 text-center transition-all hover:-translate-y-0.5 hover:border-[hsl(142,70%,45%)] hover:shadow-md dark:border-[hsl(142,30%,25%)] dark:bg-[hsl(142,20%,12%)] dark:hover:border-[hsl(142,70%,40%)]"
        >
          <span className="text-[28px]">{tool.icon}</span>
          <span className="font-quicksand text-[13px] font-bold text-[hsl(142,50%,30%)] dark:text-[hsl(142,50%,65%)]">
            {tool.name}
          </span>
          <span className="text-[11px] text-muted-foreground">{tool.desc}</span>
        </Link>
      ))}
    </div>
  );
}
```

- [ ] **Step 4: Replace the render return in DashboardClient**

Replace the entire return block (lines 567-652) with:

```tsx
  // Build stats for greeting strip (inlined — too simple for a separate file)
  const stats = {
    games: gameItems.length,
    sessions: sessionItems.length,
    agents: agentItems.length,
  };

  return (
    <div className="mx-auto flex max-w-[1200px] flex-col gap-7 px-4 pb-24 pt-4">
      <GreetingStrip displayName={displayName} stats={stats} />

      {/* Games zone (orange) */}
      <EntityZone
        entity="game"
        title="Giochi"
        count={gameItems.length}
        viewAllHref="/games"
      >
        {isNewUser ? (
          <NewUserGamesBlock
            search={gamesSearch}
            onSearchChange={setGamesSearch}
            filter={gamesFilter}
            onFilterChange={setGamesFilter}
          />
        ) : (
          <HubLayout
            searchPlaceholder="Cerca giochi..."
            searchValue={gamesSearch}
            onSearchChange={setGamesSearch}
            filterChips={GAMES_FILTERS}
            activeFilterId={gamesFilter}
            onFilterChange={setGamesFilter}
          >
            <MeepleCardGrid items={filteredGameItems} isLoading={libraryLoading} />
          </HubLayout>
        )}
      </EntityZone>

      {/* Sessions zone (indigo) — horizontal scroll */}
      <EntityZone
        entity="session"
        title="Sessioni"
        count={sessionItems.length}
        viewAllHref="/sessions"
      >
        <HubLayout
          searchPlaceholder="Filtra per stato..."
          searchValue={sessionsSearch}
          onSearchChange={setSessionsSearch}
          filterChips={SESSIONS_FILTERS}
          activeFilterId={sessionsFilter}
          onFilterChange={setSessionsFilter}
        >
          {sessionsLoading ? (
            <LoadingSkeleton count={4} />
          ) : filteredSessionItems.length === 0 ? (
            <EmptyCTA
              icon="🎯"
              title="Nessuna sessione"
              sub="Inizia una nuova partita e traccia i tuoi progressi in tempo reale."
              actions={[{ label: '＋ Crea sessione', href: '/sessions/new', primary: true }]}
            />
          ) : (
            <div className="flex gap-3.5 overflow-x-auto pb-2 scrollbar-thin scrollbar-track-transparent scrollbar-thumb-border">
              {filteredSessionItems.map(item => (
                <div key={item.id ?? item.title} className="w-[260px] shrink-0">
                  <MeepleCard {...item} />
                </div>
              ))}
            </div>
          )}
        </HubLayout>
      </EntityZone>

      {/* Agents zone (amber) */}
      <EntityZone
        entity="agent"
        title="Agenti AI"
        count={agentItems.length}
        viewAllHref="/agents"
      >
        <HubLayout
          searchPlaceholder="Cerca agenti..."
          searchValue={agentsSearch}
          onSearchChange={setAgentsSearch}
          filterChips={AGENTS_FILTERS}
          activeFilterId={agentsFilter}
          onFilterChange={setAgentsFilter}
        >
          <MeepleCardGrid
            items={filteredAgentItems}
            isLoading={agentsLoading}
            emptyNode={
              <EmptyCTA
                icon="🤖"
                title="Nessun agente attivo"
                sub="Avvia una chat con un agente AI per ricevere aiuto durante la partita."
                actions={[
                  { label: '💬 Avvia chat', href: '/chat', primary: true },
                  { label: '＋ Crea agente', href: '/agents/new' },
                ]}
              />
            }
          />
        </HubLayout>
      </EntityZone>

      {/* Toolkit zone (green) */}
      <EntityZone entity="toolkit" title="Strumenti" count={TOOLKIT_TOOLS.length}>
        <ToolkitGrid />
      </EntityZone>
    </div>
  );
```

- [ ] **Step 5: Clean up --nh-* CSS vars in remaining sub-components**

In `LoadingSkeleton`, `EmptyCTA`, `CatalogGameCard`, and `NewUserGamesBlock` (all still in DashboardClient.tsx), replace all `var(--nh-*)` references with Tailwind semantic classes:

| Old | New |
|-----|-----|
| `text-[var(--nh-text-primary,#1a1a1a)]` | `text-foreground` |
| `text-[var(--nh-text-secondary,#5a4a35)]` | `text-muted-foreground` |
| `text-[var(--nh-text-muted,#94a3b8)]` | `text-muted-foreground/60` |
| `bg-[var(--nh-bg-card,white)]` | `bg-card` |
| `border-[var(--nh-border,rgba(0,0,0,0.07))]` | `border-border` |
| `text-[var(--nh-text-primary,#1a1a2e)]` | `text-foreground` |
| `text-[var(--nh-text-secondary,#64748b)]` | `text-muted-foreground` |

Apply these replacements across:
- `EmptyCTA` (~5 instances)
- `CatalogGameCard` (~8 instances)
- `NewUserGamesBlock` (~1 instance)
- `LoadingSkeleton` — change `bg-black/5` to `bg-muted` for dark mode compat

Additionally, fix hardcoded light-only colors for dark mode:
- `bg-amber-50` → `bg-amber-50 dark:bg-amber-950/30`
- `border-amber-100` → `border-amber-100 dark:border-amber-900/50`
- `from-[#fdf0e0] to-[#fce8cc]` in CatalogGameCard thumbnail → `from-[#fdf0e0] to-[#fce8cc] dark:from-[hsl(25,20%,18%)] dark:to-[hsl(30,15%,15%)]`

- [ ] **Step 6: Run typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: PASS — no type errors

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/dashboard/DashboardClient.tsx
git commit -m "feat(dashboard): rewrite to Hybrid layout with EntityZones, GreetingStrip, and ToolkitGrid"
```

---

### Task 4: Update existing dashboard tests

**Files:**
- Modify: `apps/web/src/__tests__/app/dashboard/DashboardClient.test.tsx` (if exists, otherwise create)

- [ ] **Step 1: Check if dashboard tests exist**

Run: `cd apps/web && find src/__tests__ -path "*dashboard*" -name "*.test.*" 2>/dev/null || echo "NO TESTS"`

- [ ] **Step 2: Write/update dashboard integration test**

The test should verify the new structure renders correctly with mocked data. Key assertions:

```tsx
// Key test cases:
// 1. GreetingStrip renders with user name
// 2. EntityZone headers show for each section (games, sessions, agents, toolkit)
// 3. MeepleCard grid renders game items
// 4. Sessions section renders in horizontal scroll (flex container)
// 5. ToolkitGrid renders 4 tools
// 6. EmptyCTA shows when sessions empty
// 7. NewUserGamesBlock shows when library empty
// 8. ManaPips data flow is not broken (no direct test needed — ManaPips are per-game, not on dashboard grid cards)
```

Mock the hooks: `useAuth`, `useLibrary`, `useActiveSessions`, `useAgents`, `useGames`, `useBatchGameStatus`, `useAddGameToLibrary`.

- [ ] **Step 3: Run all dashboard-related tests**

Run: `cd apps/web && pnpm vitest run --reporter=verbose src/__tests__/components/dashboard/ src/__tests__/app/dashboard/`
Expected: ALL PASS

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/__tests__/
git commit -m "test(dashboard): update tests for Hybrid layout redesign"
```

---

### Task 5: Add ThemeToggle to TopBarV2 (visible toggle)

**Files:**
- Modify: `apps/web/src/components/layout/UserShell/TopBarV2.tsx`

The ThemeToggle already exists in `UserMenuDropdown` (inside the dropdown menu). We add it as a visible icon button in the TopBarV2 for quick access. Note: the production layout uses `TopBarV2.tsx` (imported by `DesktopShell.tsx`), NOT the old `TopBar.tsx`.

- [ ] **Step 1: Add ThemeToggle import to TopBarV2.tsx**

In `apps/web/src/components/layout/UserShell/TopBarV2.tsx`, add import:

```tsx
import { ThemeToggle } from '@/components/ui/navigation/ThemeToggle';
```

- [ ] **Step 2: Add ThemeToggle between Search button and UserMenuDropdown**

In the TopBarV2 right-side `<div className="flex items-center gap-1">`, add `<ThemeToggle size="sm" />` between the Search button and `UserMenuDropdown`:

```tsx
      {/* Right side */}
      <div className="flex items-center gap-1">
        <Button variant="ghost" size="icon" aria-label="Cerca" onClick={onSearchClick}>
          <Search className="h-5 w-5" />
        </Button>

        <ThemeToggle size="sm" />

        <UserMenuDropdown />
      </div>
```

- [ ] **Step 3: Verify visually**

Run: `cd apps/web && pnpm dev`
Check: TopBar shows sun/moon icon. Clicking toggles between light and dark theme. All dashboard sections render correctly in both themes.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/layout/UserShell/TopBar.tsx
git commit -m "feat(topbar): add visible ThemeToggle for light/dark mode switching"
```

---

### Task 6: Verify dark mode rendering of new components

**Files:**
- No new files — visual verification and minor CSS fixes if needed

- [ ] **Step 1: Run dev server and toggle to dark mode**

Run: `cd apps/web && pnpm dev`
Navigate to `/dashboard`. Toggle dark mode via the new TopBar button.

- [ ] **Step 2: Verify each component in dark mode**

Check these elements render well in dark mode:
- GreetingStrip: gradient background should be dark warm tones (already coded in Step 3 of Task 1 with `dark:` variants)
- EntityZone: dot color is entity HSL (works in both themes), title uses `text-muted-foreground` (theme-aware)
- Game MeepleCards: use `--mc-*` vars which have dark overrides in `design-tokens.css`
- Session horizontal scroll: MeepleCard list variant has dark mode support
- ToolkitGrid: green-tinted cards with `dark:` variants (already coded in Task 4 Step 3)
- EmptyCTA: uses `bg-card` and `border-border` (theme-aware after Task 4 Step 5 cleanup)
- CatalogGameCard: uses `bg-card` and `text-foreground` after cleanup

- [ ] **Step 3: Fix any dark mode issues found**

If any component has hardcoded light-only colors (e.g., `bg-amber-50`), add `dark:` variant.

- [ ] **Step 4: Commit fixes if any**

```bash
git add -u
git commit -m "fix(dashboard): dark mode rendering adjustments"
```

---

### Task 7: Final typecheck, lint, and test run

**Files:** None — validation only

- [ ] **Step 1: Run typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: PASS

- [ ] **Step 2: Run lint**

Run: `cd apps/web && pnpm lint`
Expected: PASS (or only pre-existing warnings)

- [ ] **Step 3: Run all frontend tests**

Run: `cd apps/web && pnpm test`
Expected: PASS — no regressions. ManaPips tests (in useGameManaPips and ManaPips component tests) should be unaffected since we didn't modify ManaPips code.

- [ ] **Step 4: Final commit with any lint fixes**

```bash
git add -u
git commit -m "chore(dashboard): lint and type fixes"
```

---

## Summary of Changes

| What Changed | Before | After |
|-------------|--------|-------|
| Greeting | Simple h2 + p | GreetingStrip with avatar, stats, CTAs |
| Section headers | `HubBlock` (emoji + title) | `EntityZone` (colored dot + title + count + link) |
| Games layout | grid 2/3/4 cols | Same grid, wrapped in EntityZone |
| Sessions layout | grid 2/3/4 cols | Horizontal scroll, MeepleCard `list` variant |
| Agents layout | grid 2/3/4 cols | Same grid, wrapped in EntityZone |
| Toolkit | Carousel (overflow-x) | Grid 2/4 cols, green-tinted cards |
| CSS vars | `--nh-*` with fallbacks | Tailwind semantic (`text-foreground`, `bg-card`) |
| Theme toggle | Hidden in UserMenu dropdown | Visible in TopBar + still in dropdown |
| ManaPips | Preserved | Preserved (no changes) |
| Dark mode | Partially supported | Fully supported with `dark:` variants |

## What is NOT changed

- MeepleCard component (all variants) — untouched
- ManaPips component and useGameManaPips hook — untouched
- HubLayout component — still used for search/filter within zones
- All data hooks (useLibrary, useActiveSessions, useAgents) — untouched
- NewUserGamesBlock flow — preserved, only CSS cleaned up
- OwnershipConfirmDialog — untouched
- Server page.tsx — untouched
- TopBar structure — only ThemeToggle added
