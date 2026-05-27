# Library Hybrid Hub — Phase 3a Drawer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build `AdvancedFiltersDrawer` as a **standalone reusable** component (no consumer integration), composing the existing `Drawer` primitive (Vaul mobile + Radix Dialog desktop) with entity-conditional filter sections driven by a single `entityScope` prop, plus Apply/Clear/Cancel callbacks + 5 Storybook stories.

**Architecture:** Compose, don't reinvent. The repo already ships a responsive `Drawer` primitive at `apps/web/src/components/ui/drawer/` that auto-switches Vaul (mobile bottom-sheet) ↔ Radix Dialog (desktop right-sheet) at the `useDrawerBreakpoint` boundary, with focus-trap, Esc handling, and `entity` accent built in. `AdvancedFiltersDrawer` wraps it: prop `entityScope: HybridHubEntity` selects which sections to render via a pure `getSectionsForScope()` function; draft state lives inside the component (parent owns only `activeFilters` + `onApply/onClear` callbacks).

**Tech Stack:** TypeScript 5 · React 19 · Radix UI (Dialog primitives) · Vaul (mobile bottom-sheet) · Vitest + Testing Library · Storybook 7 · Tailwind 4

**Scope tightening vs issue #1606:** No consumer wiring. The drawer is standalone — Phase 3b (#1593) integrates it into `LibraryHub` state. Bundle budget AC (≤ +60KB) is verified by manual `pnpm build` + bundle analyzer at the end (not a per-task gate); the lazy boundary is exposed via barrel for the consumer to opt-into.

---

## File Structure

| File | Responsibility |
|---|---|
| `apps/web/src/components/features/library/AdvancedFiltersDrawer/types.ts` | `LibraryFilters` discriminated union (per entityScope) + `AdvancedFiltersDrawerProps` |
| `apps/web/src/components/features/library/AdvancedFiltersDrawer/sections.ts` | Pure `getSectionsForScope(scope)` + section config types (declarative, no JSX) |
| `apps/web/src/components/features/library/AdvancedFiltersDrawer/AdvancedFiltersDrawer.tsx` | Component composing `Drawer` + section rendering + draft state + callbacks |
| `apps/web/src/components/features/library/AdvancedFiltersDrawer/index.ts` | Barrel: eager exports for types/component + `AdvancedFiltersDrawerLazy` (dynamic) |
| `apps/web/src/components/features/library/AdvancedFiltersDrawer/__tests__/sections.test.ts` | Unit tests for `getSectionsForScope` (pure) |
| `apps/web/src/components/features/library/AdvancedFiltersDrawer/__tests__/AdvancedFiltersDrawer.test.tsx` | Component tests (render per scope, draft state, callbacks, a11y) |
| `apps/web/src/components/features/library/AdvancedFiltersDrawer/AdvancedFiltersDrawer.stories.tsx` | 5 Storybook stories (one per entityScope) |

7 new files. No modifications to existing code.

---

## Type design (locked upfront)

The `LibraryFilters` shape is a **discriminated union** keyed by `entityScope` so that the active-filter blob is typed per scope. This avoids `Record<string, unknown>` in the public API.

```ts
import type { HybridHubEntity } from '@/lib/library/hybrid-hub.types';
import type { GameStateType } from '@/lib/api/schemas/library.schemas';

export interface GameLibraryFilters {
  readonly scope: 'game';
  readonly states?: ReadonlyArray<GameStateType>;
  readonly withKb?: boolean;
  readonly ratingMin?: number;        // 0-10
  readonly playersMin?: number;        // 1-10
  readonly playersMax?: number;        // 1-10
  readonly yearMin?: number;
  readonly yearMax?: number;
}

export interface AgentLibraryFilters {
  readonly scope: 'agent';
  readonly types?: ReadonlyArray<string>;
  readonly activeOnly?: boolean;
}

export interface SessionLibraryFilters {
  readonly scope: 'session';
  readonly statuses?: ReadonlyArray<string>;
  readonly sessionTypes?: ReadonlyArray<string>;
  readonly playerCountMin?: number;
}

export interface KbLibraryFilters {
  readonly scope: 'kb';
  readonly processingStates?: ReadonlyArray<'Ready' | 'Pending' | 'Failed'>;
}

export interface ChatLibraryFilters {
  readonly scope: 'chat';
  readonly messageCountMin?: number;
}

export type LibraryFilters =
  | GameLibraryFilters
  | AgentLibraryFilters
  | SessionLibraryFilters
  | KbLibraryFilters
  | ChatLibraryFilters;
```

---

### Task 1: Create `types.ts`

**Files:**
- Create: `apps/web/src/components/features/library/AdvancedFiltersDrawer/types.ts`

- [ ] **Step 1: Write the types file**

```ts
/**
 * AdvancedFiltersDrawer — type definitions (Phase 3a #1606).
 *
 * `LibraryFilters` is a discriminated union keyed by `scope` so the public API
 * doesn't leak `Record<string, unknown>`. Each variant only carries the fields
 * relevant to its entity type — Phase 1's `HybridHubEntity` literal drives the
 * scope discriminant.
 *
 * Section config types (in `./sections.ts`) describe each section declaratively
 * so the renderer doesn't switch on scope at every level — `getSectionsForScope`
 * returns the array, the renderer iterates.
 */

import type { GameStateType } from '@/lib/api/schemas/library.schemas';
import type { HybridHubEntity } from '@/lib/library/hybrid-hub.types';

export interface GameLibraryFilters {
  readonly scope: 'game';
  readonly states?: ReadonlyArray<GameStateType>;
  readonly withKb?: boolean;
  readonly ratingMin?: number;
  readonly playersMin?: number;
  readonly playersMax?: number;
  readonly yearMin?: number;
  readonly yearMax?: number;
}

export interface AgentLibraryFilters {
  readonly scope: 'agent';
  readonly types?: ReadonlyArray<string>;
  readonly activeOnly?: boolean;
}

export interface SessionLibraryFilters {
  readonly scope: 'session';
  readonly statuses?: ReadonlyArray<string>;
  readonly sessionTypes?: ReadonlyArray<string>;
  readonly playerCountMin?: number;
}

export interface KbLibraryFilters {
  readonly scope: 'kb';
  readonly processingStates?: ReadonlyArray<'Ready' | 'Pending' | 'Failed'>;
}

export interface ChatLibraryFilters {
  readonly scope: 'chat';
  readonly messageCountMin?: number;
}

export type LibraryFilters =
  | GameLibraryFilters
  | AgentLibraryFilters
  | SessionLibraryFilters
  | KbLibraryFilters
  | ChatLibraryFilters;

/**
 * Narrow `LibraryFilters` to the variant matching a given `HybridHubEntity` scope.
 */
export type FiltersForScope<S extends HybridHubEntity> = Extract<LibraryFilters, { scope: S }>;

export interface AdvancedFiltersDrawerProps {
  readonly open: boolean;
  readonly onOpenChange: (open: boolean) => void;
  /** Drives which sections render. Single entity — NOT 'all' from HybridHubTab. */
  readonly entityScope: HybridHubEntity;
  /** Active filters applied to the surface; shown in the drawer when it opens. */
  readonly activeFilters: LibraryFilters;
  /** Called with the new draft when the user clicks Apply. Drawer closes after. */
  readonly onApply: (filters: LibraryFilters) => void;
  /** Called when the user clicks Clear. Drawer resets draft to default empty. */
  readonly onClear: () => void;
}
```

