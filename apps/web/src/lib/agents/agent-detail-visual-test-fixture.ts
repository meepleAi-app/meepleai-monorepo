/**
 * Visual-regression test fixture for `/agents/[id]` (Wave C.2, Issue #581).
 *
 * **Purpose**: workflow `visual-regression-migrated.yml` runs only Next.js prod
 * (no backend API at `:8080`). The agent-detail hook cannot reach the backend in
 * CI → the surface stays in `loading` forever → no screenshot.
 *
 * **Contract**: when env var `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED === '1'`
 * is baked into the build, the orchestrator (AgentDetailViewV2) substitutes
 * the fixture for the real fetch and renders deterministic data.
 *
 * **Production safety**: production builds do NOT set the env var. The
 * constant `IS_VISUAL_TEST_BUILD` evaluates to `false` and every fixture
 * branch is dead code, eliminated by the bundler. The fixture UUID is
 * meaningless to a production deployment.
 *
 * State coverage:
 *   - `'default'`    → active agent WITH gameId set (hero + 5 tabs populated)
 *                      Used for the primary visual baseline (sp4-agent-detail.spec.ts)
 *   - `'standalone'` → active agent WITH gameId === null (Cell 10 visual baseline)
 *                      Knowledge tab shows "standalone" empty state, not the KB list
 *   - `'not-found'`  → null (agent not found — mirrors Cell 4 not-found shell)
 *   - All other v2 states (`loading`, `error`) are simulated by the orchestrator
 *     via the `?state=...` URL override hatch and do NOT hit the fixture.
 *
 * Used by:
 *   - `apps/web/e2e/visual-migrated/sp4-agent-detail.spec.ts` (Task 4)
 *   - `apps/web/e2e/v2-states/agent-detail.spec.ts` (Task 4)
 *
 * Mirror of: `apps/web/src/lib/games/game-detail-visual-test-fixture.ts` (Wave C.1)
 * Extended with: 'standalone' state for Cell 10 (Wave C.2 unique)
 */

import type { AgentDto } from '@/lib/api/schemas/agents.schemas';

/**
 * Deterministic UUIDv4-shaped sentinel encoding issue #581 (Wave C umbrella)
 * and agent detail indicator in the last group for human-debuggability.
 * The orchestrator pivots on `IS_VISUAL_TEST_BUILD`, not on this id, so
 * its only role is documentation for triage runs.
 */
export const VISUAL_TEST_FIXTURE_AGENT_DETAIL_ID = '00000000-0000-4000-8000-000000000581' as const;

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
 * Fixture state selector for `/agents/[id]` visual tests.
 *
 * - `'default'`    → fully active agent with gameId (primary baseline)
 * - `'standalone'` → active agent without gameId (Cell 10 — Knowledge standalone)
 * - `'not-found'`  → null (not-found shell)
 */
export type AgentDetailFixtureState = 'default' | 'standalone' | 'not-found';

const NOW = '2026-05-04T10:00:00.000Z';
const GAME_ID = '00000000-0000-4000-8000-000000000201';

/**
 * Shared base for all fixture agents.
 * Represents a fully-configured active Catan strategy agent.
 */
const FIXTURE_DEFAULT: AgentDto = {
  id: VISUAL_TEST_FIXTURE_AGENT_DETAIL_ID,
  name: 'Catan Strategist Pro',
  type: 'Strategist',
  strategyName: 'HybridSearch',
  strategyParameters: {
    maxResults: 10,
    confidenceThreshold: 0.75,
    enableCitations: true,
  },
  isActive: true,
  createdAt: '2026-04-01T08:00:00.000Z',
  lastInvokedAt: '2026-05-03T14:30:00.000Z',
  invocationCount: 142,
  isRecentlyUsed: true,
  isIdle: false,
  gameId: GAME_ID,
  gameName: 'Catan',
  createdByUserId: '00000000-0000-4000-8000-000000000aaa',
};

/**
 * Standalone agent fixture: same as default but WITHOUT a game association.
 * Used for Cell 10 visual baseline — Knowledge tab shows standalone empty state.
 */
const FIXTURE_STANDALONE: AgentDto = {
  ...FIXTURE_DEFAULT,
  id: '00000000-0000-4000-8000-000000005810',
  name: 'General Assistant',
  type: 'General',
  strategyName: 'VectorOnly',
  strategyParameters: {},
  invocationCount: 27,
  lastInvokedAt: NOW,
  gameId: null,
  gameName: null,
};

/**
 * Returns deterministic AgentDto iff the build is a visual-test build.
 * Returns `null` for `'not-found'` state always (mirrors not-found shell).
 * Returns `null` in dev/prod builds — caller MUST fall through to the real fetch.
 *
 * @param state - Which fixture state to load (default: 'default')
 * @returns AgentDto for 'default'/'standalone' in visual-test builds, null otherwise
 */
export function tryLoadVisualTestFixture(
  state: AgentDetailFixtureState = 'default'
): AgentDto | null {
  if (!IS_VISUAL_TEST_BUILD) return null;
  if (state === 'not-found') return null;
  if (state === 'standalone') return FIXTURE_STANDALONE;
  return FIXTURE_DEFAULT;
}
