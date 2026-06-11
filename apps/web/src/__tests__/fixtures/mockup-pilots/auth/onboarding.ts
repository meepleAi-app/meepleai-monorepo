/**
 * Onboarding page-mock fixtures (DS-17 Phase C-1 — argTypes matrix pattern).
 *
 * Consumed by `onboarding` cluster Storybook story con axis matrix:
 *   step:  0 | 1 | 2 | 3 | 4
 *   state: 'default' | 'selected-min3' | 'all-selected' | 'completion-confetti'
 *
 * Stage axis discovery: grep `GAMES|AGENTS|ACTIONS|MIN_SELECTED|STEP_LABELS`
 * in onboarding.jsx (lines 9-37).
 *
 * BGG legal constraint (#1903 ADR): User-side BGG access blocked. Catalog
 * suggestions use internal `api.games.getAll`, NOT `useSearchBggGames`.
 *
 * Refs: spec docs/superpowers/specs/2026-06-11-ds-17-phase-c-pilot-migration-design.md,
 *       umbrella #2063, sub-issue #2160.
 */

import { http, HttpResponse } from 'msw';

import type { Game } from '@/lib/api/schemas/games.schemas';

export type OnboardingState = 'default' | 'selected-min3' | 'all-selected' | 'completion-confetti';

const baseGame = (overrides: Partial<Game>): Game => ({
  id: '00000000-0000-0000-0000-000000000000',
  title: 'Sample Game',
  publisher: null,
  yearPublished: null,
  minPlayers: 1,
  maxPlayers: 4,
  minPlayTimeMinutes: 30,
  maxPlayTimeMinutes: 90,
  bggId: 0,
  createdAt: '2026-06-11T00:00:00.000Z',
  ...overrides,
});

export const MOCK_ONBOARDING_CATALOG_DEFAULT: Game[] = [
  baseGame({
    id: 'g-catan',
    title: 'Catan',
    publisher: 'Kosmos',
    yearPublished: 1995,
    minPlayers: 3,
    maxPlayers: 4,
  }),
  baseGame({
    id: 'g-carcassonne',
    title: 'Carcassonne',
    publisher: 'Hans im Glück',
    yearPublished: 2000,
    minPlayers: 2,
    maxPlayers: 5,
  }),
  baseGame({
    id: 'g-ticket',
    title: 'Ticket to Ride',
    publisher: 'Days of Wonder',
    yearPublished: 2004,
    minPlayers: 2,
    maxPlayers: 5,
  }),
  baseGame({
    id: 'g-wingspan',
    title: 'Wingspan',
    publisher: 'Stonemaier',
    yearPublished: 2019,
    minPlayers: 1,
    maxPlayers: 5,
  }),
];

export const MOCK_ONBOARDING_CATALOG_EMPTY: Game[] = [];

export const MOCK_ONBOARDING_COMPLETE_SUCCESS = {
  user: {
    id: 'usr_meeple_demo',
    email: 'marco@example.com',
    displayName: 'Marco',
    role: 'User' as const,
    emailVerified: true,
    onboardingCompleted: true,
  },
};

export function mswForOnboardingState(state: OnboardingState) {
  return [
    http.get('*/api/v1/games', () => HttpResponse.json(MOCK_ONBOARDING_CATALOG_DEFAULT)),
    http.post('*/api/v1/auth/onboarding/complete', () =>
      HttpResponse.json(MOCK_ONBOARDING_COMPLETE_SUCCESS)
    ),
    http.get('*/api/v1/auth/me', () =>
      HttpResponse.json({
        id: 'usr_meeple_demo',
        email: 'marco@example.com',
        displayName: 'Marco',
        role: 'User' as const,
        emailVerified: true,
        onboardingCompleted: state === 'completion-confetti',
      })
    ),
  ];
}