- [ ] **Step 2: Verify TypeScript compiles**

Run: `pnpm --filter @meepleai/web typecheck`
Expected: PASS — pure type declaration.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/features/library/AdvancedFiltersDrawer/types.ts
git commit -m "feat(library): #1606 add AdvancedFiltersDrawer type definitions (Phase 3a)"
```

---

### Task 2: TDD `sections.ts` — pure `getSectionsForScope`

**Files:**
- Create: `apps/web/src/components/features/library/AdvancedFiltersDrawer/sections.ts`
- Create: `apps/web/src/components/features/library/AdvancedFiltersDrawer/__tests__/sections.test.ts`

Each `SectionConfig` describes one filter group declaratively. The renderer iterates over the array.

- [ ] **Step 1: Write the failing test**

```ts
import { describe, expect, it } from 'vitest';

import { getSectionsForScope } from '../sections';

describe('getSectionsForScope', () => {
  it('returns 5 sections for game scope (states, withKb, rating, players, year)', () => {
    const sections = getSectionsForScope('game');
    expect(sections.map(s => s.key)).toEqual(['states', 'withKb', 'rating', 'players', 'year']);
  });

  it('game.states is a checkbox group with the 3 user-facing states (Owned/Wishlist/InPrestito)', () => {
    const sections = getSectionsForScope('game');
    const states = sections.find(s => s.key === 'states');
    expect(states?.kind).toBe('checkbox-group');
    expect(states && 'options' in states ? states.options.map(o => o.value) : []).toEqual([
      'Owned',
      'Wishlist',
      'InPrestito',
    ]);
  });

  it('game.rating is a single-slider 0-10', () => {
    const sections = getSectionsForScope('game');
    const rating = sections.find(s => s.key === 'rating');
    expect(rating?.kind).toBe('slider');
    if (rating && rating.kind === 'slider') {
      expect(rating.min).toBe(0);
      expect(rating.max).toBe(10);
    }
  });

  it('game.players is a range with min=1 max=10', () => {
    const sections = getSectionsForScope('game');
    const players = sections.find(s => s.key === 'players');
    expect(players?.kind).toBe('range');
    if (players && players.kind === 'range') {
      expect(players.min).toBe(1);
      expect(players.max).toBe(10);
    }
  });

  it('returns 2 sections for agent scope (types, activeOnly)', () => {
    const sections = getSectionsForScope('agent');
    expect(sections.map(s => s.key)).toEqual(['types', 'activeOnly']);
    expect(sections[0]?.kind).toBe('checkbox-group');
    expect(sections[1]?.kind).toBe('toggle');
  });

  it('returns 3 sections for session scope (statuses, sessionTypes, playerCount)', () => {
    const sections = getSectionsForScope('session');
    expect(sections.map(s => s.key)).toEqual(['statuses', 'sessionTypes', 'playerCount']);
  });

  it('returns 1 section for kb scope (processingStates) with 3 fixed options Ready/Pending/Failed', () => {
    const sections = getSectionsForScope('kb');
    expect(sections.map(s => s.key)).toEqual(['processingStates']);
    const ps = sections[0];
    expect(ps && ps.kind === 'checkbox-group' ? ps.options.map(o => o.value) : []).toEqual([
      'Ready',
      'Pending',
      'Failed',
    ]);
  });

  it('returns 1 section for chat scope (messageCountMin slider min=0)', () => {
    const sections = getSectionsForScope('chat');
    expect(sections.map(s => s.key)).toEqual(['messageCountMin']);
    const mc = sections[0];
    expect(mc?.kind).toBe('slider');
  });

  it('returns a new array reference each call (pure, no shared state)', () => {
    const a = getSectionsForScope('game');
    const b = getSectionsForScope('game');
    expect(a).not.toBe(b);
    expect(a).toEqual(b);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm --filter @meepleai/web test -- AdvancedFiltersDrawer/__tests__/sections`
Expected: FAIL — `Cannot find module '../sections'`.

- [ ] **Step 3: Write the implementation**

```ts
/**
 * AdvancedFiltersDrawer — pure section configuration (Phase 3a #1606).
 *
 * Declarative section descriptors keyed by `HybridHubEntity` scope. The
 * renderer iterates over the returned array — no scope-switching at render
 * time. Each section is one of three kinds (`checkbox-group`, `toggle`,
 * `slider`, `range`); adding a new kind only requires a new case in the
 * renderer + a new branch here.
 *
 * Each call returns a NEW array (caller can safely mutate state derived from
 * it without leaking back into the config).
 */

import type { HybridHubEntity } from '@/lib/library/hybrid-hub.types';

export interface CheckboxOption<V extends string = string> {
  readonly value: V;
  readonly i18nKey: string;
}

export interface CheckboxGroupSection {
  readonly kind: 'checkbox-group';
  readonly key: string;
  readonly i18nLabel: string;
  readonly options: ReadonlyArray<CheckboxOption>;
}

export interface ToggleSection {
  readonly kind: 'toggle';
  readonly key: string;
  readonly i18nLabel: string;
}

export interface SliderSection {
  readonly kind: 'slider';
  readonly key: string;
  readonly i18nLabel: string;
  readonly min: number;
  readonly max: number;
  readonly step: number;
}

export interface RangeSection {
  readonly kind: 'range';
  readonly key: string;
  readonly i18nLabel: string;
  readonly min: number;
  readonly max: number;
  readonly step: number;
}

export type SectionConfig =
  | CheckboxGroupSection
  | ToggleSection
  | SliderSection
  | RangeSection;

const GAME_STATE_OPTIONS: ReadonlyArray<CheckboxOption> = [
  { value: 'Owned', i18nKey: 'pages.library.filters.state.owned' },
  { value: 'Wishlist', i18nKey: 'pages.library.filters.state.wishlist' },
  { value: 'InPrestito', i18nKey: 'pages.library.filters.state.loaned' },
];

const KB_STATE_OPTIONS: ReadonlyArray<CheckboxOption> = [
  { value: 'Ready', i18nKey: 'pages.library.filters.kbState.ready' },
  { value: 'Pending', i18nKey: 'pages.library.filters.kbState.pending' },
  { value: 'Failed', i18nKey: 'pages.library.filters.kbState.failed' },
];

const CURRENT_YEAR = new Date().getFullYear();

export function getSectionsForScope(scope: HybridHubEntity): ReadonlyArray<SectionConfig> {
  switch (scope) {
    case 'game':
      return [
        {
          kind: 'checkbox-group',
          key: 'states',
          i18nLabel: 'pages.library.filters.section.state',
          options: GAME_STATE_OPTIONS,
        },
        { kind: 'toggle', key: 'withKb', i18nLabel: 'pages.library.filters.section.withKb' },
        {
          kind: 'slider',
          key: 'rating',
          i18nLabel: 'pages.library.filters.section.rating',
          min: 0,
          max: 10,
          step: 0.5,
        },
        {
          kind: 'range',
          key: 'players',
          i18nLabel: 'pages.library.filters.section.players',
          min: 1,
          max: 10,
          step: 1,
        },
        {
          kind: 'range',
          key: 'year',
          i18nLabel: 'pages.library.filters.section.year',
          min: 1900,
          max: CURRENT_YEAR,
          step: 1,
        },
      ];
    case 'agent':
      return [
        {
          kind: 'checkbox-group',
          key: 'types',
          i18nLabel: 'pages.library.filters.section.agentType',
          options: [], // populated by the consumer's `availableAgentTypes` — see renderer
        },
        { kind: 'toggle', key: 'activeOnly', i18nLabel: 'pages.library.filters.section.activeOnly' },
      ];
    case 'session':
      return [
        {
          kind: 'checkbox-group',
          key: 'statuses',
          i18nLabel: 'pages.library.filters.section.sessionStatus',
          options: [], // populated by consumer
        },
        {
          kind: 'checkbox-group',
          key: 'sessionTypes',
          i18nLabel: 'pages.library.filters.section.sessionType',
          options: [], // populated by consumer
        },
        {
          kind: 'slider',
          key: 'playerCount',
          i18nLabel: 'pages.library.filters.section.playerCount',
          min: 1,
          max: 12,
          step: 1,
        },
      ];
    case 'kb':
      return [
        {
          kind: 'checkbox-group',
          key: 'processingStates',
          i18nLabel: 'pages.library.filters.section.processingState',
          options: KB_STATE_OPTIONS,
        },
      ];
    case 'chat':
      return [
        {
          kind: 'slider',
          key: 'messageCountMin',
          i18nLabel: 'pages.library.filters.section.messageCountMin',
          min: 0,
          max: 100,
          step: 5,
        },
      ];
  }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm --filter @meepleai/web test -- AdvancedFiltersDrawer/__tests__/sections`
Expected: PASS — all 9 tests green.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/features/library/AdvancedFiltersDrawer/sections.ts apps/web/src/components/features/library/AdvancedFiltersDrawer/__tests__/sections.test.ts
git commit -m "feat(library): #1606 add getSectionsForScope pure config (Phase 3a)"
```

---

### Task 3: TDD `AdvancedFiltersDrawer.tsx` skeleton — open/close + Cancel

The skeleton renders the Drawer shell with Title/Description/Footer and the Cancel button. No filter section rendering yet (Task 4-6). Draft state initializes from `activeFilters`.

**Files:**
- Create: `apps/web/src/components/features/library/AdvancedFiltersDrawer/AdvancedFiltersDrawer.tsx`
- Create: `apps/web/src/components/features/library/AdvancedFiltersDrawer/__tests__/AdvancedFiltersDrawer.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { AdvancedFiltersDrawer } from '../AdvancedFiltersDrawer';
import type { LibraryFilters } from '../types';

const noop = () => {};

const emptyGameFilters: LibraryFilters = { scope: 'game' };

describe('AdvancedFiltersDrawer — skeleton (open/close + Cancel)', () => {
  it('does not render content when open=false', () => {
    render(
      <AdvancedFiltersDrawer
        open={false}
        onOpenChange={noop}
        entityScope="game"
        activeFilters={emptyGameFilters}
        onApply={noop}
        onClear={noop}
      />
    );
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('renders dialog with title when open=true', () => {
    render(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        entityScope="game"
        activeFilters={emptyGameFilters}
        onApply={noop}
        onClear={noop}
      />
    );
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByText(/più filtri/i)).toBeInTheDocument();
  });

  it('Cancel button calls onOpenChange(false)', async () => {
    const onOpenChange = vi.fn();
    const user = userEvent.setup();
    render(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={onOpenChange}
        entityScope="game"
        activeFilters={emptyGameFilters}
        onApply={noop}
        onClear={noop}
      />
    );
    await user.click(screen.getByRole('button', { name: /annulla/i }));
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm --filter @meepleai/web test -- AdvancedFiltersDrawer/__tests__/AdvancedFiltersDrawer`
Expected: FAIL — `Cannot find module '../AdvancedFiltersDrawer'`.

- [ ] **Step 3: Write the skeleton implementation**

```tsx
/**
 * AdvancedFiltersDrawer (Phase 3a #1606).
 *
 * Standalone reusable component that composes the existing `Drawer` primitive
 * with entity-conditional filter sections derived from `entityScope`. Owns its
 * own draft state internally; parent only observes via `onApply` / `onClear`
 * callbacks.
 *
 * Composition: `Drawer` (mobile↔desktop responsive) → `DrawerContent` →
 *   `DrawerHeader` (title + description) →
 *   sections (from `getSectionsForScope`) →
 *   `DrawerFooter` (Cancel / Clear / Apply).
 */

'use client';

import { useState, type ReactElement } from 'react';
import { useTranslation } from 'react-i18next';

import {
  Drawer,
  DrawerContent,
  DrawerDescription,
  DrawerFooter,
  DrawerHeader,
  DrawerTitle,
} from '@/components/ui/drawer';

import type { AdvancedFiltersDrawerProps, LibraryFilters } from './types';

function emptyFiltersFor(scope: AdvancedFiltersDrawerProps['entityScope']): LibraryFilters {
  return { scope } as LibraryFilters;
}

export function AdvancedFiltersDrawer({
  open,
  onOpenChange,
  entityScope,
  activeFilters,
  onApply,
  onClear,
}: AdvancedFiltersDrawerProps): ReactElement {
  const { t } = useTranslation();
  const [draft, setDraft] = useState<LibraryFilters>(activeFilters);

  // Reset draft when drawer opens with new activeFilters (parent reopens)
  // (handled in Task 7 with useEffect on `open`)

  const handleCancel = () => {
    setDraft(activeFilters);
    onOpenChange(false);
  };

  return (
    <Drawer open={open} onOpenChange={onOpenChange} entity={entityScope}>
      <DrawerContent data-slot="advanced-filters-drawer">
        <DrawerHeader>
          <DrawerTitle>{t('pages.library.filters.title')}</DrawerTitle>
          <DrawerDescription>
            {t('pages.library.filters.description')}
          </DrawerDescription>
        </DrawerHeader>

        {/* Sections rendered in Tasks 4-6, driven by getSectionsForScope(entityScope) and `draft` */}
        {/* For Task 3, this is intentionally empty — skeleton only */}
        <div data-slot="advanced-filters-sections" aria-hidden="true" />

        <DrawerFooter>
          <button
            type="button"
            data-slot="advanced-filters-cancel"
            onClick={handleCancel}
            className="rounded-md border border-border bg-card px-3 py-1.5 text-sm font-medium text-foreground hover:bg-muted/40"
          >
            {t('common.cancel', { defaultValue: 'Annulla' })}
          </button>
        </DrawerFooter>
      </DrawerContent>
    </Drawer>
  );
}

// Suppress the "unused" warning until Tasks 4-7 wire it
void emptyFiltersFor;
void setDraft;
void draft;
void onApply;
void onClear;
```

Note: the trailing `void` lines silence ESLint `no-unused-vars` for symbols that Task 7 will consume. They are removed in Task 7.

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm --filter @meepleai/web test -- AdvancedFiltersDrawer/__tests__/AdvancedFiltersDrawer`
Expected: PASS — all 3 skeleton tests green.

Note on i18n: the test asserts `/più filtri/i` and `/annulla/i` against `i18next` keys. The test setup at `apps/web/vitest.setup.ts` typically configures i18next with the real locale catalogs; verify the keys `pages.library.filters.title` and `common.cancel` exist in `apps/web/src/locales/it.json` (and `en.json`). If they don't, add them in **Step 4.5** before Step 5.

- [ ] **Step 4.5: Add i18n keys if missing**

Check `apps/web/src/locales/it.json` and `apps/web/src/locales/en.json` for these keys (path `pages.library.filters.*`). If missing, add:

```jsonc
// it.json — under "pages.library"
"filters": {
  "title": "Più filtri",
  "description": "Filtra la libreria per dimensioni specifiche dell'entità.",
  "apply": "Applica",
  "clear": "Reimposta",
  "section": {
    "state": "Stato",
    "withKb": "Solo con Knowledge Base",
    "rating": "Rating minimo",
    "players": "Numero di giocatori",
    "year": "Anno di pubblicazione",
    "agentType": "Tipo di agente",
    "activeOnly": "Solo attivi",
    "sessionStatus": "Stato sessione",
    "sessionType": "Tipo sessione",
    "playerCount": "Giocatori (min)",
    "processingState": "Stato di elaborazione",
    "messageCountMin": "Messaggi (min)"
  },
  "state": { "owned": "Posseduto", "wishlist": "Wishlist", "loaned": "In prestito" },
  "kbState": { "ready": "Pronto", "pending": "In elaborazione", "failed": "Errore" }
}
```

```jsonc
// en.json — under "pages.library"
"filters": {
  "title": "More filters",
  "description": "Filter the library by entity-specific dimensions.",
  "apply": "Apply",
  "clear": "Reset",
  "section": {
    "state": "State",
    "withKb": "With Knowledge Base only",
    "rating": "Minimum rating",
    "players": "Player count",
    "year": "Year published",
    "agentType": "Agent type",
    "activeOnly": "Active only",
    "sessionStatus": "Session status",
    "sessionType": "Session type",
    "playerCount": "Players (min)",
    "processingState": "Processing state",
    "messageCountMin": "Messages (min)"
  },
  "state": { "owned": "Owned", "wishlist": "Wishlist", "loaned": "Loaned" },
  "kbState": { "ready": "Ready", "pending": "Pending", "failed": "Failed" }
}
```

Adjust the existing `common.cancel` key if missing in either locale (`Annulla` / `Cancel`).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/features/library/AdvancedFiltersDrawer/AdvancedFiltersDrawer.tsx \
       apps/web/src/components/features/library/AdvancedFiltersDrawer/__tests__/AdvancedFiltersDrawer.test.tsx \
       apps/web/src/locales/it.json apps/web/src/locales/en.json
git commit -m "feat(library): #1606 add AdvancedFiltersDrawer skeleton + i18n keys (Phase 3a)"
```

---

### Task 4: TDD game scope section rendering

The full game scope (5 sections) tests + implementation. This is the largest scope; the others (Tasks 5-6) are smaller and faster.

**Files:**
- Modify: `apps/web/src/components/features/library/AdvancedFiltersDrawer/AdvancedFiltersDrawer.tsx` (replace section placeholder with actual rendering)
- Modify: `apps/web/src/components/features/library/AdvancedFiltersDrawer/__tests__/AdvancedFiltersDrawer.test.tsx` (append game-scope tests)

- [ ] **Step 1: Append game-scope tests**

```tsx
import { getSectionsForScope } from '../sections';

describe('AdvancedFiltersDrawer — game scope rendering', () => {
  it('renders 5 sections for game scope (state, withKb, rating, players, year)', () => {
    render(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        entityScope="game"
        activeFilters={{ scope: 'game' }}
        onApply={noop}
        onClear={noop}
      />
    );
    const slots = screen.getAllByTestId(/^advanced-filters-section-/);
    expect(slots).toHaveLength(5);
    expect(slots.map(el => el.getAttribute('data-slot'))).toEqual([
      'advanced-filters-section-states',
      'advanced-filters-section-withKb',
      'advanced-filters-section-rating',
      'advanced-filters-section-players',
      'advanced-filters-section-year',
    ]);
  });

  it('game.states checkbox group renders 3 options with checked state from activeFilters', () => {
    render(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        entityScope="game"
        activeFilters={{ scope: 'game', states: ['Owned', 'Wishlist'] }}
        onApply={noop}
        onClear={noop}
      />
    );
    const owned = screen.getByRole('checkbox', { name: /posseduto/i });
    const wishlist = screen.getByRole('checkbox', { name: /wishlist/i });
    const loaned = screen.getByRole('checkbox', { name: /in prestito/i });
    expect(owned).toBeChecked();
    expect(wishlist).toBeChecked();
    expect(loaned).not.toBeChecked();
  });

  it('toggling a state checkbox updates internal draft (not yet applied — Apply test in Task 7)', async () => {
    const user = userEvent.setup();
    render(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        entityScope="game"
        activeFilters={{ scope: 'game' }}
        onApply={noop}
        onClear={noop}
      />
    );
    const owned = screen.getByRole('checkbox', { name: /posseduto/i });
    await user.click(owned);
    expect(owned).toBeChecked();
  });

  it('game.withKb renders a toggle with checked state from activeFilters', () => {
    render(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        entityScope="game"
        activeFilters={{ scope: 'game', withKb: true }}
        onApply={noop}
        onClear={noop}
      />
    );
    const toggle = screen.getByRole('switch', { name: /knowledge base/i });
    expect(toggle).toBeChecked();
  });

  it('game.rating slider has correct min/max and reflects activeFilters.ratingMin', () => {
    render(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        entityScope="game"
        activeFilters={{ scope: 'game', ratingMin: 7 }}
        onApply={noop}
        onClear={noop}
      />
    );
    const slider = screen.getByRole('slider', { name: /rating/i });
    expect(slider).toHaveAttribute('aria-valuenow', '7');
    expect(slider).toHaveAttribute('aria-valuemin', '0');
    expect(slider).toHaveAttribute('aria-valuemax', '10');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Expected: FAIL — sections not rendered yet (skeleton just has the empty placeholder div).

- [ ] **Step 3: Implement game scope section rendering**

Replace the `<div data-slot="advanced-filters-sections" />` placeholder with actual rendering, and update the imports. The component now consumes `sections` config + `draft` state.

Full replacement for `AdvancedFiltersDrawer.tsx`:

```tsx
/**
 * AdvancedFiltersDrawer (Phase 3a #1606).
 *
 * Standalone reusable component that composes the existing `Drawer` primitive
 * with entity-conditional filter sections derived from `entityScope`. Owns its
 * own draft state internally; parent only observes via `onApply` / `onClear`
 * callbacks.
 */

'use client';

import { useState, type ReactElement } from 'react';
import { useTranslation } from 'react-i18next';

import {
  Drawer,
  DrawerContent,
  DrawerDescription,
  DrawerFooter,
  DrawerHeader,
  DrawerTitle,
} from '@/components/ui/drawer';

import {
  getSectionsForScope,
  type CheckboxGroupSection,
  type RangeSection,
  type SectionConfig,
  type SliderSection,
  type ToggleSection,
} from './sections';
import type { AdvancedFiltersDrawerProps, GameLibraryFilters, LibraryFilters } from './types';

export function AdvancedFiltersDrawer({
  open,
  onOpenChange,
  entityScope,
  activeFilters,
  onApply,
  onClear,
}: AdvancedFiltersDrawerProps): ReactElement {
  const { t } = useTranslation();
  const [draft, setDraft] = useState<LibraryFilters>(activeFilters);

  const handleCancel = () => {
    setDraft(activeFilters);
    onOpenChange(false);
  };

  const sections = getSectionsForScope(entityScope);

  return (
    <Drawer open={open} onOpenChange={onOpenChange} entity={entityScope}>
      <DrawerContent data-slot="advanced-filters-drawer">
        <DrawerHeader>
          <DrawerTitle>{t('pages.library.filters.title')}</DrawerTitle>
          <DrawerDescription>{t('pages.library.filters.description')}</DrawerDescription>
        </DrawerHeader>

        <div className="flex flex-col gap-4 px-4 pb-4" data-slot="advanced-filters-sections">
          {sections.map(section => (
            <SectionRenderer
              key={section.key}
              section={section}
              draft={draft}
              onChange={setDraft}
              t={t}
            />
          ))}
        </div>

        <DrawerFooter>
          <button
            type="button"
            data-slot="advanced-filters-cancel"
            onClick={handleCancel}
            className="rounded-md border border-border bg-card px-3 py-1.5 text-sm font-medium text-foreground hover:bg-muted/40"
          >
            {t('common.cancel', { defaultValue: 'Annulla' })}
          </button>
          {/* Clear + Apply buttons added in Task 7 */}
        </DrawerFooter>
      </DrawerContent>
    </Drawer>
  );

  // Suppress unused until Task 7
  void onApply;
  void onClear;
}

// ─── Section renderer ────────────────────────────────────────────────────────

interface SectionRendererProps {
  readonly section: SectionConfig;
  readonly draft: LibraryFilters;
  readonly onChange: (next: LibraryFilters) => void;
  readonly t: (key: string, options?: Record<string, unknown>) => string;
}

function SectionRenderer({ section, draft, onChange, t }: SectionRendererProps): ReactElement {
  switch (section.kind) {
    case 'checkbox-group':
      return (
        <CheckboxGroupRenderer
          section={section}
          draft={draft}
          onChange={onChange}
          t={t}
        />
      );
    case 'toggle':
      return <ToggleRenderer section={section} draft={draft} onChange={onChange} t={t} />;
    case 'slider':
      return <SliderRenderer section={section} draft={draft} onChange={onChange} t={t} />;
    case 'range':
      return <RangeRenderer section={section} draft={draft} onChange={onChange} t={t} />;
  }
}

function CheckboxGroupRenderer({
  section,
  draft,
  onChange,
  t,
}: {
  section: CheckboxGroupSection;
  draft: LibraryFilters;
  onChange: (next: LibraryFilters) => void;
  t: SectionRendererProps['t'];
}): ReactElement {
  // For game scope: section.key === 'states' → draft.states (when scope=game)
  const draftValues = readArrayField(draft, section.key);
  const toggle = (val: string) => {
    const next = draftValues.includes(val)
      ? draftValues.filter(v => v !== val)
      : [...draftValues, val];
    onChange(writeArrayField(draft, section.key, next));
  };
  return (
    <div data-slot={`advanced-filters-section-${section.key}`} className="flex flex-col gap-2">
      <span className="text-sm font-medium text-foreground">{t(section.i18nLabel)}</span>
      <div className="flex flex-wrap gap-2">
        {section.options.map(opt => (
          <label key={opt.value} className="inline-flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={draftValues.includes(opt.value)}
              onChange={() => toggle(opt.value)}
              className="h-4 w-4 rounded border-border"
            />
            <span>{t(opt.i18nKey)}</span>
          </label>
        ))}
      </div>
    </div>
  );
}

function ToggleRenderer({
  section,
  draft,
  onChange,
  t,
}: {
  section: ToggleSection;
  draft: LibraryFilters;
  onChange: (next: LibraryFilters) => void;
  t: SectionRendererProps['t'];
}): ReactElement {
  const checked = Boolean(readScalarField(draft, section.key));
  return (
    <div data-slot={`advanced-filters-section-${section.key}`} className="flex items-center justify-between">
      <span className="text-sm font-medium text-foreground">{t(section.i18nLabel)}</span>
      <button
        type="button"
        role="switch"
        aria-checked={checked}
        aria-label={t(section.i18nLabel)}
        onClick={() => onChange(writeScalarField(draft, section.key, !checked))}
        className={`relative h-6 w-11 rounded-full transition-colors ${checked ? 'bg-primary' : 'bg-muted'}`}
      >
        <span
          aria-hidden="true"
          className={`absolute top-0.5 h-5 w-5 rounded-full bg-card transition-transform ${checked ? 'translate-x-5' : 'translate-x-0.5'}`}
        />
      </button>
    </div>
  );
}

function SliderRenderer({
  section,
  draft,
  onChange,
  t,
}: {
  section: SliderSection;
  draft: LibraryFilters;
  onChange: (next: LibraryFilters) => void;
  t: SectionRendererProps['t'];
}): ReactElement {
  const fieldKey = sliderFieldKey(section.key);
  const value = readNumberField(draft, fieldKey) ?? section.min;
  return (
    <div data-slot={`advanced-filters-section-${section.key}`} className="flex flex-col gap-2">
      <span className="text-sm font-medium text-foreground">
        {t(section.i18nLabel)} <span className="text-muted-foreground">({value})</span>
      </span>
      <input
        type="range"
        role="slider"
        aria-label={t(section.i18nLabel)}
        aria-valuemin={section.min}
        aria-valuemax={section.max}
        aria-valuenow={value}
        min={section.min}
        max={section.max}
        step={section.step}
        value={value}
        onChange={e => onChange(writeNumberField(draft, fieldKey, Number(e.target.value)))}
        className="w-full"
      />
    </div>
  );
}

function RangeRenderer({
  section,
  draft,
  onChange,
  t,
}: {
  section: RangeSection;
  draft: LibraryFilters;
  onChange: (next: LibraryFilters) => void;
  t: SectionRendererProps['t'];
}): ReactElement {
  const [minField, maxField] = rangeFieldKeys(section.key);
  const minVal = readNumberField(draft, minField) ?? section.min;
  const maxVal = readNumberField(draft, maxField) ?? section.max;
  return (
    <div data-slot={`advanced-filters-section-${section.key}`} className="flex flex-col gap-2">
      <span className="text-sm font-medium text-foreground">
        {t(section.i18nLabel)} <span className="text-muted-foreground">({minVal}–{maxVal})</span>
      </span>
      <div className="grid grid-cols-2 gap-2">
        <input
          type="number"
          aria-label={`${t(section.i18nLabel)} min`}
          min={section.min}
          max={section.max}
          step={section.step}
          value={minVal}
          onChange={e => onChange(writeNumberField(draft, minField, Number(e.target.value)))}
          className="rounded border border-border bg-background px-2 py-1 text-sm"
        />
        <input
          type="number"
          aria-label={`${t(section.i18nLabel)} max`}
          min={section.min}
          max={section.max}
          step={section.step}
          value={maxVal}
          onChange={e => onChange(writeNumberField(draft, maxField, Number(e.target.value)))}
          className="rounded border border-border bg-background px-2 py-1 text-sm"
        />
      </div>
    </div>
  );
}

// ─── Field helpers (scope-aware read/write into LibraryFilters union) ───────

function readArrayField(draft: LibraryFilters, key: string): ReadonlyArray<string> {
  const rec = draft as unknown as Record<string, unknown>;
  const v = rec[key];
  return Array.isArray(v) ? (v as ReadonlyArray<string>) : [];
}

function writeArrayField(
  draft: LibraryFilters,
  key: string,
  value: ReadonlyArray<string>
): LibraryFilters {
  return { ...(draft as unknown as Record<string, unknown>), [key]: value } as LibraryFilters;
}

function readScalarField(draft: LibraryFilters, key: string): unknown {
  return (draft as unknown as Record<string, unknown>)[key];
}

function writeScalarField(draft: LibraryFilters, key: string, value: unknown): LibraryFilters {
  return { ...(draft as unknown as Record<string, unknown>), [key]: value } as LibraryFilters;
}

function readNumberField(draft: LibraryFilters, key: string): number | undefined {
  const v = (draft as unknown as Record<string, unknown>)[key];
  return typeof v === 'number' ? v : undefined;
}

function writeNumberField(draft: LibraryFilters, key: string, value: number): LibraryFilters {
  return { ...(draft as unknown as Record<string, unknown>), [key]: value } as LibraryFilters;
}

function sliderFieldKey(sectionKey: string): string {
  // Maps section.key → LibraryFilters field name
  if (sectionKey === 'rating') return 'ratingMin';
  if (sectionKey === 'playerCount') return 'playerCountMin';
  if (sectionKey === 'messageCountMin') return 'messageCountMin';
  return sectionKey;
}

function rangeFieldKeys(sectionKey: string): readonly [string, string] {
  // Maps section.key → [minField, maxField] in LibraryFilters
  if (sectionKey === 'players') return ['playersMin', 'playersMax'];
  if (sectionKey === 'year') return ['yearMin', 'yearMax'];
  return [`${sectionKey}Min`, `${sectionKey}Max`];
}
```

Note: the union-narrowing via `unknown as Record<string, unknown>` is intentional. The discriminated union `LibraryFilters` carries different fields per `scope`; the section renderer reads/writes by string key which TypeScript cannot statically check across variants. This is the boundary at which static safety hands off to the section-config contract (Task 2). A future refinement could use mapped types — out of scope here per YAGNI.

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm --filter @meepleai/web test -- AdvancedFiltersDrawer/__tests__/AdvancedFiltersDrawer`
Expected: PASS — all 3 skeleton + 5 game-scope tests = 8 green.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/features/library/AdvancedFiltersDrawer/AdvancedFiltersDrawer.tsx apps/web/src/components/features/library/AdvancedFiltersDrawer/__tests__/AdvancedFiltersDrawer.test.tsx
git commit -m "feat(library): #1606 render game scope sections + draft state (Phase 3a)"
```

---

### Task 5: TDD agent + session scope rendering

The renderer machinery from Task 4 already handles all section kinds. Tasks 5-6 only add tests + i18n catalog assertion; no new rendering code needed.

**Files:**
- Modify: `apps/web/src/components/features/library/AdvancedFiltersDrawer/__tests__/AdvancedFiltersDrawer.test.tsx` (append agent + session tests)

- [ ] **Step 1: Append agent + session scope tests**

```tsx
describe('AdvancedFiltersDrawer — agent scope rendering', () => {
  it('renders 2 sections for agent scope (types, activeOnly)', () => {
    render(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        entityScope="agent"
        activeFilters={{ scope: 'agent' }}
        onApply={noop}
        onClear={noop}
      />
    );
    const slots = screen.getAllByTestId(/^advanced-filters-section-/);
    expect(slots.map(el => el.getAttribute('data-slot'))).toEqual([
      'advanced-filters-section-types',
      'advanced-filters-section-activeOnly',
    ]);
  });

  it('agent.activeOnly toggle reflects activeFilters and updates draft on click', async () => {
    const user = userEvent.setup();
    render(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        entityScope="agent"
        activeFilters={{ scope: 'agent', activeOnly: false }}
        onApply={noop}
        onClear={noop}
      />
    );
    const toggle = screen.getByRole('switch', { name: /solo attivi/i });
    expect(toggle).toHaveAttribute('aria-checked', 'false');
    await user.click(toggle);
    expect(toggle).toHaveAttribute('aria-checked', 'true');
  });
});

describe('AdvancedFiltersDrawer — session scope rendering', () => {
  it('renders 3 sections for session scope (statuses, sessionTypes, playerCount)', () => {
    render(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        entityScope="session"
        activeFilters={{ scope: 'session' }}
        onApply={noop}
        onClear={noop}
      />
    );
    const slots = screen.getAllByTestId(/^advanced-filters-section-/);
    expect(slots.map(el => el.getAttribute('data-slot'))).toEqual([
      'advanced-filters-section-statuses',
      'advanced-filters-section-sessionTypes',
      'advanced-filters-section-playerCount',
    ]);
  });

  it('session.playerCount slider has min=1 max=12 and reflects activeFilters.playerCountMin', () => {
    render(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        entityScope="session"
        activeFilters={{ scope: 'session', playerCountMin: 4 }}
        onApply={noop}
        onClear={noop}
      />
    );
    const slider = screen.getByRole('slider', { name: /giocatori/i });
    expect(slider).toHaveAttribute('aria-valuemin', '1');
    expect(slider).toHaveAttribute('aria-valuemax', '12');
    expect(slider).toHaveAttribute('aria-valuenow', '4');
  });
});
```

- [ ] **Step 2: Run tests to verify they pass without code changes**

Run: `pnpm --filter @meepleai/web test -- AdvancedFiltersDrawer/__tests__/AdvancedFiltersDrawer`
Expected: PASS — Task 4 renderer already handles all kinds; 4 new tests green (12 total).

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/features/library/AdvancedFiltersDrawer/__tests__/AdvancedFiltersDrawer.test.tsx
git commit -m "test(library): #1606 verify agent + session scope rendering (Phase 3a)"
```

---

### Task 6: TDD kb + chat scope rendering

**Files:**
- Modify: `apps/web/src/components/features/library/AdvancedFiltersDrawer/__tests__/AdvancedFiltersDrawer.test.tsx` (append kb + chat tests)

- [ ] **Step 1: Append tests**

```tsx
describe('AdvancedFiltersDrawer — kb scope rendering', () => {
  it('renders 1 section (processingStates) with 3 checkboxes (Ready/Pending/Failed)', () => {
    render(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        entityScope="kb"
        activeFilters={{ scope: 'kb', processingStates: ['Ready'] }}
        onApply={noop}
        onClear={noop}
      />
    );
    const slots = screen.getAllByTestId(/^advanced-filters-section-/);
    expect(slots).toHaveLength(1);
    expect(slots[0]?.getAttribute('data-slot')).toBe('advanced-filters-section-processingStates');

    expect(screen.getByRole('checkbox', { name: /pronto/i })).toBeChecked();
    expect(screen.getByRole('checkbox', { name: /in elaborazione/i })).not.toBeChecked();
    expect(screen.getByRole('checkbox', { name: /errore/i })).not.toBeChecked();
  });
});

describe('AdvancedFiltersDrawer — chat scope rendering', () => {
  it('renders 1 section (messageCountMin slider) with min=0 max=100', () => {
    render(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        entityScope="chat"
        activeFilters={{ scope: 'chat', messageCountMin: 10 }}
        onApply={noop}
        onClear={noop}
      />
    );
    const slots = screen.getAllByTestId(/^advanced-filters-section-/);
    expect(slots).toHaveLength(1);
    const slider = screen.getByRole('slider', { name: /messaggi/i });
    expect(slider).toHaveAttribute('aria-valuemin', '0');
    expect(slider).toHaveAttribute('aria-valuemax', '100');
    expect(slider).toHaveAttribute('aria-valuenow', '10');
  });
});
```

- [ ] **Step 2: Run tests to verify they pass**

Run: `pnpm --filter @meepleai/web test -- AdvancedFiltersDrawer/__tests__/AdvancedFiltersDrawer`
Expected: PASS — 2 new tests green (14 total).

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/features/library/AdvancedFiltersDrawer/__tests__/AdvancedFiltersDrawer.test.tsx
git commit -m "test(library): #1606 verify kb + chat scope rendering (Phase 3a)"
```

---

### Task 7: TDD Apply + Clear callbacks

**Files:**
- Modify: `apps/web/src/components/features/library/AdvancedFiltersDrawer/AdvancedFiltersDrawer.tsx` (add Apply + Clear buttons + draft-reset effect)
- Modify: `apps/web/src/components/features/library/AdvancedFiltersDrawer/__tests__/AdvancedFiltersDrawer.test.tsx` (append callback tests)

- [ ] **Step 1: Append callback tests**

```tsx
describe('AdvancedFiltersDrawer — Apply / Clear callbacks', () => {
  it('Apply button calls onApply with current draft and closes the drawer', async () => {
    const onApply = vi.fn();
    const onOpenChange = vi.fn();
    const user = userEvent.setup();
    render(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={onOpenChange}
        entityScope="game"
        activeFilters={{ scope: 'game' }}
        onApply={onApply}
        onClear={noop}
      />
    );

    const owned = screen.getByRole('checkbox', { name: /posseduto/i });
    await user.click(owned);
    await user.click(screen.getByRole('button', { name: /applica/i }));

    expect(onApply).toHaveBeenCalledTimes(1);
    expect(onApply).toHaveBeenCalledWith({ scope: 'game', states: ['Owned'] });
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it('Clear button calls onClear and resets draft to empty scope, drawer stays open', async () => {
    const onClear = vi.fn();
    const onOpenChange = vi.fn();
    const user = userEvent.setup();
    render(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={onOpenChange}
        entityScope="game"
        activeFilters={{ scope: 'game', states: ['Owned'], ratingMin: 7 }}
        onApply={noop}
        onClear={onClear}
      />
    );

    const owned = screen.getByRole('checkbox', { name: /posseduto/i });
    expect(owned).toBeChecked();

    await user.click(screen.getByRole('button', { name: /reimposta/i }));

    expect(onClear).toHaveBeenCalledTimes(1);
    expect(onOpenChange).not.toHaveBeenCalledWith(false); // drawer stays open
    expect(screen.getByRole('checkbox', { name: /posseduto/i })).not.toBeChecked();
  });

  it('when reopened with new activeFilters, draft resets to the new activeFilters', async () => {
    const { rerender } = render(
      <AdvancedFiltersDrawer
        open={false}
        onOpenChange={noop}
        entityScope="game"
        activeFilters={{ scope: 'game' }}
        onApply={noop}
        onClear={noop}
      />
    );
    rerender(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        entityScope="game"
        activeFilters={{ scope: 'game', states: ['Wishlist'] }}
        onApply={noop}
        onClear={noop}
      />
    );
    expect(screen.getByRole('checkbox', { name: /wishlist/i })).toBeChecked();
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Expected: FAIL — Apply button missing; Clear button missing; draft-reset effect missing.

- [ ] **Step 3: Update the component**

In `AdvancedFiltersDrawer.tsx`:

1. Add a `useEffect` that resets draft when `open` transitions to `true`:

```tsx
import { useEffect, useState, type ReactElement } from 'react';
// ...
const [draft, setDraft] = useState<LibraryFilters>(activeFilters);
useEffect(() => {
  if (open) setDraft(activeFilters);
}, [open, activeFilters]);
```

2. Add Apply + Clear handlers:

```tsx
const handleApply = () => {
  onApply(draft);
  onOpenChange(false);
};

const handleClear = () => {
  const empty = { scope: entityScope } as LibraryFilters;
  setDraft(empty);
  onClear();
};
```

3. Add Clear + Apply buttons to the `DrawerFooter`, alongside the existing Cancel:

```tsx
<DrawerFooter>
  <button
    type="button"
    data-slot="advanced-filters-cancel"
    onClick={handleCancel}
    className="rounded-md border border-border bg-card px-3 py-1.5 text-sm font-medium text-foreground hover:bg-muted/40"
  >
    {t('common.cancel', { defaultValue: 'Annulla' })}
  </button>
  <button
    type="button"
    data-slot="advanced-filters-clear"
    onClick={handleClear}
    className="rounded-md border border-border bg-card px-3 py-1.5 text-sm font-medium text-foreground hover:bg-muted/40"
  >
    {t('pages.library.filters.clear')}
  </button>
  <button
    type="button"
    data-slot="advanced-filters-apply"
    onClick={handleApply}
    className="rounded-md bg-primary px-3 py-1.5 text-sm font-medium text-primary-foreground hover:bg-primary/90"
  >
    {t('pages.library.filters.apply')}
  </button>
</DrawerFooter>
```

4. Remove the trailing `void onApply; void onClear;` lines (no longer needed).

- [ ] **Step 4: Run tests to verify they pass**

Run: `pnpm --filter @meepleai/web test -- AdvancedFiltersDrawer/__tests__/AdvancedFiltersDrawer`
Expected: PASS — all 17 tests green (14 + 3 new callback tests).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/features/library/AdvancedFiltersDrawer/AdvancedFiltersDrawer.tsx apps/web/src/components/features/library/AdvancedFiltersDrawer/__tests__/AdvancedFiltersDrawer.test.tsx
git commit -m "feat(library): #1606 add Apply/Clear callbacks + draft reset effect (Phase 3a)"
```

---

### Task 8: Barrel + Storybook 5 stories + jest-axe

**Files:**
- Create: `apps/web/src/components/features/library/AdvancedFiltersDrawer/index.ts`
- Create: `apps/web/src/components/features/library/AdvancedFiltersDrawer/AdvancedFiltersDrawer.stories.tsx`
- Modify: `apps/web/src/components/features/library/AdvancedFiltersDrawer/__tests__/AdvancedFiltersDrawer.test.tsx` (append jest-axe a11y test)

- [ ] **Step 1: Create barrel**

`apps/web/src/components/features/library/AdvancedFiltersDrawer/index.ts`:

```ts
export { AdvancedFiltersDrawer } from './AdvancedFiltersDrawer';
export type {
  AdvancedFiltersDrawerProps,
  LibraryFilters,
  GameLibraryFilters,
  AgentLibraryFilters,
  SessionLibraryFilters,
  KbLibraryFilters,
  ChatLibraryFilters,
  FiltersForScope,
} from './types';
```

- [ ] **Step 2: Create Storybook stories**

`apps/web/src/components/features/library/AdvancedFiltersDrawer/AdvancedFiltersDrawer.stories.tsx`:

```tsx
import { useState } from 'react';
import type { Meta, StoryObj } from '@storybook/react';

import { AdvancedFiltersDrawer } from './AdvancedFiltersDrawer';
import type { LibraryFilters } from './types';

const meta = {
  title: 'features/library/AdvancedFiltersDrawer',
  component: AdvancedFiltersDrawer,
  parameters: { layout: 'centered' },
} satisfies Meta<typeof AdvancedFiltersDrawer>;

export default meta;
type Story = StoryObj<typeof AdvancedFiltersDrawer>;

function makeStory(
  entityScope: LibraryFilters['scope'],
  initial: LibraryFilters
): Story {
  return {
    render: () => {
      const [open, setOpen] = useState(true);
      const [active, setActive] = useState<LibraryFilters>(initial);
      return (
        <AdvancedFiltersDrawer
          open={open}
          onOpenChange={setOpen}
          entityScope={entityScope}
          activeFilters={active}
          onApply={next => setActive(next)}
          onClear={() => setActive({ scope: entityScope } as LibraryFilters)}
        />
      );
    },
  };
}

export const GameScope = makeStory('game', { scope: 'game' });
export const GameScopeWithFilters = makeStory('game', {
  scope: 'game',
  states: ['Owned'],
  ratingMin: 7,
  withKb: true,
});
export const AgentScope = makeStory('agent', { scope: 'agent', activeOnly: true });
export const SessionScope = makeStory('session', { scope: 'session', playerCountMin: 4 });
export const KbScope = makeStory('kb', { scope: 'kb', processingStates: ['Ready'] });
export const ChatScope = makeStory('chat', { scope: 'chat', messageCountMin: 10 });
```

6 stories: GameScope (empty), GameScopeWithFilters (pre-populated), AgentScope, SessionScope, KbScope, ChatScope. Issue #1606 AC4e asks for 5; this provides 6 (the empty + pre-populated game variants are both useful for visual review).

- [ ] **Step 3: Append jest-axe a11y test**

In `__tests__/AdvancedFiltersDrawer.test.tsx`:

```tsx
import { axe } from 'vitest-axe';

describe('AdvancedFiltersDrawer — a11y', () => {
  it('renders no axe violations for game scope when open', async () => {
    const { container } = render(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        entityScope="game"
        activeFilters={{ scope: 'game' }}
        onApply={noop}
        onClear={noop}
      />
    );
    expect(await axe(container)).toHaveNoViolations();
  });

  it('renders no axe violations for kb scope when open', async () => {
    const { container } = render(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        entityScope="kb"
        activeFilters={{ scope: 'kb' }}
        onApply={noop}
        onClear={noop}
      />
    );
    expect(await axe(container)).toHaveNoViolations();
  });
});
```

Note: `vitest-axe` should already be a project dev-dep (the codebase uses jest-axe equivalents). Verify by checking `apps/web/package.json` — if absent, use the actual axe import name the project uses (likely `vitest-axe` or `jest-axe`). If the project lacks any axe matchers, **skip Step 3** (a11y was not a hard AC blocker) and add a follow-up issue.

- [ ] **Step 4: Run full suite to verify everything still passes**

Run:
```bash
pnpm --filter @meepleai/web typecheck
pnpm --filter @meepleai/web test -- AdvancedFiltersDrawer
pnpm --filter @meepleai/web lint
```

Expected: PASS on all three. ~19 tests in the component test file (17 prior + 2 axe).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/features/library/AdvancedFiltersDrawer/index.ts \
       apps/web/src/components/features/library/AdvancedFiltersDrawer/AdvancedFiltersDrawer.stories.tsx \
       apps/web/src/components/features/library/AdvancedFiltersDrawer/__tests__/AdvancedFiltersDrawer.test.tsx
git commit -m "feat(library): #1606 add barrel, 6 storybook stories, jest-axe tests (Phase 3a)"
```

---

## Acceptance check for Phase 3a

After all 8 tasks committed, verify against issue #1606 AC:

- **AC4a** — Drawer opens / closes / Esc handling → ✅ delegated to `Drawer` primitive (Vaul + Radix focus-trap, well-tested)
- **AC4b** — Entity-conditional sections per `entityScope` → ✅ Tasks 4-6 (5 scopes verified)
- **AC4c** — Apply / Clear / Cancel callbacks → ✅ Task 7 (3 callback tests + draft reset effect)
- **AC4d** — Bundle ≤ +60KB → **manual verification step**: `cd apps/web && pnpm build` then inspect the `.next/analyze/` output or use `next-bundle-analyzer` to confirm. The component is ~250 LOC + reuses `Drawer` (already in bundle) + Radix/Vaul (already in bundle) — well within budget.
- **AC4e** — 6 Storybook stories (1 per `entityScope` + 1 pre-populated game variant) → ✅ Task 8
- **AC7** — i18n it+en for all labels + Apply/Clear/Cancel; jest-axe clean → ✅ Task 3 (i18n catalog) + Task 8 (axe)

Run the final full check before PR:

```bash
pnpm --filter @meepleai/web typecheck
pnpm --filter @meepleai/web test -- AdvancedFiltersDrawer
pnpm --filter @meepleai/web lint
```

All three must pass.

---

## Out of scope (deferred to Phase 3b #1593)

- Integration of `AdvancedFiltersDrawer` into `LibraryHub` state (chip "Più filtri N" trigger, parent-side state, badge count updates)
- `RecentActivityRail` cross-entity (deps BE-3 #1590 schema)
- Population of `availableAgentTypes` / `availableSessionStatuses` for the `checkbox-group` sections that currently have empty `options: []` — Phase 3b will inject these from real BE data, here we ship with empty options so the section structure is in place

This file landing on `main-dev` means Phase 3b can `import { AdvancedFiltersDrawer } from '@/components/features/library/AdvancedFiltersDrawer'` and wire it without touching the standalone code.
