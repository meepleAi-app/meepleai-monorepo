/**
 * Visual-regression test fixture for `/sessions` (Wave D.1).
 *
 * **Purpose**: workflow `visual-regression-migrated.yml` runs only Next.js prod
 * (no backend API at `:8080`). The sessions data hook (useActiveSessions)
 * cannot reach the backend in CI → the surface stays in `loading` forever →
 * no screenshot.
 *
 * **Contract**: when env var `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED === '1'`
 * is baked into the build, the orchestrator (SessionsView) substitutes the
 * fixture for the real fetch and renders deterministic entries.
 *
 * **Production safety**: production builds do NOT set the env var. The constant
 * `IS_VISUAL_TEST_BUILD` evaluates to `false` and every fixture branch is dead
 * code, eliminated by the bundler. The fixture UUIDs are meaningless to a
 * production deployment.
 *
 * State coverage:
 *   - `'default'` → 6 session entries (1 inprogress, 1 paused, 3 completed, 1 abandoned)
 *   - `'empty'`   → []  (sessions-empty baseline)
 *   - All other v2 states (`loading`, `filtered-empty`, `error`) are simulated
 *     by the orchestrator via the `?state=...` URL override hatch and do NOT
 *     hit the fixture.
 *
 * Used by:
 *   - `apps/web/e2e/visual-migrated/sp4-sessions-index.spec.ts` (Task 4)
 *   - `apps/web/e2e/v2-states/sessions-index.spec.ts` (Task 4)
 */

import type { SessionListItem } from './sessions-filters';
import type { SessionsUiState } from './sessions-state';

/**
 * Deterministic UUIDv4-shaped sentinel encoding the Wave D.1 sessions issue
 * (#735) in the last group for human-debuggability.
 */
export const VISUAL_TEST_FIXTURE_SESSIONS_ID = '00000000-0000-4000-8000-000000000735' as const;

/**
 * True only when the build was produced by the visual-regression CI workflow
 * (sets `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` before `pnpm build`).
 *
 * `NEXT_PUBLIC_*` env vars are inlined at build time → in production deploys
 * this is the literal `false`, allowing the bundler to dead-code-eliminate
 * the fixture and its short-circuit branches.
 */
export const IS_VISUAL_TEST_BUILD = process.env.NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED === '1';

/**
 * Build-time gating for the `?state=` URL override hatch.
 *
 * Allowing state overrides in production would expose a UI manipulation
 * surface. The hatch is enabled only when IS_VISUAL_TEST_BUILD=true or
 * NODE_ENV !== 'production' (development/test environments).
 */
export const STATE_OVERRIDE_ENABLED = process.env.NODE_ENV !== 'production' || IS_VISUAL_TEST_BUILD;

/**
 * 6 deterministic SessionListItem entries covering a realistic status spread:
 *   - s001: inprogress (live, turn 12/18, has chat)
 *   - s002: paused (turn 6/10, has chat)
 *   - s003: completed + won
 *   - s004: completed + lost
 *   - s005: completed + tie
 *   - s006: abandoned
 *
 * UUIDs follow the Wave 4 D1 sentinel pattern: 00000000-0000-4000-8000-000000000XXX.
 * Games mirror the players fixture dataset (Wingspan, Azul, Brass, Catan, Arknova).
 */
