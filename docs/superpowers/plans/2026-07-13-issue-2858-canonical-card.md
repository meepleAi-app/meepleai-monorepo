# MeepleCard Canonical Card + Decision-Table + MeepleCardGame Adapter (Issue #2858 / C1) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Elect the canonical 2-tier card taxonomy (MeepleCard = display, ExtraMeepleCard = detail), convert the last standalone rogue `MeepleCardGame` into a thin adapter over `MeepleCard`, publish a DTO×context decision-table with a traceability gate, and add an import-boundary ESLint rule that stops new code from re-assembling cards out of `meeple-card` internals.

**Architecture:** `MeepleCard` gains a first-class `href` prop so `GridCard` can render a real `<Link>` root (needed by the public `/shared-games` route). `MeepleCardGame` becomes a pure adapter that maps its shared-games props onto canonical `MeepleCardProps`. A decision-table markdown doc is validated by a `<MeepleCard>`-usage-based coverage test. A single-purpose ESLint rule bans value-imports of `meeple-card/parts/` and `meeple-card/variants/` from outside the canonical directory.

**Tech Stack:** Next.js 16 (App Router) · React 19 · TypeScript · Tailwind · Vitest + Testing Library · ESLint 9 flat config + custom rules in `apps/web/eslint-rules/` (RuleTester via `node:test`).

**Spec:** `docs/superpowers/specs/2026-07-13-issue-2858-canonical-card-design.md`

## Global Constraints

- Work on branch `feature/issue-2858-canonical-card-decision-table` (already created from `main-dev`); PR targets `main-dev`.
- All frontend paths are under `apps/web/`. Run frontend commands from `apps/web/`.
- Single-file Vitest run: `pnpm exec vitest run <path>`. Typecheck: `pnpm typecheck`. Lint: `pnpm lint`. Custom-rule unit tests: `node --test eslint-rules/<file>.test.js`.
- **A11y gate is BLOCKING.** The canonical render already uses AA tokens (`--c-*` 38% + `--c-*-text`); do not introduce hardcoded colors (`local/no-hardcoded-color-utility` is `error`).
- Design tokens: use semantic/entity utilities; never `bg-white`/`text-gray-*` etc.
- The `MeepleCardGame` public prop interface must stay unchanged **except** the `compact` prop, which is removed (it is an unwired responsive knob — `page-client.tsx` never sets it).
- Commit message format: `type(scope): description` ending with the `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>` trailer.

---

### Task 1: `href` first-class on MeepleCard / GridCard

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/types.ts` (add `href?` to `MeepleCardProps`)
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/GridCard.tsx` (render `<Link>` when `href` present)
- Test: `apps/web/src/components/ui/data-display/meeple-card/variants/__tests__/GridCard.href.test.tsx` (create)

**Interfaces:**
- Produces: `MeepleCardProps.href?: string`. When set, `GridCard`'s root is a `next/link` `<a>`; `onClick`/`role="button"` path is used only when `href` is absent. Consumed by Task 2.

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/components/ui/data-display/meeple-card/variants/__tests__/GridCard.href.test.tsx`:

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { GridCard } from '../GridCard';

vi.mock('next/link', () => ({
  default: ({
    href,
    children,
    prefetch: _prefetch,
    ...rest
  }: {
    href: string;
    children: React.ReactNode;
    prefetch?: boolean;
  } & React.AnchorHTMLAttributes<HTMLAnchorElement>) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
}));

describe('GridCard href (Issue #2858)', () => {
  it('renders the root as an anchor with href when href is provided', () => {
    render(<GridCard entity="game" variant="grid" title="Catan" href="/shared-games/1" />);
    expect(screen.getByRole('link')).toHaveAttribute('href', '/shared-games/1');
    expect(screen.getByRole('heading', { name: 'Catan' })).toBeInTheDocument();
  });

  it('renders the root as a div (no anchor) when href is absent', () => {
    const { container } = render(<GridCard entity="game" variant="grid" title="Catan" />);
    expect(container.querySelector('a')).toBeNull();
  });

  it('keeps role=button + onClick when href is absent and onClick is provided', () => {
    const onClick = vi.fn();
    render(<GridCard entity="game" variant="grid" title="Catan" onClick={onClick} />);
    fireEvent.click(screen.getByRole('button'));
    expect(onClick).toHaveBeenCalledOnce();
  });

  it('forwards data-testid to the anchor root when href is provided', () => {
    render(
      <GridCard
        entity="game"
        variant="grid"
        title="Catan"
        href="/x"
        data-testid="shared-games-card"
      />
    );
    expect(screen.getByTestId('shared-games-card').tagName).toBe('A');
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `pnpm exec vitest run src/components/ui/data-display/meeple-card/variants/__tests__/GridCard.href.test.tsx`
Expected: FAIL — `href` is not a known prop and `GridCard` never renders an `<a>`, so `getByRole('link')` finds nothing.

- [ ] **Step 3: Add `href?` to `MeepleCardProps`**

In `apps/web/src/components/ui/data-display/meeple-card/types.ts`, add the `href` field immediately after `onClick?: () => void;` (currently line 130):

```ts
  onClick?: () => void;
  /**
   * Issue #2858 (C1) — when present, the card root renders as a Next.js
   * `<Link href prefetch>` instead of a `<div role="button">`, giving real
   * anchor semantics (prefetch, middle-click / open-in-new-tab, native
   * keyboard focus, SEO) to navigable display cards on public routes.
   * Currently honored by GridCard only.
   */
  href?: string;
```

- [ ] **Step 4: Render `<Link>` in GridCard when `href` is set**

Replace the entire contents of `apps/web/src/components/ui/data-display/meeple-card/variants/GridCard.tsx` with:

```tsx
'use client';

import Link from 'next/link';

