/**
 * Visual-regression test fixture for `/gamebook` index (SP6 Phase B Task 1).
 *
 * Mirror Wave D.1 / Wave D.2 / Wave D.3 sentinel pattern: workflow
 * `visual-regression-migrated.yml` runs only Next.js prod (no backend API at
 * `:8080`). The index page uses 2 hooks (`useGamebooks`, `useQuotaInfo`) that
 * both need data to render the default cell. In CI this fixture short-circuits
 * the 2 hooks via the orchestrator.
 *
 * Production safety: production builds do NOT set
 * `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED`. The `STATE_OVERRIDE_ENABLED`
 * gate evaluates to its `process.env.NODE_ENV !== 'production'` branch only,
 * which is `false` in prod, allowing the bundler to dead-code-eliminate the
 * `?fixture=` URL hatch. The fixture data itself is also dead code in prod
 * because the orchestrator only reads it when the hatch is enabled.
 *
 * Schema reality v1 carryover (Gate B): see `schemas.ts` header. Both the
 * `useGamebooks` hook and the `useQuotaInfo` (or equivalent) backend endpoint
 * do NOT exist in v1. This fixture exposes the canonical `GamebookCardData`
 * + `QuotaInfo` shapes so the FSM and components consume the contract types,
 * regardless of how the orchestrator (Task 3) actually wires the data.
 *
 * State coverage (6 fixture kinds, mirrors FSM cells):
 *   - `default`     → 4 ready/indexing/error gamebooks, quota 12/50
 *   - `empty`       → 0 gamebooks, quota 0/50
 *   - `quota-soft`  → 4 gamebooks, quota 47/50 (ratio = 0.94)
 *   - `quota-hard`  → 4 gamebooks, quota 50/50 (banner visible)
 *   - `loading`     → empty content, skeleton-rendered
 *   - `error`       → empty content, error state rendered
 *
 * Used by:
 *   - `apps/web/e2e/visual-migrated/sp6-gamebook-index.spec.ts` (Task 4)
 *   - `apps/web/e2e/v2-states/gamebook-index.spec.ts` (Task 4)
 */

import { gamebookIndexFixtureKindSchema } from './schemas';

import type { GamebookCardData, GamebookIndexFixtureKind, QuotaInfo } from './schemas';

/**
 * True only when the build was produced by the visual-regression CI workflow
 * (which sets `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` before `pnpm build`).
 */
export const IS_VISUAL_TEST_BUILD = process.env.NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED === '1';

/**
 * Build-time gating for the `?fixture=` URL override hatch.
 *
 * Allowing fixture overrides in production would expose a UI manipulation
 * surface. The hatch is enabled only when IS_VISUAL_TEST_BUILD=true OR
 * NODE_ENV !== 'production' (development/test environments).
 */
export const STATE_OVERRIDE_ENABLED = IS_VISUAL_TEST_BUILD || process.env.NODE_ENV !== 'production';

/**
 * Composed fixture record. Exposed shape mirrors what the 2 hooks would
 * collectively return on a happy-path render.
 */
export interface GamebookIndexFixture {
  readonly gamebooks: readonly GamebookCardData[];
  readonly quota: QuotaInfo;
}

// ---------------------------------------------------------------------------
// Static building blocks
// ---------------------------------------------------------------------------

const RESET_DATE = '2026-06-01T00:00:00.000Z';

const GAME_ID_NANOLITH = '00000000-0000-4000-8000-0000000c0001';
const GAME_ID_BRASS = '00000000-0000-4000-8000-0000000c0002';
const GAME_ID_SPIRIT = '00000000-0000-4000-8000-0000000c0003';
const GAME_ID_WINGSPAN = '00000000-0000-4000-8000-0000000c0004';

/**
 * 4 deterministic gamebook entries representing diverse status states. Used
 * as the canonical "default" gamebook list for default / quota-soft /
 * quota-hard fixture kinds.
 *
 * Status mix:
 *   - Nanolith        → ready (50/50 pages, 142 chunks, 12 Q&A, 3 sessions)
 *   - Brass Birmingham → ready (32/32 pages, 96 chunks, 7 Q&A, 2 sessions)
 *   - Spirit Island   → indexing (18/45 pages, 0 chunks pending)
 *   - Wingspan        → error (12/42 pages OCR-failed at page 23)
 */
