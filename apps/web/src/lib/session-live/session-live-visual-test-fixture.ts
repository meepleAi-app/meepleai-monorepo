/**
 * Visual-regression test fixture for `/sessions/[id]/live` (Wave D.2).
 *
 * **Purpose**: workflow `visual-regression-migrated.yml` runs only Next.js prod
 * (no backend API at `:8080`). The session live hooks (useSession +
 * useSessionLiveStream) cannot reach the backend in CI → the surface stays
 * in `loading` forever → no screenshot.
 *
 * **Contract**: when env var `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED === '1'`
 * is baked into the build, the orchestrator (SessionLiveView) substitutes the
 * fixture for the real fetch and renders deterministic entries.
 *
 * **Production safety**: production builds do NOT set the env var. The constant
 * `IS_VISUAL_TEST_BUILD` evaluates to `false` and every fixture branch is dead
 * code, eliminated by the bundler. The fixture UUIDs are meaningless to a
 * production deployment.
 *
 * State coverage:
 *   - `'default'` (Player role)  → 1 active session, 5 players, 5-entry action log
 *   - `'not-found'`              → simulated via ?state=not-found URL override
 *   - `'loading'`                → simulated via ?state=loading URL override
 *   - `'error'`                  → NOT available via URL override (TanStack isError
 *                                  not reproducible deterministically via URL — covered
 *                                  by unit test instead; Wave B.1 lesson applied)
 *
 * Role variants for role-matrix visual baselines:
 *   - VISUAL_TEST_FIXTURE_SESSION          → Player (default)
 *   - VISUAL_TEST_FIXTURE_SESSION_AS_HOST  → Host
 *   - VISUAL_TEST_FIXTURE_SESSION_AS_SPECTATOR → Spectator
 *   - VISUAL_TEST_FIXTURE_SESSION_PAUSED   → Paused status (Player role)
 *
 * Wave D.2 Foundation sub-PR — Issue #746
 */

import type { ParticipantRole } from './participant-role';
import type { SessionLiveUiState } from './session-live-state';

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

// ---------------------------------------------------------------------------
// Fixture shape types
// ---------------------------------------------------------------------------

export interface LiveSessionFixture {
  readonly id: string; // UUID
  readonly name: string;
  readonly status: 'InProgress' | 'Paused';
  readonly viewerRole: ParticipantRole;
  readonly viewerId: string;
  readonly currentTurn: number;
  readonly totalTurns: number;
  readonly activePlayerId: string;
  readonly players: ReadonlyArray<LiveSessionFixturePlayer>;
  readonly actionLog: ReadonlyArray<LiveSessionFixtureLogEntry>;
}

export interface LiveSessionFixturePlayer {
  readonly id: string;
  readonly name: string;
  readonly role: ParticipantRole;
  readonly score: number;
  readonly isOnline: boolean;
}

export interface LiveSessionFixtureLogEntry {
  readonly id: string;
  readonly type: 'score' | 'tool' | 'agent' | 'chat' | 'photo' | 'event';
  readonly authorName: string;
  readonly content: string;
  readonly timestamp: string;
}

// ---------------------------------------------------------------------------
// Deterministic fixture data
// ---------------------------------------------------------------------------

/**
 * Wave D.2 sentinel UUID — encodes issue #746 in last group for human-debuggability.
 * Pattern: 00000000-0000-4000-8000-000000000XXX (mirror Wave D.1 pattern).
 * Hex d20 = 3360 decimal — last 3 hex digits 'd20' reference D.2 (D=0xd, 2=0x2, 0=0x0).
 */

/**
 * Primary fixture: Player role, InProgress session.
 * 5 players covering all 3 roles: 1 Host, 3 Player, 1 Spectator.
 * Action log covers all 5 entry types used by ActionLogTimeline component.
 *
 * NOTE: ICU plural context:
 * Orchestrator (Task 3) MUST use t(key, { count, total }) — never raw string replace.
 * Example: t('pages.sessionLive.topBar.turnLabel', { count: 12, total: 18 })
 */
