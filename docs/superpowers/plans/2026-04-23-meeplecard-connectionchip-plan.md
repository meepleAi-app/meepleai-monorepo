# MeepleCard ConnectionChip — Implementation Plan (Step 1: Additive)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace `ManaPips` + `NavFooterItem` duplication with a unified `ConnectionChip` component wearing Lucide icons, and consolidate `CardStatus` into orthogonal `ownership`/`lifecycle` axes — all additive and backward-compatible.

**Architecture:** New tokens + new components coexist with legacy props. `MeepleCard` accepts both APIs; internal adapter maps legacy `CardStatus` to the new two-axis model. Deletion of legacy files is deferred to Step 3 (out of scope here).

**Tech Stack:** Next.js 16, React 19, TypeScript, Tailwind 4, Radix Popover, Lucide icons, Vitest.

**Spec:** `docs/superpowers/specs/2026-04-23-meeplecard-connectionchip-design.md`

**Out of scope (follow-up plans):**
- Typography scale refactor (§9 of spec)
- Card surface warm tokens (§8.4)
- Dark mode entity color parity with `useEntityColor` hook (§8.3)
- Contrast validation CI script (§11.1)
- Migration of consumer call-sites (Step 2)
- Legacy cleanup (Step 3)

---

## File Structure

**New files (parts/):**
- `entity-icons.tsx` — Lucide icon registry keyed by `MeepleEntityType`
- `ConnectionChip.tsx` — unified chip replacing ManaPip + NavFooterItem visual
- `ConnectionChipStrip.tsx` — layout container (variant: footer | inline)
- `ConnectionChipPopover.tsx` — popover drill-down with items + create
- `OwnershipBadge.tsx` — corner badge for owned/wishlist/archived
- `LifecycleStateBadge.tsx` — inline badge for active/idle/completed/setup/processing/failed
- `status-adapter.ts` — pure function mapping legacy `CardStatus` → `{ownership, lifecycle}`

**Modified files:**
- `tokens.ts` — add `entityTokens()` helper returning named derived colors
- `types.ts` — add `OwnershipBadge`, `LifecycleState`, `ConnectionItem`, `ConnectionChipProps`, extend `MeepleCardProps`
- `index.ts` — re-export new components
- `MeepleCard.tsx` — internal legacy→new status adapter (no prop removal)

**New tests (parts/__tests__/):**
- `entity-icons.test.tsx`
- `ConnectionChip.test.tsx`
- `ConnectionChipStrip.test.tsx`
- `ConnectionChipPopover.test.tsx`
- `OwnershipBadge.test.tsx`
- `LifecycleStateBadge.test.tsx`
- `status-adapter.test.ts`

**Untouched (legacy, retained for backward compat until Step 3):**
- `ManaPips.tsx`, `ManaPipPopover.tsx`, `NavFooter.tsx`, `nav-items/*`, existing `entityIcon` emoji in `tokens.ts`

---

## Task 1: Create entity icons registry

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/parts/entity-icons.tsx`
- Test: `apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/entity-icons.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/entity-icons.test.tsx
import { describe, expect, it } from 'vitest';
import { render } from '@testing-library/react';

import { entityIcons, ENTITY_ICON_STROKE, ENTITY_ICON_SIZE } from '../entity-icons';

import type { MeepleEntityType } from '../../types';

const allEntities: MeepleEntityType[] = [
  'game', 'player', 'session', 'agent',
  'kb', 'chat', 'event', 'toolkit', 'tool',
];

describe('entityIcons registry', () => {
  it('has a Lucide component for every MeepleEntityType', () => {
    for (const entity of allEntities) {
      expect(entityIcons[entity]).toBeDefined();
      expect(typeof entityIcons[entity]).toBe('object');
    }
  });

  it('exports canonical stroke and size constants', () => {
    expect(ENTITY_ICON_STROKE).toBe(1.75);
    expect(ENTITY_ICON_SIZE.sm).toBe(14);
    expect(ENTITY_ICON_SIZE.md).toBe(16);
  });

  it('each icon renders as an SVG element', () => {
    for (const entity of allEntities) {
      const Icon = entityIcons[entity];
      const { container } = render(<Icon size={14} strokeWidth={1.75} />);
      const svg = container.querySelector('svg');
      expect(svg).toBeTruthy();
      expect(svg?.getAttribute('stroke-width')).toBe('1.75');
    }
  });
});
```

- [ ] **Step 2: Run test to verify failure**

Run: `cd apps/web && pnpm test src/components/ui/data-display/meeple-card/parts/__tests__/entity-icons.test.tsx`
Expected: FAIL — module not found.

- [ ] **Step 3: Create entity-icons.tsx**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/parts/entity-icons.tsx
import {
  BookOpen,
  Bot,
  Briefcase,
  Calendar,
  Dices,
  MessageCircle,
  Swords,
  UserCircle2,
  Wrench,
  type LucideIcon,
} from 'lucide-react';

import type { MeepleEntityType } from '../types';

/**
 * Unified Lucide icon registry for all MeepleEntityType values.
 *
 * Use this instead of `tokens.entityIcon` (emoji, deprecated) or `nav-items/icons.tsx`
 * (semantic keys, legacy). Every entity has a single canonical Lucide component.
 */
export const entityIcons: Record<MeepleEntityType, LucideIcon> = {
  game: Dices,
  player: UserCircle2,
  session: Swords,
  agent: Bot,
  kb: BookOpen,
  chat: MessageCircle,
  event: Calendar,
  toolkit: Briefcase,
  tool: Wrench,
};

export const ENTITY_ICON_STROKE = 1.75;

export const ENTITY_ICON_SIZE = {
  sm: 14,
  md: 16,
} as const;
```

- [ ] **Step 4: Run test to verify pass**

Run: `cd apps/web && pnpm test src/components/ui/data-display/meeple-card/parts/__tests__/entity-icons.test.tsx`
Expected: PASS 3/3 tests.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/parts/entity-icons.tsx \
        apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/entity-icons.test.tsx
git commit -m "feat(meeple-card): add unified entity-icons Lucide registry"
```

---

## Task 2: Add `entityTokens()` derived-color helper

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/tokens.ts`
- Test: `apps/web/src/components/ui/data-display/meeple-card/__tests__/tokens-entityTokens.test.ts`

- [ ] **Step 1: Write failing test**