import { useConnectionSource } from '../hooks/useConnectionSource';
import { AccentBorder } from '../parts/AccentBorder';
import { CardFooter } from '../parts/CardFooter';
import { ConnectionChipStrip } from '../parts/ConnectionChipStrip';
import { Cover } from '../parts/Cover';
import { CoverAttributionChip } from '../parts/CoverAttributionChip';
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
    headingLevel,
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
    href,
    className = '',
    attribution,
  } = props;
  const testId = props['data-testid'];

  const { source, items: csItems, variant: csVariant } = useConnectionSource(props);

  const glowColor = entityHsl(entity, 0.4);

  const rootClassName = `group relative flex cursor-pointer flex-col overflow-hidden rounded-2xl border border-[var(--mc-border)] bg-[var(--mc-bg-card)] shadow-[var(--mc-shadow-sm)] outline-2 outline-offset-2 outline-transparent backdrop-blur-[12px] backdrop-saturate-[180%] transition-all duration-[350ms] [transition-timing-function:cubic-bezier(0.4,0,0.2,1)] hover:-translate-y-1.5 hover:shadow-[var(--mc-shadow-xl)] hover:outline-[var(--mc-glow)] ${className}`;
  const rootStyle = { '--mc-glow': glowColor } as React.CSSProperties;

  const content = (
    <>
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
        {(!showQuickActions || actions.length === 0) && <MenuPlaceholder />}
        {tags.length > 0 && <TagStrip tags={tags} entity={entity} topClass="top-9" />}
        {showQuickActions && actions.length > 0 && <QuickActions actions={actions} />}
      </div>
      <div className="flex flex-1 flex-col gap-[3px] px-3.5 py-2.5 pb-2">
        {(() => {
          const HeadingTag = `h${headingLevel ?? 3}` as 'h2' | 'h3' | 'h4';
          return (
            <HeadingTag className="font-[var(--font-quicksand)] text-[0.95rem] font-bold leading-tight text-[var(--mc-text-primary)]">
              {title}
            </HeadingTag>
          );
        })()}
        {subtitle && (
          <p className="text-[0.78rem] leading-tight text-[var(--mc-text-secondary)]">{subtitle}</p>
        )}
        {rating !== undefined && <Rating value={rating} max={ratingMax} />}
        {metadata.length > 0 && <MetaChips metadata={metadata} />}
        <CoverAttributionChip attribution={attribution} />
      </div>
      {manaPips && manaPips.length > 0 && <ManaPips pips={manaPips} size="md" />}
      {source === 'connections' && csItems.length > 0 && (
        <ConnectionChipStrip connections={csItems} variant={csVariant} />
      )}
      {/* Footer: StatusDot + uppercase mono badge (#1856 DEC-5). */}
      <CardFooter status={status} badge={badge} />
    </>
  );

  if (href) {
    return (
      <Link
        href={href}
        prefetch
        className={`${rootClassName} no-underline`}
        style={rootStyle}
        data-entity={entity}
        data-testid={testId}
      >
        {content}
      </Link>
    );
  }

  return (
    <div
      className={rootClassName}
      style={rootStyle}
      onClick={onClick}
      role={onClick ? 'button' : undefined}
      tabIndex={onClick ? 0 : undefined}
      data-entity={entity}
      data-testid={testId}
    >
      {content}
    </div>
  );
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `pnpm exec vitest run src/components/ui/data-display/meeple-card/variants/__tests__/GridCard.href.test.tsx`
Expected: PASS (4 tests).

- [ ] **Step 6: Guard against regressions in the existing MeepleCard suite + typecheck**

Run: `pnpm exec vitest run src/components/ui/data-display/meeple-card`
Expected: PASS (existing variant/acceptance-matrix tests still green — the `href`-less path is byte-identical to before).
Run: `pnpm typecheck`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/types.ts \
        apps/web/src/components/ui/data-display/meeple-card/variants/GridCard.tsx \
        apps/web/src/components/ui/data-display/meeple-card/variants/__tests__/GridCard.href.test.tsx
