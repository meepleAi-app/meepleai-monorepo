# #2436 PR-A — Autosave + Draft Persistence (localStorage) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Autosave the PlayRecord create-form to per-user localStorage (debounced) with a visual save indicator, and restore the draft on return — no backend.

**Architecture:** Mirror the proven `useGameNightDraftPersist` hook (localStorage, debounce, 7d TTL, schema-version guard, userId-keyed) for the play-records create wizard. A new `usePlayRecordDraftPersist` hook + `DraftAutosaveIndicator` component wire into the existing `SessionCreateForm.tsx`. Restore is gated to `mode='create'` and skipped when a `gameNightId` prefill (`initialValues`) is present.

**Tech Stack:** React 19 + Next 16, react-hook-form, Zustand, Zod, Vitest + Testing Library. Decisions: DEC-2 (localStorage-only), DEC-4 (debounce-on-change, supersedes literal "30s"). Spec: `docs/superpowers/specs/2026-06-20-issue-2436-create-deferred-spec-panel.md`.

---

## File Structure

- **Create** `apps/web/src/lib/play-records/draft-types.ts` — draft types + zod validator + schema version. One responsibility: the persisted draft contract.
- **Create** `apps/web/src/lib/play-records/draft-types.test.ts` — validator/version guard tests.
- **Create** `apps/web/src/lib/play-records/hooks/usePlayRecordDraftPersist.ts` — autosave/restore hook.
- **Create** `apps/web/src/lib/play-records/hooks/usePlayRecordDraftPersist.test.ts` — hook tests (fake timers + localStorage).
- **Create** `apps/web/src/components/play-records/DraftAutosaveIndicator.tsx` — status indicator.
- **Create** `apps/web/src/components/play-records/__tests__/DraftAutosaveIndicator.test.tsx` — indicator tests.
- **Modify** `apps/web/src/locales/it.json` + `apps/web/src/locales/en.json` — add `playRecords.new.draft.{saving,saved}` (parity required by the MESSAGES catalog test).
- **Modify** `apps/web/src/components/play-records/SessionCreateForm.tsx` — wire hook + indicator + clear-on-submit/cancel + restore.
- **Create** `apps/web/src/components/play-records/__tests__/SessionCreateForm.draft.test.tsx` — wiring tests (restore, autosave write, clear).
- **Modify** `apps/web/src/components/play-records/__tests__/SessionCreateForm.test.tsx` — add `useCurrentUser` mock so existing tests stay green (form now consumes it).

All commands run from `apps/web/`. Single-run tests: `pnpm exec vitest run <path>`.

---

### Task 1: Draft contract (types + zod validator)

**Files:**
- Create: `apps/web/src/lib/play-records/draft-types.ts`
- Test: `apps/web/src/lib/play-records/draft-types.test.ts`

- [ ] **Step 1: Write the failing test**

```ts
// apps/web/src/lib/play-records/draft-types.test.ts
import { describe, it, expect } from 'vitest';

import {
  PLAY_RECORD_DRAFT_SCHEMA_VERSION,
  persistedPlayRecordDraftSchema,
  type PersistedPlayRecordDraft,
} from './draft-types';

function validDraft(): PersistedPlayRecordDraft {
  return {
    schemaVersion: PLAY_RECORD_DRAFT_SCHEMA_VERSION,
    currentStep: 1,
    gameType: 'catalog',
    gameId: 'game-1',
    gameName: 'Wingspan',
    sessionDate: '2026-06-20T18:00:00.000Z',
    visibility: 'Private',
    enableScoring: false,
    scoringDimensions: [],
    dimensionUnits: {},
    notes: 'gg',
    location: 'Padova',
    players: [{ id: 'p1', name: 'Marco', score: '42' }],
  };
}

describe('persistedPlayRecordDraftSchema', () => {
  it('accepts a well-formed draft of the current schema version', () => {
    const result = persistedPlayRecordDraftSchema.safeParse(validDraft());
    expect(result.success).toBe(true);
  });

  it('rejects a draft whose schemaVersion does not match (version bump guard)', () => {
    const stale = { ...validDraft(), schemaVersion: PLAY_RECORD_DRAFT_SCHEMA_VERSION + 1 };
    expect(persistedPlayRecordDraftSchema.safeParse(stale).success).toBe(false);
  });

  it('rejects a corrupted payload (missing required field)', () => {
    const broken = { ...validDraft() } as Record<string, unknown>;
    delete broken.players;
    expect(persistedPlayRecordDraftSchema.safeParse(broken).success).toBe(false);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run src/lib/play-records/draft-types.test.ts`
Expected: FAIL — cannot resolve `./draft-types`.