```ts
// apps/web/src/components/ui/data-display/meeple-card/__tests__/tokens-entityTokens.test.ts
import { describe, expect, it } from 'vitest';

import { entityTokens } from '../tokens';

describe('entityTokens()', () => {
  it('returns solid color using existing entityHsl', () => {
    const t = entityTokens('game');
    expect(t.solid).toBe('hsl(25, 95%, 45%)');
  });

  it('returns fill with 0.12 alpha', () => {
    const t = entityTokens('game');
    expect(t.fill).toBe('hsla(25, 95%, 45%, 0.12)');
  });

  it('returns border with 0.35 alpha', () => {
    const t = entityTokens('game');
    expect(t.border).toBe('hsla(25, 95%, 45%, 0.35)');
  });

  it('returns named tokens for hover, glow, shadow, muted, dashed', () => {
    const t = entityTokens('kb');
    expect(t.hover).toBe('hsla(210, 40%, 55%, 0.22)');
    expect(t.glow).toBe('hsla(210, 40%, 55%, 0.18)');
    expect(t.shadow).toBe('hsla(210, 40%, 55%, 0.25)');
    expect(t.muted).toBe('hsla(210, 40%, 55%, 0.06)');
    expect(t.dashed).toBe('hsla(210, 40%, 55%, 0.25)');
  });

  it('returns textOn = #ffffff', () => {
    expect(entityTokens('agent').textOn).toBe('#ffffff');
  });
});
```

- [ ] **Step 2: Run to verify failure**

Run: `cd apps/web && pnpm test src/components/ui/data-display/meeple-card/__tests__/tokens-entityTokens.test.ts`
Expected: FAIL — `entityTokens` not exported.

- [ ] **Step 3: Implement helper**

Append to `apps/web/src/components/ui/data-display/meeple-card/tokens.ts`:

```ts
/**
 * Named derived colors for an entity.
 *
 * Replaces inline `entityHsl(entity, 0.12)` calls with semantic names.
 * Use these when styling ConnectionChip, badges, popovers, etc.
 */
export interface EntityTokens {
  solid: string;   // full color — icons, badge bg
  fill: string;    // 0.12 — chip bg default
  border: string;  // 0.35 — chip border default
  hover: string;   // 0.22 — chip bg on hover
  glow: string;    // 0.18 — box-shadow spread
  shadow: string;  // 0.25 — box-shadow drop
  muted: string;   // 0.06 — empty/disabled bg
  dashed: string;  // 0.25 — dashed border empty state
  textOn: string;  // text color on solid bg
}

export function entityTokens(entity: MeepleEntityType): EntityTokens {
  return {
    solid: entityHsl(entity),
    fill: entityHsl(entity, 0.12),
    border: entityHsl(entity, 0.35),
    hover: entityHsl(entity, 0.22),
    glow: entityHsl(entity, 0.18),
    shadow: entityHsl(entity, 0.25),
    muted: entityHsl(entity, 0.06),
    dashed: entityHsl(entity, 0.25),
    textOn: '#ffffff',
  };
}
```

- [ ] **Step 4: Run test to verify pass**

Run: `cd apps/web && pnpm test src/components/ui/data-display/meeple-card/__tests__/tokens-entityTokens.test.ts`
Expected: PASS 5/5.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/tokens.ts \
        apps/web/src/components/ui/data-display/meeple-card/__tests__/tokens-entityTokens.test.ts
git commit -m "feat(meeple-card): add entityTokens() named derived-color helper"
```

---

## Task 3: Add new status and connection types

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/types.ts`

No test needed (pure type declarations). Compilation is the verification.

- [ ] **Step 1: Extend types.ts**

Append to `apps/web/src/components/ui/data-display/meeple-card/types.ts` (BEFORE the final `}` of the file, after existing `MeepleCardProps` interface):

```ts
// ============================================================================
// ConnectionChip & two-axis status (Step 1 additive; legacy coexists)
// ============================================================================

export interface ConnectionItem {
  id: string;
  label: string;
  href: string;
}

export interface ConnectionChipProps {
  entityType: MeepleEntityType;
  count?: number;
  items?: ConnectionItem[];
  size?: 'sm' | 'md';
  showLabel?: boolean;
  label?: string;
  onCreate?: () => void;
  createLabel?: string;
  href?: string;
  colorOverride?: string;
  disabled?: boolean;
  loading?: boolean;
}

export type OwnershipBadge = 'owned' | 'wishlist' | 'archived';

export type LifecycleState =
  | 'active'
  | 'idle'
  | 'completed'
  | 'setup'
  | 'processing'
  | 'failed';
```

And extend `MeepleCardProps` by adding (inside the interface, alongside existing `status?`, `navItems?`, `manaPips?`):

```ts
  /** New unified connections API. When present, preferred over manaPips/navItems. */
  connections?: ConnectionChipProps[];
  /** Where ConnectionChipStrip renders: 'footer' below image, 'inline' in meta-row, 'auto' per-variant default. */
  connectionsVariant?: 'footer' | 'inline' | 'auto';
  /** New ownership badge (owned/wishlist/archived). When present, preferred over status. */
  ownership?: OwnershipBadge;
  /** New lifecycle state. When present, preferred over status. */
  lifecycle?: LifecycleState;
```

- [ ] **Step 2: Verify types compile**

Run: `cd apps/web && pnpm typecheck`
Expected: no errors.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/types.ts
git commit -m "feat(meeple-card): add ConnectionChip/Ownership/Lifecycle types"
```

---

## Task 4: Create status adapter

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/parts/status-adapter.ts`
- Test: `apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/status-adapter.test.ts`

- [ ] **Step 1: Write failing test**

