# ManaPips Interactive Flow — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Transform ManaPips from decorative dots to interactive navigation/creation system, add ownership confirmation dialog, game detail page, and conditional KB selection in session wizard.

**Architecture:** ManaPips become `<button>` elements with Radix Popover for count > 0. New game detail page `/games/[id]` uses Hero MeepleCard with `lg` ManaPips. Session wizard gains conditional KB step using existing `useKbGameDocuments` hook.

**Tech Stack:** React 19, Next.js 16, Radix UI (Popover, AlertDialog), TanStack Query, Tailwind 4, Vitest

**Spec:** `docs/superpowers/specs/2026-04-17-manapips-interactive-flow-design.md`

---

## File Structure

### New Files
| File | Responsibility |
|------|---------------|
| `apps/web/src/components/ui/data-display/meeple-card/parts/ManaPipPopover.tsx` | Radix Popover showing linked entity list + "Create" button |
| `apps/web/src/components/dialogs/OwnershipConfirmDialog.tsx` | Radix AlertDialog for game ownership confirmation |
| `apps/web/src/app/(authenticated)/games/[id]/page.tsx` | Game detail page with Hero MeepleCard + tabs |
| `apps/web/src/hooks/queries/useGameManaPips.ts` | Hook aggregating session/KB/agent counts for a game |
| `apps/web/__tests__/components/ManaPipPopover.test.tsx` | Tests for ManaPipPopover |
| `apps/web/__tests__/components/OwnershipConfirmDialog.test.tsx` | Tests for OwnershipConfirmDialog |
| `apps/web/__tests__/components/ManaPips.interactive.test.tsx` | Tests for interactive ManaPips behavior |
| `apps/web/__tests__/pages/GameDetailPage.test.tsx` | Tests for game detail page |
| `apps/web/__tests__/components/SessionWizardKbStep.test.tsx` | Tests for KB selection step |

### Modified Files
| File | Change |
|------|--------|
| `apps/web/src/components/ui/data-display/meeple-card/parts/ManaPips.tsx` | `<span>` → `<button>`, onClick handlers, `lg` size variant |
| `apps/web/src/components/ui/data-display/meeple-card/types.ts` | Extend `ManaPip` interface with `items`, `onCreate`, `createLabel` |
| `apps/web/src/app/(authenticated)/sessions/new/session-wizard-mobile.tsx` | Add conditional KB step (step 4→5, step 5→6) |
| `apps/web/src/app/(authenticated)/dashboard/DashboardClient.tsx` | Replace CatalogGameCard button with ManaPip `game` interaction |

---

## Task 1: Extend ManaPip Types

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/types.ts`
- Modify: `apps/web/src/components/ui/data-display/meeple-card/parts/ManaPips.tsx`

- [ ] **Step 1: Update ManaPip interface in types.ts**

In `apps/web/src/components/ui/data-display/meeple-card/parts/ManaPips.tsx`, replace the `ManaPip` interface:

```ts
export interface ManaPipItem {
  id: string;
  label: string;
  href: string;
}

export interface ManaPip {
  entityType: MeepleEntityType;
  count?: number;
  items?: ManaPipItem[];
  onCreate?: () => void;
  createLabel?: string;
}
```

And update `ManaPipsProps` to add `lg` size:

```ts
interface ManaPipsProps {
  pips: ManaPip[];
  /** sm = 6px no badge (compact); md = 8px with count badge (grid); lg = 12px with label (hero) */
  size?: 'sm' | 'md' | 'lg';
}
```

- [ ] **Step 2: Verify types compile**

Run: `cd apps/web && pnpm typecheck`
Expected: PASS (no consumers break — added optional fields only)

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/parts/ManaPips.tsx
git commit -m "feat(meeple-card): extend ManaPip interface with items, onCreate, createLabel, lg size"
```

---

