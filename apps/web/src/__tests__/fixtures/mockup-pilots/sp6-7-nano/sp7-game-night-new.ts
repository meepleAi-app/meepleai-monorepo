/**
 * Game Night New page-mock fixtures (DS-17 Phase C-1 — argTypes matrix pattern).
 *
 * Consumed by `sp7-game-night-new` cluster Storybook story con axis matrix:
 *   step:    1 | 2 | 3 | 4
 *   variant: 'default' | 'conflict' | 'submitting' | 'error'
 *
 * Stage axis discovery: grep `STATES = [` in sp7-game-night-new.jsx
 * (config array lines 1475-1484: 8 mobile state + 1 mobile overview + 1 desktop).
 *
 * Refs: spec docs/superpowers/specs/2026-06-11-ds-17-11-sp6-7-nano-cluster-design.md,
 *       umbrella #2063, sub-issue #2166, follow-up #2366 (FE rename).
 *
 * NOTE: this is a SCAFFOLD draft. Co-located with the story under
 *   apps/web/src/__tests__/fixtures/mockup-pilots/sp6-7-nano/sp7-game-night-new.ts
 * for the Storybook tree.
 */

import { http, HttpResponse } from 'msw';

import type { GameCandidateOption } from '@/components/features/game-night-create';
import type { UserSearchResult } from '@/lib/api/schemas/auth.schemas';
import type { RegularDto } from '@/lib/api/schemas/game-nights.schemas';
import type { WizardState } from '@/lib/game-nights/wizard-types';

export type Sp7CreateState = 'default' | 'conflict' | 'submitting' | 'error';

// ── Library catalog (from mockup line 68-75 — INTERNAL catalog, NOT BGG) ──
// IMPORTANT: BGG ban ADR (#2123) — fixture uses local catalog only, never
// `useSearchBggGames` or `cf.geekdo-images.com` hosts.
export const MOCK_SP7_CREATE_LIBRARY_GAMES: readonly GameCandidateOption[] = [
  {
    id: 'g-twilight',
    title: 'Twilight Imperium 4',
    publisher: 'Fantasy Flight',
    coverUrl: undefined,
    minPlayers: 3,
    maxPlayers: 6,
    minPlayTimeMinutes: 240,
    maxPlayTimeMinutes: 480,
  },
  {
    id: 'g-brass',
    title: 'Brass: Birmingham',
    publisher: 'Roxley',
    coverUrl: undefined,
    minPlayers: 2,
    maxPlayers: 4,
    minPlayTimeMinutes: 120,
    maxPlayTimeMinutes: 180,
  },
  {
    id: 'g-spirit',
    title: 'Spirit Island',
    publisher: 'GMT',
    coverUrl: undefined,
    minPlayers: 1,
    maxPlayers: 4,
    minPlayTimeMinutes: 90,
    maxPlayTimeMinutes: 120,
  },
  {
    id: 'g-wingspan',
    title: 'Wingspan',
    publisher: 'Stonemaier',
    coverUrl: undefined,
    minPlayers: 1,
    maxPlayers: 5,
    minPlayTimeMinutes: 40,
    maxPlayTimeMinutes: 70,
  },
  {
    id: 'g-arknova',
    title: 'Ark Nova',
    publisher: 'Feuerland',
    coverUrl: undefined,
    minPlayers: 1,
    maxPlayers: 4,
    minPlayTimeMinutes: 90,
    maxPlayTimeMinutes: 150,
  },
  {
    id: 'g-azul',
    title: 'Azul',
    publisher: 'Plan B',
    coverUrl: undefined,
    minPlayers: 2,
    maxPlayers: 4,
    minPlayTimeMinutes: 30,
    maxPlayTimeMinutes: 45,
  },
] as const;

// ── Regulars from mockup line 56-65 (Marco host + 7 invited mix regulars) ──
export const MOCK_SP7_CREATE_REGULARS: readonly RegularDto[] = [
  {
    userId: 'usr-giulia',
    displayName: 'Giulia M.',
    avatarUrl: undefined,
    lastPlayedAt: '2026-05-10T20:00:00.000Z',
    playCount: 12,
  },
  {
    userId: 'usr-luca',
    displayName: 'Luca B.',
    avatarUrl: undefined,
    lastPlayedAt: '2026-05-08T20:00:00.000Z',
    playCount: 54,
  },
  {
    userId: 'usr-sara',
    displayName: 'Sara T.',
    avatarUrl: undefined,
    lastPlayedAt: '2026-05-12T20:00:00.000Z',
    playCount: 142,
  },
  {
    userId: 'usr-davide',
    displayName: 'Davide C.',
    avatarUrl: undefined,
    lastPlayedAt: '2026-05-05T20:00:00.000Z',
    playCount: 38,
  },
];

// ── Player search results for Step 3 typing-state (query "fede" → Federica) ──
export const MOCK_SP7_CREATE_PLAYER_SEARCH_TYPING: readonly UserSearchResult[] = [
  {
    id: 'usr-federica',
    displayName: 'Federica N.',
    email: 'federica.n@gmail.com',
    avatarUrl: undefined,
  },
];