```ts
// apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/status-adapter.test.ts
import { describe, expect, it } from 'vitest';

import { mapLegacyStatus } from '../status-adapter';

import type { CardStatus } from '../../types';

describe('mapLegacyStatus()', () => {
  it('maps ownership values', () => {
    expect(mapLegacyStatus('owned')).toEqual({ ownership: 'owned' });
    expect(mapLegacyStatus('wishlist')).toEqual({ ownership: 'wishlist' });
    expect(mapLegacyStatus('archived')).toEqual({ ownership: 'archived' });
  });

  it('maps direct lifecycle values', () => {
    expect(mapLegacyStatus('active')).toEqual({ lifecycle: 'active' });
    expect(mapLegacyStatus('idle')).toEqual({ lifecycle: 'idle' });
    expect(mapLegacyStatus('completed')).toEqual({ lifecycle: 'completed' });
    expect(mapLegacyStatus('setup')).toEqual({ lifecycle: 'setup' });
    expect(mapLegacyStatus('processing')).toEqual({ lifecycle: 'processing' });
    expect(mapLegacyStatus('failed')).toEqual({ lifecycle: 'failed' });
  });

  it('merges inprogress → active', () => {
    expect(mapLegacyStatus('inprogress')).toEqual({ lifecycle: 'active' });
  });

  it('merges paused → idle', () => {
    expect(mapLegacyStatus('paused')).toEqual({ lifecycle: 'idle' });
  });

  it('drops internal indexed state from UI', () => {
    expect(mapLegacyStatus('indexed')).toEqual({});
  });

  it('returns empty for undefined input', () => {
    expect(mapLegacyStatus(undefined)).toEqual({});
  });
});
```

- [ ] **Step 2: Run to verify failure**

Run: `cd apps/web && pnpm test src/components/ui/data-display/meeple-card/parts/__tests__/status-adapter.test.ts`
Expected: FAIL — module not found.

- [ ] **Step 3: Create status-adapter.ts**

```ts
// apps/web/src/components/ui/data-display/meeple-card/parts/status-adapter.ts
import type { CardStatus, LifecycleState, OwnershipBadge } from '../types';

export interface MappedStatus {
  ownership?: OwnershipBadge;
  lifecycle?: LifecycleState;
}

/**
 * Maps the legacy `CardStatus` enum (12 values, 3 mixed axes) onto the
 * two-axis model (ownership, lifecycle).
 *
 * Merges:
 *  - `inprogress` → `active` (synonym)
 *  - `paused` → `idle` (semantically equivalent)
 *  - `indexed` → {} (internal KB pipeline state, never user-facing)
 */
export function mapLegacyStatus(status: CardStatus | undefined): MappedStatus {
  switch (status) {
    case 'owned':
      return { ownership: 'owned' };
    case 'wishlist':
      return { ownership: 'wishlist' };
    case 'archived':
      return { ownership: 'archived' };
    case 'active':
    case 'inprogress':
      return { lifecycle: 'active' };
    case 'idle':
    case 'paused':
      return { lifecycle: 'idle' };
    case 'completed':
      return { lifecycle: 'completed' };
    case 'setup':
      return { lifecycle: 'setup' };
    case 'processing':
      return { lifecycle: 'processing' };
    case 'failed':
      return { lifecycle: 'failed' };
    case 'indexed':
    case undefined:
    default:
      return {};
  }
}

/**
 * Resolves final `{ownership, lifecycle}` from new props or legacy status.
 * New props win; legacy status fills gaps only.
 */
export function resolveStatus(input: {
  ownership?: OwnershipBadge;
  lifecycle?: LifecycleState;
  legacyStatus?: CardStatus;
}): MappedStatus {
  const fromLegacy = mapLegacyStatus(input.legacyStatus);
  return {
    ownership: input.ownership ?? fromLegacy.ownership,
    lifecycle: input.lifecycle ?? fromLegacy.lifecycle,
  };
}
```

- [ ] **Step 4: Run test to verify pass**

Run: `cd apps/web && pnpm test src/components/ui/data-display/meeple-card/parts/__tests__/status-adapter.test.ts`
Expected: PASS 6/6.

- [ ] **Step 5: Add test for `resolveStatus` precedence**

Append to the test file:

```ts
import { resolveStatus } from '../status-adapter';

describe('resolveStatus()', () => {
  it('prefers explicit ownership over legacy status', () => {
    expect(
      resolveStatus({ ownership: 'wishlist', legacyStatus: 'owned' }),
    ).toEqual({ ownership: 'wishlist', lifecycle: undefined });
  });

  it('prefers explicit lifecycle over legacy status', () => {
    expect(
      resolveStatus({ lifecycle: 'failed', legacyStatus: 'active' }),
    ).toEqual({ ownership: undefined, lifecycle: 'failed' });
  });

  it('falls back to legacy when explicit props are absent', () => {
    expect(resolveStatus({ legacyStatus: 'owned' })).toEqual({
      ownership: 'owned',
      lifecycle: undefined,
    });
  });

  it('returns empty dimensions when nothing is provided', () => {
    expect(resolveStatus({})).toEqual({
      ownership: undefined,
      lifecycle: undefined,
    });
  });
});
```

- [ ] **Step 6: Run expanded tests**

Run: `cd apps/web && pnpm test src/components/ui/data-display/meeple-card/parts/__tests__/status-adapter.test.ts`
Expected: PASS 10/10.

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/parts/status-adapter.ts \
        apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/status-adapter.test.ts
git commit -m "feat(meeple-card): add legacy CardStatus → two-axis adapter"
```

---

## Task 5: Create `ConnectionChipPopover`

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/parts/ConnectionChipPopover.tsx`
- Test: `apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChipPopover.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChipPopover.test.tsx
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { ConnectionChipPopover } from '../ConnectionChipPopover';

const items = [
  { id: '1', label: 'Wingspan Night #12', href: '/sessions/1' },
  { id: '2', label: 'Wingspan Night #13', href: '/sessions/2' },
];

describe('ConnectionChipPopover', () => {
  it('renders children as trigger', () => {
    render(
      <ConnectionChipPopover
        open={false}
        onOpenChange={() => {}}
        items={items}
        entityType="session"
      >
        <button>trigger</button>
      </ConnectionChipPopover>,
    );
    expect(screen.getByRole('button', { name: /trigger/i })).toBeInTheDocument();
  });

  it('shows items when open', () => {
    render(
      <ConnectionChipPopover
        open
        onOpenChange={() => {}}
        items={items}
        entityType="session"
      >
        <button>trigger</button>
      </ConnectionChipPopover>,
    );
    expect(screen.getByText('Wingspan Night #12')).toBeInTheDocument();
    expect(screen.getByText('Wingspan Night #13')).toBeInTheDocument();
  });

  it('renders create button when onCreate provided', () => {
    const onCreate = vi.fn();
    render(
      <ConnectionChipPopover
        open
        onOpenChange={() => {}}
        items={items}
        onCreate={onCreate}
        createLabel="Nuova sessione"
        entityType="session"
      >
        <button>trigger</button>
      </ConnectionChipPopover>,
    );
    expect(screen.getByRole('button', { name: /nuova sessione/i })).toBeInTheDocument();
  });

  it('calls onCreate and closes popover on create click', async () => {
    const onCreate = vi.fn();
    const onOpenChange = vi.fn();
    render(
      <ConnectionChipPopover
        open
        onOpenChange={onOpenChange}
        items={[]}
        onCreate={onCreate}
        createLabel="Nuova sessione"
        entityType="session"
      >
        <button>trigger</button>
      </ConnectionChipPopover>,
    );
    await userEvent.click(screen.getByRole('button', { name: /nuova sessione/i }));
    expect(onCreate).toHaveBeenCalledTimes(1);
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it('renders Lucide icon matching entity type (not emoji)', () => {
    const { container } = render(
      <ConnectionChipPopover
        open
        onOpenChange={() => {}}
        items={items}
        entityType="session"
      >
        <button>trigger</button>
      </ConnectionChipPopover>,
    );
    // Presence of any svg inside popover rules out emoji-only rendering
    expect(container.querySelector('svg')).toBeTruthy();
  });
});
```

