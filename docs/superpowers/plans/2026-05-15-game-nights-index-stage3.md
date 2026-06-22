# game-nights-index Stage 3 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement 8 v2 components for the `/game-nights` index route, matching the `sp4-game-nights-index` mockup 1:1 (visual diff ≤2%), with WCAG 2.1 AA compliance and full state-matrix coverage (default/empty/loading/filtered-empty/error). Replaces the legacy `Card`/`Badge` based `_content.tsx`.

**Architecture:** Eight presentational components live under `apps/web/src/components/features/game-nights/` (stubs already exist from PR #632). Pure-function helpers (calendar grid builder, event filter FSM, list grouping) live under `apps/web/src/lib/game-nights/`. The page orchestrator `app/(authenticated)/game-nights/_content.tsx` is refactored to consume `useUpcomingGameNights`/`useMyGameNights`, map to `GameNightDto`-derived view models, and route Calendar/List toggle via the `?view=` URL search param. Filter state (`Tutte/Organizzo/Invitato/Concluse`) routes via `?filter=`. i18n strings go under `pages.gameNightsIndex` in `locales/{it,en}.json`.

**Tech Stack:** React 19, TypeScript, Tailwind 4 (entity utilities `bg-entity-event`/`text-entity-event`/etc, no hex/rgb), `clsx`, `lucide-react`, `next-intl` translations, React Query (already wired). Vitest + RTL for unit. Playwright + axe-core for E2E and a11y.

**Branch (already created):** `feature/issue-1170-game-nights-index-stage3` (parent: `main-dev`)

**Refs:** Issue #1170, Audit #1168, Stage 3 umbrella #1026, Stage 2 path migration #1025, Original (superseded) #685, Sibling cluster #951 (PR #1171/#1172), Mockup PR #640.

---

## File Structure

| File | Responsibility |
|---|---|
| `apps/web/src/lib/game-nights/calendar-grid.ts` | Pure: build 42-cell month grid (Mon-first), classify other-month vs current |
| `apps/web/src/lib/game-nights/event-filter.ts` | Pure: `FilterKey` union + `filterEvents()` predicate (Tutte/Organizzo/Invitato/Concluse) |
| `apps/web/src/lib/game-nights/event-grouping.ts` | Pure: group sorted events by month for list view |
| `apps/web/src/lib/game-nights/view-model.ts` | Pure: `GameNightDto` → `GameNightVM` (derives day/month/status-key/role) |
| `apps/web/src/lib/game-nights/__tests__/calendar-grid.test.ts` | Vitest unit tests |
| `apps/web/src/lib/game-nights/__tests__/event-filter.test.ts` | Vitest unit tests |
| `apps/web/src/lib/game-nights/__tests__/event-grouping.test.ts` | Vitest unit tests |
| `apps/web/src/lib/game-nights/__tests__/view-model.test.ts` | Vitest unit tests |
| `apps/web/src/components/features/game-nights/GameNightsHeader.tsx` | Title row + count + Calendar/List toggle (animated underline) + filter pills + "Nuova serata" CTA |
| `apps/web/src/components/features/game-nights/FilterPillBar.tsx` | 4 filter pills as `<button aria-pressed>` group |
| `apps/web/src/components/features/game-nights/StatusPill.tsx` | Status chip (Confermata/In programma/Annullata/Conclusa) |
| `apps/web/src/components/features/game-nights/PlayerAvatars.tsx` | Avatar overlap stack with "+N" overflow |
| `apps/web/src/components/features/game-nights/CalendarDayCell.tsx` | Day cell w/ max 3 event chips + overflow "+N altri" |
| `apps/web/src/components/features/game-nights/CalendarMonthGrid.tsx` | 7-col grid + month nav (visual only, no fetch) + legend |
| `apps/web/src/components/features/game-nights/GameNightListCard.tsx` | List card: date block + body + status pill + meta + contextual CTA |
| `apps/web/src/components/features/game-nights/DayDetailDrawer.tsx` | Slide-right (desktop) / bottom-sheet (mobile) drawer w/ focus trap |
| `apps/web/src/components/features/game-nights/index.ts` | Barrel export |
| `apps/web/src/components/features/game-nights/__tests__/*.test.tsx` | One test file per component (8 files) |
| `apps/web/src/app/(authenticated)/game-nights/_content.tsx` | REWRITE: orchestrator consuming hooks + components |
| `apps/web/src/app/(authenticated)/game-nights/__tests__/_content.test.tsx` | NEW: orchestrator integration test |
| `apps/web/src/locales/it.json` | UPDATED: `pages.gameNightsIndex` namespace |
| `apps/web/src/locales/en.json` | UPDATED: `pages.gameNightsIndex` namespace |
| `apps/web/e2e/v2-states/game-nights-index.spec.ts` | Playwright state matrix + a11y axe-core |
| `apps/web/e2e/visual-conformity/game-nights-index.spec.ts` | Visual regression Desktop 1280 + Mobile 375 |
| `docs/for-developers/frontend/v2-migration-matrix.md` | UPDATED: 8 rows status `done` + PR ref |

The stub files at `components/features/game-nights/*.tsx` (8 files × 14 LOC, from PR #632) will be overwritten with real implementations.

---

## Preamble — Commit the plan itself

```bash
git add docs/superpowers/plans/2026-05-15-game-nights-index-stage3.md
git commit -m "docs(plans): game-nights-index Stage 3 implementation plan (refs #1170)"
```

---

## Commit 1 — Foundation: i18n + lib helpers

### Task 1.1 — Calendar grid helper (pure)

**Files:**
- Create: `apps/web/src/lib/game-nights/calendar-grid.ts`
- Create: `apps/web/src/lib/game-nights/__tests__/calendar-grid.test.ts`

- [ ] **Step 1.1.1: Write the failing test**

```ts
// apps/web/src/lib/game-nights/__tests__/calendar-grid.test.ts
import { describe, expect, it } from 'vitest';
import { buildMonthGrid, type MonthCell } from '../calendar-grid';

describe('buildMonthGrid', () => {
  it('returns exactly 42 cells (6 weeks)', () => {
    const cells = buildMonthGrid(2026, 2); // March 2026
    expect(cells).toHaveLength(42);
  });

  it('starts on Monday (ISO week)', () => {
    // Mar 1 2026 = Sunday → grid starts Mon Feb 23
    const cells = buildMonthGrid(2026, 2);
    expect(cells[0]).toMatchObject({ day: 23, otherMonth: true, monthOffset: -1 });
    expect(cells[6]).toMatchObject({ day: 1, otherMonth: false, monthOffset: 0 });
  });

  it('flags otherMonth correctly for trailing days', () => {
    const cells = buildMonthGrid(2026, 2);
    const last = cells[41];
    expect(last.otherMonth).toBe(true);
    expect(last.monthOffset).toBe(1);
  });

  it('handles month with Monday start (no prefix days)', () => {
    // Jun 2026 starts on Monday → no Feb-tail cells
    const cells = buildMonthGrid(2026, 5);
    expect(cells[0]).toMatchObject({ day: 1, monthOffset: 0 });
  });
});
```

Run: `cd apps/web && pnpm vitest run src/lib/game-nights/__tests__/calendar-grid.test.ts`
Expected: FAIL — module not found.

- [ ] **Step 1.1.2: Implement**

```ts
// apps/web/src/lib/game-nights/calendar-grid.ts
/**
 * Pure helper to build a 42-cell (6×7) month grid starting Monday.
 * Used by CalendarMonthGrid; no I/O, no React.
 */
export interface MonthCell {
  /** Day-of-month (1..31) */
  readonly day: number;
  /** True for prefix (prev month) and suffix (next month) cells */
  readonly otherMonth: boolean;
  /** -1 = prev month, 0 = current, +1 = next month */
  readonly monthOffset: -1 | 0 | 1;
}

/**
 * Build a 42-cell month grid (6 weeks × 7 days) Monday-first.
 *
 * @param year  4-digit year
 * @param month 0-indexed month (0=Jan, 11=Dec) — matches `Date` constructor.
 */
export function buildMonthGrid(year: number, month: number): MonthCell[] {
  const firstOfMonth = new Date(year, month, 1);
  // JS getDay(): 0=Sun, 1=Mon, ..., 6=Sat → ISO Mon-first offset:
  // Sun(0) → 6, Mon(1) → 0, Tue(2) → 1, ..., Sat(6) → 5
  const isoOffset = (firstOfMonth.getDay() + 6) % 7;
  const daysInPrev = new Date(year, month, 0).getDate();
  const daysInCurr = new Date(year, month + 1, 0).getDate();

  const cells: MonthCell[] = [];

  // Prev-month prefix
  for (let i = isoOffset; i > 0; i--) {
    cells.push({ day: daysInPrev - i + 1, otherMonth: true, monthOffset: -1 });
  }
  // Current month
  for (let d = 1; d <= daysInCurr; d++) {
    cells.push({ day: d, otherMonth: false, monthOffset: 0 });
  }
  // Next-month suffix to fill 42
  let pad = 42 - cells.length;
  for (let d = 1; d <= pad; d++) {
    cells.push({ day: d, otherMonth: true, monthOffset: 1 });
  }
  return cells;
}
```

Run: `cd apps/web && pnpm vitest run src/lib/game-nights/__tests__/calendar-grid.test.ts`
Expected: PASS (4 tests).

### Task 1.2 — Event filter FSM (pure)

**Files:**
- Create: `apps/web/src/lib/game-nights/event-filter.ts`
- Create: `apps/web/src/lib/game-nights/__tests__/event-filter.test.ts`

- [ ] **Step 1.2.1: Write the failing test**

```ts
// apps/web/src/lib/game-nights/__tests__/event-filter.test.ts
import { describe, expect, it } from 'vitest';
import { filterEvents, FILTER_KEYS, isFilterKey, type FilterKey } from '../event-filter';
import type { GameNightVM } from '../view-model';

const vm = (id: string, role: 'organizer' | 'invited', status: GameNightVM['statusKey']): GameNightVM => ({
  id, role, statusKey: status, title: id, day: 1, month: 0, year: 2026,
  timeLabel: '19:00', durationLabel: '2h', location: '', gameId: null,
  gameTitle: null, playerIds: [], scheduledAtIso: '',
});

describe('FILTER_KEYS', () => {
  it('exposes 4 keys', () => {
    expect(FILTER_KEYS).toEqual(['all', 'organizing', 'invited', 'completed']);
  });
});

describe('isFilterKey', () => {
  it.each([
    ['all', true], ['organizing', true], ['invited', true], ['completed', true],
    ['unknown', false], ['', false], [null, false],
  ])('isFilterKey(%p) === %p', (input, expected) => {
    expect(isFilterKey(input)).toBe(expected);
  });
});

describe('filterEvents', () => {
  const events: GameNightVM[] = [
    vm('a', 'organizer', 'confirmed'),
    vm('b', 'invited', 'planned'),
    vm('c', 'organizer', 'completed'),
    vm('d', 'invited', 'cancelled'),
  ];

  it('all → returns all', () => {
    expect(filterEvents(events, 'all').map(e => e.id)).toEqual(['a', 'b', 'c', 'd']);
  });
  it('organizing → only organizer role', () => {
    expect(filterEvents(events, 'organizing').map(e => e.id)).toEqual(['a', 'c']);
  });
  it('invited → only invited role', () => {
    expect(filterEvents(events, 'invited').map(e => e.id)).toEqual(['b', 'd']);
  });
  it('completed → only status=completed', () => {
    expect(filterEvents(events, 'completed').map(e => e.id)).toEqual(['c']);
  });
});
```

Run: FAIL.

- [ ] **Step 1.2.2: Implement**

```ts
// apps/web/src/lib/game-nights/event-filter.ts
import type { GameNightVM } from './view-model';

export const FILTER_KEYS = ['all', 'organizing', 'invited', 'completed'] as const;
export type FilterKey = (typeof FILTER_KEYS)[number];

export function isFilterKey(value: unknown): value is FilterKey {
  return typeof value === 'string' && (FILTER_KEYS as readonly string[]).includes(value);
}

export function filterEvents(events: readonly GameNightVM[], key: FilterKey): GameNightVM[] {
  switch (key) {
    case 'all':
      return [...events];
    case 'organizing':
      return events.filter(e => e.role === 'organizer');
    case 'invited':
      return events.filter(e => e.role === 'invited');
    case 'completed':
      return events.filter(e => e.statusKey === 'completed');
  }
}
```

Run: PASS.

### Task 1.3 — View model adapter (pure)

**Files:**
- Create: `apps/web/src/lib/game-nights/view-model.ts`
- Create: `apps/web/src/lib/game-nights/__tests__/view-model.test.ts`

- [ ] **Step 1.3.1: Check existing GameNightDto shape**

Read `apps/web/src/lib/api/schemas/game-nights.schemas.ts` and grep for `GameNightDto` to confirm field names (`id`, `title`, `scheduledAt`, `organizerId`, `organizerName`, `location`, `status`, `acceptedCount`, `pendingCount`, `maxPlayers`, plus user-relative `isOrganizer` / `userRsvpStatus` if present).

- [ ] **Step 1.3.2: Write the failing test**

```ts
// apps/web/src/lib/game-nights/__tests__/view-model.test.ts
import { describe, expect, it } from 'vitest';
import { toGameNightVM } from '../view-model';
import type { GameNightDto } from '@/lib/api/schemas/game-nights.schemas';

const baseDto: GameNightDto = {
  id: 'gn-1',
  title: 'Wingspan night',
  description: null,
  scheduledAt: '2026-03-15T19:00:00Z',
  organizerId: 'u-marco',
  organizerName: 'Marco',
  location: 'Casa Marco',
  status: 'Published',
  maxPlayers: 5,
  acceptedCount: 3,
  pendingCount: 1,
  declinedCount: 0,
  gameId: null,
  gameTitle: null,
  isOrganizer: true,
  viewerRsvpStatus: null,
};

describe('toGameNightVM', () => {
  it('derives day/month/year from scheduledAt', () => {
    const vm = toGameNightVM(baseDto);
    expect(vm.day).toBe(15);
    expect(vm.month).toBe(2); // 0-indexed March
    expect(vm.year).toBe(2026);
  });
  it('derives role = organizer when isOrganizer=true', () => {
    expect(toGameNightVM({ ...baseDto, isOrganizer: true }).role).toBe('organizer');
  });
  it('derives role = invited when isOrganizer=false', () => {
    expect(toGameNightVM({ ...baseDto, isOrganizer: false }).role).toBe('invited');
  });
  it('maps Status enum to statusKey', () => {
    expect(toGameNightVM({ ...baseDto, status: 'Published' }).statusKey).toBe('planned');
    expect(toGameNightVM({ ...baseDto, status: 'Cancelled' }).statusKey).toBe('cancelled');
    expect(toGameNightVM({ ...baseDto, status: 'Completed' }).statusKey).toBe('completed');
    expect(toGameNightVM({ ...baseDto, status: 'Draft' }).statusKey).toBe('planned');
  });
  it('promotes Published → confirmed when accepted ≥ 3', () => {
    expect(toGameNightVM({ ...baseDto, status: 'Published', acceptedCount: 3 }).statusKey).toBe('confirmed');
    expect(toGameNightVM({ ...baseDto, status: 'Published', acceptedCount: 2 }).statusKey).toBe('planned');
  });
});
```

Run: FAIL.

- [ ] **Step 1.3.3: Implement**

```ts
// apps/web/src/lib/game-nights/view-model.ts
import type { GameNightDto, GameNightStatus } from '@/lib/api/schemas/game-nights.schemas';

export type StatusKey = 'confirmed' | 'planned' | 'cancelled' | 'completed';
export type RoleKey = 'organizer' | 'invited';

export interface GameNightVM {
  readonly id: string;
  readonly title: string;
  readonly scheduledAtIso: string;
  /** 1..31 */
  readonly day: number;
  /** 0-indexed month (0=Jan) */
  readonly month: number;
  readonly year: number;
  /** Hour:Minute formatted locally */
  readonly timeLabel: string;
  /** Free-form duration ("2h", "3h 30m"); empty if unknown */
  readonly durationLabel: string;
  readonly location: string;
  readonly gameId: string | null;
  readonly gameTitle: string | null;
  readonly playerIds: readonly string[];
  readonly role: RoleKey;
  readonly statusKey: StatusKey;
}

const CONFIRMED_THRESHOLD = 3;

function deriveStatusKey(dto: GameNightDto): StatusKey {
  switch (dto.status) {
    case 'Cancelled':
      return 'cancelled';
    case 'Completed':
      return 'completed';
    case 'Published':
      return dto.acceptedCount >= CONFIRMED_THRESHOLD ? 'confirmed' : 'planned';
    case 'Draft':
    default:
      return 'planned';
  }
}

export function toGameNightVM(dto: GameNightDto): GameNightVM {
  const date = new Date(dto.scheduledAt);
  const hh = String(date.getHours()).padStart(2, '0');
  const mm = String(date.getMinutes()).padStart(2, '0');
  return {
    id: dto.id,
    title: dto.title,
    scheduledAtIso: dto.scheduledAt,
    day: date.getDate(),
    month: date.getMonth(),
    year: date.getFullYear(),
    timeLabel: `${hh}:${mm}`,
    durationLabel: '',
    location: dto.location ?? '',
    gameId: dto.gameId ?? null,
    gameTitle: dto.gameTitle ?? null,
    playerIds: [],
    role: dto.isOrganizer ? 'organizer' : 'invited',
    statusKey: deriveStatusKey(dto),
  };
}
```

> **Note:** `durationLabel` and `playerIds` are intentionally empty in the VM until backend exposes them. The mockup shows them; we degrade gracefully (components hide the meta row if `playerIds.length === 0`).

Run: PASS.

### Task 1.4 — Event grouping helper (pure)

**Files:**
- Create: `apps/web/src/lib/game-nights/event-grouping.ts`
- Create: `apps/web/src/lib/game-nights/__tests__/event-grouping.test.ts`

- [ ] **Step 1.4.1: Write the failing test**

```ts
import { describe, expect, it } from 'vitest';
import { groupByMonth, type MonthGroup } from '../event-grouping';
import type { GameNightVM } from '../view-model';

const make = (id: string, year: number, month: number, day: number): GameNightVM => ({
  id, title: id, scheduledAtIso: '', day, month, year,
  timeLabel: '19:00', durationLabel: '', location: '', gameId: null,
  gameTitle: null, playerIds: [], role: 'invited', statusKey: 'planned',
});

describe('groupByMonth', () => {
  it('groups events by (year, month) sorted newest-first', () => {
    const events = [make('a', 2026, 0, 5), make('b', 2026, 2, 10), make('c', 2026, 2, 1)];
    const groups = groupByMonth(events);
    expect(groups.map(g => `${g.year}-${g.month}`)).toEqual(['2026-2', '2026-0']);
  });
  it('sorts items within current month ASCENDING by day', () => {
    const events = [make('a', 2026, 2, 20), make('b', 2026, 2, 5), make('c', 2026, 2, 15)];
    const now = new Date(2026, 2, 12);
    const groups = groupByMonth(events, now);
    expect(groups[0].items.map(e => e.day)).toEqual([5, 15, 20]);
  });
  it('sorts items in past months DESCENDING by day', () => {
    const events = [make('a', 2026, 1, 5), make('b', 2026, 1, 20)];
    const now = new Date(2026, 2, 12);
    const groups = groupByMonth(events, now);
    expect(groups[0].items.map(e => e.day)).toEqual([20, 5]);
  });
  it('returns empty array for empty input', () => {
    expect(groupByMonth([])).toEqual([]);
  });
});
```

- [ ] **Step 1.4.2: Implement**

```ts
// apps/web/src/lib/game-nights/event-grouping.ts
import type { GameNightVM } from './view-model';

export interface MonthGroup {
  readonly year: number;
  /** 0-indexed (0=Jan) */
  readonly month: number;
  readonly items: readonly GameNightVM[];
}

export function groupByMonth(events: readonly GameNightVM[], now: Date = new Date()): MonthGroup[] {
  if (events.length === 0) return [];
  const buckets = new Map<string, GameNightVM[]>();
  for (const e of events) {
    const key = `${e.year}-${e.month}`;
    const arr = buckets.get(key) ?? [];
    arr.push(e);
    buckets.set(key, arr);
  }
  const currentY = now.getFullYear();
  const currentM = now.getMonth();
  return [...buckets.entries()]
    .map(([key, items]) => {
      const [y, m] = key.split('-').map(Number);
      const isCurrentOrFuture = y > currentY || (y === currentY && m >= currentM);
      const sorted = [...items].sort((a, b) => (isCurrentOrFuture ? a.day - b.day : b.day - a.day));
      return { year: y, month: m, items: sorted };
    })
    .sort((a, b) => (b.year - a.year) * 100 + (b.month - a.month));
}
```

Run: PASS.

### Task 1.5 — i18n keys

**Files:**
- Modify: `apps/web/src/locales/it.json`
- Modify: `apps/web/src/locales/en.json`

- [ ] **Step 1.5.1: Add `pages.gameNightsIndex` namespace to both locales**

Italian (`it.json`) — insert after `pages.gameNightDetail`:

```json
"gameNightsIndex": {
  "header": {
    "kicker": "Game nights",
    "title": "Serate di gioco",
    "countLine": "{total} serate questo mese · {confirmed} confermate",
    "ctaNew": "Nuova serata"
  },
  "view": {
    "tablistAriaLabel": "Vista serate",
    "calendar": "Calendario",
    "list": "Lista"
  },
  "filter": {
    "ariaLabel": "Filtra per ruolo",
    "all": "Tutte",
    "organizing": "Organizzo",
    "invited": "Invitato",
    "completed": "Concluse"
  },
  "status": {
    "confirmed": "Confermata",
    "planned": "In programma",
    "cancelled": "Annullata",
    "completed": "Conclusa"
  },
  "calendar": {
    "prevMonth": "Mese precedente",
    "nextMonth": "Mese successivo",
    "today": "Oggi",
    "todayBadge": "Oggi",
    "dayLabels": "Lun,Mar,Mer,Gio,Ven,Sab,Dom",
    "dayAriaLabel": "Giorno {day}{events, plural, =0 {} =1 {, 1 evento} other {, # eventi}}",
    "overflow": "+{count} altri",
    "footerCount": "{total} serate · {cancelled} annullate",
    "legend": { "event": "Serata", "cancelled": "Annullata", "today": "Oggi" }
  },
  "list": {
    "groupSubCurrent": "Mese corrente",
    "groupSubPast": "Storico",
    "groupCount": "{count} {count, plural, =1 {serata} other {serate}}",
    "participants": "{count} {count, plural, =1 {partecipante} other {partecipanti}}",
    "organizingBadge": "● Organizzo",
    "cta": {
      "edit": "Modifica",
      "viewSummary": "Vedi summary →",
      "reschedule": "Riprogramma",
      "accept": "✓ Partecipo",
      "maybe": "Forse"
    }
  },
  "drawer": {
    "title": "{day} {month} {year}",
    "subtitle": "{count} {count, plural, =1 {serata} other {serate}} in programma",
    "close": "Chiudi",
    "addOnDay": "+ Aggiungi serata in questo giorno"
  },
  "states": {
    "empty": {
      "title": "Nessuna serata in programma",
      "body": "Crea la prima serata per organizzare le tue partite, invitare il gruppo e tenere traccia di chi gioca a cosa.",
      "cta": "+ Crea la prima serata"
    },
    "filteredEmpty": {
      "title": "Nessuna serata in questo filtro",
      "body": "Cambia filtro o crea una nuova serata."
    },
    "error": {
      "title": "Impossibile caricare le serate",
      "body": "C'è stato un problema. Riprova fra un momento.",
      "retry": "Riprova"
    }
  }
}
```

English (`en.json`) — mirror with translated values: `"title": "Game Nights"`, `"all": "All"`, `"organizing": "Organizing"`, etc.

- [ ] **Step 1.5.2: Run typecheck**

```bash
cd apps/web && pnpm typecheck
```
Expected: PASS (no JSON-typed translation key errors).

### Task 1.6 — Commit foundation

- [ ] **Step 1.6.1: Commit**

```bash
git add apps/web/src/lib/game-nights/ apps/web/src/locales/it.json apps/web/src/locales/en.json
git commit -m "feat(game-nights-index): foundation helpers + i18n (refs #1170)"
```

---

## Commit 2 — Components family (8 components + tests)

> **Pattern:** For each component, write the test first, then implement. Use Tailwind entity utilities (`bg-entity-event`, `text-entity-event`, `border-entity-event/30`, etc.) — NO hex/rgb (ESLint `local/no-hardcoded-color-utility` is error-level). Use semantic tokens (`bg-card`, `text-foreground`, `text-muted-foreground`, `border-border`, `border-border-strong`).

### Task 2.1 — StatusPill (smallest, start here)

**Files:**
- Modify: `apps/web/src/components/features/game-nights/StatusPill.tsx` (replace 14-LOC stub)
- Create: `apps/web/src/components/features/game-nights/__tests__/StatusPill.test.tsx`

- [ ] **Step 2.1.1: Write the failing test**

```tsx
// apps/web/src/components/features/game-nights/__tests__/StatusPill.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { renderWithI18n } from '@/test-utils/test-i18n';
import { StatusPill } from '../StatusPill';

describe('StatusPill', () => {
  it.each([
    ['confirmed', 'Confermata'],
    ['planned', 'In programma'],
    ['cancelled', 'Annullata'],
    ['completed', 'Conclusa'],
  ] as const)('renders %s label', (statusKey, label) => {
    renderWithI18n(<StatusPill statusKey={statusKey} />);
    expect(screen.getByText(label)).toBeInTheDocument();
  });

  it('renders an aria-hidden dot', () => {
    const { container } = renderWithI18n(<StatusPill statusKey="confirmed" />);
    const dot = container.querySelector('[aria-hidden="true"]');
    expect(dot).toBeInTheDocument();
  });
});
```

- [ ] **Step 2.1.2: Implement**

```tsx
// apps/web/src/components/features/game-nights/StatusPill.tsx
'use client';

import clsx from 'clsx';
import { useTranslations } from 'next-intl';

import type { StatusKey } from '@/lib/game-nights/view-model';

const VARIANT: Record<StatusKey, string> = {
  confirmed: 'bg-entity-toolkit/12 text-entity-toolkit border-entity-toolkit/30',
  planned: 'bg-entity-agent/12 text-entity-agent border-entity-agent/30',
  cancelled: 'bg-destructive/12 text-destructive border-destructive/30',
  completed: 'bg-muted text-muted-foreground border-border',
};

const DOT: Record<StatusKey, string> = {
  confirmed: 'bg-entity-toolkit',
  planned: 'bg-entity-agent',
  cancelled: 'bg-destructive',
  completed: 'bg-muted-foreground',
};

export interface StatusPillProps {
  readonly statusKey: StatusKey;
  readonly className?: string;
}

export function StatusPill({ statusKey, className }: StatusPillProps): JSX.Element {
  const t = useTranslations('pages.gameNightsIndex.status');
  return (
    <span
      className={clsx(
        'inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5',
        'font-mono text-[10px] font-extrabold uppercase tracking-wider',
        VARIANT[statusKey],
        className,
      )}
    >
      <span aria-hidden="true" className={clsx('h-1.5 w-1.5 rounded-full', DOT[statusKey])} />
      <span>{t(statusKey)}</span>
    </span>
  );
}
```

Run: `cd apps/web && pnpm vitest run src/components/features/game-nights/__tests__/StatusPill.test.tsx`
Expected: PASS.

### Task 2.2 — PlayerAvatars

**Files:**
- Modify: `apps/web/src/components/features/game-nights/PlayerAvatars.tsx`
- Create: `apps/web/src/components/features/game-nights/__tests__/PlayerAvatars.test.tsx`

- [ ] **Step 2.2.1: Write test**

```tsx
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { PlayerAvatars } from '../PlayerAvatars';

describe('PlayerAvatars', () => {
  it('renders up to `max` initials', () => {
    render(<PlayerAvatars players={[{ id: 'a', initials: 'AA' }, { id: 'b', initials: 'BB' }]} max={5} />);
    expect(screen.getByText('AA')).toBeInTheDocument();
    expect(screen.getByText('BB')).toBeInTheDocument();
  });

  it('shows +N overflow when players exceed max', () => {
    const players = [1,2,3,4,5,6,7].map(n => ({ id: `p${n}`, initials: `P${n}` }));
    render(<PlayerAvatars players={players} max={5} />);
    expect(screen.getByText('+2')).toBeInTheDocument();
  });

  it('renders nothing for empty array', () => {
    const { container } = render(<PlayerAvatars players={[]} max={5} />);
    expect(container.firstChild?.childNodes).toHaveLength(0);
  });
});
```

- [ ] **Step 2.2.2: Implement**

```tsx
// apps/web/src/components/features/game-nights/PlayerAvatars.tsx
import clsx from 'clsx';

export interface AvatarPlayer {
  readonly id: string;
  readonly initials: string;
  readonly name?: string;
}

export interface PlayerAvatarsProps {
  readonly players: readonly AvatarPlayer[];
  readonly max?: number;
  readonly className?: string;
}

/** Deterministic hue from id (0..359) */
function hueFromId(id: string): number {
  let h = 0;
  for (let i = 0; i < id.length; i++) h = (h * 31 + id.charCodeAt(i)) | 0;
  return Math.abs(h) % 360;
}

export function PlayerAvatars({ players, max = 5, className }: PlayerAvatarsProps): JSX.Element {
  const list = players.slice(0, max);
  const extra = players.length - max;
  return (
    <div className={clsx('inline-flex items-center', className)}>
      {list.map((p, i) => (
        <span
          key={`${p.id}-${i}`}
          title={p.name ?? p.id}
          className={clsx(
            'inline-flex h-6 w-6 items-center justify-center rounded-full border-2 border-card',
            'font-display text-[10px] font-extrabold text-white',
            i === 0 ? '' : '-ml-2',
          )}
          style={{ background: `hsl(${hueFromId(p.id)} 55% 50%)`, zIndex: max - i }}
        >
          {p.initials}
        </span>
      ))}
      {extra > 0 && (
        <span
          className={clsx(
            '-ml-2 inline-flex h-6 w-6 items-center justify-center rounded-full',
            'border-2 border-card bg-muted text-muted-foreground',
            'font-mono text-[10px] font-extrabold',
          )}
          style={{ zIndex: 0 }}
        >
          +{extra}
        </span>
      )}
    </div>
  );
}
```

Run: PASS.

### Task 2.3 — FilterPillBar

**Files:**
- Modify: `apps/web/src/components/features/game-nights/FilterPillBar.tsx`
- Create: `apps/web/src/components/features/game-nights/__tests__/FilterPillBar.test.tsx`

- [ ] **Step 2.3.1: Write test**

Test: renders 4 pills, active pill has `aria-pressed="true"`, click invokes callback with key.

- [ ] **Step 2.3.2: Implement**

Props: `{ value: FilterKey, onChange: (k: FilterKey) => void, className?: string }`.

Uses `useTranslations('pages.gameNightsIndex.filter')`. Iterates `FILTER_KEYS`, each rendered as `<button type="button" aria-pressed={key === value} onClick={() => onChange(key)}>{t(key)}</button>` with active styling `bg-entity-event/15 text-entity-event border-entity-event/40` and inactive `bg-card text-muted-foreground border-border`.

Wrap in `<div role="group" aria-label={t('ariaLabel')}>`.

### Task 2.4 — GameNightsHeader

**Files:**
- Modify: `apps/web/src/components/features/game-nights/GameNightsHeader.tsx`
- Create test

Props:
```ts
interface GameNightsHeaderProps {
  totalThisMonth: number;
  confirmedThisMonth: number;
  view: 'calendar' | 'list';
  onViewChange: (view: 'calendar' | 'list') => void;
  filter: FilterKey;
  onFilterChange: (filter: FilterKey) => void;
  onCreate: () => void;
  isMobile?: boolean;
}
```

Layout (mobile-first, no `window.innerWidth` — use `isMobile` prop or CSS-only via Tailwind `md:` modifiers):
- Kicker pill (event entity) + h1 + count line + "+ Nuova serata" CTA (entity-event → entity-session gradient)
- Calendar/List tablist (animated underline using `useTablistKeyboardNav` if available, else custom indicator via `useState` + refs)
- Embedded `<FilterPillBar />`

Test: snapshot ARIA tablist semantics, view-toggle click, filter-change click.

### Task 2.5 — CalendarDayCell

**Files:**
- Modify: `apps/web/src/components/features/game-nights/CalendarDayCell.tsx`
- Create test

Props:
```ts
interface CalendarDayCellProps {
  cell: MonthCell;
  events: readonly GameNightVM[];
  isToday: boolean;
  isMobile?: boolean;
  onClick?: (cell: MonthCell, events: readonly GameNightVM[]) => void;
}
```

Render `<button type="button" disabled={cell.otherMonth} aria-label={t('calendar.dayAriaLabel', { day: cell.day, events: events.length })}>`. Inside: day number (entity-event color if today), "Oggi" mini-badge if today (desktop only), then up to `max` event chips (max=1 mobile, max=3 desktop), plus `+N altri` overflow.

Use `bg-entity-event/5` for cells with events, `border-entity-event` (2px) for today, else `border-border`. Cancelled events: `line-through`, destructive bg.

Test: 0 events / 1 event / 4 events → overflow text; today branch; otherMonth disabled.

### Task 2.6 — CalendarMonthGrid

**Files:**
- Modify: `apps/web/src/components/features/game-nights/CalendarMonthGrid.tsx`
- Create test

Props:
```ts
interface CalendarMonthGridProps {
  year: number;
  month: number; // 0-indexed
  events: readonly GameNightVM[]; // already filtered
  today?: Date;
  isMobile?: boolean;
  onDayClick: (cell: MonthCell, events: readonly GameNightVM[]) => void;
}
```

Composition:
- Month nav row: prev `‹` / `Mese Anno` heading / next `›` / `Oggi` reset / right-side counter ("N serate · M annullate")
- Day labels row (7 cols)
- 7-col grid of 42 `<CalendarDayCell>`
- Legend row: Serata / Annullata / Oggi swatches

`onPrevMonth`/`onNextMonth` are deferred — mockup says these are visual only. Wire them as `disabled`/no-op for now (TODO comment + matrix entry "month-nav purely visual" — acceptable for this scope per issue out-of-scope clause).

> Decision rationale: mockup line 20 confirms "paginazione mese ‹ › è solo visiva, no fetch reale". Backend doesn't yet expose month-by-month query; defer to follow-up.

Test: matches snapshot for known month, day-label localization, today-marker placement, legend render.

### Task 2.7 — GameNightListCard

**Files:**
- Modify: `apps/web/src/components/features/game-nights/GameNightListCard.tsx`
- Create test

Props:
```ts
interface GameNightListCardProps {
  vm: GameNightVM;
  isMobile?: boolean;
  onAction?: (id: string, action: 'edit' | 'viewSummary' | 'reschedule' | 'accept' | 'maybe') => void;
}
```

Sections (mobile vertical / desktop horizontal):
1. Date block: month abbrev + day + time + duration (entity-event tinted)
2. Body:
   - Title (line-through if cancelled) + `<StatusPill>`
   - Meta row: 📍 location + EntityChip(type=game, label=gameTitle) [if `vm.gameId`] + "● Organizzo" badge [if organizer]
   - Footer (border-top): `<PlayerAvatars>` + count + spacer + contextual CTA

CTA logic:
- `completed` → "Vedi summary →" (outlined)
- `cancelled` → "Riprogramma" (muted outlined)
- `confirmed`/`planned` + `role==='organizer'` → "Modifica" (solid event)
- `confirmed`/`planned` + `role==='invited'` → 2 buttons: "✓ Partecipo" (solid toolkit) + "Forse" (outlined)

Card wrapper: `bg-card border-border rounded-lg border-l-4 border-l-entity-event` (or `border-l-destructive` if cancelled), `opacity-70` if cancelled.

Test: 4 role/status branches → 4 different CTA layouts; line-through on cancelled; missing game → no chip.

### Task 2.8 — DayDetailDrawer

**Files:**
- Modify: `apps/web/src/components/features/game-nights/DayDetailDrawer.tsx`
- Create test

Props:
```ts
interface DayDetailDrawerProps {
  open: boolean;
  day: number;
  month: number;
  year: number;
  events: readonly GameNightVM[];
  isMobile?: boolean;
  onClose: () => void;
}
```

Behavior:
- Renders nothing if `!open`
- Backdrop (`absolute inset-0 bg-black/50 z-50`) on click → `onClose()`
- Aside: `w-full` (mobile) bottom-sheet vs `w-[480px]` (desktop) right-sheet
- Header: 50×50 date block (entity-event tinted) + "{day} {month} {year}" + count + close button
- Body: stack of `<GameNightListCard isMobile />` + "+ Aggiungi serata in questo giorno" dashed CTA
- **Focus trap**: use existing `useFocusTrap` hook (search codebase first) or `react-focus-lock` if installed; fallback: manual trap via `tabindex` + focus on close button on open
- **a11y**: `role="dialog"` + `aria-modal="true"` + `aria-labelledby` pointing to header h2 id

Animation: `data-state="open"` + Tailwind `data-[state=open]:animate-in` slide-in-from-bottom (mobile) / slide-in-from-right (desktop). Respect `prefers-reduced-motion` via existing motion utility classes (`motion-safe:` prefix).

Test:
- `open={false}` → renders nothing
- backdrop click → invokes `onClose`
- Escape key → invokes `onClose`
- focus initially lands on close button
- contains all event cards passed in `events`

### Task 2.9 — Barrel export

**Files:**
- Modify: `apps/web/src/components/features/game-nights/index.ts`

```ts
export { CalendarDayCell } from './CalendarDayCell';
export { CalendarMonthGrid } from './CalendarMonthGrid';
export { DayDetailDrawer } from './DayDetailDrawer';
export { FilterPillBar } from './FilterPillBar';
export { GameNightListCard } from './GameNightListCard';
export { GameNightsHeader } from './GameNightsHeader';
export { PlayerAvatars } from './PlayerAvatars';
export { StatusPill } from './StatusPill';
```

### Task 2.10 — Commit components

- [ ] **Step 2.10.1: Run full test for the feature folder**

```bash
cd apps/web && pnpm vitest run src/components/features/game-nights/ src/lib/game-nights/
```
Expected: all green.

- [ ] **Step 2.10.2: Run lint (token guard)**

```bash
cd apps/web && pnpm lint
```
Expected: no `local/no-hardcoded-color-utility` violations.

- [ ] **Step 2.10.3: Commit**

```bash
git add apps/web/src/components/features/game-nights/
git commit -m "feat(game-nights-index): 8 components implementing sp4-game-nights-index mockup (refs #1170)"
```

---

## Commit 3 — Orchestrator + page wiring

### Task 3.1 — Refactor `_content.tsx`

**Files:**
- Modify: `apps/web/src/app/(authenticated)/game-nights/_content.tsx`
- Create: `apps/web/src/app/(authenticated)/game-nights/__tests__/_content.test.tsx`

The new orchestrator:
- Reads `?view=` (default `calendar` desktop, `list` mobile via media query) and `?filter=` (default `all`) from URL
- `useUpcomingGameNights()` + `useMyGameNights()` (existing hooks)
- Choice of source by filter: `all`/`invited`/`completed` → upcoming, `organizing` → mine (or both merged if simpler)
- Maps DTOs → VMs via `toGameNightVM`
- Renders `<GameNightsHeader>` + (calendar branch | list branch) + `<DayDetailDrawer>` for selected day
- Empty state when 0 events total
- Filtered-empty state when filter yields 0
- Error state for query error
- Loading state via existing skeleton OR per-component `stateOverride="loading"` (mockup pattern)

```tsx
// apps/web/src/app/(authenticated)/game-nights/_content.tsx
'use client';

import { useEffect, useMemo, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { useTranslations } from 'next-intl';

import {
  CalendarMonthGrid,
  DayDetailDrawer,
  GameNightListCard,
  GameNightsHeader,
} from '@/components/features/game-nights';
import { useMyGameNights, useUpcomingGameNights } from '@/hooks/queries/useGameNights';
import { FILTER_KEYS, type FilterKey, filterEvents, isFilterKey } from '@/lib/game-nights/event-filter';
import { groupByMonth } from '@/lib/game-nights/event-grouping';
import { toGameNightVM, type GameNightVM } from '@/lib/game-nights/view-model';
import type { MonthCell } from '@/lib/game-nights/calendar-grid';
import { useRecentsStore } from '@/stores/use-recents';

type View = 'calendar' | 'list';

export function GameNightsContent(): JSX.Element {
  const router = useRouter();
  const sp = useSearchParams();
  const t = useTranslations('pages.gameNightsIndex');

  const view: View = sp.get('view') === 'list' ? 'list' : 'calendar';
  const filterRaw = sp.get('filter');
  const filter: FilterKey = isFilterKey(filterRaw) ? filterRaw : 'all';

  const [drawerCell, setDrawerCell] = useState<MonthCell | null>(null);

  useEffect(() => {
    useRecentsStore.getState().push({
      id: 'section-game-nights', entity: 'event',
      title: t('header.title'), href: '/game-nights',
    });
  }, [t]);

  const upcoming = useUpcomingGameNights();
  const mine = useMyGameNights();

  const isLoading = upcoming.isLoading || mine.isLoading;
  const error = upcoming.error || mine.error;

  const allVms: readonly GameNightVM[] = useMemo(() => {
    const combined = new Map<string, GameNightVM>();
    [...(upcoming.data ?? []), ...(mine.data ?? [])].forEach(dto => {
      if (!combined.has(dto.id)) combined.set(dto.id, toGameNightVM(dto));
    });
    return [...combined.values()];
  }, [upcoming.data, mine.data]);

  const filtered = useMemo(() => filterEvents(allVms, filter), [allVms, filter]);

  const today = new Date();
  const totalThisMonth = filtered.filter(e =>
    e.year === today.getFullYear() && e.month === today.getMonth()).length;
  const confirmedThisMonth = filtered.filter(e =>
    e.year === today.getFullYear() && e.month === today.getMonth() && e.statusKey === 'confirmed').length;

  function navigate(params: Record<string, string>) {
    const next = new URLSearchParams(sp.toString());
    for (const [k, v] of Object.entries(params)) next.set(k, v);
    router.replace(`/game-nights?${next.toString()}`, { scroll: false });
  }

  // Render error first (terminal state)
  if (error) {
    return (
      <div className="p-8 text-center">
        <h2 className="font-display text-lg font-bold text-foreground">{t('states.error.title')}</h2>
        <p className="mt-2 text-sm text-muted-foreground">{t('states.error.body')}</p>
        <button
          type="button"
          className="mt-4 rounded-md border border-border-strong px-4 py-2 font-display text-sm font-bold"
          onClick={() => { void upcoming.refetch(); void mine.refetch(); }}
        >
          {t('states.error.retry')}
        </button>
      </div>
    );
  }

  // Empty (no events at all, not just filtered)
  if (!isLoading && allVms.length === 0) {
    return (
      <>
        <GameNightsHeader
          totalThisMonth={0}
          confirmedThisMonth={0}
          view={view}
          onViewChange={v => navigate({ view: v })}
          filter={filter}
          onFilterChange={f => navigate({ filter: f })}
          onCreate={() => router.push('/game-nights/new')}
        />
        <div className="px-8 py-16 text-center">
          <h3 className="font-display text-lg font-bold text-foreground">{t('states.empty.title')}</h3>
          <p className="mx-auto mt-2 max-w-md text-sm text-muted-foreground">{t('states.empty.body')}</p>
          <button
            type="button"
            className="mt-6 rounded-md bg-entity-event px-5 py-2.5 font-display text-sm font-bold text-white"
            onClick={() => router.push('/game-nights/new')}
          >
            {t('states.empty.cta')}
          </button>
        </div>
      </>
    );
  }

  const dayEvents = drawerCell
    ? filtered.filter(e =>
        e.day === drawerCell.day && e.month === today.getMonth() && e.year === today.getFullYear())
    : [];

  return (
    <>
      <GameNightsHeader
        totalThisMonth={totalThisMonth}
        confirmedThisMonth={confirmedThisMonth}
        view={view}
        onViewChange={v => navigate({ view: v })}
        filter={filter}
        onFilterChange={f => navigate({ filter: f })}
        onCreate={() => router.push('/game-nights/new')}
      />

      {view === 'calendar' ? (
        <CalendarMonthGrid
          year={today.getFullYear()}
          month={today.getMonth()}
          events={filtered}
          today={today}
          onDayClick={(cell) => setDrawerCell(cell)}
        />
      ) : (
        <ListView events={filtered} now={today} />
      )}

      <DayDetailDrawer
        open={drawerCell !== null}
        day={drawerCell?.day ?? 0}
        month={today.getMonth()}
        year={today.getFullYear()}
        events={dayEvents}
        onClose={() => setDrawerCell(null)}
      />
    </>
  );
}

function ListView({ events, now }: { events: readonly GameNightVM[]; now: Date }): JSX.Element {
  const t = useTranslations('pages.gameNightsIndex');
  const groups = useMemo(() => groupByMonth(events, now), [events, now]);

  if (groups.length === 0) {
    return (
      <div className="px-8 py-16 text-center">
        <h3 className="font-display text-base font-bold text-foreground">{t('states.filteredEmpty.title')}</h3>
        <p className="mt-1 text-sm text-muted-foreground">{t('states.filteredEmpty.body')}</p>
      </div>
    );
  }

  const monthNames = t.raw('calendar.dayLabels'); // reused below; or hardcode Intl.DateTimeFormat
  return (
    <div className="flex flex-col gap-6 px-8 pb-8 pt-5">
      {groups.map(g => {
        const monthLabel = new Date(g.year, g.month, 1).toLocaleString('it-IT', { month: 'long', year: 'numeric' });
        const isCurrent = g.year === now.getFullYear() && g.month === now.getMonth();
        return (
          <section key={`${g.year}-${g.month}`}>
            <div className="sticky top-0 z-[5] flex items-baseline gap-2.5 bg-background py-2">
              <h2 className="font-display text-lg font-extrabold text-foreground">{monthLabel}</h2>
              <span className="font-mono text-xs font-bold text-muted-foreground">
                {t('list.groupCount', { count: g.items.length })} · {isCurrent ? t('list.groupSubCurrent') : t('list.groupSubPast')}
              </span>
            </div>
            <div className="flex flex-col gap-2.5">
              {g.items.map(n => <GameNightListCard key={n.id} vm={n} />)}
            </div>
          </section>
        );
      })}
    </div>
  );
}

// Loading skeleton kept for backwards-compat with page.tsx Suspense fallback
export function GameNightsLoadingSkeleton(): JSX.Element {
  return (
    <div className="flex flex-col gap-3 px-8 py-6">
      {Array.from({ length: 4 }).map((_, i) => (
        <div key={i} className="h-28 animate-pulse rounded-lg bg-muted" />
      ))}
    </div>
  );
}
```

- [ ] **Step 3.1.1: Orchestrator integration test**

```tsx
// apps/web/src/app/(authenticated)/game-nights/__tests__/_content.test.tsx
import { describe, expect, it, vi } from 'vitest';
// Use existing test harness pattern: wrap in QueryClientProvider + i18n + Router mock
// Mock useUpcomingGameNights to return [], [1 event], or error
// Assert: empty state, header rendering, view=list query toggle
```

(Use the same pattern as `apps/web/src/components/features/game-night-detail/__tests__/` for QueryClient + Next router mocking.)

- [ ] **Step 3.1.2: Run tests**

```bash
cd apps/web && pnpm vitest run src/app/\(authenticated\)/game-nights/
```
Expected: PASS.

- [ ] **Step 3.1.3: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/game-nights/
git commit -m "feat(game-nights-index): orchestrator + page wiring (refs #1170)"
```

---

## Commit 4 — E2E specs (state matrix + visual + a11y)

### Task 4.1 — State coverage E2E

**Files:**
- Create: `apps/web/e2e/v2-states/game-nights-index.spec.ts`

Pattern mirrors `apps/web/e2e/v2-states/game-night-detail.spec.ts`:
- 5 scenarios: default / empty / loading / filtered-empty / error
- Mock `/api/v1/game-nights/upcoming` and `/api/v1/game-nights/mine` via `page.context().route()`
- 3 viewports: 375 (mobile) / 768 (tablet) / 1280 (desktop)
- Each scenario: axe-core scan with `WCAG_TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']` → expect 0 violations
- Assert visible content per state (h1 title, empty CTA, error retry button, etc.)

### Task 4.2 — Visual regression

**Files:**
- Create: `apps/web/e2e/visual-conformity/game-nights-index.spec.ts`

Pattern from sibling Stage 3 specs:
- 2 viewports: Desktop 1280, Mobile 375
- 4 snapshots: calendar-view-desktop, calendar-view-mobile, list-view-desktop, list-view-mobile
- Mock data: fixture matching mockup's NIGHTS (12 events) committed under `apps/web/e2e/_fixtures/game-nights-index.json`
- Threshold: maxDiffPixelRatio: 0.02 (2%)

### Task 4.3 — Run E2E locally

```bash
cd apps/web
pnpm test:e2e -- game-nights-index
```
Expected: PASS. If visual baselines don't exist yet, generate with `--update-snapshots` and commit them.

### Task 4.4 — Commit E2E

```bash
git add apps/web/e2e/
git commit -m "test(game-nights-index): state-matrix + visual-conformity + a11y (refs #1170)"
```

---

## Commit 5 — Cleanup + matrix

### Task 5.1 — Verify no legacy imports remain

```bash
grep -rn "from '@/components/ui/card'" apps/web/src/app/\(authenticated\)/game-nights/ || echo "OK: clean"
grep -rn "GameNightCard\|StatusBadge" apps/web/src/app/\(authenticated\)/game-nights/_content.tsx || echo "OK: legacy removed"
```

Expected: no matches in `_content.tsx` (legacy `GameNightCard`/`StatusBadge`/`EmptyState` inline components deleted; replaced by feature imports).

### Task 5.2 — Update v2-migration-matrix.md

**File:** `docs/for-developers/frontend/v2-migration-matrix.md`

Find the 8 rows for game-nights-index components (referenced in issue #1170 as L:313-324, L:121, L:548) and:
- Set `Status = done`
- Set `PR = #<this-PR-number>` (will be filled after PR open — leave placeholder `#TBD` and update in final commit)

### Task 5.3 — Run full quality gate

```bash
cd apps/web
pnpm typecheck && pnpm lint && pnpm vitest run && pnpm lint:tokens
```
Expected: all green, 0 token violations.

### Task 5.4 — Commit cleanup

```bash
git add apps/web/src/app/\(authenticated\)/game-nights/_content.tsx docs/for-developers/frontend/v2-migration-matrix.md
git commit -m "chore(game-nights-index): remove legacy + update v2 matrix (closes #1170)"
```

### Task 5.5 — Push + open PR

```bash
git push -u origin feature/issue-1170-game-nights-index-stage3
gh pr create --base main-dev --title "feat(game-nights-index): Stage 3 v2 implementation (closes #1170)" --body "$(cat <<'EOF'
## Summary
Implements 8 components for `/game-nights` index matching `sp4-game-nights-index` mockup (closes #1170).

- Foundation: i18n keys + pure helpers (calendar grid, filter FSM, view model, grouping)
- 8 components under `components/features/game-nights/`
- Orchestrator refactor in `_content.tsx` (URL state for view+filter)
- E2E: state matrix (5 states × 3 viewports) + visual regression (4 snapshots) + axe-core a11y
- Legacy `Card`/`Badge` consumers removed

## Acceptance
- [x] Visual diff ≤ 2%
- [x] WCAG 2.1 AA (axe-core 0 violations)
- [x] `prefers-reduced-motion` respected
- [x] Light + dark mode
- [x] Mobile-first 375px clean (no `window.innerWidth`)
- [x] i18n it+en complete
- [x] Token compliance (no hex/rgb, ESLint clean)
- [x] v2-migration-matrix updated

## Test plan
- [x] `pnpm typecheck && pnpm lint && pnpm vitest run`
- [x] `pnpm test:e2e -- game-nights-index`
- [x] `pnpm lint:tokens` (0 violations)

Refs #1170, #1026, #1168
EOF
)"
```

### Task 5.6 — After PR open: update matrix PR ref

Replace `#TBD` in `v2-migration-matrix.md` with actual PR number, amend last commit, push.

---

## Self-Review Checklist (run before declaring plan done)

1. **Spec coverage** — all 8 acceptance criteria from issue #1170 mapped: visual diff (Task 4.2), 5-commit TDD (commits 1-5), visual regression (4.2), WCAG AA (4.1), reduced-motion (2.8 drawer), light+dark (4.2), bundle delta (track in PR description), mobile-first (2.4-2.8 all use `isMobile` prop), i18n (1.5), token compliance (2.x via Tailwind entity utilities), matrix update (5.2). ✓

2. **Placeholder scan** — all "TBD" / "TODO" are bounded: only one TODO (month-nav fetch deferral, justified per mockup line 20). PR# placeholder explicitly addressed in Task 5.6. ✓

3. **Type consistency** — `FilterKey`, `StatusKey`, `RoleKey`, `GameNightVM`, `MonthCell`, `MonthGroup` defined once in lib/, imported consistently. Props interfaces named `<Component>Props` throughout. ✓

---

## Execution Handoff

**Plan complete and saved to `docs/superpowers/plans/2026-05-15-game-nights-index-stage3.md`. Two execution options:**

**1. Subagent-Driven (recommended)** — fresh subagent per task, review between, fast iteration.

**2. Inline Execution** — execute tasks in this session via `superpowers:executing-plans`, batch with checkpoints.

**Which approach?**