## Task 2: Interactive ManaPips Component

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/parts/ManaPips.tsx`
- Test: `apps/web/__tests__/components/ManaPips.interactive.test.tsx`

- [ ] **Step 1: Write failing tests**

Create `apps/web/__tests__/components/ManaPips.interactive.test.tsx`:

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { ManaPips } from '@/components/ui/data-display/meeple-card/parts/ManaPips';

describe('ManaPips interactive', () => {
  it('renders pips as buttons when onCreate is provided', () => {
    const onCreate = vi.fn();
    render(
      <ManaPips
        pips={[{ entityType: 'session', count: 0, onCreate, createLabel: 'Nuova Sessione' }]}
        size="md"
      />
    );
    const btn = screen.getByRole('button', { name: /session/i });
    expect(btn).toBeInTheDocument();
  });

  it('calls onCreate directly when count is 0', () => {
    const onCreate = vi.fn();
    render(
      <ManaPips
        pips={[{ entityType: 'session', count: 0, onCreate, createLabel: 'Nuova Sessione' }]}
        size="md"
      />
    );
    fireEvent.click(screen.getByRole('button', { name: /session/i }));
    expect(onCreate).toHaveBeenCalledTimes(1);
  });

  it('does not call onCreate when count > 0 (popover expected)', () => {
    const onCreate = vi.fn();
    render(
      <ManaPips
        pips={[
          {
            entityType: 'session',
            count: 3,
            onCreate,
            items: [
              { id: '1', label: 'Session 1', href: '/sessions/1' },
            ],
          },
        ]}
        size="md"
      />
    );
    fireEvent.click(screen.getByRole('button', { name: /session/i }));
    expect(onCreate).not.toHaveBeenCalled();
  });

  it('renders as non-interactive span when no onCreate', () => {
    render(
      <ManaPips pips={[{ entityType: 'kb', count: 2 }]} size="md" />
    );
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it('renders lg size with text label', () => {
    render(
      <ManaPips
        pips={[{ entityType: 'session', count: 5, onCreate: vi.fn(), createLabel: 'Nuova' }]}
        size="lg"
      />
    );
    expect(screen.getByText('5 Session')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/web && pnpm vitest run __tests__/components/ManaPips.interactive.test.tsx`
Expected: FAIL — current ManaPips renders `<span>`, not `<button>`

- [ ] **Step 3: Implement interactive ManaPips**

Replace `apps/web/src/components/ui/data-display/meeple-card/parts/ManaPips.tsx`:

```tsx
'use client';

import { useState } from 'react';

import { entityHsl, entityLabel } from '../tokens';

import { ManaPipPopover } from './ManaPipPopover';

import type { MeepleEntityType } from '../types';

export interface ManaPipItem {
  id: string;
  label: string;
  href: string;
}

export interface ManaPip {
  entityType: MeepleEntityType;
  count?: number;
  items?: ManaPipItem[];
  onCreate?: () => void;
  createLabel?: string;
}

interface ManaPipsProps {
  pips: ManaPip[];
  /** sm = 6px no badge (compact); md = 8px with count badge (grid); lg = 12px with label (hero) */
  size?: 'sm' | 'md' | 'lg';
}

const MAX_VISIBLE = 3;

const DOT_SIZES = { sm: 6, md: 8, lg: 12 } as const;

export function ManaPips({ pips, size = 'md' }: ManaPipsProps) {
  if (pips.length === 0) return null;

  const visible = pips.slice(0, MAX_VISIBLE);
  const overflow = pips.length - MAX_VISIBLE;
  const dotSize = DOT_SIZES[size];

  return (
    <div className="flex items-center gap-1 px-3 pb-2 pt-0.5">
      {visible.map((pip, i) => (
        <PipRenderer key={i} pip={pip} size={size} dotSize={dotSize} />
      ))}
      {overflow > 0 && (
        <span className="text-[9px] font-semibold text-[var(--mc-text-muted,#94a3b8)]">
          +{overflow}
        </span>
      )}
    </div>
  );
}

function PipRenderer({
  pip,
  size,
  dotSize,
}: {
  pip: ManaPip;
  size: 'sm' | 'md' | 'lg';
  dotSize: number;
}) {
  const [popoverOpen, setPopoverOpen] = useState(false);
  const color = entityHsl(pip.entityType);
  const isInteractive = !!pip.onCreate;
  const count = pip.count ?? 0;

  const handleClick = () => {
    if (!isInteractive) return;
    if (count === 0) {
      pip.onCreate?.();
    } else {
      setPopoverOpen(true);
    }
  };

  const dot = (
    <span
      className="relative inline-flex items-center justify-center rounded-full"
      style={{ width: dotSize, height: dotSize, background: color, flexShrink: 0 }}
    >
      {size !== 'sm' && count > 0 && (
        <span
          className="absolute -top-2 left-1/2 -translate-x-1/2 whitespace-nowrap rounded-full px-1 text-[7px] font-bold text-white"
          style={{ background: color, lineHeight: '10px', minWidth: 12, textAlign: 'center' }}
        >
          {count}
        </span>
      )}
    </span>
  );

  const label = size === 'lg' ? (
    <span className="text-[11px] font-semibold text-[var(--mc-text-secondary,#94a3b8)]">
      {count} {entityLabel[pip.entityType]}
    </span>
  ) : null;

  if (!isInteractive) {
    return (
      <span data-pip title={pip.entityType} className="inline-flex items-center gap-1">
        {dot}
        {label}
      </span>
    );
  }

  // Interactive: wrap in button
  const buttonContent = (
    <button
      type="button"
      data-pip
      aria-label={pip.entityType}
      onClick={handleClick}
      className="inline-flex items-center gap-1 rounded-full transition-transform hover:scale-125 focus-visible:outline-2 focus-visible:outline-offset-2"
      style={{ '--pip-glow': color } as React.CSSProperties}
    >
      {dot}
      {label}
    </button>
  );

  if (count > 0 && size !== 'sm') {
    return (
      <ManaPipPopover
        open={popoverOpen}
        onOpenChange={setPopoverOpen}
        items={pip.items ?? []}
        onCreate={pip.onCreate}
        createLabel={pip.createLabel}
        entityType={pip.entityType}
      >
        {buttonContent}
      </ManaPipPopover>
    );
  }

  return buttonContent;
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run __tests__/components/ManaPips.interactive.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/parts/ManaPips.tsx apps/web/__tests__/components/ManaPips.interactive.test.tsx
git commit -m "feat(meeple-card): make ManaPips interactive with button rendering and popover support"
```