- [ ] **Step 2: Run test to verify failure**

Run: `cd apps/web && pnpm test src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChipPopover.test.tsx`
Expected: FAIL — module not found.

- [ ] **Step 3: Create ConnectionChipPopover.tsx**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/parts/ConnectionChipPopover.tsx
'use client';

import { type ReactNode } from 'react';

import { Plus } from 'lucide-react';
import Link from 'next/link';

import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/overlays/popover';

import { entityLabel, entityTokens } from '../tokens';
import { ENTITY_ICON_STROKE, entityIcons } from './entity-icons';

import type { ConnectionItem } from '../types';
import type { MeepleEntityType } from '../types';

export interface ConnectionChipPopoverProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  items: ConnectionItem[];
  onCreate?: () => void;
  createLabel?: string;
  entityType: MeepleEntityType;
  children: ReactNode;
}

export function ConnectionChipPopover({
  open,
  onOpenChange,
  items,
  onCreate,
  createLabel = 'Create',
  entityType,
  children,
}: ConnectionChipPopoverProps) {
  const tokens = entityTokens(entityType);
  const Icon = entityIcons[entityType];
  const label = entityLabel[entityType];

  return (
    <Popover open={open} onOpenChange={onOpenChange}>
      <PopoverTrigger asChild>{children}</PopoverTrigger>

      <PopoverContent
        align="start"
        sideOffset={6}
        className="w-56 p-0 overflow-hidden"
        style={{
          border: `1px solid ${tokens.border}`,
          background: 'var(--mc-bg-card, hsl(222 47% 11%))',
        }}
      >
        <div
          className="flex items-center gap-2 border-b px-3 py-2 text-xs font-semibold uppercase tracking-wide"
          style={{ borderColor: tokens.border, color: tokens.solid }}
        >
          <Icon size={14} strokeWidth={ENTITY_ICON_STROKE} aria-hidden="true" />
          <span>
            {label} ({items.length})
          </span>
        </div>

        {items.length > 0 && (
          <ul className="max-h-60 overflow-y-auto py-1" role="list">
            {items.map(item => (
              <li key={item.id}>
                <Link
                  href={item.href}
                  onClick={() => onOpenChange(false)}
                  className="flex items-center gap-2 px-3 py-1.5 text-sm transition-colors hover:bg-white/5"
                  style={{ color: 'var(--mc-text, inherit)' }}
                >
                  <span className="shrink-0" style={{ color: tokens.solid }}>
                    <Icon size={14} strokeWidth={ENTITY_ICON_STROKE} aria-hidden="true" />
                  </span>
                  <span className="truncate">{item.label}</span>
                </Link>
              </li>
            ))}
          </ul>
        )}

        {onCreate && (
          <div className="border-t px-3 py-2" style={{ borderColor: tokens.border }}>
            <button
              type="button"
              onClick={() => {
                onCreate();
                onOpenChange(false);
              }}
              className="flex w-full items-center gap-2 rounded-sm px-1 py-1 text-sm font-medium transition-colors hover:opacity-80"
              style={{ color: tokens.solid }}
            >
              <Plus size={14} aria-hidden="true" />
              <span>{createLabel}</span>
            </button>
          </div>
        )}
      </PopoverContent>
    </Popover>
  );
}
```

- [ ] **Step 4: Run test to verify pass**

Run: `cd apps/web && pnpm test src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChipPopover.test.tsx`
Expected: PASS 5/5.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/parts/ConnectionChipPopover.tsx \
        apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChipPopover.test.tsx
git commit -m "feat(meeple-card): add ConnectionChipPopover with Lucide header"
```

---

## Task 6: Create `ConnectionChip` (core component)

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/parts/ConnectionChip.tsx`
- Test: `apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChip.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChip.test.tsx
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { ConnectionChip } from '../ConnectionChip';

