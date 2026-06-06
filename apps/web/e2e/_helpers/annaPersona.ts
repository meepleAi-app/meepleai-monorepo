/**
 * Issue #1929 Task C (DEC-C-1) — Anna persona canonical fixture.
 *
 * Anna is the **single primary actor** across all cross-asse user journey
 * spec files (Journey #1 + #2 + #3). Deterministic fields enable
 * reproducible BE entity seeding (via `seedEntities.ts` factory) and
 * stable FE auth seeding (via `seedAuthSession` / `mockAuthEndpoints`).
 *
 * Each journey starts with a different initial entity state (defined by
 * `buildAnnaInitialState(journeyId)`), which the spec's `beforeEach`
 * translates into a sequence of BE seed calls scoped to a fresh
 * `testRunId` (DEC-B-5).
 *
 * Spec ref: `docs/superpowers/specs/2026-06-05-asse-d-p4-followup-spec-panel-review.md` DEC-C-1.
 */

export type JourneyId = 'journey1' | 'journey2' | 'journey3';

export interface AnnaPersona {
  readonly email: string;
  readonly displayName: string;
  readonly role: 'user';
  readonly userId: string;
  readonly onboardingCompleted: boolean;
}

export const ANNA_PERSONA: AnnaPersona = {
  email: 'anna.host@meepleai.test',
  displayName: 'Anna Host',
  role: 'user',
  userId: '00000000-0000-4000-8000-000000000001',
  onboardingCompleted: true,
};

export interface AnnaInitialState {
  readonly journeyId: JourneyId;
  readonly gameNightCount: number;
  readonly gameNightStatus: 'Draft' | 'Published' | 'InProgress' | 'Completed' | null;
  readonly playerRosterCount: number;
  readonly libraryGameCount: number;
  readonly sessionCount: number;
  readonly sessionStatus: 'InProgress' | 'Completed' | null;
}

const JOURNEY_INITIAL_STATES: Record<JourneyId, AnnaInitialState> = {
  journey1: {
    journeyId: 'journey1',
    gameNightCount: 1,
    gameNightStatus: 'Published',
    playerRosterCount: 2,
    libraryGameCount: 0,
    sessionCount: 0,
    sessionStatus: null,
  },
  journey2: {
    journeyId: 'journey2',
    gameNightCount: 0,
    gameNightStatus: null,
    playerRosterCount: 0,
    libraryGameCount: 1,
    sessionCount: 0,
    sessionStatus: null,
  },
  journey3: {
    journeyId: 'journey3',
    gameNightCount: 0,
    gameNightStatus: null,
    playerRosterCount: 0,
    libraryGameCount: 1,
    sessionCount: 15,
    sessionStatus: 'Completed',
  },
};

export function buildAnnaInitialState(journeyId: JourneyId): AnnaInitialState {
  const state = JOURNEY_INITIAL_STATES[journeyId];
  if (!state) {
    throw new Error(`buildAnnaInitialState: unknown journey id "${journeyId}"`);
  }
  return state;
}