---

## Task 3: ManaPipPopover Component

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/parts/ManaPipPopover.tsx`
- Test: `apps/web/__tests__/components/ManaPipPopover.test.tsx`

- [ ] **Step 1: Write failing tests**

Create `apps/web/__tests__/components/ManaPipPopover.test.tsx`:

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { ManaPipPopover } from '@/components/ui/data-display/meeple-card/parts/ManaPipPopover';

describe('ManaPipPopover', () => {
  const items = [
    { id: '1', label: 'Sessione 1', href: '/sessions/1' },
    { id: '2', label: 'Sessione 2', href: '/sessions/2' },
  ];

  it('renders children (trigger)', () => {
    render(
      <ManaPipPopover
        open={false}
        onOpenChange={vi.fn()}
        items={items}
        entityType="session"
      >
        <button>trigger</button>
      </ManaPipPopover>
    );
    expect(screen.getByText('trigger')).toBeInTheDocument();
  });

  it('shows items when open', () => {
    render(
      <ManaPipPopover
        open={true}
        onOpenChange={vi.fn()}
        items={items}
        entityType="session"
      >
        <button>trigger</button>
      </ManaPipPopover>
    );
    expect(screen.getByText('Sessione 1')).toBeInTheDocument();
    expect(screen.getByText('Sessione 2')).toBeInTheDocument();
  });

  it('shows create button when onCreate provided', () => {
    const onCreate = vi.fn();
    render(
      <ManaPipPopover
        open={true}
        onOpenChange={vi.fn()}
        items={items}
        onCreate={onCreate}
        createLabel="Nuova Sessione"
        entityType="session"
      >
        <button>trigger</button>
      </ManaPipPopover>
    );
    const createBtn = screen.getByRole('button', { name: /nuova sessione/i });
    fireEvent.click(createBtn);
    expect(onCreate).toHaveBeenCalledTimes(1);
  });

  it('renders empty state without items', () => {
    render(
      <ManaPipPopover
        open={true}
        onOpenChange={vi.fn()}
        items={[]}
        onCreate={vi.fn()}
        createLabel="Crea"
        entityType="session"
      >
        <button>trigger</button>
      </ManaPipPopover>
    );
    expect(screen.getByRole('button', { name: /crea/i })).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/web && pnpm vitest run __tests__/components/ManaPipPopover.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement ManaPipPopover**

Create `apps/web/src/components/ui/data-display/meeple-card/parts/ManaPipPopover.tsx`:

```tsx
'use client';

import type { ReactNode } from 'react';

import { Plus } from 'lucide-react';
import Link from 'next/link';

import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/primitives/popover';

import { entityHsl, entityIcon } from '../tokens';

import type { MeepleEntityType } from '../types';
import type { ManaPipItem } from './ManaPips';

interface ManaPipPopoverProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  items: ManaPipItem[];
  onCreate?: () => void;
  createLabel?: string;
  entityType: MeepleEntityType;
  children: ReactNode;
}