git commit -m "$(cat <<'EOF'
feat(meeple-card): first-class href renders GridCard root as a Link (#2858)

Enables real anchor semantics (prefetch, open-in-new-tab, native focus) for
navigable display cards on public routes. href-less behavior is unchanged.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: Convert `MeepleCardGame` to a thin adapter over `MeepleCard`

**Files:**
- Modify: `apps/web/src/components/ui/shared-games/meeple-card-game.tsx` (body → adapter; remove `compact`)
- Modify: `apps/web/src/components/ui/shared-games/meeple-card-game.test.tsx` (behavioral contract)
- Modify: `apps/web/src/components/ui/shared-games/shared-games-grid.tsx` (stop forwarding `compact` to the card; drop `compact` from `SharedGamesGridGame`)
- Modify: `apps/web/src/components/ui/shared-games/shared-games-grid.test.tsx` (3 selectors `data-slot` → `data-testid`)

**Interfaces:**
- Consumes: `MeepleCardProps.href` (Task 1), `MeepleCard`, `ConnectionChipProps` (`entityType`, `count`, `label`, `showLabel`).
- Produces: `MeepleCardGame` renders `<MeepleCard entity="game" variant="grid" href="/shared-games/{id}" …>`, carrying `data-testid="shared-games-card"` on the root. `MeepleCardGameProps` no longer has `compact`.

- [ ] **Step 1: Rewrite the test to the behavioral contract (failing)**

Replace the entire contents of `apps/web/src/components/ui/shared-games/meeple-card-game.test.tsx` with:

```tsx
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { MeepleCardGame, type MeepleCardGameLabels } from './meeple-card-game';

// Stub next/link (GridCard's root Link) to a plain <a> in jsdom.
vi.mock('next/link', () => ({
  default: ({
    href,
    children,
    prefetch: _prefetch,
    ...rest
  }: {
    href: string;
    children: React.ReactNode;
    prefetch?: boolean;
  } & React.AnchorHTMLAttributes<HTMLAnchorElement>) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
}));

const labels: MeepleCardGameLabels = {
  ratingAriaLabel: 'Voto',
  toolkitLabel: 'tk',
  agentLabel: 'ag',
  newWeekAriaLabel: count => `${count} nuovi questa settimana`,
};

const baseProps = {
  id: 'f47ac10b-58cc-4372-a567-0e02b2c3d479',
  title: 'Catan',
  year: 1995,
  rating: 4,
  toolkitsCount: 3,
  agentsCount: 2,
  kbsCount: 1,
  newThisWeekCount: 0,
  labels,
};

describe('MeepleCardGame (adapter over MeepleCard, #2858)', () => {
  it('renders an anchor linking to /shared-games/{id}', () => {
    render(<MeepleCardGame {...baseProps} />);
    expect(screen.getByRole('link')).toHaveAttribute('href', `/shared-games/${baseProps.id}`);
  });

  it('carries data-testid=shared-games-card on the card root', () => {
    render(<MeepleCardGame {...baseProps} />);
    expect(screen.getByTestId('shared-games-card')).toBeInTheDocument();
  });

  it('renders the title as a heading', () => {
    render(<MeepleCardGame {...baseProps} />);
    expect(screen.getByRole('heading', { name: 'Catan' })).toBeInTheDocument();
  });

  it('renders the year as the subtitle when provided', () => {
    render(<MeepleCardGame {...baseProps} />);
    expect(screen.getByText('1995')).toBeInTheDocument();
  });

  it('omits the year when null', () => {
    render(<MeepleCardGame {...baseProps} year={null} />);
    expect(screen.queryByText('1995')).not.toBeInTheDocument();
  });

  it('renders the canonical rating readout (value.toFixed(1)) from rating + ratingMax=5', () => {
    render(<MeepleCardGame {...baseProps} rating={4} />);
    expect(screen.getByText('4.0')).toBeInTheDocument();
  });

  it('renders the connection strip when any entity count > 0', () => {
    const { container } = render(<MeepleCardGame {...baseProps} />);
    expect(container.querySelector('[data-testid="connection-chip-strip"]')).not.toBeNull();
  });

  it('omits the connection strip when all entity counts are 0', () => {
    const { container } = render(
      <MeepleCardGame {...baseProps} toolkitsCount={0} agentsCount={0} kbsCount={0} />
    );
    expect(container.querySelector('[data-testid="connection-chip-strip"]')).toBeNull();
  });

  it('renders the new-this-week badge (+N) when count >= 2', () => {
    render(<MeepleCardGame {...baseProps} newThisWeekCount={3} />);
    expect(screen.getByText('+3')).toBeInTheDocument();
  });

  it('does not render the new-this-week badge when count < 2', () => {
    render(<MeepleCardGame {...baseProps} newThisWeekCount={1} />);
    expect(screen.queryByText('+1')).not.toBeInTheDocument();
  });

  it('renders the 🎲 cover fallback when coverUrl is missing', () => {
    render(<MeepleCardGame {...baseProps} coverUrl={null} />);
    expect(screen.getByText('🎲')).toBeInTheDocument();
  });

  it('renders an <img> cover when coverUrl is provided', () => {
    const { container } = render(
      <MeepleCardGame {...baseProps} coverUrl="https://cdn.example/c.jpg" />
    );
    const img = container.querySelector('img');
    expect(img).not.toBeNull();
    expect(img).toHaveAttribute('src', 'https://cdn.example/c.jpg');
  });

  describe('Wikidata attribution footer (rendered by MeepleCard for entity=game)', () => {
    it('renders <footer> with license text when wikidataCoverLicense is provided', () => {
      const { container } = render(
        <MeepleCardGame
          {...baseProps}
          wikidataCoverLicense="CC BY-SA 4.0"
          wikidataCoverAttribution="Doe, John"
          wikidataCoverSourceUrl="https://commons.wikimedia.org/wiki/File:Catan.jpg"
        />
      );
      const footer = container.querySelector('footer');
      expect(footer).not.toBeNull();
      expect(footer).toHaveTextContent('CC BY-SA 4.0');
    });

    it('renders a source link when wikidataCoverSourceUrl is provided', () => {
      const { container } = render(
        <MeepleCardGame
          {...baseProps}
          wikidataCoverLicense="CC BY-SA 4.0"
          wikidataCoverSourceUrl="https://commons.wikimedia.org/wiki/File:Catan.jpg"
        />
      );
      const link = container.querySelector('footer a');
      expect(link).not.toBeNull();
      expect(link).toHaveAttribute('href', 'https://commons.wikimedia.org/wiki/File:Catan.jpg');
      expect(link).toHaveAttribute('rel', 'nofollow noopener noreferrer');
      expect(link).toHaveAttribute('target', '_blank');
    });

    it('renders no <footer> when wikidataCoverLicense is omitted', () => {
      const { container } = render(<MeepleCardGame {...baseProps} />);
      expect(container.querySelector('footer')).toBeNull();
    });
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `pnpm exec vitest run src/components/ui/shared-games/meeple-card-game.test.tsx`
Expected: FAIL — the current inline renderer has no `data-testid="shared-games-card"`, no `connection-chip-strip`, and the rating readout is not `4.0` (it renders custom `★` glyphs).

- [ ] **Step 3: Convert the component body to an adapter**

Replace the entire contents of `apps/web/src/components/ui/shared-games/meeple-card-game.tsx` with:

```tsx
/**
 * MeepleCardGame — community shared-game tile for /shared-games index.
 *
 * Issue #2858 (C1): thin adapter over the canonical MeepleCard
 * (entity="game", variant="grid"). Previously a standalone renderer that
 * re-implemented cover/stars/badge inline; now composes MeepleCard and maps
 * the shared-games signals to canonical props. The public prop interface is
 * unchanged except `compact` (an unwired responsive knob) which was removed.
 *
 * Navigation: passes `href` so GridCard renders a real `<Link prefetch>` root
 * (public route needs prefetch + open-in-new-tab + native focus). The Wikidata
 * attribution footer is emitted by MeepleCard (entity=game) as a sibling of the
 * card root — no nested anchor.
 *
 * `labels.ratingAriaLabel` / `labels.newWeekAriaLabel` are retained on the
 * interface (to avoid churning /shared-games page-client) but are no longer
 * consumed by the canonical render; a follow-up may prune them.
 */
import type { JSX } from 'react';

import { MeepleCard } from '@/components/ui/data-display/meeple-card/MeepleCard';
import type { ConnectionChipProps } from '@/components/ui/data-display/meeple-card/types';

export interface MeepleCardGameLabels {
  /** Aria label prefix for the rating (retained for interface stability). */
  readonly ratingAriaLabel: string;
  /** Footer chip label for the toolkit count. */
  readonly toolkitLabel: string;
  /** Footer chip label for the agent count. */
  readonly agentLabel: string;
  /** Aria label fragment for the newWeek badge (retained for interface stability). */
  readonly newWeekAriaLabel: (count: number) => string;
}

export interface MeepleCardGameProps {
  readonly id: string;
  readonly title: string;
  /** Optional cover image; falls back to a tinted 🎲 emoji placeholder when absent. */
  readonly coverUrl?: string | null;
  /** Year published (rendered as the subtitle). */
  readonly year?: number | null;
  /** Average rating in 0..5 scale (already converted from backend 0..10). */
  readonly rating: number;
  readonly toolkitsCount: number;
  readonly agentsCount: number;
  readonly kbsCount: number;
  /** Count of children created this week (>=2 triggers the visible badge). */
  readonly newThisWeekCount: number;
  readonly labels: MeepleCardGameLabels;
  readonly className?: string;
  /**
   * Issue #2055 Phase 7 — Wikidata cover attribution fields. Forwarded to
   * MeepleCard, which renders MeepleCardAttributionFooter for entity=game.
   */
  readonly wikidataCoverLicense?: string | null;
  readonly wikidataCoverAttribution?: string | null;
  readonly wikidataCoverSourceUrl?: string | null;
}

export function MeepleCardGame({
  id,
  title,
  coverUrl,
  year,
  rating,
  toolkitsCount,
  agentsCount,
  kbsCount,
  newThisWeekCount,
  labels,
  className,
  wikidataCoverLicense = null,
  wikidataCoverAttribution = null,
  wikidataCoverSourceUrl = null,
}: MeepleCardGameProps): JSX.Element {
  const connections: ConnectionChipProps[] = [];
  if (toolkitsCount > 0) {
    connections.push({
      entityType: 'toolkit',
      count: toolkitsCount,
      label: labels.toolkitLabel,
      showLabel: true,
    });
  }
  if (agentsCount > 0) {
    connections.push({
      entityType: 'agent',
      count: agentsCount,
      label: labels.agentLabel,
      showLabel: true,
    });
  }
  if (kbsCount > 0) {
    connections.push({ entityType: 'kb', count: kbsCount, showLabel: false });
  }

  const badge = newThisWeekCount >= 2 ? `+${newThisWeekCount}` : undefined;

  return (
    <MeepleCard
      entity="game"
      variant="grid"
      href={`/shared-games/${id}`}
      title={title}
      subtitle={year != null ? String(year) : undefined}
      imageUrl={coverUrl ?? undefined}
      coverEmoji="🎲"
      rating={rating}
      ratingMax={5}
      badge={badge}
      connections={connections}
      className={className}
      data-testid="shared-games-card"
      wikidataCoverLicense={wikidataCoverLicense}
      wikidataCoverAttribution={wikidataCoverAttribution}
      wikidataCoverSourceUrl={wikidataCoverSourceUrl}
    />
  );
}
```

- [ ] **Step 4: Stop forwarding `compact` to the card in the grid**

In `apps/web/src/components/ui/shared-games/shared-games-grid.tsx`:

Change the `SharedGamesGridGame` type (currently line 36) from:

```ts
export type SharedGamesGridGame = Omit<MeepleCardGameProps, 'labels' | 'compact' | 'className'>;
```

to:

```ts
export type SharedGamesGridGame = Omit<MeepleCardGameProps, 'labels' | 'className'>;
```

Change the card render (currently line 95) from:

```tsx
        <MeepleCardGame key={game.id} {...game} labels={cardLabels} compact={compact} />
```

to:

```tsx
        <MeepleCardGame key={game.id} {...game} labels={cardLabels} />
```

(Leave `SharedGamesGrid`'s own `compact` prop and the `<SkeletonCard compact={compact} />` usage untouched — the skeleton still supports compact.)

- [ ] **Step 5: Update the grid test selectors**

In `apps/web/src/components/ui/shared-games/shared-games-grid.test.tsx`, replace the **3** occurrences of `[data-slot="shared-games-card"]` (currently lines 88, 125, 127) with `[data-testid="shared-games-card"]`. Do NOT touch the `[data-slot="shared-games-skeleton-card"]` selector on line 83 or the `[data-slot="shared-games-grid"]` selectors.

- [ ] **Step 6: Run the shared-games tests to verify they pass**

Run: `pnpm exec vitest run src/components/ui/shared-games`
Expected: PASS (`meeple-card-game.test.tsx` + `shared-games-grid.test.tsx` green).

- [ ] **Step 7: Typecheck**

Run: `pnpm typecheck`
Expected: PASS. (If `page-client.tsx` referenced `compact` on a game object it would fail here — it does not; `SharedGamesGridGame` never included a value for it.)

- [ ] **Step 8: Commit**

```bash
git add apps/web/src/components/ui/shared-games/meeple-card-game.tsx \
        apps/web/src/components/ui/shared-games/meeple-card-game.test.tsx \
        apps/web/src/components/ui/shared-games/shared-games-grid.tsx \
        apps/web/src/components/ui/shared-games/shared-games-grid.test.tsx
git commit -m "$(cat <<'EOF'
refactor(shared-games): MeepleCardGame becomes a MeepleCard adapter (#2858)

Eliminates the last standalone entity-card renderer. Maps rating/counts/
newThisWeek/cover onto canonical MeepleCard props; drops the unwired `compact`
knob. Public interface otherwise unchanged.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: ESLint import-boundary rule `local/no-standalone-card-renderer`

**Files:**
- Create: `apps/web/eslint-rules/no-standalone-card-renderer.js`
- Create: `apps/web/eslint-rules/no-standalone-card-renderer.test.js`
- Modify: `apps/web/eslint-rules/index.js` (export the rule)
- Modify: `apps/web/eslint.config.mjs` (import + register + severity `error`)

**Interfaces:**
- Produces: ESLint rule `local/no-standalone-card-renderer` at `error`. Bans value-imports of `meeple-card/parts/` and `meeple-card/variants/` from outside `ui/data-display/meeple-card/`. Exempts the canonical dir, test files, `import type`, and `PATH_ALLOWLIST` (currently `src/hooks/queries/useGameManaPips.ts`).

- [ ] **Step 1: Write the failing RuleTester test**

Create `apps/web/eslint-rules/no-standalone-card-renderer.test.js`:

```js
'use strict';

const test = require('node:test');
const { RuleTester } = require('eslint');
const tsParser = require('@typescript-eslint/parser');
const rule = require('./no-standalone-card-renderer.js');

const ruleTester = new RuleTester({
  languageOptions: {
    parser: tsParser,
    ecmaVersion: 2022,
    sourceType: 'module',
    parserOptions: { ecmaFeatures: { jsx: true } },
  },
});

test('no-standalone-card-renderer (import-boundary)', () => {
  ruleTester.run('no-standalone-card-renderer', rule, {
    valid: [
      // Composes the public MeepleCard — fine.
      {
        filename: 'apps/web/src/components/games/MeepleGameCard.tsx',
        code:
          "import { MeepleCard } from '@/components/ui/data-display/meeple-card';\n" +
          'export function MeepleGameCard() { return <MeepleCard entity="game" title="x" />; }',
      },
      // Inside the canonical dir — internals may reach into parts.
      {
        filename:
          'apps/web/src/components/ui/data-display/meeple-card/variants/GridCard.tsx',
        code:
          "import { Cover } from '../parts/Cover';\n" +
          'export function GridCard() { return <div><Cover /></div>; }',
      },
      // Test file — may import internals to test them.
      {
        filename: 'apps/web/src/__tests__/components/meeple-card/ManaPips.test.tsx',
        code:
          "import { ManaPips } from '@/components/ui/data-display/meeple-card/parts/ManaPips';\n" +
          'ManaPips;',
      },
      // Type-only import — carries no rendering logic.
      {
        filename: 'apps/web/src/hooks/queries/useSomething.ts',
        code:
          "import type { ManaPip } from '@/components/ui/data-display/meeple-card/parts/ManaPips';\n" +
          'const x = [] as ManaPip[];\nx;',
      },
      // Allowlisted value-util import.
      {
        filename: 'apps/web/src/hooks/queries/useGameManaPips.ts',
        code:
          "import { getKbPipColor } from '@/components/ui/data-display/meeple-card/parts/ManaPips';\n" +
          'getKbPipColor;',
      },
    ],
    invalid: [
      // Value deep-import of a part from outside the canonical dir.
      {
        filename: 'apps/web/src/components/games/RogueCard.tsx',
        code:
          "import { Cover } from '@/components/ui/data-display/meeple-card/parts/Cover';\n" +
          'export function RogueCard() { return <Cover />; }',
        errors: [{ messageId: 'deepImport' }],
      },
      // Value deep-import of a variant.
      {
        filename: 'apps/web/src/components/games/RogueCard2.tsx',
        code:
          "import { GridCard } from '@/components/ui/data-display/meeple-card/variants/GridCard';\n" +
          'export function RogueCard2() { return <GridCard entity="game" title="x" />; }',
        errors: [{ messageId: 'deepImport' }],
      },
    ],
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd apps/web && node --test eslint-rules/no-standalone-card-renderer.test.js`
Expected: FAIL — `Cannot find module './no-standalone-card-renderer.js'`.

- [ ] **Step 3: Implement the rule**

Create `apps/web/eslint-rules/no-standalone-card-renderer.js`:

```js
'use strict';

/**
 * ESLint Custom Rule: no-standalone-card-renderer (import-boundary)
 *
 * Issue #2858 (C1) — canonical card taxonomy guard.
 *
 * Forbids value-imports of meeple-card INTERNALS (parts/ and variants/) from
 * files outside ui/data-display/meeple-card/. Only the public `MeepleCard`
 * export (the dir root) is consumable, so a card cannot be re-assembled from
 * the atomic parts.
 *
 * NOT in this rule (by design):
 *   - name-based compose-check — infeasible: the codebase has 100+ generic
 *     `*Card` components unrelated to MeepleCard.
 *   - inline cover/stars/badge body-inspection — that is C4 (#2861).
 *
 * Exemptions:
 *   - files inside ui/data-display/meeple-card/ (the internals themselves)
 *   - test files (import internals to test them)
 *   - `import type` (type-only imports carry no rendering logic)
 *   - PATH_ALLOWLIST (audited value-util imports)
 */

const PART_IMPORT_RE = /\/meeple-card\/(?:parts|variants)\//;
const INSIDE_MEEPLE_CARD_RE = /\/ui\/data-display\/meeple-card\//;
const TEST_FILE_RE = /(?:\.test\.[jt]sx?$|\/__tests__\/)/;

// Audited value-util imports from parts/ that are NOT card re-assembly.
// Matched as a suffix of the (normalized) filename.
const PATH_ALLOWLIST = [
  // getKbPipColor: a pip-color utility that happens to live in parts/ManaPips.
  'src/hooks/queries/useGameManaPips.ts',
];

function normalize(p) {
  return p.replace(/\\/g, '/');
}

module.exports = {
  meta: {
    type: 'problem',
    docs: {
      description:
        'Forbid value-imports of meeple-card internals (parts/, variants/) from outside the canonical dir. Compose the public <MeepleCard> instead. Issue #2858 (C1).',
      category: 'Best Practices',
      recommended: false,
    },
    messages: {
      deepImport:
        'Do not value-import meeple-card internals ("{{source}}"). Compose the public <MeepleCard> instead; only ui/data-display/meeple-card/ may reach into parts/ and variants/. If this is an audited utility import, add the file to PATH_ALLOWLIST in eslint-rules/no-standalone-card-renderer.js. (#2858)',
    },
    schema: [],
  },

  create(context) {
    const filename = normalize(context.getFilename());

    if (INSIDE_MEEPLE_CARD_RE.test(filename)) return {};
    if (TEST_FILE_RE.test(filename)) return {};
    if (PATH_ALLOWLIST.some(suffix => filename.endsWith(suffix))) return {};

    return {
      ImportDeclaration(node) {
        // Skip whole `import type { ... } from '...'`.
        if (node.importKind === 'type') return;

        const source = node.source.value;
        if (typeof source !== 'string') return;
        if (!PART_IMPORT_RE.test(normalize(source))) return;

        // Skip if EVERY specifier is `import { type Foo }` (inline type-only).
        const hasValueSpecifier = node.specifiers.some(
          spec => spec.type !== 'ImportSpecifier' || spec.importKind !== 'type'
        );
        if (!hasValueSpecifier) return;

        context.report({ node, messageId: 'deepImport', data: { source } });
      },
    };
  },
};
```

- [ ] **Step 4: Run the rule test to verify it passes**

Run: `cd apps/web && node --test eslint-rules/no-standalone-card-renderer.test.js`
Expected: PASS (all valid + invalid cases).

- [ ] **Step 5: Export the rule from the manifest**

In `apps/web/eslint-rules/index.js`, add the require + export:

After `const preferUseGameTitle = require('./prefer-use-game-title.js');` add:

```js
const noStandaloneCardRenderer = require('./no-standalone-card-renderer.js');
```

And in the `module.exports.rules` object, after `'prefer-use-game-title': preferUseGameTitle,` add:

```js
    'no-standalone-card-renderer': noStandaloneCardRenderer,
```

- [ ] **Step 6: Register the rule in the flat config at `error`**

In `apps/web/eslint.config.mjs`:

After the import `import noStoreScoresDirect from "./eslint-rules/no-store-scores-direct.js";` (currently line 32), add:

```js
import noStandaloneCardRenderer from "./eslint-rules/no-standalone-card-renderer.js";
```

In the `local` plugin `rules` map, after `"prefer-use-game-title": preferUseGameTitle,` (currently line 125), add:

```js
          // Issue #2858 (C1) — no value-import of meeple-card internals outside the canonical dir.
          "no-standalone-card-renderer": noStandaloneCardRenderer,
```

In the `rules` section, after `"local/prefer-use-game-title": "warn",` (currently line 302), add:

```js
      // Issue #2858 (C1) — compose the public <MeepleCard>; do not re-assemble
      // cards from meeple-card/parts or /variants. Import-boundary only; the
      // inline-reimplementation body gate is C4 (#2861).
      "local/no-standalone-card-renderer": "error",
```

- [ ] **Step 7: Run full lint to verify zero violations**

Run: `cd apps/web && pnpm lint`
Expected: PASS with no `local/no-standalone-card-renderer` errors. (The only external deep-imports of parts/variants are: 2 test files — exempt; and `src/hooks/queries/useGameManaPips.ts` — allowlisted. If a NEW violation appears, it is a genuine finding: either refactor the importer to compose `<MeepleCard>`, or, if it is an audited utility import, add its path to `PATH_ALLOWLIST` with a reason.)

- [ ] **Step 8: Commit**

```bash
git add apps/web/eslint-rules/no-standalone-card-renderer.js \
        apps/web/eslint-rules/no-standalone-card-renderer.test.js \
        apps/web/eslint-rules/index.js \
        apps/web/eslint.config.mjs
git commit -m "$(cat <<'EOF'
feat(eslint): local/no-standalone-card-renderer import-boundary rule (#2858)

Bans value-imports of meeple-card/parts and /variants from outside the
canonical dir, at error. Type-only imports, tests, and the audited
useGameManaPips util import are exempt. Body-level reimplementation detection
is deferred to C4 (#2861).

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: Card decision-table doc + `<MeepleCard>`-usage coverage test

**Files:**
- Create: `docs/for-developers/frontend/card-decision-table.md`
- Test: `apps/web/src/__tests__/card-decision-table.test.ts` (create)

**Interfaces:**
- Consumes: the finished adapter set (Tasks 1-3). No code interface produced; this task produces the living-documentation gate.

- [ ] **Step 1: Write the coverage test (failing)**

Create `apps/web/src/__tests__/card-decision-table.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { sync as globSync } from 'glob';

// This test lives at apps/web/src/__tests__/ → up 2 to apps/web, up 4 to repo root.
const APPS_WEB = resolve(__dirname, '..', '..');
const REPO_ROOT = resolve(APPS_WEB, '..', '..');
const DOC_PATH = resolve(REPO_ROOT, 'docs/for-developers/frontend/card-decision-table.md');

// Exported components (function|const).
const EXPORT_RE = /export\s+(?:default\s+)?(?:function|const)\s+([A-Z][A-Za-z0-9]*)/g;
// Any exported symbol incl. types (used for the no-dangling existence check so a
// type mentioned in prose — e.g. `MeepleCardProps` — is not flagged).
const EXPORT_ANY_RE =
  /export\s+(?:default\s+)?(?:function|const|interface|type|class)\s+([A-Z][A-Za-z0-9]*)/g;
// `<MeepleCard` followed by whitespace, `/`, or `>` — excludes MeepleCardGame,
// MeepleCardAttributionFooter, MeepleCardSkeleton, etc.
const RENDERS_MEEPLE_CARD_RE = /<MeepleCard[\s/>]/;

function readDocNames(): Set<string> {
  const doc = readFileSync(DOC_PATH, 'utf8');
  return new Set([...doc.matchAll(/`([A-Z][A-Za-z0-9]*)`/g)].map(m => m[1]));
}

describe('card decision-table living documentation (#2858)', () => {
  it('every <MeepleCard>-rendering production file has an exported component in the decision-table', () => {
    const files = globSync('src/{app,components}/**/*.tsx', {
      cwd: APPS_WEB,
      ignore: [
        '**/__tests__/**',
        'src/app/(public)/dev/**',
        'src/components/**/dev/**',
        '**/showcase/**',
        // The dispatcher package renders the variants, not <MeepleCard>; excluded
        // for clarity so only true adapters are considered.
        'src/components/ui/data-display/meeple-card/**',
      ],
      absolute: true,
    });

    // Safeguard: a broken glob/cwd would make this pass vacuously.
    expect(files.length).toBeGreaterThan(50);

    const docNames = readDocNames();
    const undocumented: string[] = [];

    for (const file of files) {
      const src = readFileSync(file, 'utf8');
      if (!RENDERS_MEEPLE_CARD_RE.test(src)) continue;
      // Among files that render <MeepleCard>, a reusable ADAPTER exports a
      // component whose name contains "Card". Files that render <MeepleCard>
      // inline but export no Card-named component (pages/containers such as
      // DashboardClient, EntityListView, sessions/games pages) are inline
      // consumers, not reusable adapters — skip them.
      const cardExports = [...src.matchAll(EXPORT_RE)]
        .map(m => m[1])
        .filter(name => /Card/.test(name));
      if (cardExports.length === 0) continue;
      if (!cardExports.some(name => docNames.has(name))) {
        undocumented.push(
          `${file.replace(/\\/g, '/')} (card exports: ${cardExports.join(', ')})`
        );
      }
    }

    expect(
      undocumented,
      `These files render <MeepleCard> but no exported component is listed in ` +
        `docs/for-developers/frontend/card-decision-table.md. Add a row for each:\n` +
        undocumented.join('\n')
    ).toEqual([]);
  });

  it('every adapter named in the decision-table exists as an export', () => {
    const componentFiles = globSync('src/components/**/*.tsx', {
      cwd: APPS_WEB,
      ignore: ['**/__tests__/**'],
      absolute: true,
    });
    const allExports = new Set<string>();
    for (const file of componentFiles) {
      const src = readFileSync(file, 'utf8');
      for (const m of src.matchAll(EXPORT_ANY_RE)) allExports.add(m[1]);
    }

    const docNames = readDocNames();
    const dangling = [...docNames].filter(
      name =>
        /^(?:Meeple[A-Za-z0-9]*|[A-Za-z0-9]*ExtraMeepleCard)$/.test(name) &&
        !allExports.has(name)
    );

    expect(
      dangling,
      `The decision-table names these adapters but no matching export was found in ` +
        `src/components/**: ${dangling.join(', ')}`
    ).toEqual([]);
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `pnpm exec vitest run src/__tests__/card-decision-table.test.ts`
Expected: FAIL — the doc does not exist yet (`ENOENT`).

- [ ] **Step 3: Create the decision-table doc with the known rows**

Create `docs/for-developers/frontend/card-decision-table.md`:

```markdown
# Card decision-table — which component for which DTO × context

**Issue #2858 (C1).** Two canonical tiers. When you port a mockup that shows a
"card": **pick the tier from the context** (list/grid → DISPLAY; drawer/detail
page → DETAIL), **then the adapter from the DTO**. If no adapter exists, create
one that composes the canonical card — **never** a standalone renderer.

- **DISPLAY tier** = `MeepleCard` (`ui/data-display/meeple-card/`), 5 variants
  (`grid` · `list` · `compact` · `featured` · `hero`). Consumed via a
  DTO→`MeepleCardProps` adapter (mappers in `lib/card-mappers/`).
- **DETAIL tier** = `ExtraMeepleCard` (`ui/data-display/extra-meeple-card/`),
  the 600×900 tabbed drawer/detail card. Consumed via `ExtraMeepleCardDrawer`
  (cascade-navigation-store).

> This table is enforced by `apps/web/src/__tests__/card-decision-table.test.ts`:
> every production file that renders `<MeepleCard>` must have an exported
> component listed below (coverage), and every adapter named below must exist
> (no dangling rows). Keep it in sync — a missing row breaks the build.

## DISPLAY tier — MeepleCard adapters

| Context / route | Primary DTO | Entity | Adapter | Typical variant |
|---|---|---|---|---|
| `/shared-games` | shared tile | game | `MeepleCardGame` | grid |
| `/games?tab=discover`, `/games?tab=catalog`, `/games?tab=trending` | `SharedGame` | game | `MeepleGameCatalogCard` | grid · featured · hero |
| `/games` catalog (legacy Game API), dashboard recent | `Game` | game | `MeepleGameCard` | grid · compact |
| `/library?tab=games` (owned) | `UserLibraryEntry` | game | `MeepleUserLibraryCard` | grid |
| `/library?tab=games`, dashboard, home feed | `UserLibraryEntry` | game | `MeepleLibraryGameCard` | grid · compact · list |
| `/agents`, `/library?tab=agents`, `/hub/agents` | `AgentDto` / `AgentSummary` | agent | `MeepleAgentCard` | grid |
| `/library?tab=kb`, game detail KB list | `PdfDocumentDto` | kb | `MeepleKbCard` | grid |
| `/library?tab=sessions`, sessions grid | `GameSessionDto` | session | `MeepleSessionCard` | grid |
| `/library?tab=chat`, dashboard recent chats | `ChatSessionSummaryDto` | chat | `MeepleChatCard` | grid |
| `/dashboard#Prossimi`, `/dashboard#Recenti` | `GameNightSummary` | event | `MeepleEventCard` | list · compact |
| session participant lists | `SessionPlayer` | player | `MeeplePlayerCard` | compact |

## DETAIL tier — ExtraMeepleCard adapters (drawer / detail)

| Context | Primary DTO | Entity | Adapter |
|---|---|---|---|
| cascade drawer (game) | `GameDetailData` | game | `GameExtraMeepleCard` |
| cascade drawer (chat) | `ChatDetailData` | chat | `ChatExtraMeepleCard` |
| cascade drawer (kb) | `KbDetailData` | kb | `KbExtraMeepleCard` |
| admin shared-game detail | `SharedGameDetail` | game | `SharedGameExtraMeepleCard` |

## Rule of thumb

- Need a new list/grid card for entity X? Write `Meeple<X>Card` as an adapter
  that returns `<MeepleCard entity="x" … />`. Do **not** import from
  `meeple-card/parts/` or `meeple-card/variants/` (ESLint
  `local/no-standalone-card-renderer` forbids it) and do **not** hand-roll
  cover/stars/badge (C4 body-gate, #2861).
- Need a detail/drawer surface? Add an `ExtraMeepleCard` entity variant.
```

- [ ] **Step 4: Run the coverage test and reconcile any additional adapters**

Run: `pnpm exec vitest run src/__tests__/card-decision-table.test.ts`
Expected: The first test lists any `<MeepleCard>`-rendering file whose exports are not yet in the doc (there may be adapters beyond the seed list above — e.g. hub/wishlist/contributor cards that compose `MeepleCard`). For **each** file the test reports, add a row to the appropriate tier table in `card-decision-table.md` using the reported component name and its route/DTO (infer the context from the file path and the props it maps). Re-run until both tests are green.

Expected final: PASS (2 tests). If the second test reports a dangling name, fix the doc (typo) or confirm the export exists.

- [ ] **Step 5: Commit**

```bash
git add docs/for-developers/frontend/card-decision-table.md \
        apps/web/src/__tests__/card-decision-table.test.ts
git commit -m "$(cat <<'EOF'
docs(meeple-card): card decision-table + <MeepleCard>-usage coverage gate (#2858)

DTO x context -> tier + adapter map, enforced by a living-documentation test:
every production file rendering <MeepleCard> must appear in the table, and no
row may dangle. Name-independent (keys off <MeepleCard> usage, not the *Card
name).

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 5: Final verification + PR

**Files:** none (verification only).

- [ ] **Step 1: Full targeted test + quality gates**

Run each and confirm PASS:
- `cd apps/web && pnpm exec vitest run src/components/ui/data-display/meeple-card src/components/ui/shared-games src/__tests__/card-decision-table.test.ts`
- `cd apps/web && node --test eslint-rules/no-standalone-card-renderer.test.js`
- `cd apps/web && pnpm typecheck`
- `cd apps/web && pnpm lint`
- `cd apps/web && pnpm build`

- [ ] **Step 2: A11y spot-check on `/shared-games`**

The a11y E2E gate is BLOCKING. Confirm no new axe color-contrast/ARIA violations on `/shared-games` after the render switched to the canonical card (the canonical Rating/Cover/ConnectionChip are already used AA-clean across the app). If the CI a11y job flags a real regression, investigate — do not skip.

- [ ] **Step 3: Push + open PR to `main-dev`**

```bash
git push -u origin feature/issue-2858-canonical-card-decision-table
gh pr create --base main-dev --title "feat(meeple-card): canonical card taxonomy + decision-table + MeepleCardGame adapter (#2858)" --body "$(cat <<'EOF'
Closes #2858 (C1 / ST4 of umbrella #2863).

## What
- `MeepleCard` gains first-class `href` → `GridCard` renders a `<Link>` root (public `/shared-games` needs real anchor semantics).
- `MeepleCardGame` converted from a standalone renderer to a thin adapter over `MeepleCard` (canonical look + mapped signals; `compact` removed).
- `docs/for-developers/frontend/card-decision-table.md` (DTO×context → tier+adapter) + `<MeepleCard>`-usage coverage gate.
- ESLint `local/no-standalone-card-renderer` (import-boundary, `error`): no value-imports of meeple-card internals outside the canonical dir.

## Design
`docs/superpowers/specs/2026-07-13-issue-2858-canonical-card-design.md`. The name-based compose-check was dropped (100+ generic `*Card` components); the "don't re-implement cover/stars/badge inline" body-gate is C4 (#2861). Decision-table coverage keys off `<MeepleCard>` usage, not names.

## Verification
`pnpm test` (meeple-card + shared-games + decision-table), `node --test` (rule), `pnpm typecheck`, `pnpm lint`, `pnpm build` — all green.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 4: After CI green + merge — close-out**

Update issue #2858 (state + DoD) and tick its box in umbrella #2863's Phase C checklist. C4 (#2861) and C5 (#2862) become unblocked.

---

## Self-Review notes (author)

- **Spec coverage:** §3 taxonomy → Task 4 doc; §4 adapter + `href` → Tasks 1-2; §5 import-boundary rule → Task 3; §6 decision-table + coverage → Task 4; §7 TDD order → task order; §10 acceptance → Task 5 gates. All mapped.
- **Type consistency:** `href?: string` added in Task 1 is consumed verbatim in Task 2 (`MeepleCard … href={…}`). `ConnectionChipProps` fields (`entityType`/`count`/`label`/`showLabel`) match `types.ts`. `data-testid="shared-games-card"` set in Task 2, asserted in Task 2 tests, queried in updated `shared-games-grid.test.tsx`.
- **Ordering:** import-boundary rule (Task 3) does not touch `MeepleCardGame` (which imports the `MeepleCard` root, not parts), so Task 2/Task 3 order is not load-bearing; the sequence keeps CI green between commits.
- **Known deltas (accepted in brainstorm):** new-this-week badge loses `--c-event` rose (neutral canonical badge); cover fallback gains the canonical gradient/shimmer; rating loses the `role="img"` "Voto N di 5" label (canonical Rating readout, consistent with every other game card).