export const VISUAL_TEST_FIXTURE_SESSIONS: ReadonlyArray<SessionListItem> = [
  {
    id: '00000000-0000-4000-8000-000000000001',
    gameName: 'Brass: Birmingham',
    date: 'oggi',
    when: 'in corso',
    duration: '1h 12m',
    status: 'inprogress',
    outcome: null,
    playerCount: 4,
    scores: [
      { name: 'Marco', score: 24 },
      { name: 'Anna', score: 31 },
      { name: 'Luca', score: 19 },
      { name: 'Sara', score: 28 },
    ],
    hasChat: true,
    chatCount: 7,
    turn: '12/18',
  },
  {
    id: '00000000-0000-4000-8000-000000000002',
    gameName: 'Wingspan',
    date: 'oggi · pausa',
    when: 'in pausa',
    duration: '45m',
    status: 'paused',
    outcome: null,
    playerCount: 3,
    scores: [
      { name: 'Marco', score: 38 },
      { name: 'Luca', score: 42 },
      { name: 'Anna', score: 35 },
    ],
    hasChat: true,
    chatCount: 1,
    turn: '6/10',
    paused: true,
  },
  {
    id: '00000000-0000-4000-8000-000000000003',
    gameName: 'Wingspan',
    date: '23 apr 2026',
    when: '2 giorni fa',
    duration: '1h 24m',
    status: 'completed',
    outcome: 'won',
    playerCount: 4,
    scores: [
      { name: 'Marco', score: 89, winner: true },
      { name: 'Anna', score: 76 },
      { name: 'Luca', score: 64 },
      { name: 'Sara', score: 58 },
    ],
    hasChat: true,
    chatCount: 3,
  },
  {
    id: '00000000-0000-4000-8000-000000000004',
    gameName: 'Azul',
    date: '21 apr 2026',
    when: '4 giorni fa',
    duration: '42m',
    status: 'completed',
    outcome: 'lost',
    playerCount: 3,
    scores: [
      { name: 'Sara', score: 81, winner: true },
      { name: 'Marco', score: 72 },
      { name: 'Luca', score: 65 },
    ],
    hasChat: false,
  },
  {
    id: '00000000-0000-4000-8000-000000000005',
    gameName: 'Ark Nova',
    date: '15 apr 2026',
    when: '10 giorni fa',
    duration: '2h 48m',
    status: 'completed',
    outcome: 'tie',
    playerCount: 2,
    scores: [
      { name: 'Marco', score: 124 },
      { name: 'Anna', score: 124 },
    ],
    hasChat: true,
    chatCount: 2,
  },
  {
    id: '00000000-0000-4000-8000-000000000006',
    gameName: '7 Wonders',
    date: '5 apr 2026',
    when: '3 settimane fa',
    duration: '28m',
    status: 'abandoned',
    outcome: null,
    playerCount: 2,
    scores: [
      { name: 'Marco', score: 32 },
      { name: 'Sara', score: 41 },
    ],
    hasChat: false,
  },
];

/** Empty fixture for the `empty` FSM state visual baseline. */
export const VISUAL_TEST_FIXTURE_SESSIONS_EMPTY: ReadonlyArray<SessionListItem> = [];

export type SessionsFixtureState = 'default' | 'empty';

/**
 * Returns deterministic session entries iff the build is a visual-test build.
 * Returns `null` otherwise — caller MUST fall through to the real fetch.
 */
export function tryLoadVisualTestFixture(
  state: SessionsFixtureState = 'default'
): ReadonlyArray<SessionListItem> | null {
  if (!IS_VISUAL_TEST_BUILD) return null;
  if (state === 'empty') return VISUAL_TEST_FIXTURE_SESSIONS_EMPTY;
  return VISUAL_TEST_FIXTURE_SESSIONS;
}

/**
 * Parses the `?state=` URL search param into a SessionsUiState override.
 *
 * Only valid when `STATE_OVERRIDE_ENABLED` is true (dev/test or visual-test CI builds).
 * Returns `null` in production or for unknown/unsupported state values.
 *
 * Valid overrides: 'loading', 'empty', 'filtered-empty'
 * NOT valid: 'error' — TanStack Query isError is not reproducible via URL deterministically.
 */
export function parseStateOverride(
  searchParams: URLSearchParams | Record<string, string>
): SessionsUiState | null {
  if (!STATE_OVERRIDE_ENABLED) return null;

  const raw =
    searchParams instanceof URLSearchParams
      ? searchParams.get('state')
      : ((searchParams as Record<string, string>)['state'] ?? null);

  const validOverrides: SessionsUiState[] = ['loading', 'empty', 'filtered-empty'];
  if (raw && validOverrides.includes(raw as SessionsUiState)) {
    return raw as SessionsUiState;
  }
  return null;
}
