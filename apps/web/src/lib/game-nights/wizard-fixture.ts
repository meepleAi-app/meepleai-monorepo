/**
 * Visual-regression test fixture for `/game-nights/new` (Issue #950 W2 Foundation).
 *
 * **Purpose**: workflow `visual-regression-migrated.yml` runs only Next.js prod
 * (no backend API at `:8080`). The wizard's hooks (player search, conflict
 * check, regulars) cannot reach the backend in CI → states like
 * `step3-typing` / `step1-warning` never materialise → no screenshot.
 *
 * **Contract**: when env var `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED === '1'`
 * is baked into the build, the wizard orchestrator substitutes the fixture
 * for live hook results and renders the deterministic FSM state.
 *
 * **Production safety**: production builds do NOT set the env var. The
 * constant `IS_VISUAL_TEST_BUILD` evaluates to `false` and every fixture
 * branch is dead code, eliminated by the bundler.
 *
 * State coverage matches spec §4 FSM inventory:
 *   01 `step1-date`         — Step 1 default empty
 *   02 `step1-warning`      — Step 1 conflict warning shown
 *   03 `step2-location`     — Step 2 location toggle visible
 *   04 `step3-empty`        — Step 3 empty + regulars suggestions
 *   05 `step3-typing`       — Step 3 autocomplete dropdown active
 *   06 `step3-filled`       — Step 3 with 6 invitees (mixed registered + email)
 *   07 `step4-games`        — Step 4 game candidates picker
 *   08 `step4-decide-group` — Step 4 "lascia decidere al gruppo" active
 *   09 `mobile-step-flow`   — Mobile multi-step compact overview
 *   10 `desktop-split`      — Desktop split-form (RSVP-card live preview)
 *
 * Used by:
 *   - `apps/web/e2e/visual-migrated/sp7-game-night-create.spec.ts` (Wave 3 Week 4)
 *   - `apps/web/e2e/v2-states/game-night-create.spec.ts` (Wave 3 Week 4)
 *
 * Mirror of: `apps/web/src/lib/agents/agent-detail-visual-test-fixture.ts` (Wave C.2)
 */

import { initialWizardState } from './wizard-reducer';

import type { Invitee, WizardState } from './wizard-types';

/**
 * Deterministic UUIDv4-shaped sentinel encoding issue #950.
 */
export const VISUAL_TEST_FIXTURE_WIZARD_ID = '00000000-0000-4000-8000-000000000950' as const;

/**
 * True only when the build was produced by the visual-regression CI workflow.
 * `NEXT_PUBLIC_*` env vars are inlined at build time → in production deploys
 * this is the literal `false`, allowing the bundler to dead-code-eliminate
 * the fixture and its short-circuit branches.
 */
export const IS_VISUAL_TEST_BUILD = process.env.NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED === '1';

/**
 * Enumerated FSM states from spec §4. Numbered IDs in case the FE uses
 * `?fixture=01` style URLs; named IDs for fixture file headers.
 */
export type WizardFixtureState =
  | 'step1-date'
  | 'step1-warning'
  | 'step2-location'
  | 'step3-empty'
  | 'step3-typing'
  | 'step3-filled'
  | 'step4-games'
  | 'step4-decide-group'
  | 'mobile-step-flow'
  | 'desktop-split';

// Stable deterministic UUIDs for fixture invitees/games. Hard-coded so visual
// snapshots are byte-stable across CI runs.
const FIXTURE_USERS: Record<string, Invitee> = {
  laura: {
    kind: 'user',
    id: '00000000-0000-4000-8000-000000000101',
    displayName: 'Laura Rossi',
    email: 'laura@example.com',
  },
  marco: {
    kind: 'user',
    id: '00000000-0000-4000-8000-000000000102',
    displayName: 'Marco Bianchi',
    email: 'marco@example.com',
  },
  sara: {
    kind: 'user',
    id: '00000000-0000-4000-8000-000000000103',
    displayName: 'Sara Verdi',
    email: 'sara@example.com',
  },
  giulia: {
    kind: 'user',
    id: '00000000-0000-4000-8000-000000000104',
    displayName: 'Giulia Russo',
    email: 'giulia@example.com',
  },
};

const FIXTURE_GAMES = [
  '00000000-0000-4000-8000-000000000201', // Catan
  '00000000-0000-4000-8000-000000000202', // Azul
  '00000000-0000-4000-8000-000000000203', // Wingspan
] as const;

/**
 * Future date 14 days out, frozen for fixture determinism. Real wizard
 * computes its own date in setDate; here we lock the value so screenshots
 * don't drift day-to-day.
 *
 * Note: the spec recommends Date.parse + 1h validation, so this string must
 * remain > 1h ahead of any plausible test-build clock. 2099-12-31 is safely
 * out of band for any conceivable CI environment.
 */
const FIXTURE_DATE_ISO = '2099-12-31T20:00:00.000Z';

/**
 * Returns the wizard state corresponding to the requested FSM fixture.
 * Returns `null` when not in a visual-test build so callers can fall through
 * to live hooks in production.
 */