- [ ] **Step 3: Write minimal implementation**

```ts
// apps/web/src/lib/play-records/draft-types.ts
import { z } from 'zod';

import type { PlayRecordVisibility } from '@/lib/api/schemas/play-records.schemas';

/** Bump to invalidate all persisted drafts on a breaking shape change. */
export const PLAY_RECORD_DRAFT_SCHEMA_VERSION = 1;

export interface PlayRecordDraftPlayer {
  id: string;
  name: string;
  score: string;
}

/** In-memory draft state (sessionDate as Date) — input to the persist hook. */
export interface PlayRecordDraftState {
  currentStep: number;
  gameType: 'catalog' | 'freeform';
  gameId?: string;
  gameName: string;
  sessionDate: Date;
  visibility: PlayRecordVisibility;
  enableScoring: boolean;
  scoringDimensions: string[];
  dimensionUnits: Record<string, string>;
  notes?: string;
  location?: string;
  players: PlayRecordDraftPlayer[];
}

/** Serialized draft persisted to localStorage (sessionDate as ISO string). */
export interface PersistedPlayRecordDraft {
  schemaVersion: number;
  currentStep: number;
  gameType: 'catalog' | 'freeform';
  gameId?: string;
  gameName: string;
  sessionDate: string;
  visibility: PlayRecordVisibility;
  enableScoring: boolean;
  scoringDimensions: string[];
  dimensionUnits: Record<string, string>;
  notes?: string;
  location?: string;
  players: PlayRecordDraftPlayer[];
}

const playerSchema = z.object({
  id: z.string(),
  name: z.string(),
  score: z.string(),
});

export const persistedPlayRecordDraftSchema = z.object({
  schemaVersion: z.literal(PLAY_RECORD_DRAFT_SCHEMA_VERSION),
  currentStep: z.number(),
  gameType: z.enum(['catalog', 'freeform']),
  gameId: z.string().optional(),
  gameName: z.string(),
  sessionDate: z.string(),
  visibility: z.string(),
  enableScoring: z.boolean(),
  scoringDimensions: z.array(z.string()),
  dimensionUnits: z.record(z.string(), z.string()),
  notes: z.string().optional(),
  location: z.string().optional(),
  players: z.array(playerSchema),
});
```

> Note: `visibility` is validated as `z.string()` (cast to `PlayRecordVisibility` on read) — the hard guard is `schemaVersion: z.literal(...)`, which discards drafts after a version bump. If `z.record(z.string(), z.string())` errors on this zod version, fall back to `z.record(z.string())`.

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm exec vitest run src/lib/play-records/draft-types.test.ts`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/play-records/draft-types.ts apps/web/src/lib/play-records/draft-types.test.ts
git commit -m "feat(play-records): #2436 PR-A draft contract types + zod guard"
```

---

### Task 2: `usePlayRecordDraftPersist` hook

**Files:**
- Create: `apps/web/src/lib/play-records/hooks/usePlayRecordDraftPersist.ts`
- Test: `apps/web/src/lib/play-records/hooks/usePlayRecordDraftPersist.test.ts`

- [ ] **Step 1: Write the failing test**

