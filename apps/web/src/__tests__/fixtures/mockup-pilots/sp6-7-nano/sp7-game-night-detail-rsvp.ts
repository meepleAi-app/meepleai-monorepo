/**
 * Game Night Detail + RSVP page-mock fixtures (DS-17 Phase C-1 — argTypes matrix pattern).
 *
 * Consumed by `sp7-game-night-detail-rsvp` cluster Storybook story con axis matrix:
 *   state: 'host-pending' | 'host-ready' | 'invitee-pending' | 'invitee-confirmed'
 *        | 'voting-active' | 'voting-tied' | 'cancelled' | 'in-progress'
 *        | 'completed' | 'loading' | 'error'
 *
 * Stage axis discovery: grep `STATES = [` in sp7-game-night-detail-rsvp.jsx
 * (config array lines 1346-1367: 10 mobile state + 2 desktop split-view).
 *
 * Refs: spec docs/superpowers/specs/2026-06-11-ds-17-11-sp6-7-nano-cluster-design.md,
 *       umbrella #2063, sub-issue #2166.
 *
 * NOTE: this is a SCAFFOLD draft. Move to
 *   apps/web/src/__tests__/fixtures/mockup-pilots/sp6-7-nano/sp7-game-night-detail-rsvp.ts
 * when wiring into the Storybook tree.
 */

import { http, HttpResponse } from 'msw';

export type Sp7DetailRsvpState =
  | 'host-pending'
  | 'host-ready'
  | 'invitee-pending'
  | 'invitee-confirmed'
  | 'voting-active'
  | 'voting-tied'
  | 'cancelled'
  | 'in-progress'
  | 'completed'
  | 'loading'
  | 'error';

// ── Shared base fixtures ─────────────────────────────────────────────────

const HOST_USER = {
  id: 'usr-marco',
  email: 'marco@example.com',
  displayName: 'Marco R.',
  role: 'User' as const,
  emailVerified: true,
};

const INVITEE_USER = {
  id: 'usr-davide',
  email: 'davide@example.com',
  displayName: 'Davide C.',
  role: 'User' as const,
  emailVerified: true,
};

const NIGHT_BASE = {
  id: 'gn-padovani-may17',
  title: 'Sabato boardgame con i Padovani',
  organizerId: 'usr-marco',
  organizerName: 'Marco R.',
  scheduledAt: '2026-05-17T21:00:00.000Z',
  location: 'Casa Marco · Padova',
  estimatedDurationMinutes: 180,
  gameIds: ['g-twilight', 'g-brass', 'g-spirit'],
  createdAt: '2026-05-07T20:00:00.000Z',
};

// ── RSVPs by state ───────────────────────────────────────────────────────

const RSVPS_HOST_PENDING = [
  { userId: 'usr-marco', userName: 'Marco R.', status: 'Accepted' },
  { userId: 'usr-giulia', userName: 'Giulia M.', status: 'Accepted' },
  { userId: 'usr-luca', userName: 'Luca B.', status: 'Accepted' },
  { userId: 'usr-sara', userName: 'Sara T.', status: 'Pending' },
  { userId: 'usr-davide', userName: 'Davide C.', status: 'Pending' },
  { userId: 'usr-aaron', userName: 'Aaron K.', status: 'Pending' },
  { userId: 'usr-matteo', userName: 'Matteo Z.', status: 'Pending' },
  { userId: 'usr-federica', userName: 'Federica N.', status: 'Pending' },
];

const RSVPS_HOST_READY = RSVPS_HOST_PENDING.map(r => ({ ...r, status: 'Accepted' as const }));

const RSVPS_INVITEE_PENDING = RSVPS_HOST_PENDING;

const RSVPS_INVITEE_CONFIRMED = RSVPS_HOST_PENDING.map(r =>
  r.userId === 'usr-davide' ? { ...r, status: 'Accepted' as const } : r
);

// ── Per-state event payload ──────────────────────────────────────────────

function eventForState(state: Sp7DetailRsvpState) {
  switch (state) {
    case 'cancelled':
      return { ...NIGHT_BASE, status: 'Cancelled', cancellationReason: 'Conflitto reggia' };
    case 'in-progress':
      return { ...NIGHT_BASE, status: 'Published', startedAt: '2026-05-17T21:00:00.000Z' };
    case 'completed':
      return { ...NIGHT_BASE, status: 'Completed', completedAt: '2026-05-18T02:30:00.000Z' };
    default:
      return { ...NIGHT_BASE, status: 'Published' };
  }
}

function rsvpsForState(state: Sp7DetailRsvpState) {
  switch (state) {
    case 'host-ready':
      return RSVPS_HOST_READY;
    case 'invitee-confirmed':
      return RSVPS_INVITEE_CONFIRMED;
    case 'invitee-pending':
      return RSVPS_INVITEE_PENDING;
    default:
      return RSVPS_HOST_PENDING;
  }
}

function viewerForState(state: Sp7DetailRsvpState) {
  return state.startsWith('invitee') ? INVITEE_USER : HOST_USER;
}

// ── MSW handlers ─────────────────────────────────────────────────────────

export function mswForSp7DetailRsvpState(state: Sp7DetailRsvpState) {
  if (state === 'loading') {
    return [
      http.get('*/api/v1/auth/me', () => HttpResponse.json(HOST_USER)),
      http.get('*/api/v1/game-nights/:id', () => new Promise<Response>(() => {})),
    ];
  }
  if (state === 'error') {
    return [
      http.get('*/api/v1/auth/me', () => HttpResponse.json(HOST_USER)),
      http.get('*/api/v1/game-nights/:id', () =>
        HttpResponse.json({ error: 'Not found' }, { status: 404 })
      ),
    ];
  }

  const viewer = viewerForState(state);
  const event = eventForState(state);
  const rsvps = rsvpsForState(state);

  return [
    http.get('*/api/v1/auth/me', () => HttpResponse.json(viewer)),
    http.get('*/api/v1/game-nights/:id', () =>
      HttpResponse.json({
        event,
        rsvps,
      })
    ),
    http.get('*/api/v1/shared-games*', () =>
      HttpResponse.json({
        items: [
          {
            id: 'g-twilight',
            title: 'Twilight Imperium 4',
            minPlayers: 3,
            maxPlayers: 6,
            thumbnailUrl: null,
          },
          {
            id: 'g-brass',
            title: 'Brass: Birmingham',
            minPlayers: 2,
            maxPlayers: 4,
            thumbnailUrl: null,
          },
          {
            id: 'g-spirit',
            title: 'Spirit Island',
            minPlayers: 1,
            maxPlayers: 4,
            thumbnailUrl: null,
          },
        ],
      })
    ),
    http.post('*/api/v1/game-nights/:id/rsvp', () =>
      HttpResponse.json({ status: 'Accepted', updatedAt: new Date().toISOString() })
    ),
    http.post('*/api/v1/game-nights/:id/cancel', () => HttpResponse.json({ status: 'Cancelled' })),
    http.post('*/api/v1/game-nights/:id/publish', () => HttpResponse.json({ status: 'Published' })),
  ];
}
