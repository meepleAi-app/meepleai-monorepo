# MeepleCardGrid SP4 Conformance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the 5 structural conformance fixes between the current `MeepleCard` primitive and the canonical SP4 mockup (`admin-mockups/design_files/sp4-library-desktop.jsx:657-749`).

**Architecture:** Surgical primitive restructure (Approach A, 1 PR). Each mockup gap maps to a single `parts/*.tsx` or `variants/GridCard.tsx` file. `Cover` introduces dual-mode (image-mode vs emoji-band-mode); `AccentBorder` reorients; `EntityBadge` restyles to glass; `GridCard` orchestrator gains `MenuPlaceholder` + `CardFooter` parts and removes top-left `StatusBadge`.

**Tech Stack:** TypeScript, React 19, Vitest + React Testing Library (RTL), Tailwind 4. All test files use `describe`/`it`/`expect` from `vitest` and `render`/`screen` from `@testing-library/react`.

**Spec:** [`docs/superpowers/specs/2026-06-04-meeple-card-sp4-conformance-design.md`](../specs/2026-06-04-meeple-card-sp4-conformance-design.md)

**Branch:** `feat/issue-1856-meeple-card-sp4-conformance` (already created, parent: `main-dev`)

**Cwd assumption:** All commands assume `cwd=apps/web` unless otherwise noted (project root `D:\Repositories\meepleai-monorepo-frontend`). For relative test paths, prefix with `apps/web/` when running from repo root.

---

## File map

| Order | Action | File | Responsibility |
|---|---|---|---|
| T1 | Modify | `apps/web/src/components/ui/data-display/meeple-card/types.ts` | Add `coverEmoji?: string` to `MeepleCardProps` |
| T2 | Modify | `apps/web/src/components/ui/data-display/meeple-card/parts/AccentBorder.tsx` | Vertical-left → horizontal-top |
| T3 | Create | `apps/web/src/components/ui/data-display/meeple-card/parts/MenuPlaceholder.tsx` | Hover-visible glass ⋯ button, no-op |
| T4 | Create | `apps/web/src/components/ui/data-display/meeple-card/parts/CardFooter.tsx` | StatusDot + uppercase mono badge with border-top |
| T5 | Modify | `apps/web/src/components/ui/data-display/meeple-card/parts/EntityBadge.tsx` | Solid bg → glass pill + entity color text + emoji prefix |
| T6 | Modify | `apps/web/src/components/ui/data-display/meeple-card/parts/Cover.tsx` | Dual-mode rendering (image-mode preserved, fallback → emoji-band) |
| T7 | Modify | `apps/web/src/components/ui/data-display/meeple-card/parts/index.ts` | Export `MenuPlaceholder`, `CardFooter` |
| T8 | Modify | `apps/web/src/components/ui/data-display/meeple-card/variants/GridCard.tsx` + `variants/__tests__/GridCard.test.tsx` | Integration: orchestrate all parts + update regression-guard tests for DEC-5 |
| T9 | Create | `apps/web/src/components/ui/data-display/meeple-card/__tests__/MeepleCard.contract.test.tsx` | Type contract assertions for `coverEmoji` prop |
| T10 | Create | `apps/web/src/components/ui/data-display/meeple-card/__tests__/consumer-categories.smoke.test.tsx` | Render smoke per surface category (no snapshot) |

---

## Task 1: Add `coverEmoji` prop to `MeepleCardProps`

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/types.ts`

- [ ] **Step 1.1: Write the failing test**

Create file `apps/web/src/components/ui/data-display/meeple-card/__tests__/coverEmoji-prop.contract.test.ts`:

```typescript
import { describe, it, expectTypeOf } from 'vitest';

import type { MeepleCardProps } from '../types';

describe('MeepleCardProps coverEmoji contract', () => {
  it('accepts coverEmoji as optional string', () => {
    expectTypeOf<MeepleCardProps>().toHaveProperty('coverEmoji').toEqualTypeOf<string | undefined>();
  });

  it('allows omitting coverEmoji', () => {
    const props: MeepleCardProps = { entity: 'game', title: 'Catan' };
    expectTypeOf(props.coverEmoji).toEqualTypeOf<string | undefined>();
  });

  it('allows passing coverEmoji as string', () => {
    const props: MeepleCardProps = { entity: 'game', title: 'Catan', coverEmoji: '🎲' };
    expectTypeOf(props.coverEmoji).toEqualTypeOf<string | undefined>();
  });
});
```

- [ ] **Step 1.2: Run test to verify it fails**

Run from `apps/web/`:
```bash
pnpm vitest run src/components/ui/data-display/meeple-card/__tests__/coverEmoji-prop.contract.test.ts
```
Expected: FAIL with TypeScript error `Type '{ entity: "game"; title: string; coverEmoji: string; }' is not assignable to type 'MeepleCardProps'. Object literal may only specify known properties, and 'coverEmoji' does not exist in type 'MeepleCardProps'.`

- [ ] **Step 1.3: Implement minimal change in types.ts**

In `apps/web/src/components/ui/data-display/meeple-card/types.ts`, find the `MeepleCardProps` interface (line 84) and add `coverEmoji` immediately after `imageUrl`:

Replace:
```typescript
  imageUrl?: string;
  rating?: number;
```

With:
```typescript
  imageUrl?: string;
  /**
   * UTF-8 emoji shown in the squat-band cover mode (when `imageUrl` is absent).
   * Falls back to `entityIcon[entity]` when omitted.
   * Example: 🎲 for game, 🎯 for session, 🤖 for agent.
   * Naming endorses existing FE convention (Toolkit.coverEmoji, play-records StatsHero.tsx:137).
   */
  coverEmoji?: string;
  rating?: number;
```

- [ ] **Step 1.4: Run test to verify it passes**

Run from `apps/web/`:
```bash
pnpm vitest run src/components/ui/data-display/meeple-card/__tests__/coverEmoji-prop.contract.test.ts
```
Expected: PASS (3 tests).

- [ ] **Step 1.5: Run full type-check**

Run from `apps/web/`:
```bash
pnpm typecheck
```
Expected: 0 errors.

- [ ] **Step 1.6: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/types.ts apps/web/src/components/ui/data-display/meeple-card/__tests__/coverEmoji-prop.contract.test.ts
git commit -m "feat(meeple-card): #1856 T1 add coverEmoji prop to MeepleCardProps"
```

---

## Task 2: AccentBorder horizontal-top reorientation

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/parts/AccentBorder.tsx`
- Create: `apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/AccentBorder.test.tsx`

- [ ] **Step 2.1: Write the failing test**

Create `apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/AccentBorder.test.tsx`:

```tsx
import { describe, it, expect } from 'vitest';
import { render } from '@testing-library/react';

import { AccentBorder } from '../AccentBorder';
import { entityHsl } from '../../tokens';