```ts
// apps/web/src/lib/play-records/hooks/usePlayRecordDraftPersist.test.ts
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { renderHook, act } from '@testing-library/react';

import { PLAY_RECORD_DRAFT_SCHEMA_VERSION, type PlayRecordDraftState } from '../draft-types';
import { usePlayRecordDraftPersist } from './usePlayRecordDraftPersist';

const KEY = 'meepleai:play-record-create-draft:user-1';

function baseState(overrides: Partial<PlayRecordDraftState> = {}): PlayRecordDraftState {
  return {
    currentStep: 0,
    gameType: 'catalog',
    gameName: 'Wingspan',
    sessionDate: new Date('2026-06-20T18:00:00.000Z'),
    visibility: 'Private',
    enableScoring: false,
    scoringDimensions: [],
    dimensionUnits: {},
    players: [],
    ...overrides,
  };
}

beforeEach(() => {
  localStorage.clear();
  vi.useFakeTimers();
});

afterEach(() => {
  vi.useRealTimers();
});

describe('usePlayRecordDraftPersist', () => {
  it('does NOT persist on pristine mount (first run skipped)', () => {
    renderHook(() => usePlayRecordDraftPersist({ userId: 'user-1', state: baseState() }));
    act(() => vi.advanceTimersByTime(2000));
    expect(localStorage.getItem(KEY)).toBeNull();
  });

  it('persists to localStorage (debounced) after the state changes', () => {
    const { rerender } = renderHook((props) => usePlayRecordDraftPersist(props), {
      initialProps: { userId: 'user-1' as string | null, state: baseState() },
    });
    rerender({ userId: 'user-1', state: baseState({ location: 'Padova' }) });
    act(() => vi.advanceTimersByTime(800));
    const raw = localStorage.getItem(KEY);
    expect(raw).not.toBeNull();
    const env = JSON.parse(raw as string);
    expect(env.draft.location).toBe('Padova');
    expect(env.draft.schemaVersion).toBe(PLAY_RECORD_DRAFT_SCHEMA_VERSION);
    expect(env.draft.sessionDate).toBe('2026-06-20T18:00:00.000Z');
  });

  it('returns initialDraft from a valid persisted envelope on mount', () => {
    localStorage.setItem(
      KEY,
      JSON.stringify({
        savedAt: 1_700_000_000_000,
        draft: {
          schemaVersion: PLAY_RECORD_DRAFT_SCHEMA_VERSION,
          currentStep: 2,
          gameType: 'catalog',
          gameName: 'Catan',
          sessionDate: '2026-06-19T10:00:00.000Z',
          visibility: 'Private',
          enableScoring: false,
          scoringDimensions: [],
          dimensionUnits: {},
          players: [{ id: 'p1', name: 'Ada', score: '10' }],
        },
      })
    );
    vi.setSystemTime(new Date('2026-06-20T00:00:00.000Z'));
    const { result } = renderHook(() =>
      usePlayRecordDraftPersist({ userId: 'user-1', state: baseState() })
    );
    expect(result.current.initialDraft?.gameName).toBe('Catan');
    expect(result.current.initialDraft?.currentStep).toBe(2);
  });

  it('discards a stale draft older than the 7-day TTL and clears the key', () => {
    vi.setSystemTime(new Date('2026-06-20T00:00:00.000Z'));
    const eightDaysAgo = new Date('2026-06-20T00:00:00.000Z').getTime() - 8 * 24 * 60 * 60 * 1000;
    localStorage.setItem(
      KEY,
      JSON.stringify({
        savedAt: eightDaysAgo,
        draft: {
          schemaVersion: PLAY_RECORD_DRAFT_SCHEMA_VERSION,
          currentStep: 0,
          gameType: 'catalog',
          gameName: 'Old',
          sessionDate: '2026-06-10T00:00:00.000Z',
          visibility: 'Private',
          enableScoring: false,
          scoringDimensions: [],
          dimensionUnits: {},
          players: [],
        },
      })
    );
    const { result } = renderHook(() =>
      usePlayRecordDraftPersist({ userId: 'user-1', state: baseState() })
    );
    expect(result.current.initialDraft).toBeNull();
    expect(localStorage.getItem(KEY)).toBeNull();
  });

  it('clear() removes the persisted draft', () => {
    localStorage.setItem(KEY, JSON.stringify({ savedAt: Date.now(), draft: {} }));
    const { result } = renderHook(() =>
      usePlayRecordDraftPersist({ userId: 'user-1', state: baseState() })
    );
    act(() => result.current.clear());
    expect(localStorage.getItem(KEY)).toBeNull();
  });

  it('is inert when userId is null (no read, no write)', () => {
    const { rerender } = renderHook((props) => usePlayRecordDraftPersist(props), {
      initialProps: { userId: null as string | null, state: baseState() },
    });
    rerender({ userId: null, state: baseState({ location: 'X' }) });
    act(() => vi.advanceTimersByTime(800));
    expect(localStorage.getItem(KEY)).toBeNull();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run src/lib/play-records/hooks/usePlayRecordDraftPersist.test.ts`
Expected: FAIL — cannot resolve `./usePlayRecordDraftPersist`.

- [ ] **Step 3: Write minimal implementation**