const GAMEBOOKS_DEFAULT: readonly GamebookCardData[] = [
  {
    id: '00000000-0000-4000-8000-000000000001',
    gameId: GAME_ID_NANOLITH,
    title: 'Nanolith',
    publisher: 'Self-published',
    year: 2024,
    pages: 50,
    totalPages: 50,
    chunks: 142,
    status: 'ready',
    cover: null,
    emoji: '⚛️',
    qaCount: 12,
    sessionsCount: 3,
    errorMsg: null,
  },
  {
    id: '00000000-0000-4000-8000-000000000002',
    gameId: GAME_ID_BRASS,
    title: 'Brass Birmingham',
    publisher: 'Roxley Games',
    year: 2018,
    pages: 32,
    totalPages: 32,
    chunks: 96,
    status: 'ready',
    cover: null,
    emoji: '🏭',
    qaCount: 7,
    sessionsCount: 2,
    errorMsg: null,
  },
  {
    id: '00000000-0000-4000-8000-000000000003',
    gameId: GAME_ID_SPIRIT,
    title: 'Spirit Island',
    publisher: 'Greater Than Games',
    year: 2017,
    pages: 18,
    totalPages: 45,
    chunks: 0,
    status: 'indexing',
    cover: null,
    emoji: '🌿',
    qaCount: 0,
    sessionsCount: 0,
    errorMsg: null,
  },
  {
    id: '00000000-0000-4000-8000-000000000004',
    gameId: GAME_ID_WINGSPAN,
    title: 'Wingspan',
    publisher: 'Stonemaier Games',
    year: 2019,
    pages: 12,
    totalPages: 42,
    chunks: 0,
    status: 'error',
    cover: null,
    emoji: '🦅',
    qaCount: 0,
    sessionsCount: 0,
    errorMsg: 'OCR failed at page 23 — luce insufficiente',
  },
];

const QUOTA_FRESH: QuotaInfo = {
  used: 12,
  total: 50,
  resetDate: RESET_DATE,
  tier: 'free',
};

const QUOTA_EMPTY_FRESH: QuotaInfo = {
  used: 0,
  total: 50,
  resetDate: RESET_DATE,
  tier: 'free',
};

const QUOTA_SOFT: QuotaInfo = {
  used: 47,
  total: 50,
  resetDate: RESET_DATE,
  tier: 'free',
};

const QUOTA_HARD: QuotaInfo = {
  used: 50,
  total: 50,
  resetDate: RESET_DATE,
  tier: 'free',
};

// ---------------------------------------------------------------------------
// 6 fixture variants (one per FSM cell kind)
// ---------------------------------------------------------------------------

const FIXTURE_DEFAULT: GamebookIndexFixture = {
  gamebooks: GAMEBOOKS_DEFAULT,
  quota: QUOTA_FRESH,
};

const FIXTURE_EMPTY: GamebookIndexFixture = {
  gamebooks: [],
  quota: QUOTA_EMPTY_FRESH,
};

const FIXTURE_QUOTA_SOFT: GamebookIndexFixture = {
  gamebooks: GAMEBOOKS_DEFAULT,
  quota: QUOTA_SOFT,
};

const FIXTURE_QUOTA_HARD: GamebookIndexFixture = {
  gamebooks: GAMEBOOKS_DEFAULT,
  quota: QUOTA_HARD,
};

/**
 * Loading fixture exposes empty gamebooks (skeleton replaces them) and a
 * fresh quota. Orchestrator (Task 3) consumes this when forcing the
 * `loading` cell via `?fixture=loading` — the FSM still renders skeletons
 * because it short-circuits to the loading cell upstream of fixture data.
 */
const FIXTURE_LOADING: GamebookIndexFixture = {
  gamebooks: [],
  quota: QUOTA_EMPTY_FRESH,
};

/**
 * Error fixture mirrors loading: orchestrator forces the error cell upstream,
 * the data inside the fixture is irrelevant but kept stable for snapshots.
 */
const FIXTURE_ERROR: GamebookIndexFixture = {
  gamebooks: [],
  quota: QUOTA_EMPTY_FRESH,
};

/**
 * Single source of truth for the 6 visual fixture variants. Keyed by
 * `GamebookIndexFixtureKind` — the discriminant is also used by the
 * `?fixture=` URL override.
 */
export const gamebookIndexFixtures: Readonly<
  Record<GamebookIndexFixtureKind, GamebookIndexFixture>
> = {
  default: FIXTURE_DEFAULT,
  empty: FIXTURE_EMPTY,
  'quota-soft': FIXTURE_QUOTA_SOFT,
  'quota-hard': FIXTURE_QUOTA_HARD,
  loading: FIXTURE_LOADING,
  error: FIXTURE_ERROR,
};

// ---------------------------------------------------------------------------
// URL override parsing
// ---------------------------------------------------------------------------

/**
 * Parses the `?fixture=` URL search param into a `GamebookIndexFixtureKind`.
 *
 * Returns `null` when:
 *   - `STATE_OVERRIDE_ENABLED` is false (production builds without the env var)
 *   - The param is missing, empty, or not a recognized fixture kind
 *
 * Accepts either a `URLSearchParams`, a plain string (the param value), or
 * `null`/`undefined` for ergonomic call sites.
 */
export function parseStateOverride(
  searchParam: string | URLSearchParams | null | undefined
): GamebookIndexFixtureKind | null {
  if (!STATE_OVERRIDE_ENABLED) return null;
  let raw: string | null;
  if (searchParam == null) {
    raw = null;
  } else if (typeof searchParam === 'string') {
    raw = searchParam;
  } else {
    raw = searchParam.get('fixture');
  }
  if (!raw) return null;
  const parsed = gamebookIndexFixtureKindSchema.safeParse(raw);
  return parsed.success ? parsed.data : null;
}