export function ManaPipPopover({
  open,
  onOpenChange,
  items,
  onCreate,
  createLabel,
  entityType,
  children,
}: ManaPipPopoverProps) {
  const color = entityHsl(entityType);
  const icon = entityIcon[entityType];

  return (
    <Popover open={open} onOpenChange={onOpenChange}>
      <PopoverTrigger asChild>{children}</PopoverTrigger>
      <PopoverContent
        className="w-56 rounded-xl border border-[var(--mc-border)] bg-[var(--mc-bg-card)] p-0 shadow-lg backdrop-blur-md"
        sideOffset={8}
        align="start"
      >
        {items.length > 0 && (
          <ul className="max-h-48 overflow-y-auto py-1">
            {items.map(item => (
              <li key={item.id}>
                <Link
                  href={item.href}
                  onClick={() => onOpenChange(false)}
                  className="flex items-center gap-2 px-3 py-2 text-sm text-[var(--mc-text-primary)] transition-colors hover:bg-[var(--mc-bg-hover,rgba(255,255,255,0.05))]"
                >
                  <span className="text-xs">{icon}</span>
                  <span className="truncate">{item.label}</span>
                </Link>
              </li>
            ))}
          </ul>
        )}
        {onCreate && (
          <button
            type="button"
            aria-label={createLabel ?? 'Crea nuovo'}
            onClick={() => {
              onCreate();
              onOpenChange(false);
            }}
            className="flex w-full items-center gap-2 border-t border-[var(--mc-border)] px-3 py-2 text-sm font-semibold transition-colors hover:bg-[var(--mc-bg-hover,rgba(255,255,255,0.05))]"
            style={{ color }}
          >
            <Plus className="h-3.5 w-3.5" />
            {createLabel ?? 'Crea nuovo'}
          </button>
        )}
      </PopoverContent>
    </Popover>
  );
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run __tests__/components/ManaPipPopover.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/parts/ManaPipPopover.tsx apps/web/__tests__/components/ManaPipPopover.test.tsx
git commit -m "feat(meeple-card): add ManaPipPopover for linked entity navigation"
```

---

## Task 4: OwnershipConfirmDialog

**Files:**
- Create: `apps/web/src/components/dialogs/OwnershipConfirmDialog.tsx`
- Test: `apps/web/__tests__/components/OwnershipConfirmDialog.test.tsx`

- [ ] **Step 1: Write failing tests**

Create `apps/web/__tests__/components/OwnershipConfirmDialog.test.tsx`:

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { OwnershipConfirmDialog } from '@/components/dialogs/OwnershipConfirmDialog';

describe('OwnershipConfirmDialog', () => {
  it('renders game title when open', () => {
    render(
      <OwnershipConfirmDialog
        open={true}
        onOpenChange={vi.fn()}
        gameTitle="Mage Knight"
        onConfirm={vi.fn()}
        confirming={false}
      />
    );
    expect(screen.getByText('Mage Knight')).toBeInTheDocument();
  });

  it('shows copyright/KB motivation text', () => {
    render(
      <OwnershipConfirmDialog
        open={true}
        onOpenChange={vi.fn()}
        gameTitle="Mage Knight"
        onConfirm={vi.fn()}
        confirming={false}
      />
    );
    expect(screen.getByText(/knowledge base/i)).toBeInTheDocument();
    expect(screen.getByText(/copyright/i)).toBeInTheDocument();
  });

  it('calls onConfirm when confirm button clicked', () => {
    const onConfirm = vi.fn();
    render(
      <OwnershipConfirmDialog
        open={true}
        onOpenChange={vi.fn()}
        gameTitle="Mage Knight"
        onConfirm={onConfirm}
        confirming={false}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: /possiedo il gioco/i }));
    expect(onConfirm).toHaveBeenCalledTimes(1);
  });

  it('disables confirm button while confirming', () => {
    render(
      <OwnershipConfirmDialog
        open={true}
        onOpenChange={vi.fn()}
        gameTitle="Mage Knight"
        onConfirm={vi.fn()}
        confirming={true}
      />
    );
    expect(screen.getByRole('button', { name: /possiedo il gioco/i })).toBeDisabled();
  });

  it('has cancel button', () => {
    const onOpenChange = vi.fn();
    render(
      <OwnershipConfirmDialog
        open={true}
        onOpenChange={onOpenChange}
        gameTitle="Mage Knight"
        onConfirm={vi.fn()}
        confirming={false}
      />
    );
    expect(screen.getByRole('button', { name: /annulla/i })).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/web && pnpm vitest run __tests__/components/OwnershipConfirmDialog.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement OwnershipConfirmDialog**

Create `apps/web/src/components/dialogs/OwnershipConfirmDialog.tsx`:

```tsx
'use client';

import {
  AlertDialog,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogTitle,
  AlertDialogCancel,
  AlertDialogAction,
} from '@/components/ui/primitives/alert-dialog';

interface OwnershipConfirmDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  gameTitle: string;
  onConfirm: () => void;
  confirming: boolean;
}

export function OwnershipConfirmDialog({
  open,
  onOpenChange,
  gameTitle,
  onConfirm,
  confirming,
}: OwnershipConfirmDialogProps) {
  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent className="max-w-sm rounded-2xl">
        <div className="flex flex-col items-center gap-3 text-center">
          <span className="text-4xl">🎲</span>
          <AlertDialogTitle className="font-quicksand text-lg font-bold">
            {gameTitle}
          </AlertDialogTitle>
          <AlertDialogDescription className="text-sm leading-relaxed text-muted-foreground">
            Per accedere alla Knowledge Base di questo gioco (regolamento, FAQ, strategie), conferma
            di possederne una copia fisica. Questo ci permette di offrirti il contenuto nel rispetto
            del copyright.
          </AlertDialogDescription>
        </div>
        <div className="mt-4 flex flex-col gap-2">
          <AlertDialogAction
            onClick={onConfirm}
            disabled={confirming}
            className="w-full rounded-xl bg-amber-600 font-semibold text-white hover:bg-amber-700"
          >
            {confirming ? 'Aggiunta in corso...' : 'Possiedo il gioco'}
          </AlertDialogAction>
          <AlertDialogCancel className="w-full rounded-xl">Annulla</AlertDialogCancel>
        </div>
      </AlertDialogContent>
    </AlertDialog>
  );
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run __tests__/components/OwnershipConfirmDialog.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/dialogs/OwnershipConfirmDialog.tsx apps/web/__tests__/components/OwnershipConfirmDialog.test.tsx
git commit -m "feat(ui): add OwnershipConfirmDialog for copyright-aware library addition"
```

---

## Task 5: useGameManaPips Hook

**Files:**
- Create: `apps/web/src/hooks/queries/useGameManaPips.ts`

- [ ] **Step 1: Implement the hook**

Create `apps/web/src/hooks/queries/useGameManaPips.ts`:

```ts
'use client';