```ts
// apps/web/src/lib/play-records/hooks/usePlayRecordDraftPersist.ts
'use client';

import { useEffect, useMemo, useRef, useState } from 'react';

import {
  PLAY_RECORD_DRAFT_SCHEMA_VERSION,
  persistedPlayRecordDraftSchema,
  type PersistedPlayRecordDraft,
  type PlayRecordDraftState,
} from '@/lib/play-records/draft-types';

const STORAGE_PREFIX = 'meepleai:play-record-create-draft:';
const DEBOUNCE_MS = 800;
const TTL_MS = 7 * 24 * 60 * 60 * 1000; // 7 days

interface StoredEnvelope {
  readonly savedAt: number;
  readonly draft: PersistedPlayRecordDraft;
}

function storageKey(userId: string): string {
  return `${STORAGE_PREFIX}${userId}`;
}

function readDraft(userId: string): PersistedPlayRecordDraft | null {
  if (typeof window === 'undefined') return null;
  try {
    const raw = window.localStorage.getItem(storageKey(userId));
    if (raw == null) return null;
    const parsed = JSON.parse(raw) as StoredEnvelope;
    if (Date.now() - parsed.savedAt > TTL_MS) {
      window.localStorage.removeItem(storageKey(userId));
      return null;
    }
    const validated = persistedPlayRecordDraftSchema.safeParse(parsed.draft);
    if (!validated.success) {
      window.localStorage.removeItem(storageKey(userId));
      return null;
    }
    return validated.data as PersistedPlayRecordDraft;
  } catch {
    return null;
  }
}

function toPersisted(state: PlayRecordDraftState): PersistedPlayRecordDraft {
  return {
    schemaVersion: PLAY_RECORD_DRAFT_SCHEMA_VERSION,
    currentStep: state.currentStep,
    gameType: state.gameType,
    gameId: state.gameId,
    gameName: state.gameName,
    sessionDate:
      state.sessionDate instanceof Date
        ? state.sessionDate.toISOString()
        : String(state.sessionDate),
    visibility: state.visibility,
    enableScoring: state.enableScoring,
    scoringDimensions: state.scoringDimensions,
    dimensionUnits: state.dimensionUnits,
    notes: state.notes,
    location: state.location,
    players: state.players,
  };
}

function writeDraft(userId: string, state: PlayRecordDraftState): void {
  if (typeof window === 'undefined') return;
  const envelope: StoredEnvelope = { savedAt: Date.now(), draft: toPersisted(state) };
  try {
    window.localStorage.setItem(storageKey(userId), JSON.stringify(envelope));
  } catch {
    // localStorage full or denied — best-effort persistence (spec AC-A1 risks).
  }
}

function deleteDraft(userId: string): void {
  if (typeof window === 'undefined') return;
  try {
    window.localStorage.removeItem(storageKey(userId));
  } catch {
    // ignore
  }
}

export interface UsePlayRecordDraftPersistOptions {
  readonly userId: string | null;
  readonly state: PlayRecordDraftState;
  /** Skip autosave/restore entirely (e.g. edit mode). */
  readonly enabled?: boolean;
}

export interface UsePlayRecordDraftPersistResult {
  /** Draft loaded from storage on first render, or null if none / stale. */
  readonly initialDraft: PersistedPlayRecordDraft | null;
  /** Imperative clear (call after successful submit / explicit discard). */
  readonly clear: () => void;
  /** Whether a save is currently scheduled (debounced). */
  readonly isPending: boolean;
  /** Epoch ms of the last successful write, or null. */
  readonly lastSavedAt: number | null;
}

export function usePlayRecordDraftPersist({
  userId,
  state,
  enabled = true,
}: UsePlayRecordDraftPersistOptions): UsePlayRecordDraftPersistResult {
  // Snapshot the initial draft ONCE so consumers restore exactly once.
  const [initialDraft] = useState<PersistedPlayRecordDraft | null>(() =>
    userId && enabled ? readDraft(userId) : null
  );

  const [isPending, setIsPending] = useState(false);
  const [lastSavedAt, setLastSavedAt] = useState<number | null>(null);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Latest state read by the debounced writer (avoids stale closures).
  const stateRef = useRef(state);
  stateRef.current = state;

  // Skip the first effect run so a pristine form does not persist on mount.
  const firstRun = useRef(true);

  const signature = useMemo(
    () =>
      JSON.stringify({
        s: state.currentStep,
        gt: state.gameType,
        gi: state.gameId ?? null,
        gn: state.gameName,
        sd: state.sessionDate instanceof Date ? state.sessionDate.getTime() : state.sessionDate,
        v: state.visibility,
        es: state.enableScoring,
        sdim: state.scoringDimensions,
        du: state.dimensionUnits,
        n: state.notes ?? null,
        l: state.location ?? null,
        p: state.players,
      }),
    [state]
  );

  useEffect(() => {
    if (!enabled || !userId) return undefined;
    if (firstRun.current) {
      firstRun.current = false;
      return undefined;
    }
    setIsPending(true);
    if (timer.current) clearTimeout(timer.current);
    timer.current = setTimeout(() => {
      writeDraft(userId, stateRef.current);
      setIsPending(false);
      setLastSavedAt(Date.now());
    }, DEBOUNCE_MS);
    return () => {
      if (timer.current) clearTimeout(timer.current);
    };
    // signature captures every persisted field; stateRef supplies the value.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [enabled, userId, signature]);

  return {
    initialDraft,
    clear: () => {
      if (timer.current) clearTimeout(timer.current);
      setIsPending(false);
      if (userId) deleteDraft(userId);
    },
    isPending,
    lastSavedAt,
  };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm exec vitest run src/lib/play-records/hooks/usePlayRecordDraftPersist.test.ts`