// ── Wizard state variants (drives the step indicator + payload reducer) ──
// initialWizardState shape comes from wizard-reducer; here we stub minimal
// step+date+location+invitees+games per variant.
export const MOCK_SP7_CREATE_WIZARD_STATE_DEFAULT: WizardState = {
  step: 1,
  date: '2026-05-17',
  startTime: '21:00',
  durationHours: 3,
  location: { kind: 'host', label: 'Casa Marco · Padova' },
  invitees: [],
  emailInvites: [],
  minPlayers: 3,
  maxPlayers: 6,
  autoConfirmRegulars: true,
  games: [],
  decideAtTable: false,
  title: 'Sabato boardgame con i Padovani',
} as unknown as WizardState;

export const MOCK_SP7_CREATE_WIZARD_STATE_STEP2: WizardState = {
  ...MOCK_SP7_CREATE_WIZARD_STATE_DEFAULT,
  step: 2,
} as unknown as WizardState;

export const MOCK_SP7_CREATE_WIZARD_STATE_STEP3_TYPING: WizardState = {
  ...MOCK_SP7_CREATE_WIZARD_STATE_DEFAULT,
  step: 3,
} as unknown as WizardState;

export const MOCK_SP7_CREATE_WIZARD_STATE_STEP3_FILLED: WizardState = {
  ...MOCK_SP7_CREATE_WIZARD_STATE_DEFAULT,
  step: 3,
  invitees: [
    { userId: 'usr-giulia', displayName: 'Giulia M.' },
    { userId: 'usr-luca', displayName: 'Luca B.' },
    { userId: 'usr-sara', displayName: 'Sara T.' },
    { userId: 'usr-davide', displayName: 'Davide C.' },
  ],
  emailInvites: ['federica.n@gmail.com'],
} as unknown as WizardState;

export const MOCK_SP7_CREATE_WIZARD_STATE_STEP4_DECIDE_GROUP: WizardState = {
  ...MOCK_SP7_CREATE_WIZARD_STATE_STEP3_FILLED,
  step: 4,
  decideAtTable: true,
} as unknown as WizardState;

// ── Labels (i18n stub — orchestrator would inject via buildWizardLabels) ──
export const MOCK_SP7_CREATE_LABELS = {
  title: 'Nuova serata',
  subtitle: 'Pianifica una serata con il tuo gruppo',
  nav: { back: 'Indietro', next: 'Avanti', submit: 'Crea serata', cancel: 'Annulla' },
  steps: {
    step1: 'Quando',
    step2: 'Dove',
    step3: 'Chi',
    step4: 'Cosa',
    progress: (c: number, t: number) => `Step ${c}/${t}`,
  },
  a11y: { wizardLabel: 'Wizard creazione serata', stepperLabel: 'Indicatore step' },
  // sub-component labels stubbed minimally — real wiring see _content.tsx buildWizardLabels()
  step1: {} as never,
  step2: {} as never,
  step3: {} as never,
  step4: {} as never,
  preview: {} as never,
} as never;

// ── MSW handlers ─────────────────────────────────────────────────────────
export function mswForSp7CreateState(state: Sp7CreateState) {
  if (state === 'submitting') {
    return [http.post('*/api/v1/game-nights', () => new Promise<Response>(() => {}))];
  }
  if (state === 'error') {
    return [
      http.post('*/api/v1/game-nights', () =>
        HttpResponse.json({ error: 'Conflict detected', code: 'CONFLICT' }, { status: 409 })
      ),
    ];
  }
  if (state === 'conflict') {
    return [
      http.get('*/api/v1/game-nights/conflict-check*', () =>
        HttpResponse.json({
          hasConflict: true,
          conflict: {
            eventId: 'gn-catan-may22',
            title: 'Catan Maratona',
            location: 'Casa Luca',
            overlappingPlayers: ['Giulia', 'Davide', 'Sara', 'Luca'],
          },
        })
      ),
      http.get('*/api/v1/game-nights/regulars*', () => HttpResponse.json(MOCK_SP7_CREATE_REGULARS)),
      http.get('*/api/v1/games*', () =>
        HttpResponse.json({
          items: MOCK_SP7_CREATE_LIBRARY_GAMES,
          total: MOCK_SP7_CREATE_LIBRARY_GAMES.length,
        })
      ),
    ];
  }
  // default
  return [
    http.post('*/api/v1/game-nights', () =>
      HttpResponse.json({ id: 'gn-new-001', status: 'Draft' }, { status: 201 })
    ),
    http.get('*/api/v1/game-nights/conflict-check*', () =>
      HttpResponse.json({ hasConflict: false })
    ),
    http.get('*/api/v1/game-nights/regulars*', () => HttpResponse.json(MOCK_SP7_CREATE_REGULARS)),
    http.get('*/api/v1/games*', () =>
      HttpResponse.json({
        items: MOCK_SP7_CREATE_LIBRARY_GAMES,
        total: MOCK_SP7_CREATE_LIBRARY_GAMES.length,
      })
    ),
    http.get('*/api/v1/users/search*', ({ request }) => {
      const url = new URL(request.url);
      const q = (url.searchParams.get('q') ?? '').toLowerCase();
      if (q.startsWith('fede')) {
        return HttpResponse.json(MOCK_SP7_CREATE_PLAYER_SEARCH_TYPING);
      }
      return HttpResponse.json([]);
    }),
  ];
}