import { useQuery } from '@tanstack/react-query';

import { api } from '@/lib/api';

import type { ManaPip, ManaPipItem } from '@/components/ui/data-display/meeple-card/parts/ManaPips';

export const gameManaPipsKeys = {
  byGame: (gameId: string) => ['game-mana-pips', gameId] as const,
} as const;

interface GameManaPipsData {
  sessions: { count: number; items: ManaPipItem[] };
  kbs: { count: number; items: ManaPipItem[] };
  agents: { count: number; items: ManaPipItem[] };
}

/**
 * Aggregates session, KB, and agent counts for a game to build ManaPips data.
 * Makes parallel calls to existing endpoints.
 */
export function useGameManaPips(gameId: string | undefined) {
  return useQuery<GameManaPipsData>({
    queryKey: gameManaPipsKeys.byGame(gameId ?? ''),
    queryFn: async (): Promise<GameManaPipsData> => {
      // NOTE: No getSessionsByGame endpoint exists.
      // Use getActive() and filter client-side by gameName match.
      // api.agents.getUserAgentsForGame(gameId) exists.
      // api.knowledgeBase.getGameDocuments(gameId) exists.
      const [activeSessions, kbDocs, agents] = await Promise.all([
        api.liveSessions.getActive().catch(() => []),
        api.knowledgeBase.getGameDocuments(gameId!).catch(() => []),
        api.agents.getUserAgentsForGame(gameId!).catch(() => []),
      ]);

      // Filter sessions by gameId (LiveSessionSummaryDto has gameId field)
      const gameSessions = activeSessions.filter(
        (s: { gameId?: string }) => s.gameId === gameId
      );

      return {
        sessions: {
          count: gameSessions.length,
          items: gameSessions.slice(0, 10).map((s: { id: string; gameName?: string; createdAt: string }) => ({
            id: s.id,
            label: s.gameName ?? `Sessione ${new Date(s.createdAt).toLocaleDateString('it-IT')}`,
            href: `/sessions/live/${s.id}`,
          })),
        },
        kbs: {
          count: kbDocs.length,
          items: kbDocs.slice(0, 10).map(d => ({
            id: d.id,
            label: d.title ?? d.fileName,
            href: `/games/${gameId}/rules`,
          })),
        },
        agents: {
          count: agents.length,
          items: agents.slice(0, 10).map(a => ({
            id: a.id,
            label: a.name ?? 'Agente',
            href: `/agents/${a.id}/chat`,
          })),
        },
      };
    },
    enabled: !!gameId,
    staleTime: 2 * 60_000,
  });
}

/**
 * Builds ManaPip[] array from hook data, with action callbacks.
 */
export function buildGameManaPips(
  data: GameManaPipsData | undefined,
  actions: {
    onCreateSession?: () => void;
    onCreateAgent?: () => void;
  }
): ManaPip[] {
  if (!data) return [];

  const pips: ManaPip[] = [];

  pips.push({
    entityType: 'session',
    count: data.sessions.count,
    items: data.sessions.items,
    onCreate: actions.onCreateSession,
    createLabel: 'Nuova Sessione',
  });

  if (data.kbs.count > 0) {
    pips.push({
      entityType: 'kb',
      count: data.kbs.count,
      items: data.kbs.items,
    });
  }

  if (data.agents.count > 0 || actions.onCreateAgent) {
    pips.push({
      entityType: 'agent',
      count: data.agents.count,
      items: data.agents.items,
      onCreate: actions.onCreateAgent,
      createLabel: 'Nuovo Agente',
    });
  }

  return pips;
}
```

- [ ] **Step 2: Verify types compile**

Run: `cd apps/web && pnpm typecheck`
Expected: PASS (or note API method name mismatches to fix — check `api.sessions`, `api.agents` clients for actual method names)

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/hooks/queries/useGameManaPips.ts
git commit -m "feat(hooks): add useGameManaPips hook aggregating session/KB/agent counts"
```

---

## Task 6: Game Detail Page

**Files:**
- Create: `apps/web/src/app/(authenticated)/games/[id]/page.tsx`
- Test: `apps/web/__tests__/pages/GameDetailPage.test.tsx`

- [ ] **Step 1: Write failing test**