Expected: PASS (6 tests).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/play-records/hooks/usePlayRecordDraftPersist.ts apps/web/src/lib/play-records/hooks/usePlayRecordDraftPersist.test.ts
git commit -m "feat(play-records): #2436 PR-A localStorage draft autosave hook"
```

---

### Task 3: `DraftAutosaveIndicator` component

**Files:**
- Create: `apps/web/src/components/play-records/DraftAutosaveIndicator.tsx`
- Test: `apps/web/src/components/play-records/__tests__/DraftAutosaveIndicator.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// apps/web/src/components/play-records/__tests__/DraftAutosaveIndicator.test.tsx
import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';

import { DraftAutosaveIndicator } from '../DraftAutosaveIndicator';

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({
    t: (key: string) =>
      ({
        'playRecords.new.draft.saving': 'Salvataggio…',
        'playRecords.new.draft.saved': 'Bozza salvata {time}',
      })[key] ?? key,
  }),
}));

describe('DraftAutosaveIndicator', () => {
  it('renders nothing on the pristine state (not pending, never saved)', () => {
    const { container } = render(<DraftAutosaveIndicator isPending={false} lastSavedAt={null} />);
    expect(container.firstChild).toBeNull();
  });

  it('shows the saving label while a save is pending', () => {
    render(<DraftAutosaveIndicator isPending lastSavedAt={null} />);
    const el = screen.getByTestId('draft-autosave-indicator');
    expect(el).toHaveAttribute('role', 'status');
    expect(el).toHaveTextContent('Salvataggio…');
  });

  it('shows the saved label with the interpolated time when not pending', () => {
    const ts = new Date('2026-06-20T18:05:00.000Z').getTime();
    render(<DraftAutosaveIndicator isPending={false} lastSavedAt={ts} />);
    const el = screen.getByTestId('draft-autosave-indicator');
    expect(el).toHaveTextContent(/Bozza salvata \d{2}:\d{2}/);
    expect(el).not.toHaveTextContent('{time}');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/play-records/__tests__/DraftAutosaveIndicator.test.tsx`
Expected: FAIL — cannot resolve `../DraftAutosaveIndicator`.

- [ ] **Step 3: Write minimal implementation**

```tsx
// apps/web/src/components/play-records/DraftAutosaveIndicator.tsx
'use client';

import { useTranslation } from '@/hooks/useTranslation';
import { cn } from '@/lib/utils';

export interface DraftAutosaveIndicatorProps {
  isPending: boolean;
  lastSavedAt: number | null;
}

function formatTime(ts: number): string {
  const d = new Date(ts);
  const hh = String(d.getHours()).padStart(2, '0');
  const mm = String(d.getMinutes()).padStart(2, '0');
  return `${hh}:${mm}`;
}

/**
 * #2436 PR-A AC-A2 — draft autosave status. Renders nothing until the first
 * save is pending or completed. role="status" + aria-live="polite" announces
 * the state change politely to assistive tech.
 */
export function DraftAutosaveIndicator({ isPending, lastSavedAt }: DraftAutosaveIndicatorProps) {
  const { t } = useTranslation();

  if (!isPending && lastSavedAt === null) return null;

  return (
    <span
      role="status"
      aria-live="polite"
      data-testid="draft-autosave-indicator"
      className="inline-flex items-center gap-1.5 font-mono text-[10px] font-bold uppercase tracking-widest text-muted-foreground"
    >
      <span
        className={cn(
          'h-1.5 w-1.5 rounded-full',
          isPending ? 'bg-entity-session animate-pulse' : 'bg-entity-session/50'
        )}
        aria-hidden="true"
      />
      {isPending
        ? t('playRecords.new.draft.saving')
        : t('playRecords.new.draft.saved').replace('{time}', formatTime(lastSavedAt as number))}
    </span>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm exec vitest run src/components/play-records/__tests__/DraftAutosaveIndicator.test.tsx`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/play-records/DraftAutosaveIndicator.tsx apps/web/src/components/play-records/__tests__/DraftAutosaveIndicator.test.tsx
git commit -m "feat(play-records): #2436 PR-A draft autosave indicator component"
```

---

### Task 4: i18n keys (it + en parity)

**Files:**
- Modify: `apps/web/src/locales/it.json` (object `playRecords.new`)
- Modify: `apps/web/src/locales/en.json` (object `playRecords.new`)

- [ ] **Step 1: Add the `draft` block to it.json**

In `apps/web/src/locales/it.json`, inside `playRecords.new`, add a sibling key to `actions` (the existing `playRecords.new` keys include `pageTitle, pageSubtitle, …, actions, success, error, loading, a11y`):

```json
"draft": {
  "saving": "Salvataggio…",
  "saved": "Bozza salvata {time}"
}
```

- [ ] **Step 2: Add the same block to en.json**

In `apps/web/src/locales/en.json`, inside `playRecords.new`:

```json
"draft": {
  "saving": "Saving…",
  "saved": "Draft saved {time}"
}
```

- [ ] **Step 3: Verify catalog parity + JSON validity**

Run: `pnpm exec vitest run src/__tests__ -t "messages"`
Expected: PASS (the i18n catalog/MESSAGES parity test stays green — both locales now expose `playRecords.new.draft.saving` + `.saved`).
If no such test matches, instead verify both files parse: `node -e "require('./src/locales/it.json');require('./src/locales/en.json');console.log('ok')"` → prints `ok`.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/locales/it.json apps/web/src/locales/en.json
git commit -m "feat(play-records): #2436 PR-A i18n draft autosave strings (it+en)"
```

---

### Task 5: Wire autosave + restore into SessionCreateForm

**Files:**
- Modify: `apps/web/src/components/play-records/SessionCreateForm.tsx`
- Create: `apps/web/src/components/play-records/__tests__/SessionCreateForm.draft.test.tsx`
- Modify: `apps/web/src/components/play-records/__tests__/SessionCreateForm.test.tsx` (add `useCurrentUser` mock)

- [ ] **Step 1: Write the failing wiring test**

```tsx
// apps/web/src/components/play-records/__tests__/SessionCreateForm.draft.test.tsx
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { SessionCreateForm } from '../SessionCreateForm';
import { PLAY_RECORD_DRAFT_SCHEMA_VERSION } from '@/lib/play-records/draft-types';

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));
vi.mock('next/navigation', () => ({ useRouter: () => ({ push: vi.fn() }) }));
const mockUseMediaQuery = vi.fn(() => true); // mobile
vi.mock('@/lib/hooks/useMediaQuery', () => ({
  useMediaQuery: () => mockUseMediaQuery(),
}));
const mockSetSessionField = vi.fn();
let mockCurrentStep = 0;
vi.mock('@/lib/stores/play-records-store', () => ({
  usePlayRecordsStore: () => ({
    sessionCreation: { currentStep: mockCurrentStep },
    nextStep: vi.fn(),
    prevStep: vi.fn(),
    resetSessionCreation: vi.fn(),
    setSessionField: mockSetSessionField,
  }),
}));
vi.mock('@/components/play-records/GameCombobox', () => ({
  GameCombobox: () => <div data-testid="game-combobox" />,
}));
vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => ({ data: { id: 'user-1' } }),
}));

const KEY = 'meepleai:play-record-create-draft:user-1';

function wrapper() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  );
}