describe('AccentBorder', () => {
  it('renders horizontal top bar (mockup-conformant: top-0 left-0 right-0 h-[3px])', () => {
    const { container } = render(<AccentBorder entity="game" />);
    const el = container.firstChild as HTMLElement;
    expect(el.className).toMatch(/\btop-0\b/);
    expect(el.className).toMatch(/\bleft-0\b/);
    expect(el.className).toMatch(/\bright-0\b/);
    expect(el.className).toMatch(/\bh-\[3px\]\b/);
  });

  it('does NOT render vertical-left bar (regression guard against old layout)', () => {
    const { container } = render(<AccentBorder entity="game" />);
    const el = container.firstChild as HTMLElement;
    expect(el.className).not.toMatch(/\bbottom-0\b/);
    expect(el.className).not.toMatch(/\bw-\[3px\]\b/);
  });

  it('uses entityHsl for inline background', () => {
    const { container } = render(<AccentBorder entity="player" />);
    const el = container.firstChild as HTMLElement;
    expect(el.style.background).toBe(entityHsl('player'));
  });

  it('grows on group-hover via height transition', () => {
    const { container } = render(<AccentBorder entity="game" />);
    const el = container.firstChild as HTMLElement;
    expect(el.className).toMatch(/group-hover:h-\[5px\]/);
    expect(el.className).not.toMatch(/group-hover:w-\[5px\]/);
  });
});
```

- [ ] **Step 2.2: Run test to verify it fails**

Run from `apps/web/`:
```bash
pnpm vitest run src/components/ui/data-display/meeple-card/parts/__tests__/AccentBorder.test.tsx
```
Expected: FAIL — `expected 'absolute bottom-0 left-0 top-0 z-[5] w-[3px] transition-[width] duration-200 group-hover:w-[5px]' to match /\btop-0\b/` and 3 more failures.

- [ ] **Step 2.3: Implement AccentBorder reorientation**

Replace `apps/web/src/components/ui/data-display/meeple-card/parts/AccentBorder.tsx` content:

```tsx
import { entityHsl } from '../tokens';

import type { MeepleEntityType } from '../types';

interface AccentBorderProps {
  entity: MeepleEntityType;
}

export function AccentBorder({ entity }: AccentBorderProps) {
  return (
    <div
      className="absolute left-0 right-0 top-0 z-[5] h-[3px] transition-[height] duration-200 group-hover:h-[5px]"
      style={{ background: entityHsl(entity) }}
    />
  );
}
```

- [ ] **Step 2.4: Run test to verify it passes**

Run from `apps/web/`:
```bash
pnpm vitest run src/components/ui/data-display/meeple-card/parts/__tests__/AccentBorder.test.tsx
```
Expected: PASS (4 tests).

- [ ] **Step 2.5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/parts/AccentBorder.tsx apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/AccentBorder.test.tsx
git commit -m "feat(meeple-card): #1856 T2 AccentBorder horizontal-top reorientation"
```

---

## Task 3: Create `MenuPlaceholder` part

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/parts/MenuPlaceholder.tsx`
- Create: `apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/MenuPlaceholder.test.tsx`

- [ ] **Step 3.1: Write the failing test**

Create `apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/MenuPlaceholder.test.tsx`:

```tsx
import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';

import { MenuPlaceholder } from '../MenuPlaceholder';

describe('MenuPlaceholder', () => {
  it('renders a button with aria-label="Azioni"', () => {
    render(<MenuPlaceholder />);
    expect(screen.getByRole('button', { name: 'Azioni' })).toBeInTheDocument();
  });

  it('renders the ⋯ glyph', () => {
    render(<MenuPlaceholder />);
    expect(screen.getByRole('button', { name: 'Azioni' }).textContent).toContain('⋯');
  });

  it('starts hidden (opacity-0) and becomes visible on parent group-hover', () => {
    render(<MenuPlaceholder />);
    const btn = screen.getByRole('button', { name: 'Azioni' });
    expect(btn.className).toMatch(/\bopacity-0\b/);
    expect(btn.className).toMatch(/group-hover:opacity-100/);
    expect(btn.className).toMatch(/transition-opacity/);
  });

  it('positions absolute top-2 right-2 with glass style', () => {
    render(<MenuPlaceholder />);
    const btn = screen.getByRole('button', { name: 'Azioni' });
    expect(btn.className).toMatch(/\babsolute\b/);
    expect(btn.className).toMatch(/\btop-2\b/);
    expect(btn.className).toMatch(/\bright-2\b/);
    expect(btn.className).toMatch(/bg-white\/85/);
    expect(btn.className).toMatch(/backdrop-blur-md/);
  });

  it('stops click event propagation (prevents triggering parent card onClick)', () => {
    const parentClick = vi.fn();
    render(
      <div onClick={parentClick}>
        <MenuPlaceholder />
      </div>
    );
    fireEvent.click(screen.getByRole('button', { name: 'Azioni' }));
    expect(parentClick).not.toHaveBeenCalled();
  });
});
```

- [ ] **Step 3.2: Run test to verify it fails**

Run from `apps/web/`:
```bash
pnpm vitest run src/components/ui/data-display/meeple-card/parts/__tests__/MenuPlaceholder.test.tsx
```
Expected: FAIL with `Failed to resolve import "../MenuPlaceholder"` (file does not exist yet).

- [ ] **Step 3.3: Implement MenuPlaceholder**

Create `apps/web/src/components/ui/data-display/meeple-card/parts/MenuPlaceholder.tsx`:

```tsx
/* eslint-disable local/no-hardcoded-color-utility -- glass pill bg-white/85 follows the mockup .e-bg pattern; entity-neutral surface for action affordance. */
'use client';

/**
 * Hover-visible glass button placeholder for card actions (3-dot menu).
 *
 * **No functional handler** — this is a visual-only placeholder matching the SP4
 * mockup at `admin-mockups/design_files/sp4-library-desktop.jsx:709-721`. Click
 * stops propagation so it doesn't trigger the parent card's `onClick`. Future
 * issue may wire a consumer-defined menu action via a prop.
 *
 * See #1856 DEC-4.
 */
export function MenuPlaceholder() {
  return (
    <button
      type="button"
      aria-label="Azioni"
      onClick={(e) => e.stopPropagation()}
      className="absolute right-2 top-2 z-10 flex h-6 w-6 items-center justify-center rounded-md border-none bg-white/85 text-sm font-extrabold text-foreground opacity-0 backdrop-blur-md transition-opacity duration-200 group-hover:opacity-100"
    >
      ⋯
    </button>
  );
}
```

- [ ] **Step 3.4: Run test to verify it passes**

Run from `apps/web/`:
```bash
pnpm vitest run src/components/ui/data-display/meeple-card/parts/__tests__/MenuPlaceholder.test.tsx
```
Expected: PASS (5 tests).

- [ ] **Step 3.5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/parts/MenuPlaceholder.tsx apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/MenuPlaceholder.test.tsx
git commit -m "feat(meeple-card): #1856 T3 add MenuPlaceholder part (hover-visible glass ⋯ button)"
```

---

## Task 4: Create `CardFooter` part

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/parts/CardFooter.tsx`
- Create: `apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/CardFooter.test.tsx`

- [ ] **Step 4.1: Write the failing test**

Create `apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/CardFooter.test.tsx`:

```tsx
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';

import { CardFooter } from '../CardFooter';