Create `apps/web/__tests__/pages/GameDetailPage.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

// Mock next/navigation
vi.mock('next/navigation', () => ({
  useParams: () => ({ id: 'game-123' }),
  useRouter: () => ({ push: vi.fn() }),
}));

// Mock hooks
vi.mock('@/hooks/queries/useSharedGames', () => ({
  useSharedGame: () => ({
    data: {
      id: 'game-123',
      title: 'Mage Knight',
      publisher: 'Wizkids',
      averageRating: 8.1,
      imageUrl: null,
    },
    isLoading: false,
  }),
}));

vi.mock('@/hooks/queries/useLibrary', () => ({
  useGameInLibraryStatus: () => ({
    data: { inLibrary: true },
    isLoading: false,
  }),
}));

vi.mock('@/hooks/queries/useGameManaPips', () => ({
  useGameManaPips: () => ({
    data: {
      sessions: { count: 2, items: [] },
      kbs: { count: 1, items: [] },
      agents: { count: 0, items: [] },
    },
    isLoading: false,
  }),
  buildGameManaPips: () => [
    { entityType: 'session', count: 2, items: [] },
    { entityType: 'kb', count: 1, items: [] },
  ],
}));

describe('GameDetailPage', () => {
  it('renders game title in hero card', async () => {
    const { default: GameDetailPage } = await import(
      '@/app/(authenticated)/games/[id]/page'
    );
    render(<GameDetailPage />);
    expect(screen.getByText('Mage Knight')).toBeInTheDocument();
  });

  it('renders publisher subtitle', async () => {
    const { default: GameDetailPage } = await import(
      '@/app/(authenticated)/games/[id]/page'
    );
    render(<GameDetailPage />);
    expect(screen.getByText('Wizkids')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run __tests__/pages/GameDetailPage.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement game detail page**

Create `apps/web/src/app/(authenticated)/games/[id]/page.tsx`:

```tsx
'use client';

import { useParams, useRouter } from 'next/navigation';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { useSharedGame } from '@/hooks/queries/useSharedGames';
import { useGameInLibraryStatus } from '@/hooks/queries/useLibrary';
import { useGameManaPips, buildGameManaPips } from '@/hooks/queries/useGameManaPips';

