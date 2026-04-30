/**
 * Visual-regression test fixture for `/agents` (Wave B.2, Issue #634).
 *
 * **Purpose**: workflow `visual-regression-migrated.yml` runs only Next.js prod
 * (no backend API at `:8080`). The agents data hook cannot reach the backend in
 * CI → the surface stays in `loading` forever → no screenshot.
 *
 * **Contract**: when env var `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED === '1'`
 * is baked into the build, the orchestrator (AgentsLibraryView) substitutes
 * the fixture for the real fetch and renders deterministic entries.
 *
 * **Production safety**: production builds do NOT set the env var. The
 * constant `IS_VISUAL_TEST_BUILD` evaluates to `false` and every fixture
 * branch is dead code, eliminated by the bundler. The fixture UUID is
 * meaningless to a production deployment.
 *
 * State coverage:
 *   - `'default'` → 6 entries (hero stats + grid baseline, 3 attivo + 2 in-setup + 1 archiviato)
 *   - `'empty'`   → []        (agents-empty baseline)
 *   - All other v2 states (`loading`, `filtered-empty`, `error`) are simulated
 *     by the orchestrator via the `?state=...` URL override hatch and do NOT
 *     hit the fixture.
 *
 * Used by:
 *   - `apps/web/e2e/visual-migrated/sp4-agents-index.spec.ts` (planned Commit 4)
 *   - `apps/web/e2e/v2-states/agents-index.spec.ts` (planned Commit 4)
 */

import type { AgentDto } from '@/lib/api/schemas/agents.schemas';

/**
 * Deterministic UUIDv4-shaped sentinel encoding issue #634 in the last group
 * for human-debuggability. The orchestrator pivots on `IS_VISUAL_TEST_BUILD`,
 * not on this id, so its only role is documentation for triage runs.
 */
export const VISUAL_TEST_FIXTURE_AGENTS_ID = '00000000-0000-4000-8000-000000000634' as const;

/**
 * True only when the build was produced by the visual-regression CI workflow
 * (sets `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` before `pnpm build`).
 *
 * `NEXT_PUBLIC_*` env vars are inlined at build time → in production deploys
 * this is the literal `false`, allowing the bundler to dead-code-eliminate
 * the fixture and its short-circuit branches.
 */
export const IS_VISUAL_TEST_BUILD = process.env.NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED === '1';

export type AgentsFixtureState = 'default' | 'empty';

const NOW = '2026-04-30T10:00:00.000Z';

const baseAgent = (
  overrides: Partial<AgentDto> & Pick<AgentDto, 'id' | 'name' | 'type' | 'strategyName'>
): AgentDto => ({
  strategyParameters: {},
  isActive: true,
  createdAt: NOW,
  lastInvokedAt: null,
  invocationCount: 0,
  isRecentlyUsed: false,
  isIdle: false,
  gameId: null,
  gameName: null,
  createdByUserId: null,
  ...overrides,
});

const FIXTURE_DEFAULT: readonly AgentDto[] = [
  baseAgent({
    id: '00000000-0000-4000-8000-000000000601',
    name: 'Catan Coach',
    type: 'Strategist',
    strategyName: 'qa-tutor',
    isActive: true,
    invocationCount: 142,
    lastInvokedAt: '2026-04-29T14:30:00.000Z',
    isRecentlyUsed: true,
    gameId: '00000000-0000-4000-8000-000000000201',
    gameName: 'Catan',
  }),
  baseAgent({
    id: '00000000-0000-4000-8000-000000000602',
    name: 'Wingspan Rules Expert',
    type: 'Rules',
    strategyName: 'rules-lawyer',
    isActive: true,
    invocationCount: 87,
    lastInvokedAt: '2026-04-28T09:15:00.000Z',
    gameId: '00000000-0000-4000-8000-000000000203',
    gameName: 'Wingspan',
  }),
  baseAgent({
    id: '00000000-0000-4000-8000-000000000603',
    name: 'Terraforming Mars Setup',
    type: 'Setup',
    strategyName: 'setup-guide',
    isActive: true,
    invocationCount: 24,
    lastInvokedAt: '2026-04-25T18:45:00.000Z',
    gameId: '00000000-0000-4000-8000-000000000202',
    gameName: 'Terraforming Mars',
  }),
  baseAgent({
    id: '00000000-0000-4000-8000-000000000604',
    name: 'Azul Tutor',
    type: 'Tutor',
    strategyName: 'qa-tutor',
    isActive: true,
    invocationCount: 0,
    gameId: '00000000-0000-4000-8000-000000000204',
    gameName: 'Azul',
  }),
  baseAgent({
    id: '00000000-0000-4000-8000-000000000605',
    name: 'Carcassonne Strategist',
    type: 'Strategist',
    strategyName: 'rules-lawyer',
    isActive: true,
    invocationCount: 0,
    gameId: '00000000-0000-4000-8000-000000000205',
    gameName: 'Carcassonne',
  }),
  baseAgent({
    id: '00000000-0000-4000-8000-000000000606',
    name: 'Old Pandemic Helper',
    type: 'Rules',
    strategyName: 'qa-tutor',
    isActive: false,
    invocationCount: 53,
    lastInvokedAt: '2026-02-15T12:00:00.000Z',
    isIdle: true,
    gameName: 'Pandemic',
  }),
];

/**
 * Returns deterministic agent entries iff the build is a visual-test build.
 * Returns `null` otherwise — caller MUST fall through to the real fetch.
 */
export function tryLoadVisualTestFixture(
  state: AgentsFixtureState = 'default'
): readonly AgentDto[] | null {
  if (!IS_VISUAL_TEST_BUILD) return null;
  if (state === 'empty') return [];
  return FIXTURE_DEFAULT;
}