const props = { onSubmit: vi.fn(), onCancel: vi.fn(), isSubmitting: false };

beforeEach(() => {
  localStorage.clear();
  mockCurrentStep = 0;
  vi.clearAllMocks();
});
afterEach(() => vi.useRealTimers());

describe('SessionCreateForm — draft persistence wiring', () => {
  it('AC-A3: restores a persisted draft into the form on mount (no prefill)', () => {
    mockCurrentStep = 1; // Step 2 "Quando" — location field visible
    localStorage.setItem(
      KEY,
      JSON.stringify({
        savedAt: Date.now(),
        draft: {
          schemaVersion: PLAY_RECORD_DRAFT_SCHEMA_VERSION,
          currentStep: 1,
          gameType: 'catalog',
          gameName: 'Catan',
          sessionDate: '2026-06-19T10:00:00.000Z',
          visibility: 'Private',
          enableScoring: false,
          scoringDimensions: [],
          dimensionUnits: {},
          notes: '',
          location: 'Verona',
          players: [],
        },
      })
    );
    render(<SessionCreateForm {...props} />, { wrapper: wrapper() });
    expect(screen.getByDisplayValue('Verona')).toBeInTheDocument();
    expect(mockSetSessionField).toHaveBeenCalledWith('currentStep', 1);
  });

  it('AC-A3: does NOT restore when initialValues (gameNight prefill) is present', () => {
    mockCurrentStep = 1;
    localStorage.setItem(
      KEY,
      JSON.stringify({
        savedAt: Date.now(),
        draft: {
          schemaVersion: PLAY_RECORD_DRAFT_SCHEMA_VERSION,
          currentStep: 1,
          gameType: 'catalog',
          gameName: 'Catan',
          sessionDate: '2026-06-19T10:00:00.000Z',
          visibility: 'Private',
          enableScoring: false,
          scoringDimensions: [],
          dimensionUnits: {},
          location: 'Verona',
          players: [],
        },
      })
    );
    render(
      <SessionCreateForm {...props} initialValues={{ gameName: 'Brass', location: 'Bologna' }} />,
      { wrapper: wrapper() }
    );
    expect(screen.getByDisplayValue('Bologna')).toBeInTheDocument();
    expect(screen.queryByDisplayValue('Verona')).toBeNull();
  });

  it('AC-A1: autosaves to localStorage after editing a field (debounced)', () => {
    vi.useFakeTimers();
    mockCurrentStep = 1;
    render(<SessionCreateForm {...props} />, { wrapper: wrapper() });
    const location = screen.getByLabelText('step2.locationLabel');
    fireEvent.change(location, { target: { value: 'Milano' } });
    act(() => vi.advanceTimersByTime(800));
    const raw = localStorage.getItem(KEY);
    expect(raw).not.toBeNull();
    expect(JSON.parse(raw as string).draft.location).toBe('Milano');
  });

  it('AC-A6: clears the draft on successful submit', () => {
    vi.useFakeTimers();
    mockCurrentStep = 2; // Step 3 — submit button present
    localStorage.setItem(KEY, JSON.stringify({ savedAt: Date.now(), draft: {} }));
    render(<SessionCreateForm {...props} />, { wrapper: wrapper() });
    const saveBtn = screen.getByRole('button', { name: /actions\.save/i });
    fireEvent.click(saveBtn);
    act(() => vi.advanceTimersByTime(0));
    expect(localStorage.getItem(KEY)).toBeNull();
  });
});
```

> The `t` mock returns the raw key, so `getByLabelText('step2.locationLabel')` and `name: /actions\.save/i` match the rendered key strings. `getByDisplayValue` asserts restored field values directly.

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/play-records/__tests__/SessionCreateForm.draft.test.tsx`
Expected: FAIL — autosave/restore not wired (no localStorage write; `Verona` not restored).