describe('CardFooter', () => {
  it('renders nothing when both status and badge are absent', () => {
    const { container } = render(<CardFooter />);
    expect(container.firstChild).toBeNull();
  });

  it('renders status text when status is provided', () => {
    render(<CardFooter status="owned" />);
    expect(screen.getByText(/owned/i)).toBeInTheDocument();
  });

  it('renders badge text in uppercase when badge is provided', () => {
    const { container } = render(<CardFooter badge="indexed" />);
    const text = container.textContent ?? '';
    expect(text.toLowerCase()).toContain('indexed');
    // The element rendering the badge uses uppercase via class.
    const badgeEl = container.querySelector('[data-slot="footer-badge"]');
    expect(badgeEl?.className).toMatch(/uppercase/);
  });

  it('prefers badge over status as the displayed label when both present', () => {
    const { container } = render(<CardFooter status="owned" badge="indexed" />);
    const badgeEl = container.querySelector('[data-slot="footer-badge"]');
    expect(badgeEl?.textContent?.toLowerCase()).toContain('indexed');
    expect(badgeEl?.textContent?.toLowerCase()).not.toContain('owned');
  });

  it('uses border-top divider class', () => {
    const { container } = render(<CardFooter status="owned" />);
    const root = container.firstChild as HTMLElement;
    expect(root.className).toMatch(/border-t\b/);
  });

  it('renders a StatusDot when status is provided', () => {
    const { container } = render(<CardFooter status="owned" />);
    const dot = container.querySelector('[data-slot="footer-status-dot"]');
    expect(dot).not.toBeNull();
  });
});
```

- [ ] **Step 4.2: Run test to verify it fails**

Run from `apps/web/`:
```bash
pnpm vitest run src/components/ui/data-display/meeple-card/parts/__tests__/CardFooter.test.tsx
```
Expected: FAIL with `Failed to resolve import "../CardFooter"`.

- [ ] **Step 4.3: Implement CardFooter**

Create `apps/web/src/components/ui/data-display/meeple-card/parts/CardFooter.tsx`:

```tsx
import { statusColors } from '../tokens';

import type { CardStatus } from '../types';

interface CardFooterProps {
  status?: CardStatus;
  badge?: string;
}

/**
 * Footer row for MeepleCardGrid following SP4 mockup
 * (`admin-mockups/design_files/sp4-library-desktop.jsx:736-745`).
 *
 * Renders a border-top divider, a `StatusDot` for the lifecycle color, and the
 * label (preferring `badge` over `status` when both are present). Renderless when
 * both inputs are absent — keeps the card body flush.
 *
 * See #1856 DEC-5.
 */
export function CardFooter({ status, badge }: CardFooterProps) {
  const label = badge ?? status;
  if (!label) return null;

  const dotColor = status ? statusColors[status]?.text ?? 'var(--muted-foreground)' : 'var(--muted-foreground)';

  return (
    <div className="mt-1 flex items-center gap-1.5 border-t border-border-light px-3.5 py-1.5">
      {status && (
        <span
          data-slot="footer-status-dot"
          aria-hidden="true"
          className="inline-block h-1.5 w-1.5 shrink-0 rounded-full"
          style={{ background: dotColor }}
        />
      )}
      <span
        data-slot="footer-badge"
        className="font-mono text-[9.5px] font-bold uppercase tracking-wide text-muted-foreground"
      >
        {label}
      </span>
    </div>
  );
}
```

- [ ] **Step 4.4: Run test to verify it passes**

Run from `apps/web/`:
```bash
pnpm vitest run src/components/ui/data-display/meeple-card/parts/__tests__/CardFooter.test.tsx
```
Expected: PASS (6 tests).

- [ ] **Step 4.5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/parts/CardFooter.tsx apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/CardFooter.test.tsx
git commit -m "feat(meeple-card): #1856 T4 add CardFooter part (border-top + StatusDot + uppercase mono badge)"
```

---

## Task 5: EntityBadge glass restyle

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/parts/EntityBadge.tsx`
- Create: `apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/EntityBadge.test.tsx`

- [ ] **Step 5.1: Write the failing test**

Create `apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/EntityBadge.test.tsx`:

```tsx
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';

import { EntityBadge } from '../EntityBadge';
import { entityHslText, entityIcon, entityLabel } from '../../tokens';

describe('EntityBadge (glass restyle, #1856)', () => {
  it('renders the entity emoji prefix followed by the label', () => {
    render(<EntityBadge entity="game" />);
    const badge = screen.getByText(new RegExp(`${entityIcon.game}.*${entityLabel.game}`));
    expect(badge).toBeInTheDocument();
  });

  it('uses the glass background (bg-white/85 + backdrop-blur-md)', () => {
    const { container } = render(<EntityBadge entity="game" />);
    const el = container.querySelector('[data-slot="meeple-card-entity-badge"]') as HTMLElement;
    expect(el.className).toMatch(/bg-white\/85/);
    expect(el.className).toMatch(/backdrop-blur-md/);
  });

  it('uses the entity text color (not white text on solid bg)', () => {
    const { container } = render(<EntityBadge entity="game" />);
    const el = container.querySelector('[data-slot="meeple-card-entity-badge"]') as HTMLElement;
    // No solid entity bg via inline style.
    expect(el.style.background).toBe('');
    // Inline color uses entityHslText for AA-safe contrast on glass bg.
    expect(el.style.color).toBe(entityHslText('game'));
    // No text-white class (regression: glass style uses entity color text).
    expect(el.className).not.toMatch(/\btext-white\b/);
  });

  it('keeps absolute positioning by default', () => {
    const { container } = render(<EntityBadge entity="game" />);
    const el = container.querySelector('[data-slot="meeple-card-entity-badge"]') as HTMLElement;
    expect(el.className).toMatch(/\babsolute\b/);
    expect(el.className).toMatch(/\btop-2\b/);
    expect(el.className).toMatch(/\bleft-2\.5\b/);
  });

  it('switches to self-start (no absolute) when stacked=true', () => {
    const { container } = render(<EntityBadge entity="game" stacked />);
    const el = container.querySelector('[data-slot="meeple-card-entity-badge"]') as HTMLElement;
    expect(el.className).toMatch(/self-start/);
    expect(el.className).not.toMatch(/\babsolute\b/);
  });

  it('renders the same glass style for all 9 entity types', () => {
    const entities = ['game', 'player', 'session', 'agent', 'kb', 'chat', 'event', 'toolkit', 'tool'] as const;
    for (const e of entities) {
      const { container, unmount } = render(<EntityBadge entity={e} />);
      const el = container.querySelector('[data-slot="meeple-card-entity-badge"]') as HTMLElement;
      expect(el.className).toMatch(/bg-white\/85/);
      expect(el.style.color).toBe(entityHslText(e));
      unmount();
    }
  });
});
```

- [ ] **Step 5.2: Run test to verify it fails**

Run from `apps/web/`:
```bash
pnpm vitest run src/components/ui/data-display/meeple-card/parts/__tests__/EntityBadge.test.tsx
```
Expected: FAIL — current EntityBadge uses `bg:entityHsl(entity)` solid bg + `text-white`, missing emoji prefix.

- [ ] **Step 5.3: Implement EntityBadge glass restyle**

Replace `apps/web/src/components/ui/data-display/meeple-card/parts/EntityBadge.tsx`:

```tsx
/* eslint-disable local/no-hardcoded-color-utility -- glass bg-white/85 follows the mockup .e-bg pattern; entity color text via inline style. DS-12 primitive — see token-bridge-map.md for migration plan. */
import { entityHslText, entityIcon, entityLabel } from '../tokens';

import type { MeepleEntityType } from '../types';

interface EntityBadgeProps {
  entity: MeepleEntityType;
  className?: string;
  /**
   * When true, renders without absolute positioning (no top/left).
   * Used when the badge is wrapped in an external flex stack container
   * (e.g. GridCard's BadgeStack). Default: false (legacy absolute positioning).
   */
  stacked?: boolean;
}

