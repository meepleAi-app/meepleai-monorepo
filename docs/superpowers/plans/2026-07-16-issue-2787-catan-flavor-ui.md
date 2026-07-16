# Catan flavor UI (#2787, G6a LIVE) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the Catan per-game LIVE flavor as a lazy-loaded, game-conditional tab in the session-live shell, mounted polymorphically by `gameSlug`, rendering only real `LiveSessionDto` data.

**Architecture:** A new ADR-070 Option B `FlavorRenderer` dispatcher lazy-loads (`next/dynamic`, `ssr:false`) a `CatanLiveFlavor` component keyed by `gameSlug`. The flavor is a pure, read-only component that themes the data already on `LiveSessionDto` (players with `color`/`totalScore`/`currentRank`/`isActive`, turn info, optional scoring dimensions). It mounts as a conditional `flavor` tab in both the desktop `RightColumnTabs` and the mobile `MobileBottomSheetDrawer`; games without a flavor see zero change.

**Tech Stack:** Next.js 16 (App Router), React 19, TypeScript, Tailwind 4, Zustand, TanStack Query, Vitest + Testing Library, Playwright + @axe-core/playwright.

## Global Constraints

- **No mock data** (RULES.md): render only fields present on `LiveSessionDto`. Board/dice/trades/resources are OMITTED, never faked.
- **Token discipline** (ESLint `local/no-hardcoded-color-utility`, error): no `bg-white`/`bg-slate-*`/`text-gray-*` etc. Dynamic per-player colors go through **inline `style={{ backgroundColor: 'hsl(...)' }}`** (not Tailwind classes). Static styling uses semantic tokens (`bg-card`, `text-foreground`, `border-border`) + entity utilities (`text-entity-session`).
- **i18n parity**: every new UI string added to BOTH `apps/web/src/locales/it.json` and `apps/web/src/locales/en.json` under identical keys. Components receive pre-resolved strings via `labels` props (pattern: `RightColumnTabs`, `ScoringPanelRenderer`).
- **a11y AA**: 0 axe violations; `role`/`aria-*` on interactive + live regions; `data-slot` attributes for test targeting.
- **Read-only**: the flavor never mutates state (the host editor stays in the `score` tab).
- **TypeScript**: `pnpm --dir apps/web typecheck` must pass. If a pre-commit fails on stale `.next/types/*` `TS2307`, run `rm -rf apps/web/.next/types` (never `--no-verify`).
- **Branch**: `feature/issue-2787-catan-flavor-ui` (parent `main-dev`, already created + spec committed).
- **Commands run from** `apps/web/` unless noted. Test runner: `pnpm exec vitest run <path>`.

## Key file locations (verified)

- Shell orchestrator: `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx`
  - `parseLiveTab` L204 · `parseMobileTab` L224 · `handleTabChange` L507 · `rightColumnTabsLabels` L909 · `mobileBodyLabels` L879 · `liveSessionDto = sessionQuery.data` L1083 · `mobileSheetContent` switch L1319 · `desktopRightColumn` L1522 · `MobileBody` render L1641.
- Desktop tabs: `apps/web/src/components/features/session-live/RightColumnTabs.tsx` (`LiveTab` union L37, static `ORDERED_TABS` L39, `useTablistKeyboardNav` L101).
- Mobile drawer: `.../MobileBottomSheetDrawer.tsx` (static `ORDERED_TABS` L66) · wrapper `.../MobileBody.tsx` (maps labels L126).
- Barrel: `.../session-live/index.ts`.
- Data types: `apps/web/src/lib/api/schemas/live-sessions.schemas.ts` (`LiveSessionDto`, `LiveSessionPlayerDto`, `PlayerColor`, `LiveSessionRoundScoreDto`).
- Fidelity: `admin-mockups/design_files/sp4-session-catan-live.fidelity.json`.

---

## Task 1: `catan-palette.ts` — PlayerColor → hsl mapping

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/catan/catan-palette.ts`
- Test: `apps/web/src/components/features/session-live/flavors/catan/__tests__/catan-palette.test.ts`

**Interfaces:**
- Produces: `catanPieceColor(color: PlayerColor | string): string` (returns an `hsl(...)` string; neutral fallback for unknown), and `CATAN_NEUTRAL_HSL` constant.

- [ ] **Step 1: Write the failing test**

```ts
// __tests__/catan-palette.test.ts
import { describe, expect, it } from 'vitest';

import type { PlayerColor } from '@/lib/api/schemas/live-sessions.schemas';

import { CATAN_NEUTRAL_HSL, catanPieceColor } from '../catan-palette';

const ALL_COLORS: PlayerColor[] = [
  'Red', 'Blue', 'Green', 'Yellow', 'Purple', 'Orange', 'White', 'Black', 'Pink', 'Teal',
];