- [ ] **Step 3: Wire the hook into SessionCreateForm.tsx**

3a. Update the React import (line 16) to add `useEffect`, `useRef`:

```tsx
import { useState, useId, useEffect, useRef } from 'react';
```

3b. Add imports near the other `@/` imports (after line 42 `usePlayRecordsStore` import):

```tsx
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import { usePlayRecordDraftPersist } from '@/lib/play-records/hooks/usePlayRecordDraftPersist';
import type { PlayRecordDraftState } from '@/lib/play-records/draft-types';

import { DraftAutosaveIndicator } from './DraftAutosaveIndicator';
```

3c. Add `setSessionField` to the store destructure (line 647):

```tsx
const { sessionCreation, nextStep, prevStep, resetSessionCreation, setSessionField } =
  usePlayRecordsStore();
```

3d. After the `form` is created (after line 667, before `STEP_FIELDS`), add the draft state + hook + restore effect:

```tsx
  // ── Draft autosave (localStorage) — #2436 PR-A ─────────────────────────────
  const { data: currentUser } = useCurrentUser();
  const userId = currentUser?.id ?? null;

  const watched = form.watch();
  const draftState: PlayRecordDraftState = {
    currentStep,
    gameType: watched.gameType,
    gameId: watched.gameId,
    gameName: watched.gameName ?? '',
    sessionDate: watched.sessionDate instanceof Date ? watched.sessionDate : new Date(),
    visibility: watched.visibility,
    enableScoring: watched.enableScoring,
    scoringDimensions: watched.scoringDimensions ?? [],
    dimensionUnits: watched.dimensionUnits ?? {},
    notes: watched.notes,
    location: watched.location,
    players,
  };

  const { initialDraft, clear, isPending, lastSavedAt } = usePlayRecordDraftPersist({
    userId,
    state: draftState,
    enabled: mode === 'create',
  });

  // AC-A3: restore the persisted draft once on mount, unless a gameNight
  // prefill (initialValues) is present — the prefill takes precedence.
  const restoredRef = useRef(false);
  useEffect(() => {
    if (restoredRef.current) return;
    if (mode !== 'create' || initialValues || !initialDraft) return;
    restoredRef.current = true;
    form.reset({
      gameType: initialDraft.gameType,
      gameId: initialDraft.gameId,
      gameName: initialDraft.gameName,
      sessionDate: new Date(initialDraft.sessionDate),
      visibility: initialDraft.visibility,
      enableScoring: initialDraft.enableScoring,
      scoringDimensions: initialDraft.scoringDimensions,
      dimensionUnits: initialDraft.dimensionUnits,
      notes: initialDraft.notes ?? '',
      location: initialDraft.location ?? '',
    });
    setPlayers(initialDraft.players);
    setSessionField('currentStep', initialDraft.currentStep);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- mount-once restore
  }, []);
```