describe('ConnectionChip', () => {
  it('renders count badge when count > 0', () => {
    render(<ConnectionChip entityType="session" count={5} />);
    expect(screen.getByTestId('connection-chip-badge')).toHaveTextContent('5');
  });

  it('renders "99+" when count > 99', () => {
    render(<ConnectionChip entityType="kb" count={150} />);
    expect(screen.getByTestId('connection-chip-badge')).toHaveTextContent('99+');
  });

  it('omits badge when count is 0', () => {
    render(<ConnectionChip entityType="chat" count={0} />);
    expect(screen.queryByTestId('connection-chip-badge')).not.toBeInTheDocument();
  });

  it('shows plus overlay when count=0 and onCreate provided', () => {
    render(<ConnectionChip entityType="player" count={0} onCreate={() => {}} />);
    expect(screen.getByTestId('connection-chip-plus')).toBeInTheDocument();
  });

  it('omits plus overlay when count=0 and no onCreate', () => {
    render(<ConnectionChip entityType="player" count={0} />);
    expect(screen.queryByTestId('connection-chip-plus')).not.toBeInTheDocument();
  });

  it('calls onCreate when empty chip is clicked', async () => {
    const onCreate = vi.fn();
    render(<ConnectionChip entityType="player" count={0} onCreate={onCreate} />);
    await userEvent.click(screen.getByRole('button'));
    expect(onCreate).toHaveBeenCalledTimes(1);
  });

  it('is not clickable when disabled', async () => {
    const onCreate = vi.fn();
    render(
      <ConnectionChip entityType="player" count={0} onCreate={onCreate} disabled />,
    );
    const btn = screen.getByRole('button');
    expect(btn).toHaveAttribute('aria-disabled', 'true');
    await userEvent.click(btn);
    expect(onCreate).not.toHaveBeenCalled();
  });

  it('renders skeleton when loading=true', () => {
    render(<ConnectionChip entityType="kb" loading />);
    expect(screen.getByTestId('connection-chip-loading')).toBeInTheDocument();
    expect(screen.queryByTestId('connection-chip-badge')).not.toBeInTheDocument();
  });

  it('renders a Lucide SVG icon (not emoji) for the entity', () => {
    const { container } = render(<ConnectionChip entityType="game" count={3} />);
    expect(container.querySelector('svg')).toBeTruthy();
  });

  it('shows label under chip when showLabel=true', () => {
    render(<ConnectionChip entityType="kb" count={3} showLabel label="Docs" />);
    expect(screen.getByText('Docs')).toBeInTheDocument();
  });

  it('opens popover when clicked and items are present', async () => {
    render(
      <ConnectionChip
        entityType="session"
        count={2}
        items={[
          { id: '1', label: 'First', href: '/sessions/1' },
          { id: '2', label: 'Second', href: '/sessions/2' },
        ]}
      />,
    );
    await userEvent.click(screen.getByRole('button'));
    expect(await screen.findByText('First')).toBeInTheDocument();
    expect(screen.getByText('Second')).toBeInTheDocument();
  });

  it('has aria-label including count and entity label', () => {
    render(<ConnectionChip entityType="session" count={5} />);
    const btn = screen.getByRole('button');
    expect(btn.getAttribute('aria-label')).toMatch(/5/);
    expect(btn.getAttribute('aria-label')?.toLowerCase()).toMatch(/session/);
  });
});
```

- [ ] **Step 2: Run to verify failure**

Run: `cd apps/web && pnpm test src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChip.test.tsx`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement ConnectionChip.tsx**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/parts/ConnectionChip.tsx
'use client';

import { useState } from 'react';

import { Plus } from 'lucide-react';

import { entityLabel, entityTokens } from '../tokens';
import { ConnectionChipPopover } from './ConnectionChipPopover';
import { ENTITY_ICON_SIZE, ENTITY_ICON_STROKE, entityIcons } from './entity-icons';

import type { ConnectionChipProps } from '../types';

const CHIP_PX = { sm: 22, md: 28 } as const;
const BADGE_PX = { sm: 14, md: 16 } as const;
const PLUS_PX = { sm: 12, md: 14 } as const;

function formatCount(count: number): string {
  return count > 99 ? '99+' : String(count);
}

export function ConnectionChip({
  entityType,
  count = 0,
  items,
  size = 'md',
  showLabel,
  label,
  onCreate,
  createLabel,
  href,
  colorOverride,
  disabled = false,
  loading = false,
}: ConnectionChipProps) {
  const [popoverOpen, setPopoverOpen] = useState(false);
  const tokens = entityTokens(entityType);
  const color = colorOverride ?? tokens.solid;
  const Icon = entityIcons[entityType];
  const chipPx = CHIP_PX[size];
  const iconPx = ENTITY_ICON_SIZE[size];
  const badgePx = BADGE_PX[size];
  const plusPx = PLUS_PX[size];
  const labelEffective = label ?? entityLabel[entityType];
  const showLabelEffective = showLabel ?? size === 'md';

  const hasCount = count > 0;
  const isEmpty = count === 0;
  const hasCreate = onCreate !== undefined;
  const hasItems = (items?.length ?? 0) > 0;
  const isInteractive = !disabled && !loading && (hasItems || hasCreate || href);

  // Loading skeleton
  if (loading) {
    return (
      <span
        data-testid="connection-chip-loading"
        className="inline-flex animate-pulse rounded-full"
        style={{
          width: chipPx,
          height: chipPx,
          background: tokens.muted,
        }}
      />
    );
  }

  const chipFaceStyle: React.CSSProperties = {
    width: chipPx,
    height: chipPx,
    background: isEmpty ? tokens.muted : tokens.fill,
    border: `1px ${isEmpty ? 'dashed' : 'solid'} ${isEmpty ? tokens.dashed : tokens.border}`,
    color,
    opacity: isEmpty ? (hasCreate ? 0.85 : 0.6) : 1,
    '--mc-chip-color': color,
    '--mc-chip-glow': tokens.glow,
    '--mc-chip-shadow': tokens.shadow,
    '--mc-chip-hover-bg': tokens.hover,
  } as React.CSSProperties;

  const chipInner = (
    <span
      className="relative inline-flex items-center justify-center rounded-full transition-all duration-200 group-hover/chip:scale-[1.08] group-hover/chip:bg-[var(--mc-chip-hover-bg)] group-hover/chip:shadow-[0_0_0_4px_var(--mc-chip-glow),0_4px_12px_var(--mc-chip-shadow)] motion-reduce:group-hover/chip:scale-100"
      style={chipFaceStyle}
    >
      <Icon
        size={iconPx}
        strokeWidth={ENTITY_ICON_STROKE}
        aria-hidden="true"
        style={{ opacity: isEmpty ? (hasCreate ? 0.7 : 0.45) : 1 }}
      />

      {hasCount && (
        <span
          data-testid="connection-chip-badge"
          className="absolute flex items-center justify-center rounded-full font-semibold text-white shadow-sm [font-feature-settings:'tnum']"
          style={{
            top: -4,
            right: -4,
            height: badgePx,
            minWidth: badgePx,
            padding: '0 3px',
            fontSize: size === 'sm' ? 9 : 10,
            lineHeight: 1,
            background: color,
            boxShadow: '0 0 0 2px var(--mc-bg-card, #fff)',
          }}
        >
          {formatCount(count)}
        </span>
      )}

      {isEmpty && hasCreate && (
        <span
          data-testid="connection-chip-plus"
          aria-hidden="true"
          className="absolute flex items-center justify-center rounded-full text-white shadow-sm"
          style={{
            bottom: -3,
            right: -3,
            height: plusPx,
            width: plusPx,
            background: color,
          }}
        >
          <Plus size={plusPx - 4} strokeWidth={3} />
        </span>
      )}
    </span>
  );

  const labelEl = showLabelEffective ? (
    <span className="mt-0.5 text-[9px] font-semibold uppercase tracking-wide text-[var(--mc-text-muted)] group-hover/chip:text-[var(--mc-text-secondary)]">
      {labelEffective}
    </span>
  ) : null;

  const handleActivate = () => {
    if (!isInteractive) return;
    if (hasItems) {
      setPopoverOpen(true);
    } else if (hasCreate) {
      onCreate?.();
    }
  };

  const ariaLabel = hasCount
    ? `${count} ${labelEffective}`
    : hasCreate
      ? (createLabel ?? `Aggiungi ${labelEffective}`)
      : labelEffective;

  const buttonEl = (
    <button
      type="button"
      aria-label={ariaLabel}
      aria-disabled={disabled || undefined}
      disabled={disabled}
      onClick={handleActivate}
      className={`group/chip relative inline-flex flex-col items-center outline-none focus-visible:ring-2 focus-visible:ring-offset-1 ${
        disabled ? 'cursor-not-allowed opacity-40' : isInteractive ? 'cursor-pointer' : 'cursor-default'
      }`}
      style={{ '--tw-ring-color': tokens.solid } as React.CSSProperties}
    >
      {chipInner}
      {labelEl}
    </button>
  );

  // Wrap in popover when items present
  if (hasItems && !disabled) {
    return (
      <ConnectionChipPopover
        open={popoverOpen}
        onOpenChange={setPopoverOpen}
        items={items ?? []}
        onCreate={onCreate}
        createLabel={createLabel}
        entityType={entityType}
      >
        {buttonEl}
      </ConnectionChipPopover>
    );
  }

  return buttonEl;
}
```