describe('catanPieceColor', () => {
  it('returns a distinct hsl(...) string for every PlayerColor enum member', () => {
    const seen = new Set<string>();
    for (const c of ALL_COLORS) {
      const hsl = catanPieceColor(c);
      expect(hsl).toMatch(/^hsl\(/);
      seen.add(hsl);
    }
    expect(seen.size).toBe(ALL_COLORS.length); // all distinct
  });

  it('falls back to the neutral hsl for an unknown color', () => {
    expect(catanPieceColor('Chartreuse')).toBe(CATAN_NEUTRAL_HSL);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm --dir apps/web exec vitest run src/components/features/session-live/flavors/catan/__tests__/catan-palette.test.ts`
Expected: FAIL — `Cannot find module '../catan-palette'`.

- [ ] **Step 3: Write minimal implementation**

```ts
// catan-palette.ts
/**
 * Catan flavor palette — maps the session PlayerColor enum to display hsl
 * strings applied via inline style (token-lint safe; see plan Global Constraints).
 * Values are a Catan-leaning piece palette derived from the mockup terrain set.
 */
import type { PlayerColor } from '@/lib/api/schemas/live-sessions.schemas';

export const CATAN_NEUTRAL_HSL = 'hsl(0, 0%, 60%)';

const PALETTE: Record<PlayerColor, string> = {
  Red: 'hsl(0, 70%, 50%)',
  Blue: 'hsl(215, 70%, 50%)',
  Green: 'hsl(140, 55%, 42%)',
  Yellow: 'hsl(45, 85%, 50%)',
  Purple: 'hsl(270, 55%, 55%)',
  Orange: 'hsl(28, 85%, 52%)',
  White: 'hsl(0, 0%, 88%)',
  Black: 'hsl(0, 0%, 22%)',
  Pink: 'hsl(330, 75%, 62%)',
  Teal: 'hsl(175, 60%, 42%)',
};

export function catanPieceColor(color: PlayerColor | string): string {
  return PALETTE[color as PlayerColor] ?? CATAN_NEUTRAL_HSL;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm --dir apps/web exec vitest run src/components/features/session-live/flavors/catan/__tests__/catan-palette.test.ts`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/features/session-live/flavors/catan/catan-palette.ts apps/web/src/components/features/session-live/flavors/catan/__tests__/catan-palette.test.ts
git commit -m "feat(session-live): #2787 catan-palette PlayerColor→hsl mapping"
```

---

## Task 2: `CatanLiveFlavor.tsx` — pure themed LIVE view

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/catan/CatanLiveFlavor.tsx`
- Test: `apps/web/src/components/features/session-live/flavors/catan/__tests__/CatanLiveFlavor.test.tsx`

**Interfaces:**
- Consumes: `catanPieceColor` (Task 1); `LiveSessionDto` / `LiveSessionPlayerDto` / `LiveSessionRoundScoreDto` from schemas.
- Produces:
  - `interface CatanLiveFlavorLabels { panelAriaLabel; roundTemplate; activePlayerTemplate; leaderboardHeading; leaderBadgeLabel; scoreAriaTemplate; dimensionsHeading; emptyLabel; }` (all `string`; templates use `{n}` / `{name}` / `{score}` placeholders).
  - `interface CatanLiveFlavorProps { session: LiveSessionDto; labels: CatanLiveFlavorLabels; className?: string; }`
  - `function CatanLiveFlavor(props: CatanLiveFlavorProps): ReactElement`

**Render contract (real data only):**
1. Empty guard: `session.players.length === 0` → `role=status aria-live` empty label.
2. Turn header (`data-slot="catan-flavor-turn"`): `roundTemplate` with `{n}` = `currentTurnIndex + 1`; if a player matches `currentTurnPlayerId`, append `activePlayerTemplate` with `{name}`.
3. Leaderboard (`data-slot="catan-flavor-leaderboard"`): players sorted by `totalScore` desc; leader = `score === leadScore && idx === 0`. Each row: inline-style color swatch (`catanPieceColor(player.color)`), `displayName`, `totalScore`, leader crown (sr-only `leaderBadgeLabel`), `isActive` ring highlight.
4. Dimension breakdown (`data-slot="catan-flavor-dimensions"`): rendered **only if** `session.scoringConfig.enabledDimensions.length > 0` — per dimension, each player's summed `roundScores` value for that dimension. Omitted otherwise.

- [ ] **Step 1: Write the failing test**

```tsx
// __tests__/CatanLiveFlavor.test.tsx
import { render, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import type { LiveSessionDto } from '@/lib/api/schemas/live-sessions.schemas';

import { CatanLiveFlavor, type CatanLiveFlavorLabels } from '../CatanLiveFlavor';

const LABELS: CatanLiveFlavorLabels = {
  panelAriaLabel: 'Pannello Catan',
  roundTemplate: 'Round {n}',
  activePlayerTemplate: 'Turno di {name}',
  leaderboardHeading: 'Punti Vittoria',
  leaderBadgeLabel: 'In testa',
  scoreAriaTemplate: 'Punti di {name}: {score}',
  dimensionsHeading: 'Dettaglio punti',
  emptyLabel: 'In attesa dei dati della partita…',
};

function makeSession(over: Partial<LiveSessionDto> = {}): LiveSessionDto {
  const base: LiveSessionDto = {
    id: '11111111-1111-1111-1111-111111111111',
    sessionCode: 'S-CATAN',
    gameId: '22222222-2222-2222-2222-222222222222',
    gameName: 'Catan',
    gameSlug: 'catan',
    createdByUserId: '33333333-3333-3333-3333-333333333333',
    status: 'InProgress',
    visibility: 'Private',
    groupId: null,
    createdAt: '2026-07-16T10:00:00Z',
    startedAt: '2026-07-16T10:05:00Z',
    pausedAt: null,
    completedAt: null,
    updatedAt: '2026-07-16T10:30:00Z',
    lastSavedAt: null,
    currentTurnIndex: 3,
    currentTurnPlayerId: 'p2',
    agentMode: 'None',
    notes: null,
    players: [
      { id: 'p1', userId: null, displayName: 'Alice', avatarUrl: null, color: 'Red', role: 'Player', teamId: null, totalScore: 8, currentRank: 1, joinedAt: '2026-07-16T10:00:00Z', isActive: false },
      { id: 'p2', userId: null, displayName: 'Bruno', avatarUrl: null, color: 'Blue', role: 'Player', teamId: null, totalScore: 6, currentRank: 2, joinedAt: '2026-07-16T10:00:00Z', isActive: true },
    ],
    teams: [],
    roundScores: [],
    scoringConfig: { enabledDimensions: [], dimensionUnits: {} },
    ...over,
  };
  return base;
}

describe('CatanLiveFlavor', () => {
  it('renders the empty state when there are no players', () => {
    render(<CatanLiveFlavor session={makeSession({ players: [] })} labels={LABELS} />);
    expect(screen.getByText('In attesa dei dati della partita…')).toBeInTheDocument();
  });

  it('renders the turn header with round + active player', () => {
    render(<CatanLiveFlavor session={makeSession()} labels={LABELS} />);
    const header = screen.getByTestId('catan-flavor-turn');
    expect(header).toHaveTextContent('Round 4'); // currentTurnIndex 3 + 1
    expect(header).toHaveTextContent('Turno di Bruno'); // currentTurnPlayerId p2
  });

  it('renders the leaderboard sorted by score desc with the leader first', () => {
    render(<CatanLiveFlavor session={makeSession()} labels={LABELS} />);
    const rows = within(screen.getByTestId('catan-flavor-leaderboard')).getAllByRole('listitem');
    expect(rows).toHaveLength(2);
    expect(rows[0]).toHaveTextContent('Alice'); // 8 pts leads
    expect(rows[0]).toHaveTextContent('8');
    expect(rows[1]).toHaveTextContent('Bruno');
  });

  it('omits the dimensions section when no scoring dimensions are configured', () => {
    render(<CatanLiveFlavor session={makeSession()} labels={LABELS} />);
    expect(screen.queryByTestId('catan-flavor-dimensions')).not.toBeInTheDocument();
  });

  it('renders per-dimension breakdown from roundScores when dimensions are configured', () => {
    const session = makeSession({
      scoringConfig: { enabledDimensions: ['Città'], dimensionUnits: {} },
      roundScores: [
        { playerId: 'p1', round: 1, dimension: 'Città', value: 2, unit: null, recordedAt: '2026-07-16T10:10:00Z' },
        { playerId: 'p1', round: 2, dimension: 'Città', value: 2, unit: null, recordedAt: '2026-07-16T10:20:00Z' },
        { playerId: 'p2', round: 1, dimension: 'Città', value: 4, unit: null, recordedAt: '2026-07-16T10:20:00Z' },
      ],
    });
    render(<CatanLiveFlavor session={session} labels={LABELS} />);
    const dim = screen.getByTestId('catan-flavor-dimensions');
    expect(dim).toHaveTextContent('Città');
    expect(dim).toHaveTextContent('Alice'); // p1 summed 2+2 = 4
    const p1Cell = dim.querySelector('[data-player="p1"]'); // robust: avoids ambiguous "4"
    expect(p1Cell).not.toBeNull();
    expect(p1Cell).toHaveTextContent('4');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm --dir apps/web exec vitest run src/components/features/session-live/flavors/catan/__tests__/CatanLiveFlavor.test.tsx`
Expected: FAIL — `Cannot find module '../CatanLiveFlavor'`.

- [ ] **Step 3: Write minimal implementation**

```tsx
// CatanLiveFlavor.tsx
'use client';

import { type ReactElement } from 'react';

import type { LiveSessionDto } from '@/lib/api/schemas/live-sessions.schemas';

import { catanPieceColor } from './catan-palette';

export interface CatanLiveFlavorLabels {
  readonly panelAriaLabel: string;
  readonly roundTemplate: string; // "Round {n}"
  readonly activePlayerTemplate: string; // "Turno di {name}"
  readonly leaderboardHeading: string;
  readonly leaderBadgeLabel: string;
  readonly scoreAriaTemplate: string; // "Punti di {name}: {score}"
  readonly dimensionsHeading: string;
  readonly emptyLabel: string;
}

export interface CatanLiveFlavorProps {
  readonly session: LiveSessionDto;
  readonly labels: CatanLiveFlavorLabels;
  readonly className?: string;
}

function sumDimension(
  roundScores: LiveSessionDto['roundScores'],
  playerId: string,
  dimension: string
): number {
  return roundScores
    .filter(rs => rs.playerId === playerId && rs.dimension === dimension)
    .reduce((sum, rs) => sum + rs.value, 0);
}

export function CatanLiveFlavor({ session, labels, className }: CatanLiveFlavorProps): ReactElement {
  const { players, roundScores, scoringConfig, currentTurnIndex, currentTurnPlayerId } = session;

  if (players.length === 0) {
    return (
      <div
        role="status"
        aria-live="polite"
        data-slot="catan-flavor-empty"
        className={`${className ?? ''} text-xs text-muted-foreground`.trim()}
      >
        {labels.emptyLabel}
      </div>
    );
  }

  const sorted = [...players].sort((a, b) => b.totalScore - a.totalScore);
  const leadScore = sorted[0]?.totalScore;
  const activePlayer = players.find(p => p.id === currentTurnPlayerId) ?? null;
  const dimensions = scoringConfig.enabledDimensions;

  return (
    <section
      aria-label={labels.panelAriaLabel}
      data-slot="catan-flavor-live"
      className={`flex flex-col gap-4 ${className ?? ''}`.trim()}
    >
      {/* Turn / phase header */}
      <header
        data-slot="catan-flavor-turn"
        aria-live="polite"
        className="flex flex-col gap-0.5 rounded-lg border border-entity-session/25 bg-entity-session/8 px-3 py-2"
      >
        <span className="text-sm font-semibold text-foreground">
          {labels.roundTemplate.replace('{n}', String(currentTurnIndex + 1))}
        </span>
        {activePlayer && (
          <span className="text-xs text-muted-foreground">
            {labels.activePlayerTemplate.replace('{name}', activePlayer.displayName)}
          </span>
        )}
      </header>

      {/* Leaderboard */}
      <div data-slot="catan-flavor-leaderboard" className="flex flex-col gap-2">
        <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
          {labels.leaderboardHeading}
        </h3>
        <ul role="list" className="flex flex-col gap-1" aria-label={labels.leaderboardHeading}>
          {sorted.map((player, idx) => {
            const isLeader = player.totalScore === leadScore && idx === 0;
            const scoreAria = labels.scoreAriaTemplate
              .replace('{name}', player.displayName)
              .replace('{score}', String(player.totalScore));
            return (
              <li
                key={player.id}
                data-slot="catan-flavor-row"
                data-active={player.isActive ? 'true' : 'false'}
                className={[
                  'flex items-center gap-2 rounded-lg px-2 py-1.5',
                  player.isActive
                    ? 'border border-entity-session/40 bg-entity-session/10'
                    : 'border border-transparent bg-card',
                ].join(' ')}
              >
                <span
                  aria-hidden="true"
                  data-slot="catan-flavor-swatch"
                  className="h-3.5 w-3.5 shrink-0 rounded-full border border-border-strong"
                  style={{ backgroundColor: catanPieceColor(player.color) }}
                />
                <span className="min-w-0 flex-1 truncate text-xs font-medium text-foreground">
                  {player.displayName}
                  {isLeader && <span className="sr-only">, {labels.leaderBadgeLabel}</span>}
                  {isLeader && <span aria-hidden="true"> 👑</span>}
                </span>
                <span
                  aria-label={scoreAria}
                  className={[
                    'shrink-0 tabular-nums text-sm font-bold',
                    isLeader ? 'text-entity-session' : 'text-foreground',
                  ].join(' ')}
                >
                  {player.totalScore}
                </span>
              </li>
            );
          })}
        </ul>
      </div>

      {/* Optional per-dimension breakdown (real roundScores only) */}
      {dimensions.length > 0 && (
        <div data-slot="catan-flavor-dimensions" className="flex flex-col gap-2">
          <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            {labels.dimensionsHeading}
          </h3>
          <ul role="list" className="flex flex-col gap-1">
            {dimensions.map(dim => (
              <li key={dim} className="flex flex-col gap-0.5 rounded-lg bg-card px-2 py-1.5">
                <span className="text-xs font-medium text-foreground">{dim}</span>
                <span className="flex flex-wrap gap-x-3 gap-y-0.5 text-xs text-muted-foreground">
                  {players.map(p => (
                    <span key={p.id} className="tabular-nums">
                      {p.displayName}:{' '}
                      <span data-player={p.id} className="font-semibold text-foreground">
                        {sumDimension(roundScores, p.id, dim)}
                      </span>
                    </span>
                  ))}
                </span>
              </li>
            ))}
          </ul>
        </div>
      )}
    </section>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm --dir apps/web exec vitest run src/components/features/session-live/flavors/catan/__tests__/CatanLiveFlavor.test.tsx`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/features/session-live/flavors/catan/CatanLiveFlavor.tsx apps/web/src/components/features/session-live/flavors/catan/__tests__/CatanLiveFlavor.test.tsx
git commit -m "feat(session-live): #2787 CatanLiveFlavor read-only themed view"
```

---

## Task 3: `FlavorRenderer` dispatcher + `hasFlavor` + skeleton + barrel

**Files:**
- Create: `apps/web/src/components/features/session-live/FlavorLoadingSkeleton.tsx`
- Create: `apps/web/src/components/features/session-live/FlavorRenderer.tsx`
- Modify: `apps/web/src/components/features/session-live/index.ts` (barrel export)
- Test: `apps/web/src/components/features/session-live/__tests__/FlavorRenderer.test.tsx`

**Interfaces:**
- Consumes: `CatanLiveFlavor`, `CatanLiveFlavorLabels`, `CatanLiveFlavorProps` (Task 2).
- Produces:
  - `function hasFlavor(gameSlug: string | null | undefined): boolean`
  - `type FlavorView = 'live'` (only live now; union widens with G6a-2 summary)
  - `interface FlavorRendererProps { gameSlug: string | null | undefined; view: FlavorView; session: LiveSessionDto; labels: CatanLiveFlavorLabels; className?: string; }`
  - `function FlavorRenderer(props: FlavorRendererProps): ReactElement | null`

- [ ] **Step 1: Write the failing test**

```tsx
// __tests__/FlavorRenderer.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import type { LiveSessionDto } from '@/lib/api/schemas/live-sessions.schemas';

import type { CatanLiveFlavorLabels } from '../flavors/catan/CatanLiveFlavor';
import { FlavorRenderer, hasFlavor } from '../FlavorRenderer';

const LABELS: CatanLiveFlavorLabels = {
  panelAriaLabel: 'Pannello Catan',
  roundTemplate: 'Round {n}',
  activePlayerTemplate: 'Turno di {name}',
  leaderboardHeading: 'Punti Vittoria',
  leaderBadgeLabel: 'In testa',
  scoreAriaTemplate: 'Punti di {name}: {score}',
  dimensionsHeading: 'Dettaglio punti',
  emptyLabel: 'In attesa…',
};

const SESSION = {
  gameSlug: 'catan',
  currentTurnIndex: 0,
  currentTurnPlayerId: null,
  scoringConfig: { enabledDimensions: [], dimensionUnits: {} },
  roundScores: [],
  players: [
    { id: 'p1', userId: null, displayName: 'Alice', avatarUrl: null, color: 'Red', role: 'Player', teamId: null, totalScore: 5, currentRank: 1, joinedAt: '', isActive: false },
  ],
} as unknown as LiveSessionDto;

describe('hasFlavor', () => {
  it('is true for catan, false for unknown / null', () => {
    expect(hasFlavor('catan')).toBe(true);
    expect(hasFlavor('chess')).toBe(false);
    expect(hasFlavor(null)).toBe(false);
    expect(hasFlavor(undefined)).toBe(false);
  });
});

describe('FlavorRenderer', () => {
  it('returns null for a game without a flavor', () => {
    const { container } = render(
      <FlavorRenderer gameSlug="chess" view="live" session={SESSION} labels={LABELS} />
    );
    expect(container).toBeEmptyDOMElement();
  });

  it('lazy-loads and renders the Catan flavor for gameSlug=catan', async () => {
    render(<FlavorRenderer gameSlug="catan" view="live" session={SESSION} labels={LABELS} />);
    expect(await screen.findByTestId('catan-flavor-live')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm --dir apps/web exec vitest run src/components/features/session-live/__tests__/FlavorRenderer.test.tsx`
Expected: FAIL — `Cannot find module '../FlavorRenderer'`.

- [ ] **Step 3: Write minimal implementation**

```tsx
// FlavorLoadingSkeleton.tsx
import { type ReactElement } from 'react';

export function FlavorLoadingSkeleton(): ReactElement {
  return (
    <div
      role="status"
      aria-live="polite"
      data-slot="flavor-loading-skeleton"
      className="flex flex-col gap-2 p-3 animate-pulse"
    >
      <div className="h-10 rounded-lg bg-muted/40" />
      <div className="h-6 rounded-md bg-muted/40" />
      <div className="h-6 rounded-md bg-muted/40" />
    </div>
  );
}
```

```tsx
// FlavorRenderer.tsx
'use client';

import dynamic from 'next/dynamic';
import { type ComponentType, type ReactElement } from 'react';

import type { LiveSessionDto } from '@/lib/api/schemas/live-sessions.schemas';

import type { CatanLiveFlavorLabels, CatanLiveFlavorProps } from './flavors/catan/CatanLiveFlavor';
import { FlavorLoadingSkeleton } from './FlavorLoadingSkeleton';

export type FlavorView = 'live';

type FlavorComponent = ComponentType<CatanLiveFlavorProps>;

// Lazy chunks are created at MODULE scope — NEVER inside render (that would
// mint a new component identity every render → remount loop). The loader
// returns `{ default }` to match the codebase precedent (editor/page.tsx L35,
// KbGlobaleView.tsx L56) and satisfy next/dynamic's TS loader type.
const CatanLiveFlavorLazy: FlavorComponent = dynamic(
  () => import('./flavors/catan/CatanLiveFlavor').then(m => ({ default: m.CatanLiveFlavor })),
  { ssr: false, loading: () => <FlavorLoadingSkeleton /> }
);

/**
 * ADR-070 Option B — per-game flavor registry. Each value is a module-level
 * lazy component (content-hashed chunk fetched ONLY when that game's live
 * session is opened; verified by pnpm bundle:check). Summary entries arrive
 * with G6a-2; other 6 games with G6b–g.
 */
const FLAVOR_MAP: Record<string, Partial<Record<FlavorView, FlavorComponent>>> = {
  catan: { live: CatanLiveFlavorLazy },
};

export function hasFlavor(gameSlug: string | null | undefined): boolean {
  return gameSlug != null && FLAVOR_MAP[gameSlug]?.live != null;
}

export interface FlavorRendererProps {
  readonly gameSlug: string | null | undefined;
  readonly view: FlavorView;
  readonly session: LiveSessionDto;
  readonly labels: CatanLiveFlavorLabels;
  readonly className?: string;
}

export function FlavorRenderer({
  gameSlug,
  view,
  session,
  labels,
  className,
}: FlavorRendererProps): ReactElement | null {
  const LazyFlavor = gameSlug != null ? FLAVOR_MAP[gameSlug]?.[view] : undefined;
  if (LazyFlavor == null) return null;
  return <LazyFlavor session={session} labels={labels} className={className} />;
}
```

- [ ] **Step 4: Add barrel exports**

In `apps/web/src/components/features/session-live/index.ts`, append after the `RightColumnTabs` export block (around L144):

```ts
// ─── G6a #2787 Catan flavor (ADR-070 lazy per-game modules) ───────────────────
export { FlavorRenderer, hasFlavor } from '@/components/features/session-live/FlavorRenderer';
export type {
  FlavorRendererProps,
  FlavorView,
} from '@/components/features/session-live/FlavorRenderer';
export type {
  CatanLiveFlavorLabels,
  CatanLiveFlavorProps,
} from '@/components/features/session-live/flavors/catan/CatanLiveFlavor';
```

- [ ] **Step 5: Run test to verify it passes**

Run: `pnpm --dir apps/web exec vitest run src/components/features/session-live/__tests__/FlavorRenderer.test.tsx`
Expected: PASS (3 tests). If the async `findByTestId` flakes on the dynamic import, keep `await` — `next/dynamic` resolves the promise in jsdom.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/features/session-live/FlavorRenderer.tsx apps/web/src/components/features/session-live/FlavorLoadingSkeleton.tsx apps/web/src/components/features/session-live/index.ts apps/web/src/components/features/session-live/__tests__/FlavorRenderer.test.tsx
git commit -m "feat(session-live): #2787 FlavorRenderer ADR-070 lazy dispatcher + hasFlavor"
```

---

## Task 4: `RightColumnTabs` — conditional `flavor` tab (desktop)

**Files:**
- Modify: `apps/web/src/components/features/session-live/RightColumnTabs.tsx`
- Test: `apps/web/src/components/features/session-live/__tests__/RightColumnTabs.test.tsx` (extend)

**Interfaces:**
- Produces (changed): `LiveTab` union += `'flavor'`; `RightColumnTabsLabels` += `tabFlavor: string`; `RightColumnTabsProps` += `showFlavorTab?: boolean` (default `false`).
- Behaviour: when `showFlavorTab`, tab order is `['flavor', 'score', 'turn', 'widget', 'notes', 'photos', 'agent']`; otherwise unchanged. Keyboard nav (`useTablistKeyboardNav`) uses the computed order.

- [ ] **Step 1: Write the failing test** (append to the existing file's fixtures + a new describe block)

```tsx
// add tabFlavor to the LABELS fixture (top of file):
//   tabFlavor: 'Catan',

describe('RightColumnTabs — conditional flavor tab (#2787)', () => {
  it('does NOT render the flavor tab by default', () => {
    renderTabs();
    expect(screen.queryByRole('tab', { name: 'Catan' })).not.toBeInTheDocument();
  });

  it('renders the flavor tab FIRST when showFlavorTab is true', () => {
    renderTabs({ showFlavorTab: true, activeTab: 'flavor' });
    const tabs = screen.getAllByRole('tab');
    expect(tabs[0]).toHaveTextContent('Catan');
    expect(screen.getByRole('tab', { name: 'Catan' })).toHaveAttribute('aria-selected', 'true');
  });

  it('fires onTabChange when the flavor tab is clicked', async () => {
    const user = userEvent.setup();
    const { onTabChange } = renderTabs({ showFlavorTab: true });
    await user.click(screen.getByRole('tab', { name: 'Catan' }));
    expect(onTabChange).toHaveBeenCalledWith('flavor');
  });
});
```

(Update the top-of-file `LABELS` fixture to include `tabFlavor: 'Catan'`.)

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm --dir apps/web exec vitest run src/components/features/session-live/__tests__/RightColumnTabs.test.tsx`
Expected: FAIL — `showFlavorTab` not a prop / flavor tab not rendered.

- [ ] **Step 3: Write minimal implementation** (edit `RightColumnTabs.tsx`)

Replace the `LiveTab` type (L37):
```ts
export type LiveTab = 'flavor' | 'score' | 'turn' | 'widget' | 'notes' | 'photos' | 'agent';
```

Replace the static `ORDERED_TABS` (L39-46) with a base + helper:
```ts
const BASE_TABS: ReadonlyArray<LiveTab> = ['score', 'turn', 'widget', 'notes', 'photos', 'agent'];
```

Add `tabFlavor` to `RightColumnTabsLabels` (after `tabsAriaLabel`):
```ts
  readonly tabFlavor: string;
```

Add `showFlavorTab` to `RightColumnTabsProps`:
```ts
  readonly showFlavorTab?: boolean;
```

In the component body, before `useTablistKeyboardNav`, compute the order and labels:
```ts
  const orderedTabs = useMemo<ReadonlyArray<LiveTab>>(
    () => (showFlavorTab ? ['flavor', ...BASE_TABS] : BASE_TABS),
    [showFlavorTab]
  );

  const tabLabels: Record<LiveTab, string> = useMemo(
    () => ({
      flavor: labels.tabFlavor,
      score: labels.tabScore,
      turn: labels.tabTurn,
      widget: labels.tabWidget,
      notes: labels.tabNotes,
      photos: labels.tabPhotos,
      agent: labels.tabAgent,
    }),
    [
      labels.tabFlavor,
      labels.tabScore,
      labels.tabTurn,
      labels.tabWidget,
      labels.tabNotes,
      labels.tabPhotos,
      labels.tabAgent,
    ]
  );
```
(Replace the old `tabLabels` memo; keep granular per-key deps as in the original — `labels` is memoized upstream, but granular deps preserve the original contract and avoid any refocus risk.)

Point keyboard nav + render at `orderedTabs`:
```ts
  const { tabRefs, handleKeyDown } = useTablistKeyboardNav<LiveTab>({
    orderedKeys: orderedTabs,
    onChange: onTabChange,
    orientation: 'horizontal',
  });
```
and change `{ORDERED_TABS.map(...)}` → `{orderedTabs.map(...)}`.

Add `showFlavorTab = false` to the destructured props with a default.

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm --dir apps/web exec vitest run src/components/features/session-live/__tests__/RightColumnTabs.test.tsx`
Expected: PASS (existing tests + 3 new).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/features/session-live/RightColumnTabs.tsx apps/web/src/components/features/session-live/__tests__/RightColumnTabs.test.tsx
git commit -m "feat(session-live): #2787 RightColumnTabs conditional flavor tab"
```

---

## Task 5: `MobileBottomSheetDrawer` + `MobileBody` — flavor tab (mobile)

> **Independently deferrable:** this task adds mobile parity for the flavor tab. It can be reviewed/dropped without affecting desktop (Tasks 3–4, 6). If dropped, the flavor is desktop-only for the pilot.

**Files:**
- Modify: `apps/web/src/components/features/session-live/MobileBottomSheetDrawer.tsx`
- Modify: `apps/web/src/components/features/session-live/MobileBody.tsx`
- Test: `apps/web/src/components/features/session-live/__tests__/MobileBottomSheetDrawer.test.tsx` (create or extend)

**Interfaces:**
- `MobileBottomSheetDrawerLabels` += `tabFlavor: string`; `MobileBottomSheetDrawerProps` += `showFlavorTab?: boolean`.
- `MobileBodyLabels` += `tabFlavor: string`; `MobileBodyProps` += `showFlavorTab?: boolean`.

- [ ] **Step 1: Write the failing test**

```tsx
// __tests__/MobileBottomSheetDrawer.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { MobileBottomSheetDrawer, type MobileBottomSheetDrawerLabels } from '../MobileBottomSheetDrawer';

const LABELS: MobileBottomSheetDrawerLabels = {
  drawerTitle: 'Pannello',
  closeAriaLabel: 'Chiudi',
  tabsAriaLabel: 'Tab',
  tabScore: 'Score',
  tabTurn: 'Turni',
  tabWidget: 'Widget',
  tabNotes: 'Note',
  tabPhotos: 'Foto',
  tabAgent: 'Arbitro',
  tabFlavor: 'Catan',
};

describe('MobileBottomSheetDrawer — flavor tab (#2787)', () => {
  it('omits the flavor tab by default', () => {
    render(
      <MobileBottomSheetDrawer open onOpenChange={vi.fn()} activeTab="score" onTabChange={vi.fn()} labels={LABELS}>
        <div />
      </MobileBottomSheetDrawer>
    );
    expect(screen.queryByRole('tab', { name: 'Catan' })).not.toBeInTheDocument();
  });

  it('renders the flavor tab first when showFlavorTab is set', () => {
    render(
      <MobileBottomSheetDrawer open showFlavorTab onOpenChange={vi.fn()} activeTab="flavor" onTabChange={vi.fn()} labels={LABELS}>
        <div />
      </MobileBottomSheetDrawer>
    );
    const tabs = screen.getAllByRole('tab');
    expect(tabs[0]).toHaveTextContent('Catan');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm --dir apps/web exec vitest run src/components/features/session-live/__tests__/MobileBottomSheetDrawer.test.tsx`
Expected: FAIL — `showFlavorTab` / `tabFlavor` unknown.

- [ ] **Step 3: Write minimal implementation**

In `MobileBottomSheetDrawer.tsx`:
- Add `readonly tabFlavor: string;` to `MobileBottomSheetDrawerLabels` (after `tabsAriaLabel`).
- Add `readonly showFlavorTab?: boolean;` to `MobileBottomSheetDrawerProps`.
- Replace the static `ORDERED_TABS` with a base + computed list:
```ts
const BASE_TABS: ReadonlyArray<{ id: LiveTab; labelKey: keyof MobileBottomSheetDrawerLabels }> = [
  { id: 'score', labelKey: 'tabScore' },
  { id: 'turn', labelKey: 'tabTurn' },
  { id: 'widget', labelKey: 'tabWidget' },
  { id: 'notes', labelKey: 'tabNotes' },
  { id: 'photos', labelKey: 'tabPhotos' },
  { id: 'agent', labelKey: 'tabAgent' },
];
const FLAVOR_TAB = { id: 'flavor' as LiveTab, labelKey: 'tabFlavor' as const };
```
- Destructure `showFlavorTab = false` in the component; compute `const orderedTabs = showFlavorTab ? [FLAVOR_TAB, ...BASE_TABS] : BASE_TABS;` and map over `orderedTabs` instead of `ORDERED_TABS`.

In `MobileBody.tsx`:
- Add `readonly tabFlavor: string;` to `MobileBodyLabels`.
- Add `readonly showFlavorTab?: boolean;` to `MobileBodyProps`; destructure with default `false`.
- Pass through to the drawer: add `showFlavorTab={showFlavorTab}` and `tabFlavor: labels.tabFlavor,` inside the `labels={{ ... }}` object.

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm --dir apps/web exec vitest run src/components/features/session-live/__tests__/MobileBottomSheetDrawer.test.tsx`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/features/session-live/MobileBottomSheetDrawer.tsx apps/web/src/components/features/session-live/MobileBody.tsx apps/web/src/components/features/session-live/__tests__/MobileBottomSheetDrawer.test.tsx
git commit -m "feat(session-live): #2787 mobile bottom-sheet flavor tab"
```

---

## Task 6: Wire `SessionLiveView` (desktop + mobile) + i18n

**Files:**
- Modify: `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx`
- Modify: `apps/web/src/locales/it.json`, `apps/web/src/locales/en.json`
- Test: `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/__tests__/SessionLiveView.test.tsx` (extend)

**Interfaces:**
- Consumes: `FlavorRenderer`, `hasFlavor`, `CatanLiveFlavorLabels` (barrel).

- [ ] **Step 1: Add i18n keys** (both files, identical keys)

`it.json` — under `pages.sessionLive.rightColumn` add:
```json
"tabFlavor": "Catan",
```
`it.json` — add a `pages.sessionLive.flavor` block:
```json
"flavor": {
  "catan": {
    "panelAriaLabel": "Pannello Catan",
    "roundTemplate": "Round {n}",
    "activePlayerTemplate": "Turno di {name}",
    "leaderboardHeading": "Punti Vittoria",
    "leaderBadgeLabel": "In testa",
    "scoreAriaTemplate": "Punti di {name}: {score}",
    "dimensionsHeading": "Dettaglio punti",
    "emptyLabel": "In attesa dei dati della partita…"
  }
}
```
`en.json` — mirror with English values (`"tabFlavor": "Catan"`, `"roundTemplate": "Round {n}"`, `"activePlayerTemplate": "{name}'s turn"`, `"leaderboardHeading": "Victory Points"`, `"leaderBadgeLabel": "Leading"`, `"scoreAriaTemplate": "{name}'s points: {score}"`, `"dimensionsHeading": "Points breakdown"`, `"emptyLabel": "Waiting for game data…"`).
Also add `"tabFlavor"` under whatever `pages.sessionLive.mobile.*` namespace supplies `mobileBodyLabels` tab strings (verify the exact keys used at `SessionLiveView.tsx:879` — the mobile drawer tab labels reuse `rightColumn.tab*` today; reuse `rightColumn.tabFlavor` for the mobile label too).

- [ ] **Step 2: Write the failing test** (extend SessionLiveView.test.tsx)

Add a test asserting that a Catan session renders the desktop flavor tab. Follow the file's existing fixture/mock setup (it mocks `useLiveSession` via `vi.hoisted`). Minimal new test:

```tsx
it('#2787 shows the Catan flavor tab for a catan session', async () => {
  // Arrange the existing test harness so useLiveSession returns gameSlug 'catan'
  // (reuse the file's helper that seeds sessionQuery.data; set gameSlug: 'catan').
  renderSessionLiveView({ gameSlug: 'catan' }); // adapt to the file's actual render helper
  expect(await screen.findByRole('tab', { name: 'Catan' })).toBeInTheDocument();
});

it('#2787 hides the flavor tab for a non-catan session', async () => {
  renderSessionLiveView({ gameSlug: 'chess' });
  expect(await screen.findByRole('tab', { name: 'Score' })).toBeInTheDocument();
  expect(screen.queryByRole('tab', { name: 'Catan' })).not.toBeInTheDocument();
});
```
> Adapt `renderSessionLiveView` + the `gameSlug` seam to the existing helpers in the file. If the file lacks a gameSlug-parameterised helper, extend the existing `useLiveSession` mock fixture to accept a `gameSlug` override.

- [ ] **Step 3: Run test to verify it fails**

Run: `pnpm --dir apps/web exec vitest run "src/app/(authenticated)/sessions/[id]/live/_components/__tests__/SessionLiveView.test.tsx"`
Expected: FAIL — no `Catan` tab.

- [ ] **Step 4: Wire the shell** (edit `SessionLiveView.tsx`)

1. Add `'flavor'` recognition to `parseLiveTab` (L204) and `parseMobileTab` (L224):
```ts
  if (raw === 'flavor') return 'flavor';
```
(place before the alias fallbacks; keep `return 'score'` default).

2. Import from the barrel (top imports region ~L100-110), add:
```ts
  FlavorRenderer,
  hasFlavor,
```
to the existing `@/components/features/session-live` import, plus `type CatanLiveFlavorLabels,`.

3. Add `tabFlavor` to `rightColumnTabsLabels` (L909 memo):
```ts
      tabFlavor: t('pages.sessionLive.rightColumn.tabFlavor'),
```
and to `mobileBodyLabels` (L879 memo):
```ts
      tabFlavor: t('pages.sessionLive.rightColumn.tabFlavor'),
```

4. After `const liveSessionDto = sessionQuery.data;` (L1083), add flavor gating + labels:
```ts
  const showFlavorTab = hasFlavor(liveSessionDto?.gameSlug);
  // Placeholder-bearing templates ({n}/{name}/{score}) are read RAW from
  // intl.messages so react-intl does NOT try to ICU-interpolate them — the
  // component does the runtime .replace. This is the PRESCRIBED approach (not
  // optional): it mirrors the toolkitRenderer aria templates at L933. Plain
  // labels (no placeholders) use t() normally.
  const catanFlavorLabels = useMemo<CatanLiveFlavorLabels>(
    () => ({
      panelAriaLabel: t('pages.sessionLive.flavor.catan.panelAriaLabel'),
      roundTemplate:
        (intl.messages['pages.sessionLive.flavor.catan.roundTemplate'] as string) ?? 'Round {n}',
      activePlayerTemplate:
        (intl.messages['pages.sessionLive.flavor.catan.activePlayerTemplate'] as string) ??
        'Turno di {name}',
      leaderboardHeading: t('pages.sessionLive.flavor.catan.leaderboardHeading'),
      leaderBadgeLabel: t('pages.sessionLive.flavor.catan.leaderBadgeLabel'),
      scoreAriaTemplate:
        (intl.messages['pages.sessionLive.flavor.catan.scoreAriaTemplate'] as string) ??
        'Punti di {name}: {score}',
      dimensionsHeading: t('pages.sessionLive.flavor.catan.dimensionsHeading'),
      emptyLabel: t('pages.sessionLive.flavor.catan.emptyLabel'),
    }),
    [t, intl.messages]
  );
```
> `intl` is already in scope in `SessionLiveView` (used for the toolkitRenderer aria templates at L933). Templates keep their `{n}`/`{name}`/`{score}` tokens intact for the component's runtime `.replace`.

5. Desktop mount — pass `showFlavorTab` to `RightColumnTabs` (L1523) and add the flavor branch as the FIRST child:
```tsx
    <RightColumnTabs activeTab={tab} onTabChange={handleTabChange} labels={rightColumnTabsLabels} showFlavorTab={showFlavorTab}>
      {tab === 'flavor' && liveSessionDto != null && (
        <FlavorRenderer
          gameSlug={liveSessionDto.gameSlug}
          view="live"
          session={liveSessionDto}
          labels={catanFlavorLabels}
          className="p-3"
        />
      )}
      {tab === 'score' && ( /* …unchanged… */ )}
```

6. Mobile mount — add the flavor branch to `mobileSheetContent` switch (L1319) and pass `showFlavorTab` to `MobileBody` (L1641):
```tsx
      case 'flavor':
        return liveSessionDto != null ? (
          <FlavorRenderer
            gameSlug={liveSessionDto.gameSlug}
            view="live"
            session={liveSessionDto}
            labels={catanFlavorLabels}
            className="p-3"
          />
        ) : null;
```
and on `<MobileBody … />`: add `showFlavorTab={showFlavorTab}`.
> Add `liveSessionDto` and `showFlavorTab`/`catanFlavorLabels` to the `mobileSheetContent` `useMemo` dependency array.

- [ ] **Step 5: Run test + typecheck**

Run: `pnpm --dir apps/web exec vitest run "src/app/(authenticated)/sessions/[id]/live/_components/__tests__/SessionLiveView.test.tsx"`
Expected: PASS.
Run: `rm -rf apps/web/.next/types && pnpm --dir apps/web typecheck`
Expected: no errors.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx apps/web/src/locales/it.json apps/web/src/locales/en.json apps/web/src/app/(authenticated)/sessions/[id]/live/_components/__tests__/SessionLiveView.test.tsx
git commit -m "feat(session-live): #2787 wire Catan flavor tab into live shell + i18n"
```

---

## Task 7: fidelity.json + reconciliation note

**Files:**
- Modify: `admin-mockups/design_files/sp4-session-catan-live.fidelity.json`

- [ ] **Step 1: Flip design_intent + wire refs**

Set `"design_intent": "forward-refactor"` (was `"deferred"`) and drop the `obsolete_tracking_issue` (or repoint to `""`), since the LIVE flavor is now implemented. Leave `sp4-session-catan-summary.fidelity.json` untouched (`deferred`, summary = G6a-2). Add a `_comment` note: `"LIVE implemented in #2787 (forward-refactor MVP: board/dice/trades omitted). Supersedes deferred DS-17 #2234 for Catan LIVE."`

- [ ] **Step 2: Validate**

Run: `pnpm --dir apps/web lint:fidelity`
Expected: PASS (accepts `forward-refactor`). If the script is defined at repo root, run `pnpm lint:fidelity` from root instead.

- [ ] **Step 3: Commit**

```bash
git add admin-mockups/design_files/sp4-session-catan-live.fidelity.json
git commit -m "docs(fidelity): #2787 Catan live flavor forward-refactor + #2234 reconciliation"
```

---

## Task 8: E2E skeleton + axe AA

**Files:**
- Create: `apps/web/e2e/session-live-catan-flavor.smoke.spec.ts`
- Create: `apps/web/e2e/a11y/session-live-catan-flavor.spec.ts`

- [ ] **Step 1: Write the E2E skeleton**

Mirror `apps/web/e2e/session-live.smoke.spec.ts` (triple helper `seedAuthSession()` + `seedCookieConsent()` + `mockAuthEndpoints()`, `?fixture=host`, `data-slot` waits). Assert: for a Catan session fixture, `[role="tab"]` named "Catan" is visible and clicking it reveals `[data-slot="catan-flavor-live"]`. If no Catan host-fixture exists, add one keyed on `gameSlug='catan'` following the visual-test-fixture pattern, and `test.fixme()` the assertion with a comment linking #2787 if the fixture infra can't carry a rich `LiveSessionDto` yet (document the gap in `log()` / a comment — never silently skip).

- [ ] **Step 2: Write the axe AA spec**

Mirror `apps/web/e2e/a11y/game-detail.spec.ts`: `AxeBuilder` with `['wcag2a','wcag2aa','wcag21a','wcag21aa']`, scan the flavor tab panel (`data-slot="catan-flavor-live"`), assert 0 violations.

- [ ] **Step 3: Run (best-effort locally) + commit**

Run: `pnpm --dir apps/web exec playwright test e2e/session-live-catan-flavor.smoke.spec.ts` (may require the dev server; if unavailable locally, rely on CI — commit the spec).
```bash
git add apps/web/e2e/session-live-catan-flavor.smoke.spec.ts apps/web/e2e/a11y/session-live-catan-flavor.spec.ts
git commit -m "test(session-live): #2787 Catan flavor E2E smoke + axe AA skeleton"
```

---

## Task 9: Bundle budget + full verification + issue update

**Files:** none new (verification + tracking).

- [ ] **Step 1: Verify lazy bundle**

Run: `pnpm --dir apps/web build` then `pnpm --dir apps/web bundle:check`
Expected: `/sessions/[id]/live` route budget not exceeded; the Catan flavor chunk is separate (lazy). If the route budget grows because the flavor leaked into the main bundle, confirm `FlavorRenderer` uses `next/dynamic` (not a static import) and that `SessionLiveView` imports `FlavorRenderer` (which is static — fine) but NOT `CatanLiveFlavor` directly (must stay lazy). Add a `note` entry to `.bundle-budgets.json` only if a small justified bump is needed.

- [ ] **Step 2: Full quality gate**

Run:
```
rm -rf apps/web/.next/types
pnpm --dir apps/web typecheck
pnpm --dir apps/web lint
pnpm --dir apps/web exec vitest run src/components/features/session-live "src/app/(authenticated)/sessions/[id]/live"
```
Expected: all green. Fix any `local/no-hardcoded-color-utility` hits in the flavor by moving colors to inline `style`.

- [ ] **Step 3: Push + open PR to `main-dev`**

```bash
git push -u origin feature/issue-2787-catan-flavor-ui
gh pr create --base main-dev --title "feat(session-live): #2787 Catan flavor UI (G6a, LIVE)" --body "<summary + DoD mapping + 'summary deferred to G6a-2' + #2234 reconciliation note>"
```

- [ ] **Step 4: Update issue tracking**

Comment on #2787: LIVE shipped, SUMMARY deferred to a new G6a-2 sub-issue (open it, link it). Note in #2377 that G6a LIVE is done. Leave #2234 link/close to the maintainer (per governance decision).

---

## Self-Review

**Spec coverage:**
- FlavorRenderer lazy dispatcher (ADR-070 B) → Task 3 ✅
- CatanLiveFlavor themed live view → Task 2 ✅
- gameSlug dispatch + graceful null fallback → Task 3 (`hasFlavor`/null) ✅
- Conditional flavor tab (desktop) → Task 4 ✅; (mobile) → Task 5 ✅
- Read-only, real data only → Tasks 2/6 (session.players/roundScores; no store mutation) ✅
- Palette token discipline → Task 1 (inline hsl) ✅
- i18n parity → Task 6 ✅
- fidelity.json + #2234 reconciliation → Task 7 ✅
- E2E + axe AA → Task 8 ✅
- Bundle budget lazy verification → Task 9 ✅
- Summary deferred → Task 9 step 4 (G6a-2) ✅

**Placeholder scan:** Task 6 step 4 (i18n `t()` interpolation) + Task 8 (fixture availability) carry explicit *verify-against-the-file* notes, not silent TODOs — the implementer must confirm the exact seam. Task 6's SessionLiveView test uses `renderSessionLiveView` as a stand-in for the file's real helper (the file has an established harness; adapt to it). These are intentional adapt-points, flagged inline.

**Type consistency:** `CatanLiveFlavorLabels`/`CatanLiveFlavorProps` defined in Task 2, re-exported in Task 3, consumed in Task 6 — names consistent. `LiveTab` gains `'flavor'` in Task 4; `RightColumnTabsLabels.tabFlavor` + `MobileBottomSheetDrawerLabels.tabFlavor` + `MobileBodyLabels.tabFlavor` all added. `hasFlavor` signature stable across Tasks 3/6. `FlavorRenderer` props (`gameSlug/view/session/labels/className`) consistent between Task 3 definition and Task 6 usage.

## Risks / adapt-points (implementer must confirm against the live file)

1. **`t()` interpolation** — RESOLVED (plan review): template keys (`roundTemplate`/`activePlayerTemplate`/`scoreAriaTemplate`) are read via `intl.messages['key'] as string` (raw, no ICU interpolation), per Task 6. Only confirm `intl` is the in-scope variable name in `SessionLiveView` (it is, at L933).
2. **SessionLiveView test harness** — adapt the new tests to the file's real render helper + `useLiveSession` mock (parameterise `gameSlug`).
3. **E2E fixture** — a rich Catan `LiveSessionDto` host-fixture may not exist; add one or `test.fixme()` with an explicit logged gap (never silent-skip).
4. **`lint:fidelity` location** — root vs `apps/web` script; run where defined.