3e. Call `clear()` in `handleCancel` (line 697) and `handleFormSubmit` (line 705):

```tsx
  const handleCancel = () => {
    clear();
    resetSessionCreation();
    form.reset();
    setPlayers([]);
    setNewPlayerName('');
    onCancel?.();
  };

  const handleFormSubmit = form.handleSubmit(data => {
    onSubmit(data);
    clear();
    resetSessionCreation();
    form.reset();
    setPlayers([]);
    setNewPlayerName('');
  });
```

3f. Render the indicator in the action bar left `<div>` (lines 753-766). Replace the opening of that left container so the indicator sits beside the back/cancel controls:

```tsx
      <div className="flex items-center gap-3">
        {currentStep > 0 && (
          <Button type="button" variant="outline" onClick={handlePrev} disabled={isSubmitting}>
            <ArrowLeft className="w-4 h-4 mr-2" />
            {t('actions.back')}
          </Button>
        )}
        {currentStep === 0 && onCancel && (
          <Button type="button" variant="ghost" onClick={handleCancel} disabled={isSubmitting}>
            {t('actions.cancel')}
          </Button>
        )}
        {mode === 'create' && (
          <DraftAutosaveIndicator isPending={isPending} lastSavedAt={lastSavedAt} />
        )}
      </div>
```

- [ ] **Step 4: Run the wiring test to verify it passes**

Run: `pnpm exec vitest run src/components/play-records/__tests__/SessionCreateForm.draft.test.tsx`
Expected: PASS (4 tests).

- [ ] **Step 5: Fix the existing SessionCreateForm.test.tsx (add useCurrentUser mock)**

The form now calls `useCurrentUser`; the existing suite must mock it so no real query runs. Add this mock alongside the others (after the `usePlayRecordsStore` mock block, ~line 62) and extend the store mock with `setSessionField`:

```tsx
vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => ({ data: undefined }),
}));
```

And in the existing `vi.mock('@/lib/stores/play-records-store', …)` factory, add `setSessionField: vi.fn(),` to the returned object.

> `data: undefined` → `userId` null → autosave/restore inert → existing 30+ tests behave exactly as before.

- [ ] **Step 6: Run the full play-records suite + typecheck + lint**

Run: `pnpm exec vitest run src/components/play-records src/lib/play-records`
Expected: PASS (existing SessionCreateForm suite + all new tests).

Run: `pnpm typecheck`
Expected: no errors.

Run: `pnpm lint`
Expected: no new errors.

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/play-records/SessionCreateForm.tsx apps/web/src/components/play-records/__tests__/SessionCreateForm.draft.test.tsx apps/web/src/components/play-records/__tests__/SessionCreateForm.test.tsx
git commit -m "feat(play-records): #2436 PR-A wire draft autosave into create form"
```

---

## Self-Review

**1. Spec coverage (vs spec AC):**
- AC-A1 autosave debounced → Task 2 (hook write) + Task 5 (wiring test "autosaves after editing"). ✓
- AC-A2 visual indicator role=status → Task 3. ✓
- AC-A3 restore on mount, skip on prefill → Task 5 restore effect + 2 tests. ✓
- AC-A4 7d TTL → Task 2 test "discards stale". ✓
- AC-A5 schema-version guard → Task 1 test + Task 2 readDraft. ✓
- AC-A6 clear on submit/cancel → Task 5 (clear calls + submit test). ✓
- AC-A7 create-only → Task 5 `enabled: mode === 'create'` + indicator `mode === 'create'`. ✓
- AC-A8 userId null inert → Task 2 test "inert when userId null". ✓

**2. Placeholder scan:** No TBD/TODO; every code step shows complete code; commands have expected output. ✓

**3. Type consistency:** `PlayRecordDraftState` / `PersistedPlayRecordDraft` defined in Task 1, consumed identically in Task 2 (`toPersisted`) and Task 5 (`draftState`). Hook result fields `{ initialDraft, clear, isPending, lastSavedAt }` defined in Task 2, consumed in Tasks 3+5. `PLAY_RECORD_DRAFT_SCHEMA_VERSION` defined Task 1, used in Tasks 1/2/5 tests. ✓

**Risk note (executor):** `form.watch()` returns a new object each render — the hook's `signature` memo + `firstRun` skip prevent a write storm and pristine-mount persistence. If `pnpm lint` flags the mount-once effect, keep the existing `eslint-disable-next-line` comment (matches `useGameNightDraftPersist` convention).