export function EntityBadge({ entity, className = '', stacked = false }: EntityBadgeProps) {
  const positioning = stacked ? 'self-start' : 'absolute left-2.5 top-2 z-10';
  return (
    <span
      data-slot="meeple-card-entity-badge"
      className={`${positioning} inline-flex items-center gap-1 rounded-md bg-white/85 px-2 py-0.5 font-[var(--font-quicksand)] text-[9px] font-extrabold uppercase tracking-wide shadow-sm backdrop-blur-md ${className}`}
      style={{ color: entityHslText(entity) }}
    >
      <span aria-hidden="true">{entityIcon[entity]}</span>
      {entityLabel[entity]}
    </span>
  );
}
```

- [ ] **Step 5.4: Run test to verify it passes**

Run from `apps/web/`:
```bash
pnpm vitest run src/components/ui/data-display/meeple-card/parts/__tests__/EntityBadge.test.tsx
```
Expected: PASS (6 tests).

- [ ] **Step 5.5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/parts/EntityBadge.tsx apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/EntityBadge.test.tsx
git commit -m "feat(meeple-card): #1856 T5 EntityBadge glass restyle (entity color text + emoji prefix)"
```

---

## Task 6: Cover dual-mode (image-mode preserved, emoji-band fallback)

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/parts/Cover.tsx`
- Create: `apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/Cover.test.tsx`

- [ ] **Step 6.1: Write the failing test**

Create `apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/Cover.test.tsx`:

```tsx
import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';

import { Cover } from '../Cover';
import { entityIcon } from '../../tokens';

vi.mock('@/lib/games/cover-utils', () => ({
  shouldUsePlaceholder: (url?: string) => !url || url.includes('boardgamegeek'),
  hashToHue: () => 100,
  extractInitials: (t: string) => t.charAt(0) || '?',
}));

describe('Cover dual-mode (#1856 DEC-3)', () => {
  describe('image-mode (imageUrl present)', () => {
    it('renders <img> with aspect-[7/10] when imageUrl is a non-BGG URL', () => {
      const { container } = render(
        <Cover entity="game" variant="grid" imageUrl="https://cdn.example.com/catan.webp" alt="Catan" />
      );
      const img = container.querySelector('img');
      expect(img).not.toBeNull();
      expect(img?.getAttribute('src')).toBe('https://cdn.example.com/catan.webp');
      const wrapper = container.firstChild as HTMLElement;
      expect(wrapper.className).toMatch(/aspect-\[7\/10\]/);
    });

    it('switches to emoji-band on <img> onError', () => {
      const { container } = render(
        <Cover entity="game" variant="grid" imageUrl="https://cdn.example.com/broken.webp" alt="X" />
      );
      const img = container.querySelector('img') as HTMLImageElement;
      fireEvent.error(img);
      // After error, <img> is replaced by emoji-band fallback.
      expect(container.querySelector('img')).toBeNull();
      expect(screen.getByText(entityIcon.game)).toBeInTheDocument();
    });
  });

  describe('emoji-band-mode (imageUrl absent or BGG-blocked)', () => {
    it('renders squat band with h-[100px] when imageUrl is undefined', () => {
      const { container } = render(<Cover entity="session" variant="grid" />);
      // Outer wrapper still uses variant aspect ratio (grid → aspect-[7/10]).
      // Inner emoji-band uses h-[100px]:
      const band = container.querySelector('[data-slot="cover-emoji-band"]') as HTMLElement;
      expect(band).not.toBeNull();
      expect(band.className).toMatch(/h-\[100px\]/);
    });

    it('renders coverEmoji prop when provided', () => {
      render(<Cover entity="session" variant="grid" coverEmoji="🎲" />);
      expect(screen.getByText('🎲')).toBeInTheDocument();
    });

    it('falls back to entityIcon[entity] when coverEmoji is omitted', () => {
      render(<Cover entity="agent" variant="grid" />);
      expect(screen.getByText(entityIcon.agent)).toBeInTheDocument();
    });

    it('emoji is rendered at 38px (text-[38px])', () => {
      render(<Cover entity="session" variant="grid" />);
      const emoji = screen.getByText(entityIcon.session);
      expect(emoji.className).toMatch(/text-\[38px\]/);
    });

    it('renders emoji-band for entity=game (DEC-2: no GameCoverPlaceholder fallback)', () => {
      const { container } = render(<Cover entity="game" variant="grid" gameId="g1" alt="Catan" />);
      const placeholder = container.querySelector('[data-testid="game-cover-placeholder"]');
      expect(placeholder).toBeNull();
      const band = container.querySelector('[data-slot="cover-emoji-band"]');
      expect(band).not.toBeNull();
      expect(screen.getByText(entityIcon.game)).toBeInTheDocument();
    });

    it('renders emoji-band when imageUrl is BGG-blocked', () => {
      const { container } = render(
        <Cover entity="game" variant="grid" imageUrl="https://boardgamegeek.com/x.jpg" alt="X" />
      );
      expect(container.querySelector('img')).toBeNull();
      const band = container.querySelector('[data-slot="cover-emoji-band"]');
      expect(band).not.toBeNull();
    });
  });

  describe('CoverProps backwards compat', () => {
    it('accepts the legacy props (gameId, alt) without throwing', () => {
      expect(() =>
        render(<Cover entity="game" variant="grid" gameId="g1" alt="Catan" coverEmoji="🎲" />)
      ).not.toThrow();
    });
  });
});
```

- [ ] **Step 6.2: Run test to verify it fails**

Run from `apps/web/`:
```bash
pnpm vitest run src/components/ui/data-display/meeple-card/parts/__tests__/Cover.test.tsx
```
Expected: FAIL — current Cover uses GameCoverPlaceholder for entity='game', no `coverEmoji` prop, no `data-slot="cover-emoji-band"`.

- [ ] **Step 6.3: Implement Cover dual-mode**

Replace `apps/web/src/components/ui/data-display/meeple-card/parts/Cover.tsx`:

```tsx
'use client';

import { useState } from 'react';

import { shouldUsePlaceholder } from '@/lib/games/cover-utils';

import { entityHsl, entityIcon } from '../tokens';

import type { MeepleEntityType, MeepleCardVariant } from '../types';

interface CoverProps {
  entity: MeepleEntityType;
  variant: MeepleCardVariant;
  imageUrl?: string;
  alt?: string;
  /**
   * Stable id kept for backwards compatibility with existing consumers.
   * Post-#1856 (DEC-2) the emoji-band fallback no longer uses GameCoverPlaceholder,
   * so gameId is unused inside this component — retained to keep the consumer API
   * stable.
   */
  gameId?: string;
  /**
   * UTF-8 emoji rendered in the squat-band fallback when `imageUrl` is absent or
   * blocked. Falls back to `entityIcon[entity]`. See #1856 DEC-2/DEC-3.
   */
  coverEmoji?: string;
}

const aspectRatioClass: Record<MeepleCardVariant, string> = {
  grid: 'aspect-[7/10]',
  list: 'aspect-square',
  compact: 'aspect-square',
  featured: 'aspect-video',
  hero: 'aspect-video',
  focus: 'aspect-[7/10]',
};