export const VISUAL_TEST_FIXTURE_SESSION: LiveSessionFixture = {
  id: '00000000-0000-4000-8000-000000000d20',
  name: 'Wingspan da Marco — fixture',
  status: 'InProgress',
  viewerRole: 'Player',
  viewerId: '00000000-0000-4000-8000-0000000000a1',
  currentTurn: 12,
  totalTurns: 18,
  activePlayerId: '00000000-0000-4000-8000-0000000000a1',
  players: [
    {
      id: '00000000-0000-4000-8000-0000000000a1',
      name: 'Marco',
      role: 'Host',
      score: 32,
      isOnline: true,
    },
    {
      id: '00000000-0000-4000-8000-0000000000a2',
      name: 'Anna',
      role: 'Player',
      score: 28,
      isOnline: true,
    },
    {
      id: '00000000-0000-4000-8000-0000000000a3',
      name: 'Luca',
      role: 'Player',
      score: 24,
      isOnline: true,
    },
    {
      id: '00000000-0000-4000-8000-0000000000a4',
      name: 'Sara',
      role: 'Player',
      score: 19,
      isOnline: false,
    },
    {
      id: '00000000-0000-4000-8000-0000000000a5',
      name: 'Giulia',
      role: 'Spectator',
      score: 0,
      isOnline: true,
    },
  ],
  actionLog: [
    {
      id: 'log-1',
      type: 'score',
      authorName: 'Marco',
      content: 'Marco +5 punti (uovo)',
      timestamp: '2026-05-05T18:30:00Z',
    },
    {
      id: 'log-2',
      type: 'tool',
      authorName: 'Anna',
      content: 'Anna ha tirato 1d6 → 4',
      timestamp: '2026-05-05T18:32:00Z',
    },
    {
      id: 'log-3',
      type: 'chat',
      authorName: 'Luca',
      content: 'Bella mossa!',
      timestamp: '2026-05-05T18:33:00Z',
    },
    {
      id: 'log-4',
      type: 'agent',
      authorName: 'BrainstormBot',
      content: 'Suggerimento: gioca la carta foresta',
      timestamp: '2026-05-05T18:34:00Z',
    },
    {
      id: 'log-5',
      type: 'event',
      authorName: 'Marco',
      content: 'Turno 12/18 iniziato',
      timestamp: '2026-05-05T18:35:00Z',
    },
  ],
};

/**
 * Spectator role variant — same session, viewer is Giulia (Spectator).
 * Used for role-matrix visual baselines (spectator sees read-only UI, no write CTAs).
 */
export const VISUAL_TEST_FIXTURE_SESSION_AS_SPECTATOR: LiveSessionFixture = {
  ...VISUAL_TEST_FIXTURE_SESSION,
  viewerRole: 'Spectator',
  viewerId: '00000000-0000-4000-8000-0000000000a5',
};

/**
 * Host role variant — same session, viewer is Marco (Host).
 * Host sees all write CTAs: pause, resume, endgame, kick participant.
 */
export const VISUAL_TEST_FIXTURE_SESSION_AS_HOST: LiveSessionFixture = {
  ...VISUAL_TEST_FIXTURE_SESSION,
  viewerRole: 'Host',
  viewerId: '00000000-0000-4000-8000-0000000000a1',
};

/**
 * Paused status variant — same session (Player role), status is Paused.
 * Triggers PauseOverlay component rendering.
 */
export const VISUAL_TEST_FIXTURE_SESSION_PAUSED: LiveSessionFixture = {
  ...VISUAL_TEST_FIXTURE_SESSION,
  status: 'Paused',
};

// ---------------------------------------------------------------------------
// State override hatch
// ---------------------------------------------------------------------------

/**
 * Parses the `?state=` URL search param into a SessionLiveUiState override.
 *
 * Only valid when `STATE_OVERRIDE_ENABLED` is true (dev/test or visual-test CI builds).
 * Returns `null` in production or for unknown/unsupported state values.
 *
 * Valid overrides: 'loading', 'not-found'
 * NOT valid: 'error' — TanStack Query isError is not reproducible via URL deterministically.
 *   (Wave B.1 lesson applied: visual baseline for error state covered by unit test instead.)
 *
 * Additional D.2 specific overrides (to support connection-state visual baselines):
 *   'connection-lost' → forces ConnectionLostBanner shell
 *   'pause-dialog'    → forces PauseOverlay open (visual baseline)
 *   'endgame-dialog'  → forces EndgameDialog open (visual baseline)
 *   These are Interactions sub-PR concerns; Foundation sub-PR only handles FSM states.
 */
export function parseStateOverride(searchParams: URLSearchParams): SessionLiveUiState | null {
  if (!STATE_OVERRIDE_ENABLED) return null;
  const raw = searchParams.get('state');
  if (raw === 'loading') return 'loading';
  if (raw === 'not-found') return 'not-found';
  // 'error' intentionally excluded — not reproducible via URL deterministically.
  return null;
}