- [ ] **Step 4: Run tests**

Run: `cd apps/web && pnpm test src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChip.test.tsx`
Expected: PASS 12/12.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/parts/ConnectionChip.tsx \
        apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChip.test.tsx
git commit -m "feat(meeple-card): add unified ConnectionChip component"
```

---

## Task 7: Create `ConnectionChipStrip`

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/parts/ConnectionChipStrip.tsx`
- Test: `apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChipStrip.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChipStrip.test.tsx
import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';

import { ConnectionChipStrip } from '../ConnectionChipStrip';

describe('ConnectionChipStrip', () => {
  it('returns null when connections array is empty', () => {
    const { container } = render(<ConnectionChipStrip variant="footer" connections={[]} />);
    expect(container.firstChild).toBeNull();
  });

  it('renders a chip per connection (footer variant)', () => {
    render(
      <ConnectionChipStrip
        variant="footer"
        connections={[
          { entityType: 'kb', count: 3 },
          { entityType: 'session', count: 5 },
        ]}
      />,
    );
    expect(screen.getAllByRole('button')).toHaveLength(2);
  });

  it('renders smaller chips in inline variant by default', () => {
    const { container } = render(
      <ConnectionChipStrip
        variant="inline"
        connections={[{ entityType: 'kb', count: 3 }]}
      />,
    );
    // inline variant forces size="sm" — chip dimension 22px
    const chipFace = container.querySelector('span[style*="width: 22px"]');
    expect(chipFace).toBeTruthy();
  });

  it('renders larger chips in footer variant by default', () => {
    const { container } = render(
      <ConnectionChipStrip
        variant="footer"
        connections={[{ entityType: 'kb', count: 3 }]}
      />,
    );
    const chipFace = container.querySelector('span[style*="width: 28px"]');
    expect(chipFace).toBeTruthy();
  });
});
```

- [ ] **Step 2: Run to verify failure**

Run: `cd apps/web && pnpm test src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChipStrip.test.tsx`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement ConnectionChipStrip.tsx**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/parts/ConnectionChipStrip.tsx
'use client';

import { ConnectionChip } from './ConnectionChip';

import type { ConnectionChipProps } from '../types';

export type ConnectionChipStripVariant = 'footer' | 'inline';

interface ConnectionChipStripProps {
  variant: ConnectionChipStripVariant;
  connections: ConnectionChipProps[];
  className?: string;
}

/**
 * Layout container for multiple ConnectionChip.
 *
 * - `footer`: md chips, border-top, labels visible, used in grid/featured/hero card footer.
 * - `inline`: sm chips, no border, no labels, used in list/compact/focus meta-row.
 */
export function ConnectionChipStrip({
  variant,
  connections,
  className,
}: ConnectionChipStripProps) {
  if (connections.length === 0) return null;

  const defaultSize: 'sm' | 'md' = variant === 'footer' ? 'md' : 'sm';

  const containerClass =
    variant === 'footer'
      ? 'flex items-center justify-center gap-3 border-t border-[var(--mc-border-light)] bg-[var(--mc-nav-footer-bg)] px-2.5 py-2 backdrop-blur-lg'
      : 'flex items-center gap-1.5';

  return (
    <div className={`${containerClass} ${className ?? ''}`.trim()} data-strip-variant={variant}>
      {connections.map((chipProps, i) => (
        <ConnectionChip
          key={`${chipProps.entityType}-${i}`}
          size={chipProps.size ?? defaultSize}
          showLabel={chipProps.showLabel ?? variant === 'footer'}
          {...chipProps}
        />
      ))}
    </div>
  );
}
```

- [ ] **Step 4: Run test**

Run: `cd apps/web && pnpm test src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChipStrip.test.tsx`
Expected: PASS 4/4.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/parts/ConnectionChipStrip.tsx \
        apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChipStrip.test.tsx
git commit -m "feat(meeple-card): add ConnectionChipStrip layout container"
```

---

## Task 8: Create `OwnershipBadge`

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/parts/OwnershipBadge.tsx`
- Test: `apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/OwnershipBadge.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/OwnershipBadge.test.tsx
import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';

import { OwnershipBadge } from '../OwnershipBadge';