export function Cover({ entity, variant, imageUrl, alt, coverEmoji }: CoverProps) {
  const gradientColor = entityHsl(entity, 0.15);
  const bandGradient = `linear-gradient(135deg, ${entityHsl(entity, 0.35)} 0%, ${entityHsl(entity, 0.55)} 100%)`;

  // #1822: refuse to render BGG-hosted URLs at runtime (rate-limit + ToS).
  // `onError` flips this to true so the next render switches to emoji-band.
  const [hasImgError, setHasImgError] = useState(false);
  const usePlaceholder = hasImgError || shouldUsePlaceholder(imageUrl);

  const emoji = coverEmoji ?? entityIcon[entity];

  return (
    <div className={`relative overflow-hidden ${aspectRatioClass[variant]}`}>
      {usePlaceholder ? (
        <div
          data-slot="cover-emoji-band"
          className="flex h-[100px] w-full items-center justify-center"
          style={{ background: bandGradient }}
          aria-hidden="true"
        >
          <span
            className="text-[38px]"
            style={{ filter: 'drop-shadow(0 2px 6px rgba(0,0,0,.3))' }}
          >
            {emoji}
          </span>
        </div>
      ) : (
        <img
          src={imageUrl}
          alt={alt ?? ''}
          className="h-full w-full object-cover transition-transform duration-500 ease-out group-hover:scale-[1.06]"
          loading="lazy"
          onError={() => setHasImgError(true)}
        />
      )}
      {/* Shimmer overlay */}
      <div
        className="pointer-events-none absolute inset-0 -translate-x-full transition-none group-hover:animate-[shimmer_0.8s_ease-out_forwards]"
        style={{
          background:
            'linear-gradient(105deg, transparent 40%, rgba(255,255,255,0.25) 50%, transparent 60%)',
        }}
      />
      {/* Entity gradient overlay */}
      <div
        className="pointer-events-none absolute inset-0"
        style={{
          background: `linear-gradient(to top, ${gradientColor}, transparent 60%)`,
        }}
      />
    </div>
  );
}
```

- [ ] **Step 6.4: Run test to verify it passes**

Run from `apps/web/`:
```bash
pnpm vitest run src/components/ui/data-display/meeple-card/parts/__tests__/Cover.test.tsx
```
Expected: PASS (9 tests).

- [ ] **Step 6.5: Run existing GameCoverPlaceholder tests (should still pass — component unused but untouched)**

Run from `apps/web/`:
```bash
pnpm vitest run src/components/ui/data-display/meeple-card/parts/__tests__/GameCoverPlaceholder.test.tsx
```
Expected: PASS (existing tests — GameCoverPlaceholder remains in the codebase for reuse).

- [ ] **Step 6.6: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/parts/Cover.tsx apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/Cover.test.tsx
git commit -m "feat(meeple-card): #1856 T6 Cover dual-mode (image-mode preserved, emoji-band fallback per DEC-2/DEC-3)"
```

---

## Task 7: parts/index.ts exports

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/parts/index.ts`

- [ ] **Step 7.1: Inspect current exports**

Run:
```bash
cat apps/web/src/components/ui/data-display/meeple-card/parts/index.ts
```
Expected: shows current barrel exports. Note the alphabetical ordering convention used.

- [ ] **Step 7.2: Add MenuPlaceholder + CardFooter exports**

Add to `apps/web/src/components/ui/data-display/meeple-card/parts/index.ts` (insert in alphabetical order with the existing exports — typically after `AccentBorder` for `CardFooter`, and after `ManaPipPopover` for `MenuPlaceholder`):

```typescript
export { CardFooter } from './CardFooter';
export { MenuPlaceholder } from './MenuPlaceholder';
```

If `index.ts` uses re-export pattern `export * from './X'`, follow the same pattern. Otherwise use named re-exports.

- [ ] **Step 7.3: Verify exports compile**

Run from `apps/web/`:
```bash
pnpm typecheck
```
Expected: 0 errors.

- [ ] **Step 7.4: Verify imports work via barrel**

Add a sanity test in `apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/index.barrel.test.ts`:

```typescript
import { describe, it, expect } from 'vitest';

import { CardFooter, MenuPlaceholder } from '../index';

describe('parts/index barrel', () => {
  it('exports CardFooter as a function component', () => {
    expect(typeof CardFooter).toBe('function');
  });

  it('exports MenuPlaceholder as a function component', () => {
    expect(typeof MenuPlaceholder).toBe('function');
  });
});
```

- [ ] **Step 7.5: Run barrel test**

Run from `apps/web/`:
```bash
pnpm vitest run src/components/ui/data-display/meeple-card/parts/__tests__/index.barrel.test.ts
```
Expected: PASS (2 tests).

- [ ] **Step 7.6: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/parts/index.ts apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/index.barrel.test.ts
git commit -m "feat(meeple-card): #1856 T7 export CardFooter + MenuPlaceholder from parts barrel"
```

---

## Task 8: GridCard integration + update regression-guard tests

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/GridCard.tsx`
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/__tests__/GridCard.test.tsx`

This task is the largest — it orchestrates all parts and updates the existing regression-guard tests to reflect DEC-5 (StatusBadge removed from top-left stack, moved to footer).

- [ ] **Step 8.1: Write the new failing integration tests**