export default function GameDetailPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const { data: game, isLoading: gameLoading } = useSharedGame(id);
  const { data: libraryStatus } = useGameInLibraryStatus(id);
  const { data: manaPipsData } = useGameManaPips(id);

  const manaPips = buildGameManaPips(manaPipsData, {
    onCreateSession: () => {
      router.push(`/sessions/new?gameId=${id}&gameName=${encodeURIComponent(game?.title ?? '')}`);
    },
  });

  if (gameLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <p className="text-sm text-muted-foreground">Caricamento...</p>
      </div>
    );
  }

  if (!game) {
    return (
      <div className="flex items-center justify-center py-24">
        <p className="text-sm text-muted-foreground">Gioco non trovato</p>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-2xl space-y-6 px-4 py-6">
      <MeepleCard
        entity="game"
        variant="hero"
        title={game.title}
        subtitle={game.publisher ?? undefined}
        imageUrl={game.imageUrl ?? undefined}
        rating={game.averageRating ?? undefined}
        ratingMax={10}
        badge={libraryStatus?.inLibrary ? 'In libreria' : undefined}
        manaPips={manaPips}
      />

      {/* Tabs placeholder — will integrate existing sub-pages */}
      <div className="rounded-2xl border border-[var(--mc-border)] bg-[var(--mc-bg-card)] p-4">
        <nav className="flex gap-4 border-b border-[var(--mc-border)] pb-2">
          <button
            onClick={() => router.push(`/games/${id}/rules`)}
            className="text-sm font-semibold text-amber-500"
          >
            Regole
          </button>
          <button
            onClick={() => router.push(`/games/${id}/faqs`)}
            className="text-sm text-muted-foreground hover:text-foreground"
          >
            FAQ
          </button>
          <button
            onClick={() => router.push(`/games/${id}/sessions`)}
            className="text-sm text-muted-foreground hover:text-foreground"
          >
            Sessioni
          </button>
          <button
            onClick={() => router.push(`/games/${id}/strategies`)}
            className="text-sm text-muted-foreground hover:text-foreground"
          >
            Strategie
          </button>
        </nav>
        <p className="pt-4 text-center text-sm text-muted-foreground">
          Seleziona una sezione per visualizzarla
        </p>
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run __tests__/pages/GameDetailPage.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/games/\[id\]/page.tsx apps/web/__tests__/pages/GameDetailPage.test.tsx
git commit -m "feat(games): add game detail page with Hero MeepleCard and ManaPips"
```

---

## Task 7: KB Selection Step in Session Wizard

**Files:**
- Modify: `apps/web/src/app/(authenticated)/sessions/new/session-wizard-mobile.tsx`
- Test: `apps/web/__tests__/components/SessionWizardKbStep.test.tsx`

- [ ] **Step 1: Write failing test**

Create `apps/web/__tests__/components/SessionWizardKbStep.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

// Inline KB step component for isolated testing
import { KbSelectionStep } from '@/app/(authenticated)/sessions/new/KbSelectionStep';

vi.mock('@/hooks/queries/useGameDocuments', () => ({
  useKbGameDocuments: () => ({
    data: [
      { id: 'doc-1', title: 'Regolamento Base', fileName: 'rules.pdf', documentType: 'Rulebook' },
      { id: 'doc-2', title: 'Espansione Lost Legion', fileName: 'expansion.pdf', documentType: 'Expansion' },
    ],
    isLoading: false,
  }),
}));

describe('KbSelectionStep', () => {
  it('renders document list when multiple KBs', () => {
    render(
      <KbSelectionStep
        gameId="game-123"
        selectedDocIds={['doc-1']}
        onSelectionChange={vi.fn()}
      />
    );
    expect(screen.getByText('Regolamento Base')).toBeInTheDocument();
    expect(screen.getByText('Espansione Lost Legion')).toBeInTheDocument();
  });

  it('shows toggle for each document', () => {
    render(
      <KbSelectionStep
        gameId="game-123"
        selectedDocIds={['doc-1']}
        onSelectionChange={vi.fn()}
      />
    );
    const switches = screen.getAllByRole('switch');
    expect(switches).toHaveLength(2);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run __tests__/components/SessionWizardKbStep.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Create KbSelectionStep component**

Create `apps/web/src/app/(authenticated)/sessions/new/KbSelectionStep.tsx`:

```tsx
'use client';

import { BookOpen } from 'lucide-react';

import { Switch } from '@/components/ui/primitives/switch';
import { useKbGameDocuments } from '@/hooks/queries/useGameDocuments';

interface KbSelectionStepProps {
  gameId: string;
  selectedDocIds: string[];
  onSelectionChange: (docIds: string[]) => void;
}

export function KbSelectionStep({ gameId, selectedDocIds, onSelectionChange }: KbSelectionStepProps) {
  const { data: documents = [], isLoading } = useKbGameDocuments(gameId);

  const toggleDoc = (docId: string) => {
    if (selectedDocIds.includes(docId)) {
      onSelectionChange(selectedDocIds.filter(id => id !== docId));
    } else {
      onSelectionChange([...selectedDocIds, docId]);
    }
  };

  if (isLoading) {
    return <p className="py-8 text-center text-sm text-muted-foreground">Caricamento documenti...</p>;
  }

  return (
    <div className="space-y-4">
      <div>
        <h2 className="text-lg font-bold font-quicksand">Knowledge Base</h2>
        <p className="text-sm text-muted-foreground">
          Seleziona quali documenti usare per l&apos;assistente AI durante la sessione.
        </p>
      </div>
      <div className="space-y-2">
        {documents.map(doc => {
          const isSelected = selectedDocIds.includes(doc.id);
          return (
            <div
              key={doc.id}
              className="flex items-center justify-between rounded-xl border border-border bg-card px-3 py-2.5"
            >
              <div className="flex items-center gap-2">
                <BookOpen className="h-4 w-4 text-muted-foreground" />
                <div>
                  <p className="text-sm font-medium">{doc.title ?? doc.fileName}</p>
                  {doc.documentType && (
                    <span className="text-[10px] font-semibold uppercase text-muted-foreground">
                      {doc.documentType}
                    </span>
                  )}
                </div>
              </div>
              <Switch
                checked={isSelected}
                onCheckedChange={() => toggleDoc(doc.id)}
                aria-label={`Seleziona ${doc.title ?? doc.fileName}`}
              />
            </div>
          );
        })}
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run __tests__/components/SessionWizardKbStep.test.tsx`
Expected: PASS

- [ ] **Step 5: Integrate KB step into session wizard**

In `apps/web/src/app/(authenticated)/sessions/new/session-wizard-mobile.tsx`, make these changes:

1. Add import at top:
```ts
import { BookOpen } from 'lucide-react';
import { useKbGameDocuments } from '@/hooks/queries/useGameDocuments';
import { KbSelectionStep } from './KbSelectionStep';
```

2. Change `WizardStep` type from `1 | 2 | 3 | 4 | 5` to `1 | 2 | 3 | 4 | 5 | 6`

3. Add state for KB:
```ts
const [selectedDocIds, setSelectedDocIds] = useState<string[]>([]);
```

4. Add KB document query:
```ts
const { data: kbDocuments = [] } = useKbGameDocuments(selectedGameId ?? undefined);
const showKbStep = kbDocuments.length >= 2;
```

5. Update `STEP_ICONS` to include KB step:
```ts
const STEP_ICONS: Record<WizardStep, React.ElementType> = {
  1: Gamepad2,
  2: Users,
  3: ArrowUpDown,
  4: showKbStep ? BookOpen : Layers,
  5: showKbStep ? Layers : Rocket,
  6: Rocket,
};
```

6. Adjust step navigation: after step 3, go to step 4 (KB) if `showKbStep`, else skip to step 5 (phases). The "Ready" step becomes 5 or 6 depending on `showKbStep`.

7. Add KB step rendering between Turn Order and Phases steps:
```tsx
{step === 4 && showKbStep && (
  <KbSelectionStep
    gameId={selectedGameId!}
    selectedDocIds={selectedDocIds}
    onSelectionChange={setSelectedDocIds}
  />
)}
```

- [ ] **Step 6: Verify typecheck passes**

Run: `cd apps/web && pnpm typecheck`
Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/sessions/new/KbSelectionStep.tsx apps/web/src/app/\(authenticated\)/sessions/new/session-wizard-mobile.tsx apps/web/__tests__/components/SessionWizardKbStep.test.tsx
git commit -m "feat(sessions): add conditional KB selection step in session wizard"
```

---

## Task 8: Wire Dashboard CatalogGameCard with ManaPips

**Files:**
- Modify: `apps/web/src/app/(authenticated)/dashboard/DashboardClient.tsx`

- [ ] **Step 1: Add OwnershipConfirmDialog integration to dashboard**

In `DashboardClient.tsx`, replace the `CatalogGameCard` component's "Aggiungi" button flow with ManaPip `game` interaction:

1. Add imports:
```ts
import { OwnershipConfirmDialog } from '@/components/dialogs/OwnershipConfirmDialog';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { useRouter } from 'next/navigation';
```

2. In the `NewUserGamesBlock` component, add state:
```ts
const [confirmGame, setConfirmGame] = useState<{ id: string; title: string } | null>(null);
const router = useRouter();
```

3. Replace `CatalogGameCard` usage with `MeepleCard` using ManaPip `game`:
```tsx
<MeepleCard
  entity="game"
  variant="compact"
  title={game.title}
  subtitle={game.publisher ?? undefined}
  imageUrl={game.imageUrl ?? undefined}
  rating={game.averageRating ?? undefined}
  ratingMax={10}
  manaPips={
    inLibrary
      ? []
      : [
          {
            entityType: 'game',
            count: 0,
            onCreate: () => setConfirmGame({ id: game.id, title: game.title }),
            createLabel: 'Aggiungi a libreria',
          },
        ]
  }
  onClick={() => {
    if (inLibrary) router.push(`/games/${game.id}`);
  }}
/>
```

4. Add dialog at component root:
```tsx
<OwnershipConfirmDialog
  open={!!confirmGame}
  onOpenChange={open => { if (!open) setConfirmGame(null); }}
  gameTitle={confirmGame?.title ?? ''}
  onConfirm={() => {
    if (confirmGame) {
      addToLibrary(confirmGame.id);
      setConfirmGame(null);
      router.push(`/games/${confirmGame.id}`);
    }
  }}
  confirming={adding}
/>
```

- [ ] **Step 2: Verify typecheck passes**

Run: `cd apps/web && pnpm typecheck`
Expected: PASS

- [ ] **Step 3: Smoke test in browser**

Navigate to http://localhost:3000, verify:
- Dashboard shows game cards
- Clicking ManaPip game on a catalog card opens ownership dialog
- Confirming adds to library and redirects to game detail

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/dashboard/DashboardClient.tsx
git commit -m "feat(dashboard): wire CatalogGameCard with ManaPip game + OwnershipConfirmDialog"
```

---

## Task 9: HeroCard ManaPips Rendering

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/HeroCard.tsx`

- [ ] **Step 1: Add ManaPips to HeroCard**

The current HeroCard doesn't render `manaPips`. Add it:

```tsx
// Add import at top
import { ManaPips } from '../parts/ManaPips';

// In the props destructuring, add:
const { manaPips, ...rest } = props;

// Before the closing </div> of the outer container, after the rating block:
{manaPips && manaPips.length > 0 && (
  <div className="mt-3">
    <ManaPips pips={manaPips} size="lg" />
  </div>
)}
```

- [ ] **Step 2: Verify in browser**

Navigate to http://localhost:3000/games/[any-game-id], verify ManaPips render in `lg` size with labels on the hero card.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/variants/HeroCard.tsx
git commit -m "feat(meeple-card): render ManaPips in HeroCard variant"
```

---

## Task 10: Final Typecheck + Lint + Existing Tests

- [ ] **Step 1: Run typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: PASS

- [ ] **Step 2: Run linter**

Run: `cd apps/web && pnpm lint`
Expected: PASS (fix any issues)

- [ ] **Step 3: Run all existing tests**

Run: `cd apps/web && pnpm test`
Expected: All existing tests still pass. Fix any regressions from ManaPips interface changes.

- [ ] **Step 4: Final commit**

```bash
git add -A
git commit -m "chore: fix lint and test regressions from ManaPips interactive flow"
```