describe('OwnershipBadge', () => {
  it('renders owned with CheckCircle2', () => {
    const { container } = render(<OwnershipBadge value="owned" />);
    expect(screen.getByTestId('ownership-badge-owned')).toBeInTheDocument();
    expect(container.querySelector('svg')).toBeTruthy();
  });

  it('renders wishlist with Heart', () => {
    render(<OwnershipBadge value="wishlist" />);
    expect(screen.getByTestId('ownership-badge-wishlist')).toBeInTheDocument();
  });

  it('renders archived with Archive', () => {
    render(<OwnershipBadge value="archived" />);
    expect(screen.getByTestId('ownership-badge-archived')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run to verify failure**

Run: `cd apps/web && pnpm test src/components/ui/data-display/meeple-card/parts/__tests__/OwnershipBadge.test.tsx`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement OwnershipBadge.tsx**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/parts/OwnershipBadge.tsx
'use client';

import { Archive, CheckCircle2, Heart, type LucideIcon } from 'lucide-react';

import type { OwnershipBadge as OwnershipValue } from '../types';

interface OwnershipConfig {
  icon: LucideIcon;
  fg: string;
  bg: string;
  fill?: boolean;
}

const CONFIG: Record<OwnershipValue, OwnershipConfig> = {
  owned: {
    icon: CheckCircle2,
    fg: 'hsl(152 76% 40%)',
    bg: 'hsl(152 76% 40% / 0.12)',
    fill: true,
  },
  wishlist: {
    icon: Heart,
    fg: 'hsl(350 89% 60%)',
    bg: 'hsl(350 89% 60% / 0.12)',
    fill: true,
  },
  archived: {
    icon: Archive,
    fg: 'hsl(215 20% 50%)',
    bg: 'hsl(215 20% 50% / 0.08)',
  },
};

const LABEL: Record<OwnershipValue, string> = {
  owned: 'Owned',
  wishlist: 'Wishlist',
  archived: 'Archived',
};

interface OwnershipBadgeProps {
  value: OwnershipValue;
  size?: number;
  className?: string;
}

export function OwnershipBadge({ value, size = 20, className }: OwnershipBadgeProps) {
  const cfg = CONFIG[value];
  const Icon = cfg.icon;

  return (
    <span
      data-testid={`ownership-badge-${value}`}
      aria-label={LABEL[value]}
      title={LABEL[value]}
      className={`inline-flex items-center justify-center rounded-full ${className ?? ''}`.trim()}
      style={{
        width: size,
        height: size,
        background: cfg.bg,
        color: cfg.fg,
      }}
    >
      <Icon
        size={Math.round(size * 0.65)}
        strokeWidth={1.75}
        fill={cfg.fill ? 'currentColor' : 'none'}
        aria-hidden="true"
      />
    </span>
  );
}
```

- [ ] **Step 4: Run test**

Run: `cd apps/web && pnpm test src/components/ui/data-display/meeple-card/parts/__tests__/OwnershipBadge.test.tsx`
Expected: PASS 3/3.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/parts/OwnershipBadge.tsx \
        apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/OwnershipBadge.test.tsx
git commit -m "feat(meeple-card): add OwnershipBadge component"
```

---

## Task 9: Create `LifecycleStateBadge`

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/parts/LifecycleStateBadge.tsx`
- Test: `apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/LifecycleStateBadge.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/LifecycleStateBadge.test.tsx
import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';

import { LifecycleStateBadge } from '../LifecycleStateBadge';

describe('LifecycleStateBadge', () => {
  it.each(['active', 'idle', 'completed', 'setup', 'processing', 'failed'] as const)(
    'renders for value=%s',
    value => {
      render(<LifecycleStateBadge value={value} entityType="game" />);
      expect(screen.getByTestId(`lifecycle-badge-${value}`)).toBeInTheDocument();
    },
  );

  it('renders processing with spinning Loader2 icon', () => {
    const { container } = render(<LifecycleStateBadge value="processing" entityType="kb" />);
    const svg = container.querySelector('svg');
    expect(svg?.getAttribute('class')).toMatch(/animate-spin/);
  });

  it('renders active with entity-colored dot', () => {
    render(<LifecycleStateBadge value="active" entityType="session" />);
    const badge = screen.getByTestId('lifecycle-badge-active');
    // Session color is hsl(240, 60%, 55%)
    expect(badge.getAttribute('style') ?? '').toMatch(/240/);
  });
});
```

- [ ] **Step 2: Run to verify failure**

Run: `cd apps/web && pnpm test src/components/ui/data-display/meeple-card/parts/__tests__/LifecycleStateBadge.test.tsx`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement LifecycleStateBadge.tsx**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/parts/LifecycleStateBadge.tsx
'use client';

import {
  AlertTriangle,
  CheckCheck,
  Loader2,
  Settings2,
  type LucideIcon,
} from 'lucide-react';

import { entityHsl } from '../tokens';

import type { LifecycleState, MeepleEntityType } from '../types';

interface LifecycleStateBadgeProps {
  value: LifecycleState;
  entityType: MeepleEntityType;
  className?: string;
}

interface StaticConfig {
  kind: 'icon';
  icon: LucideIcon;
  color: string;
  label: string;
  spin?: boolean;
}

interface DotConfig {
  kind: 'dot';
  fillEntity: boolean; // true = entity color, false = neutral
  outline?: boolean;
  label: string;
  pulse?: boolean;
}

const CONFIG: Record<LifecycleState, StaticConfig | DotConfig> = {
  active: { kind: 'dot', fillEntity: true, label: 'Active', pulse: true },
  idle: { kind: 'dot', fillEntity: false, outline: true, label: 'Idle' },
  completed: {
    kind: 'icon',
    icon: CheckCheck,
    color: 'hsl(152 76% 40%)',
    label: 'Completed',
  },
  setup: {
    kind: 'icon',
    icon: Settings2,
    color: 'hsl(38 92% 50%)',
    label: 'Setup',
  },
  processing: {
    kind: 'icon',
    icon: Loader2,
    color: 'hsl(200 89% 55%)',
    label: 'Processing',
    spin: true,
  },
  failed: {
    kind: 'icon',
    icon: AlertTriangle,
    color: 'hsl(0 84% 60%)',
    label: 'Failed',
  },
};

export function LifecycleStateBadge({
  value,
  entityType,
  className,
}: LifecycleStateBadgeProps) {
  const cfg = CONFIG[value];

  if (cfg.kind === 'dot') {
    const color = cfg.fillEntity ? entityHsl(entityType) : 'hsl(215 20% 60%)';
    return (
      <span
        data-testid={`lifecycle-badge-${value}`}
        aria-label={cfg.label}
        title={cfg.label}
        className={`inline-block rounded-full ${cfg.pulse ? 'animate-pulse' : ''} ${className ?? ''}`.trim()}
        style={{
          width: 8,
          height: 8,
          background: cfg.outline ? 'transparent' : color,
          border: cfg.outline ? `1.5px solid ${color}` : 'none',
        }}
      />
    );
  }

  const Icon = cfg.icon;
  return (
    <span
      data-testid={`lifecycle-badge-${value}`}
      aria-label={cfg.label}
      title={cfg.label}
      className={`inline-flex items-center ${className ?? ''}`.trim()}
      style={{ color: cfg.color }}
    >
      <Icon
        size={14}
        strokeWidth={1.75}
        aria-hidden="true"
        className={cfg.spin ? 'animate-spin' : undefined}
      />
    </span>
  );
}
```

- [ ] **Step 4: Run test**

Run: `cd apps/web && pnpm test src/components/ui/data-display/meeple-card/parts/__tests__/LifecycleStateBadge.test.tsx`
Expected: PASS 8/8 (6 from it.each + 2 specific).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/parts/LifecycleStateBadge.tsx \
        apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/LifecycleStateBadge.test.tsx
git commit -m "feat(meeple-card): add LifecycleStateBadge component"
```

---

## Task 10: Extend `index.ts` public exports

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/index.ts`

- [ ] **Step 1: Read current exports**

Run: `cat apps/web/src/components/ui/data-display/meeple-card/index.ts`

- [ ] **Step 2: Append new exports**

Add to the file:

```ts
export { ConnectionChip } from './parts/ConnectionChip';
export { ConnectionChipStrip } from './parts/ConnectionChipStrip';
export { ConnectionChipPopover } from './parts/ConnectionChipPopover';
export { OwnershipBadge } from './parts/OwnershipBadge';
export { LifecycleStateBadge } from './parts/LifecycleStateBadge';
export { entityIcons, ENTITY_ICON_SIZE, ENTITY_ICON_STROKE } from './parts/entity-icons';
export { mapLegacyStatus, resolveStatus } from './parts/status-adapter';
export { entityTokens } from './tokens';
export type {
  ConnectionChipProps,
  ConnectionItem,
  OwnershipBadge as OwnershipBadgeValue,
  LifecycleState,
} from './types';
```

- [ ] **Step 3: Verify typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: no errors.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/index.ts
git commit -m "feat(meeple-card): export ConnectionChip primitives from package"
```

---

## Task 11: Full test run + lint

- [ ] **Step 1: Run the whole meeple-card suite**

Run: `cd apps/web && pnpm test src/components/ui/data-display/meeple-card`
Expected: all existing tests still pass; new tests (entity-icons, tokens-entityTokens, status-adapter, ConnectionChip, ConnectionChipStrip, ConnectionChipPopover, OwnershipBadge, LifecycleStateBadge) pass.

- [ ] **Step 2: Run typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: no errors.

- [ ] **Step 3: Run lint**

Run: `cd apps/web && pnpm lint`
Expected: no errors in modified files. If lint auto-fixes, re-run tests.

- [ ] **Step 4: Final commit (any lint fixups)**

If lint produced changes:
```bash
git add -u
git commit -m "chore(meeple-card): lint fixups for ConnectionChip additions"
```

Otherwise skip.

---

## Task 12: Push branch and open PR

- [ ] **Step 1: Detect current branch and parent**

Run:
```bash
git branch --show-current
```
Expected: current feature branch (e.g. `feature/meeplecard-connectionchip`). If on `main-dev` or `main`, create a feature branch first:
```bash
git checkout -b feature/meeplecard-connectionchip-step1
git config branch.feature/meeplecard-connectionchip-step1.parent main-dev
```

- [ ] **Step 2: Push**

Run: `git push -u origin HEAD`

- [ ] **Step 3: Open PR to parent**

Run:
```bash
gh pr create --base main-dev --title "feat(meeple-card): Step 1 — ConnectionChip additive rollout" --body "$(cat <<'EOF'
## Summary
- Adds unified `ConnectionChip` / `ConnectionChipStrip` / `ConnectionChipPopover` replacing the ManaPips+NavFooterItem duplication visually and functionally.
- Restores the functional role users expected: count badge + drill-down popover + create affordance.
- Adds Lucide `entityIcons` registry — emoji `entityIcon` in `tokens.ts` remains deprecated and will be removed in Step 3.
- Adds `entityTokens()` named derived-color helper (`fill/border/hover/glow/…`).
- Introduces two-axis status model (`OwnershipBadge`, `LifecycleStateBadge`) with a pure `mapLegacyStatus` adapter that preserves existing `CardStatus` behaviour.
- Fully **additive** — no legacy API removed, all existing tests must still pass.

## Spec
`docs/superpowers/specs/2026-04-23-meeplecard-connectionchip-design.md`

## Plan
`docs/superpowers/plans/2026-04-23-meeplecard-connectionchip-plan.md`

## Test plan
- [ ] New unit tests (entity-icons, tokens-entityTokens, status-adapter, ConnectionChip, ConnectionChipStrip, ConnectionChipPopover, OwnershipBadge, LifecycleStateBadge) pass
- [ ] `pnpm typecheck` clean
- [ ] `pnpm lint` clean
- [ ] Existing meeple-card tests (ManaPips, NavFooter, MeepleCard) still pass
- [ ] Manual smoke: no consumer of MeepleCard breaks

## Follow-ups (separate PRs, see spec §10)
- Step 2 — call-site migration sweep per bounded area
- Step 3 — legacy cleanup + bundle size rebaseline
- Typography scale, dark-mode color parity, contrast CI (follow-up plans)

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 4: Record PR URL for memory**

Note the PR URL returned by `gh pr create`.

---

## Done Criteria

- All 8 new unit test files pass (entity-icons, tokens-entityTokens, status-adapter, ConnectionChip, ConnectionChipStrip, ConnectionChipPopover, OwnershipBadge, LifecycleStateBadge)
- Typecheck + lint clean
- Existing meeple-card test suite unchanged (no regressions)
- PR opened against parent branch (not `main`)
- No legacy prop (`status`, `navItems`, `manaPips`) removed — backward compat guaranteed