Append to `apps/web/src/components/ui/data-display/meeple-card/variants/__tests__/GridCard.test.tsx` (do NOT delete existing tests yet — they'll be updated in Step 8.5):

```tsx
/**
 * #1856 SP4 mockup conformance integration tests.
 *
 * Verifies GridCard orchestrates the 5 mockup parts:
 *   1. AccentBorder horizontal-top
 *   2. Cover dual-mode (emoji-band when no imageUrl)
 *   3. EntityBadge glass (top-left, alone in stack)
 *   4. MenuPlaceholder (top-right, hover-visible)
 *   5. CardFooter (border-top + StatusDot + uppercase mono badge)
 */
describe('GridCard SP4 mockup conformance (#1856)', () => {
  it('renders AccentBorder horizontal-top (not vertical-left)', () => {
    const { container } = render(<GridCard entity="game" title="Catan" />);
    // AccentBorder is the first absolute-positioned child with top-0 right-0.
    const accent = container.querySelector('.absolute.top-0.right-0');
    expect(accent).not.toBeNull();
  });

  it('renders MenuPlaceholder button with aria-label="Azioni"', () => {
    render(<GridCard entity="game" title="Catan" />);
    expect(screen.getByRole('button', { name: 'Azioni' })).toBeInTheDocument();
  });

  it('renders CardFooter with status when status prop is set', () => {
    const { container } = render(<GridCard entity="game" title="Catan" status="owned" />);
    const footer = container.querySelector('[data-slot="footer-badge"]');
    expect(footer).not.toBeNull();
    expect(footer?.textContent?.toLowerCase()).toContain('owned');
  });

  it('renders CardFooter with badge taking precedence over status', () => {
    const { container } = render(<GridCard entity="kb" title="Catan KB" status="indexed" badge="processing" />);
    const footer = container.querySelector('[data-slot="footer-badge"]');
    expect(footer?.textContent?.toLowerCase()).toContain('processing');
  });

  it('renders emoji-band Cover when imageUrl is absent (game entity gets entityIcon[game])', () => {
    const { container } = render(<GridCard entity="game" title="Catan" />);
    const band = container.querySelector('[data-slot="cover-emoji-band"]');
    expect(band).not.toBeNull();
  });

  it('uses coverEmoji prop in the emoji-band when provided', () => {
    render(<GridCard entity="session" title="Game night #1" coverEmoji="🎯" />);
    expect(screen.getByText('🎯')).toBeInTheDocument();
  });

  it('does NOT render StatusBadge inside the top-left badge-stack (moved to footer per DEC-5)', () => {
    const { container } = render(<GridCard entity="game" title="Catan" status="owned" />);
    const stack = container.querySelector('[data-slot="badge-stack"]');
    expect(stack).not.toBeNull();
    // Stack should contain ONLY the EntityBadge.
    expect(stack?.children).toHaveLength(1);
    const entityBadge = stack?.querySelector('[data-slot="meeple-card-entity-badge"]');
    expect(entityBadge).not.toBeNull();
  });

  it('does NOT render inline badge in the header (moved to footer per DEC-5)', () => {
    const { container } = render(<GridCard entity="game" title="Catan" badge="OWNED" />);
    // The legacy inline-badge slot must be gone.
    const inlineBadge = container.querySelector('[data-slot="badge"]');
    expect(inlineBadge).toBeNull();
    // The footer is now the source of truth for the badge text.
    const footer = container.querySelector('[data-slot="footer-badge"]');
    expect(footer?.textContent?.toLowerCase()).toContain('owned');
  });
});
```

- [ ] **Step 8.2: Run new tests to verify they fail**

Run from `apps/web/`:
```bash
pnpm vitest run src/components/ui/data-display/meeple-card/variants/__tests__/GridCard.test.tsx
```
Expected: NEW tests in the `SP4 mockup conformance` describe block fail. Existing `connections path` and `top-left badge stack` describe blocks STILL PASS (they reflect pre-DEC-5 behavior).

- [ ] **Step 8.3: Implement GridCard integration**

Replace `apps/web/src/components/ui/data-display/meeple-card/variants/GridCard.tsx`:

```tsx
'use client';

import { useConnectionSource } from '../hooks/useConnectionSource';
import { AccentBorder } from '../parts/AccentBorder';
import { CardFooter } from '../parts/CardFooter';
import { ConnectionChipStrip } from '../parts/ConnectionChipStrip';
import { Cover } from '../parts/Cover';
import { EntityBadge } from '../parts/EntityBadge';
import { ManaPips } from '../parts/ManaPips';
import { MenuPlaceholder } from '../parts/MenuPlaceholder';
import { MetaChips } from '../parts/MetaChips';
import { QuickActions } from '../parts/QuickActions';
import { Rating } from '../parts/Rating';
import { TagStrip } from '../parts/TagStrip';
import { entityHsl } from '../tokens';

import type { MeepleCardProps } from '../types';

export function GridCard(props: MeepleCardProps) {
  const {
    entity,
    title,
    id,
    subtitle,
    imageUrl,
    coverEmoji,
    rating,
    ratingMax,
    metadata = [],
    tags = [],
    status,
    badge,
    actions = [],
    manaPips,
    showQuickActions,
    onClick,
    className = '',
  } = props;
  const testId = props['data-testid'];

  const { source, items: csItems, variant: csVariant } = useConnectionSource(props);

  const glowColor = entityHsl(entity, 0.4);

  return (
    <div
      className={`group relative flex cursor-pointer flex-col overflow-hidden rounded-2xl border border-[var(--mc-border)] bg-[var(--mc-bg-card)] shadow-[var(--mc-shadow-sm)] outline-2 outline-offset-2 outline-transparent backdrop-blur-[12px] backdrop-saturate-[180%] transition-all duration-[350ms] [transition-timing-function:cubic-bezier(0.4,0,0.2,1)] hover:-translate-y-1.5 hover:shadow-[var(--mc-shadow-xl)] hover:outline-[var(--mc-glow)] ${className}`}
      style={{ '--mc-glow': glowColor } as React.CSSProperties}
      onClick={onClick}
      role={onClick ? 'button' : undefined}
      tabIndex={onClick ? 0 : undefined}
      data-entity={entity}
      data-testid={testId}
    >
      <AccentBorder entity={entity} />
      <div className="relative">
        <Cover
          entity={entity}
          variant="grid"
          imageUrl={imageUrl}
          alt={title}
          gameId={id}
          coverEmoji={coverEmoji}
        />
        {/* Top-left badge stack: EntityBadge only (StatusBadge moved to footer per #1856 DEC-5). */}
        <div
          className="absolute left-2.5 top-2 z-10 flex flex-col items-start gap-1"
          data-slot="badge-stack"
        >
          <EntityBadge entity={entity} stacked />
        </div>
        {/* Top-right hover-visible 3-dot menu placeholder (#1856 DEC-4). */}
        <MenuPlaceholder />
        {tags.length > 0 && <TagStrip tags={tags} entity={entity} topClass="top-9" />}
        {showQuickActions && actions.length > 0 && <QuickActions actions={actions} />}
      </div>
      <div className="flex flex-1 flex-col gap-[3px] px-3.5 py-2.5 pb-2">
        <h3 className="font-[var(--font-quicksand)] text-[0.95rem] font-bold leading-tight text-[var(--mc-text-primary)]">
          {title}
        </h3>
        {subtitle && (
          <p className="text-[0.78rem] leading-tight text-[var(--mc-text-secondary)]">{subtitle}</p>
        )}
        {rating !== undefined && <Rating value={rating} max={ratingMax} />}
        {metadata.length > 0 && <MetaChips metadata={metadata} />}
      </div>
      {manaPips && manaPips.length > 0 && <ManaPips pips={manaPips} size="md" />}
      {source === 'connections' && csItems.length > 0 && (
        <ConnectionChipStrip connections={csItems} variant={csVariant} />
      )}
      {/* Footer: StatusDot + uppercase mono badge (#1856 DEC-5). */}
      <CardFooter status={status} badge={badge} />
    </div>
  );
}
```

- [ ] **Step 8.4: Run all GridCard tests — NEW pass, OLD regression-guard tests fail**

Run from `apps/web/`:
```bash
pnpm vitest run src/components/ui/data-display/meeple-card/variants/__tests__/GridCard.test.tsx
```
Expected:
- **New `SP4 mockup conformance` tests**: PASS (8 tests).
- **Old `top-left badge stack (overlap fix)` tests**: FAIL (they expect StatusBadge in the stack — now removed per DEC-5).
- **Old `connections path` tests**: PASS (unchanged behavior).

- [ ] **Step 8.5: Update the old regression-guard tests to reflect DEC-5**

In `apps/web/src/components/ui/data-display/meeple-card/variants/__tests__/GridCard.test.tsx`, replace the entire `describe('GridCard top-left badge stack (overlap fix)', ...)` block with:

```tsx
/**
 * Post-#1856 DEC-5: the top-left badge stack contains ONLY EntityBadge.
 * StatusBadge was moved to the new CardFooter (border-top + StatusDot + label).
 * The legacy overlap-fix regression test is superseded — TagStrip now shifts to
 * a single offset because the stack has at most 1 badge.
 *
 * Original pre-#1856 invariants:
 *   - Stack wraps EntityBadge + StatusBadge → SUPERSEDED (only EntityBadge now)
 *   - TagStrip shifts to top-14 when 2 badges present → SUPERSEDED (always top-9)
 */
describe('GridCard top-left badge stack (post-#1856)', () => {
  it('renders EntityBadge alone in the stack (StatusBadge moved to footer)', () => {
    const { container } = render(
      <GridCard entity="game" title="Catan" status="owned" tags={['Strategy']} />
    );
    const stack = container.querySelector('[data-slot="badge-stack"]');
    expect(stack).not.toBeNull();
    expect(stack?.className).toMatch(/flex/);
    expect(stack?.className).toMatch(/flex-col/);
    // Only EntityBadge (containing the "Game" label) — no StatusBadge ("Posseduto").
    expect(stack?.children).toHaveLength(1);
    expect(stack?.textContent).toMatch(/Game/i);
    expect(stack?.textContent).not.toMatch(/Posseduto/i);
  });

  it('renders EntityBadge in the stack also when status is omitted', () => {
    const { container } = render(<GridCard entity="game" title="Catan" />);
    const stack = container.querySelector('[data-slot="badge-stack"]');
    expect(stack).not.toBeNull();
    expect(stack?.children).toHaveLength(1);
  });

  it('keeps TagStrip at top-9 since the stack now has at most 1 badge', () => {
    const { container } = render(
      <GridCard entity="game" title="Catan" status="owned" tags={['A', 'B']} />
    );
    const tagStrip = container.querySelector('[data-testid="tag-strip"]');
    expect(tagStrip?.className).toMatch(/top-9/);
  });
});
```

- [ ] **Step 8.6: Run all GridCard tests — all PASS**

Run from `apps/web/`:
```bash
pnpm vitest run src/components/ui/data-display/meeple-card/variants/__tests__/GridCard.test.tsx
```
Expected: ALL tests PASS (connections + post-#1856 stack + SP4 conformance).

- [ ] **Step 8.7: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/variants/GridCard.tsx apps/web/src/components/ui/data-display/meeple-card/variants/__tests__/GridCard.test.tsx
git commit -m "feat(meeple-card): #1856 T8 GridCard integration (5 SP4 parts orchestrated + regression-guard tests updated per DEC-5)"
```

---

## Task 9: Contract test for `MeepleCard` props

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/__tests__/MeepleCard.sp4-contract.test.tsx`

- [ ] **Step 9.1: Write the failing test**

Create the file:

```tsx
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';

import { MeepleCard } from '../MeepleCard';
import { entityIcon } from '../tokens';

describe('MeepleCard SP4 contract (#1856)', () => {
  it('mounts without errors for all 9 entity types using default grid variant', () => {
    const entities = ['game', 'player', 'session', 'agent', 'kb', 'chat', 'event', 'toolkit', 'tool'] as const;
    for (const e of entities) {
      const { unmount } = render(<MeepleCard entity={e} title={`${e} title`} />);
      expect(screen.getByText(`${e} title`)).toBeInTheDocument();
      unmount();
    }
  });

  it('coverEmoji prop renders in the emoji-band fallback', () => {
    render(<MeepleCard entity="session" title="X" coverEmoji="🎲" />);
    expect(screen.getByText('🎲')).toBeInTheDocument();
  });

  it('omitting coverEmoji falls back to entityIcon for the entity type', () => {
    render(<MeepleCard entity="agent" title="Bot" />);
    expect(screen.getByText(entityIcon.agent)).toBeInTheDocument();
  });

  it('status renders in footer (not top-left stack)', () => {
    const { container } = render(<MeepleCard entity="kb" title="Doc" status="indexed" />);
    const footer = container.querySelector('[data-slot="footer-badge"]');
    expect(footer?.textContent?.toLowerCase()).toContain('indexed');
    const stack = container.querySelector('[data-slot="badge-stack"]');
    expect(stack?.children).toHaveLength(1); // EntityBadge only
  });

  it('hides footer entirely when status and badge are both undefined', () => {
    const { container } = render(<MeepleCard entity="game" title="Catan" />);
    expect(container.querySelector('[data-slot="footer-badge"]')).toBeNull();
  });
});
```

- [ ] **Step 9.2: Run test to verify it passes (T1-T8 already wired up the behavior)**

Run from `apps/web/`:
```bash
pnpm vitest run src/components/ui/data-display/meeple-card/__tests__/MeepleCard.sp4-contract.test.tsx
```
Expected: PASS (5 tests).

- [ ] **Step 9.3: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/__tests__/MeepleCard.sp4-contract.test.tsx
git commit -m "test(meeple-card): #1856 T9 SP4 contract test (9 entities + coverEmoji + footer)"
```

---

## Task 10: Consumer category smoke test (post-implementation, NON-gate)

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/__tests__/consumer-categories.smoke.test.tsx`

- [ ] **Step 10.1: Write the smoke test (no failing-test step — this is post-impl smoke)**

Create the file:

```tsx
/**
 * Smoke test: verifies MeepleCard mounts without throwing for each of the 9
 * entity types representative of the 12 consumer surface categories.
 *
 * This is NOT a visual diff test (per #1856 DEC-6 — Visual Gate REMOVED
 * 2026-05-20). It's a render-smoke gate that catches structural breakage
 * (e.g. missing prop wiring, runtime errors in new parts).
 */
import { describe, it, expect } from 'vitest';
import { render } from '@testing-library/react';

import { MeepleCard } from '../MeepleCard';

import type { MeepleEntityType } from '../types';

const surfaceCategories: Array<{
  name: string;
  entity: MeepleEntityType;
  withImage: boolean;
  withCoverEmoji: boolean;
  withStatus: boolean;
}> = [
  { name: 'library / games', entity: 'game', withImage: false, withCoverEmoji: false, withStatus: true },
  { name: 'games hub', entity: 'game', withImage: true, withCoverEmoji: false, withStatus: false },
  { name: 'sessions', entity: 'session', withImage: false, withCoverEmoji: true, withStatus: true },
  { name: 'players', entity: 'player', withImage: false, withCoverEmoji: false, withStatus: false },
  { name: 'agents', entity: 'agent', withImage: false, withCoverEmoji: false, withStatus: true },
  { name: 'kb', entity: 'kb', withImage: false, withCoverEmoji: false, withStatus: true },
  { name: 'chat', entity: 'chat', withImage: false, withCoverEmoji: false, withStatus: false },
  { name: 'events / game-night', entity: 'event', withImage: false, withCoverEmoji: true, withStatus: false },
  { name: 'toolkits', entity: 'toolkit', withImage: false, withCoverEmoji: true, withStatus: false },
  { name: 'tools', entity: 'tool', withImage: false, withCoverEmoji: false, withStatus: false },
];

describe('MeepleCard consumer-category smoke (#1856)', () => {
  for (const cat of surfaceCategories) {
    it(`mounts for ${cat.name} surface (entity=${cat.entity})`, () => {
      expect(() =>
        render(
          <MeepleCard
            entity={cat.entity}
            title={`Smoke ${cat.name}`}
            imageUrl={cat.withImage ? 'https://cdn.example.com/x.webp' : undefined}
            coverEmoji={cat.withCoverEmoji ? '🎯' : undefined}
            status={cat.withStatus ? 'owned' : undefined}
            badge={cat.withStatus ? 'OWNED' : undefined}
          />
        )
      ).not.toThrow();
    });
  }
});
```

- [ ] **Step 10.2: Run smoke test**

Run from `apps/web/`:
```bash
pnpm vitest run src/components/ui/data-display/meeple-card/__tests__/consumer-categories.smoke.test.tsx
```
Expected: PASS (10 tests).

- [ ] **Step 10.3: Run full primitive test suite — all green**

Run from `apps/web/`:
```bash
pnpm vitest run src/components/ui/data-display/meeple-card/
```
Expected: ALL PASS (existing + new tests). No regressions.

- [ ] **Step 10.4: Run typecheck on full apps/web**

Run from `apps/web/`:
```bash
pnpm typecheck
```
Expected: 0 errors. Confirms `coverEmoji` prop addition + Cover/EntityBadge restructure didn't break any of the 72 consumer files.

- [ ] **Step 10.5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/__tests__/consumer-categories.smoke.test.tsx
git commit -m "test(meeple-card): #1856 T10 consumer-category render smoke (10 entities, non-gate per DEC-6)"
```

---

## Final verification + PR

- [ ] **Step F.1: Run full FE test suite**

Run from `apps/web/`:
```bash
pnpm test
```
Expected: ALL PASS. If any pre-existing flake from `AdvancedFilterPanel` or similar (per #1851 baseline) surfaces, re-run once: `pnpm vitest run --reporter verbose <failing-file>`. If it still fails, document in the PR body that it's #1851 baseline (NOT introduced by #1856).

- [ ] **Step F.2: Run lint**

Run from `apps/web/`:
```bash
pnpm lint
```
Expected: 0 errors, 0 warnings related to our changes. Existing `local/no-hardcoded-color-utility` disable comments in `EntityBadge` and new `MenuPlaceholder` are pre-justified inline.

- [ ] **Step F.3: Push branch**

Run from repo root:
```bash
git push -u origin feat/issue-1856-meeple-card-sp4-conformance
```

- [ ] **Step F.4: Open PR to main-dev (parent branch per P125)**

Use `gh pr create` with HEREDOC body. Title: `feat(meeple-card): #1856 SP4 MeepleCardGrid mockup conformance reskin`. Body should include:

```
## Summary

Implements #1856 — SP4 MeepleCardGrid mockup conformance reskin (cross-cutting, 72 consumers).

Surgical primitive restructure (Approach A) per spec `docs/superpowers/specs/2026-06-04-meeple-card-sp4-conformance-design.md`.

## Changes

- **types.ts**: Add `coverEmoji?: string` to `MeepleCardProps` (additive, backwards compat).
- **AccentBorder**: vertical-left → horizontal-top (3px → 5px on hover).
- **Cover**: dual-mode rendering — imageUrl present → aspect-[7/10] img; absent → h-[100px] emoji-band with `coverEmoji ?? entityIcon[entity]` at 38px.
- **EntityBadge**: solid bg → glass `bg-white/85 backdrop-blur-md` + entity color text + emoji prefix.
- **MenuPlaceholder** (new): hover-visible glass ⋯ button top-right, no-op handler (placeholder per DEC-4).
- **CardFooter** (new): border-top + StatusDot + uppercase mono badge.
- **GridCard**: orchestrates all parts; StatusBadge removed from top-left stack (moved to footer per DEC-5).

## Decisions (locked in brainstorming)

| ID | Decision |
|---|---|
| DEC-1 | Surgical primitive restructure, 1 PR |
| DEC-2 | Cover emoji-band for ALL entities (game included; GameCoverPlaceholder retained for reuse but not invoked) |
| DEC-3 | Cover dual-mode (image-mode preserved for aspect-7/10 cover photos) |
| DEC-4 | 3-dot menu = visual placeholder, no functional handler |
| DEC-5 | Footer StatusDot+badge replaces top-left StatusBadge |
| DEC-6 | DOM structure assertions only (no visual regression infra reintro) |

## Tests

- 7 new test files: `AccentBorder.test`, `Cover.test`, `EntityBadge.test`, `MenuPlaceholder.test`, `CardFooter.test`, `MeepleCard.sp4-contract.test`, `consumer-categories.smoke.test`.
- Existing `GridCard.test` regression-guard tests updated to reflect DEC-5 (StatusBadge no longer in top-left stack).
- All 9 entity types covered.
- NO `jest-axe` in this PR — a11y impacts (EntityBadge text color, MenuPlaceholder trigger) deferred to #1842.
- NO visual regression infra — designer review post-preview-deploy is the gate (DEC-6).

## Designer review checklist

Preview deploy: <vercel/staging URL>

- [ ] Library card (`/library`)
- [ ] Games card (`/games/[gameId]`)
- [ ] Sessions card (`/sessions`)
- [ ] Players card (`/players`)
- [ ] Agents card (`/agents`)
- [ ] Dashboard / KB / chat / events / toolkits surfaces

## Out of scope

See spec §8 — BE schema for `coverEmoji`, a11y rule re-enable (#1842), visual regression, non-grid variants, MenuPlaceholder handler.

## Related

- Closes #1856
- Deferred follow-up: #1842 (a11y headingLevel)
- Spec: `docs/superpowers/specs/2026-06-04-meeple-card-sp4-conformance-design.md`
- Plan: `docs/superpowers/plans/2026-06-04-meeple-card-sp4-conformance.md`
```

Run:
```bash
gh pr create --base main-dev --title "feat(meeple-card): #1856 SP4 MeepleCardGrid mockup conformance reskin" --body "$(cat <<'EOF'
[paste body from above]
EOF
)"
```

- [ ] **Step F.5: Watch CI**

Run:
```bash
gh pr checks --watch
```
Expected: All CI green. If `Frontend Tests shard 1/3 + 2/3` baseline fail per #1851 (memoria), document in PR body. If `AdminSideDrawer` baseline (P75 risolto via #1526) re-emerges, document.

- [ ] **Step F.6: Wait for designer review per category, then merge normale (NO admin override)**

After designer review approval (or after this PR is the first to confirm #1851 + AdminSideDrawer baselines are stable), merge normale:

```bash
gh pr merge --squash --delete-branch
```

If P145 admin-override is required (baseline #1851 still blocking), use:
```bash
gh pr merge --squash --admin --delete-branch
```
Document the reason in a PR comment.

---

## Self-review checklist (writing-plans skill §Self-Review)

**1. Spec coverage**: Walked through spec sections:
- §1 Context → covered in plan header
- §2 Decisions → embedded in task implementations (Cover DEC-2/3, EntityBadge DEC-5, etc.)
- §3 Architecture → file-level isolation honored (1 task per file mostly)
- §4 Components file map → T1-T8 covers every listed file
- §5 Data flow → coverEmoji wiring tested in T6 + T9 + T10
- §6 Error handling → onError + footer renderless tested in T6 + T4
- §7 Testing strategy → all 7 test files generated (+ barrel + contract + smoke)
- §8 Out of scope → respected (no BE migration, no axe, no visual diff)
- §9 Rollout & risk → Final verification + PR steps cover deploy + designer review
✅ All sections covered.

**2. Placeholder scan**: No "TBD", "TODO", "implement later", or vague directives. Every step has actual code or actual commands.

**3. Type consistency**:
- `MeepleCardProps.coverEmoji?: string` — used identically in T1, T6, T8, T9, T10.
- `CardStatus` type — used in T4 `CardFooterProps.status` and matches existing `MeepleCardProps.status` type.
- `MeepleEntityType` — used in T2, T3, T5, T6 consistently.
- `entityHslText` — used in T5 (EntityBadge color), `entityHsl` in T2 (AccentBorder bg), `entityIcon` in T5 + T6.
- `statusColors` — used in T4 only.
- `MeepleCardVariant` — used in T6 (`aspectRatioClass` lookup).
- `entityLabel` — used in T5 (EntityBadge text content).

All type names, prop names, and helper names are consistent across tasks. No drift detected.