export function getWizardFixture(state: WizardFixtureState): WizardState | null {
  if (!IS_VISUAL_TEST_BUILD) return null;

  switch (state) {
    case 'step1-date':
      return initialWizardState;

    case 'step1-warning': {
      // Conflict surfaced after running the check-conflict effect. Iso is set,
      // checkedAt is recent, conflictResult contains 1 entry.
      return {
        ...initialWizardState,
        date: {
          iso: FIXTURE_DATE_ISO,
          conflictCheckedAt: '2099-12-30T18:00:00.000Z',
          conflictResult: {
            hasConflict: true,
            conflicts: [
              {
                id: '00000000-0000-4000-8000-000000000301',
                title: 'Festa di compleanno',
                scheduledAt: '2099-12-31T19:00:00.000Z',
                role: 'invitee',
              },
            ],
          },
        },
      };
    }

    case 'step2-location':
      return {
        ...initialWizardState,
        step: 2,
        date: { iso: FIXTURE_DATE_ISO, conflictCheckedAt: null, conflictResult: null },
      };

    case 'step3-empty':
      return {
        ...initialWizardState,
        step: 3,
        date: { iso: FIXTURE_DATE_ISO, conflictCheckedAt: null, conflictResult: null },
        location: { kind: 'friend', details: 'Casa di Sara' },
      };

    case 'step3-typing':
      // Same as step3-empty — the FE renders the typing dropdown based on
      // a sibling React state, not on the wizard reducer. Fixture leaves
      // invitees empty so the dropdown is the dominant visual element.
      return {
        ...initialWizardState,
        step: 3,
        date: { iso: FIXTURE_DATE_ISO, conflictCheckedAt: null, conflictResult: null },
        location: { kind: 'friend', details: 'Casa di Sara' },
      };

    case 'step3-filled':
      return {
        ...initialWizardState,
        step: 3,
        date: { iso: FIXTURE_DATE_ISO, conflictCheckedAt: null, conflictResult: null },
        location: { kind: 'friend', details: 'Casa di Sara' },
        invitees: [
          FIXTURE_USERS.laura,
          FIXTURE_USERS.marco,
          FIXTURE_USERS.sara,
          FIXTURE_USERS.giulia,
          { kind: 'email', address: 'federica@example.com' },
          { kind: 'email', address: 'ospite@example.com' },
        ],
      };

    case 'step4-games':
      return {
        ...initialWizardState,
        step: 4,
        date: { iso: FIXTURE_DATE_ISO, conflictCheckedAt: null, conflictResult: null },
        location: { kind: 'friend', details: 'Casa di Sara' },
        invitees: [FIXTURE_USERS.laura, FIXTURE_USERS.marco, FIXTURE_USERS.sara],
        games: { decideAtGroup: false, selected: [...FIXTURE_GAMES] },
      };

    case 'step4-decide-group':
      return {
        ...initialWizardState,
        step: 4,
        date: { iso: FIXTURE_DATE_ISO, conflictCheckedAt: null, conflictResult: null },
        location: { kind: 'friend', details: 'Casa di Sara' },
        invitees: [FIXTURE_USERS.laura, FIXTURE_USERS.marco, FIXTURE_USERS.sara],
        games: { decideAtGroup: true, selected: [] },
      };

    case 'mobile-step-flow':
      // Compact overview: all 4 steps side-by-side. Reducer state reflects
      // the "moving from 3 → 4" mid-flow situation.
      return {
        ...initialWizardState,
        step: 3,
        date: { iso: FIXTURE_DATE_ISO, conflictCheckedAt: null, conflictResult: null },
        location: { kind: 'friend', details: 'Casa di Sara' },
        invitees: [FIXTURE_USERS.laura, FIXTURE_USERS.marco],
      };

    case 'desktop-split':
      // Live preview consumes the full state; same shape as step4-games
      // but the desktop layout is what's being snapshot, not the data.
      return {
        ...initialWizardState,
        step: 4,
        date: { iso: FIXTURE_DATE_ISO, conflictCheckedAt: null, conflictResult: null },
        location: { kind: 'friend', details: 'Casa di Sara' },
        invitees: [FIXTURE_USERS.laura, FIXTURE_USERS.marco, FIXTURE_USERS.sara],
        games: { decideAtGroup: false, selected: [...FIXTURE_GAMES] },
      };

    default: {
      const _exhaustive: never = state;
      void _exhaustive;
      return null;
    }
  }
}

/**
 * Convenience for E2E specs that pivot on a `?fixture=…` URL parameter.
 * Returns the fixture state for known values, `null` otherwise.
 */
export function parseFixtureParam(value: string | null | undefined): WizardFixtureState | null {
  if (!value) return null;
  const known: WizardFixtureState[] = [
    'step1-date',
    'step1-warning',
    'step2-location',
    'step3-empty',
    'step3-typing',
    'step3-filled',
    'step4-games',
    'step4-decide-group',
    'mobile-step-flow',
    'desktop-split',
  ];
  return (known as string[]).includes(value) ? (value as WizardFixtureState) : null;
}
