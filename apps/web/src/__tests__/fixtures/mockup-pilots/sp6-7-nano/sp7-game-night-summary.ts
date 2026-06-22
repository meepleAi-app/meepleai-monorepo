/**
 * Game Night Summary page-mock fixtures (DS-17 Phase C-1 — argTypes matrix pattern).
 *
 * Consumed by `sp7-game-night-summary` cluster Storybook story con axis matrix:
 *   archived: boolean
 *   mobile:   boolean
 *   games:    full (3) | single (1) | empty
 *   photos:   full (6) | empty
 *   mvp:      MVP | null
 *   shareSuccess: { visible: true } | undefined
 *
 * Stage axis discovery: grep `<PhoneShell id=` + `<DesktopShell id=` in
 * sp7-game-night-summary.jsx lines 1046-1098 (6 frames total).
 *
 * Refs: spec docs/superpowers/specs/2026-06-11-ds-17-11-sp6-7-nano-cluster-design.md,
 *       umbrella #2063, sub-issue #2166.
 *
 * NOTE: this is a SCAFFOLD draft. Move to
 *   apps/web/src/__tests__/fixtures/mockup-pilots/sp6-7-nano/sp7-game-night-summary.ts
 * when wiring into the Storybook tree.
 */

import { http, HttpResponse } from 'msw';

import type {
  NightSummaryMVP,
  NightSummaryNight,
  NightSummaryPhoto,
  PerGameRecapGame,
} from '@/components/features/game-nights/summary';

export type Sp7SummaryState = 'default' | 'loading' | 'error';

// ── Night metadata ───────────────────────────────────────────────────────
export const MOCK_SP7_SUMMARY_NIGHT: NightSummaryNight = {
  title: 'Sabato boardgame con i Padovani',
  dateLine: 'sabato 17 maggio 2026',
  location: 'Casa Marco · Padova',
  startedAt: '21:00',
  endedAt: '03:15',
  duration: '6h 15m',
  nightCode: '#GN-042',
};

// ── MVP ──────────────────────────────────────────────────────────────────
export const MOCK_SP7_SUMMARY_MVP: NightSummaryMVP = {
  id: 'p-davide',
  name: 'Davide',
  initials: 'DC',
  color: 200,
  achievements: '1 vittoria · 11 eventi diary · top scorer Brass',
};

// ── Games per frame (full 3 games vs single 1 game) ──────────────────────
export const MOCK_SP7_SUMMARY_GAMES_FULL: ReadonlyArray<PerGameRecapGame> = [
  {
    id: 'gs-brass-1',
    sessionId: 's-brass-may17',
    title: 'Brass: Birmingham',
    emoji: '🏭',
    cover: ['hsl(220 35% 28%)', 'hsl(28 60% 38%)'],
    order: 1,
    duration: '2h 45m',
    eventsCount: 11,
    winner: { id: 'p-davide', name: 'Davide', initials: 'DC', color: 200, score: 178 },
    topScores: [
      { id: 'p-marco', name: 'Marco', initials: 'MR', color: 262, score: 142 },
      { id: 'p-giulia', name: 'Giulia', initials: 'GM', color: 10, score: 128 },
    ],
  },
  {
    id: 'gs-spirit-1',
    sessionId: 's-spirit-may17',
    title: 'Spirit Island',
    emoji: '🌋',
    cover: ['hsl(210 50% 30%)', 'hsl(150 50% 38%)'],
    order: 2,
    duration: '1h 50m',
    eventsCount: 9,
    coopMode: true,
    topScores: [
      { id: 'p-marco', name: 'Marco', initials: 'MR', color: 262, score: 0 },
      { id: 'p-davide', name: 'Davide', initials: 'DC', color: 200, score: 0 },
    ],
  },
  {
    id: 'gs-wing-1',
    sessionId: 's-wing-may17',
    title: 'Wingspan',
    emoji: '🦜',
    cover: ['hsl(85 40% 45%)', 'hsl(35 60% 50%)'],
    order: 3,
    duration: '1h 10m',
    eventsCount: 8,
    winner: { id: 'p-sara', name: 'Sara', initials: 'ST', color: 320, score: 96 },
    topScores: [
      { id: 'p-giulia', name: 'Giulia', initials: 'GM', color: 10, score: 88 },
      { id: 'p-aaron', name: 'Aaron', initials: 'AK', color: 140, score: 74 },
    ],
  },
];

export const MOCK_SP7_SUMMARY_GAMES_SINGLE: ReadonlyArray<PerGameRecapGame> = [
  MOCK_SP7_SUMMARY_GAMES_FULL[0],
];

// ── Photos per frame ────────────────────────────────────────────────────
export const MOCK_SP7_SUMMARY_PHOTOS_FULL: ReadonlyArray<NightSummaryPhoto> = [
  { id: 'ph01', label: 'Setup', gradient: ['hsl(220 35% 28%)', 'hsl(28 60% 38%)'] },
  { id: 'ph02', label: 'Era 1', gradient: ['hsl(220 60% 30%)', 'hsl(28 80% 50%)'] },
  { id: 'ph03', label: 'Brass winner', gradient: ['hsl(40 70% 50%)', 'hsl(20 80% 45%)'] },
  { id: 'ph04', label: 'Spirit setup', gradient: ['hsl(210 50% 30%)', 'hsl(150 50% 38%)'] },
  { id: 'ph05', label: 'Boss panico', gradient: ['hsl(150 60% 35%)', 'hsl(190 60% 30%)'] },
  { id: 'ph06', label: 'Wingspan birds', gradient: ['hsl(85 40% 45%)', 'hsl(35 60% 50%)'] },
];

export const MOCK_SP7_SUMMARY_PHOTOS_EMPTY: ReadonlyArray<NightSummaryPhoto> = [];

// ── Diary event counts ──────────────────────────────────────────────────
export const MOCK_SP7_SUMMARY_EVENTS_COUNT_FULL = 28;
export const MOCK_SP7_SUMMARY_EVENTS_COUNT_SINGLE = 11;

// ── MSW handlers ────────────────────────────────────────────────────────
// NOTE: NightSummaryView is presentational — all data comes via props.
// Handlers exist for future `useGameNightSummary(id)` integration and for
// share/archive mutation wiring (page-client TODO).
export function mswForSp7SummaryState(state: Sp7SummaryState) {
  if (state === 'loading') {
    return [
      http.get('*/api/v1/auth/me', () =>
        HttpResponse.json({
          id: 'usr-marco',
          email: 'marco@example.com',
          displayName: 'Marco R.',
          role: 'User' as const,
          emailVerified: true,
        })
      ),
      http.get('*/api/v1/game-nights/:id/summary', () => new Promise<Response>(() => {})),
    ];
  }
  if (state === 'error') {
    return [
      http.get('*/api/v1/auth/me', () =>
        HttpResponse.json({
          id: 'usr-marco',
          email: 'marco@example.com',
          displayName: 'Marco R.',
          role: 'User' as const,
          emailVerified: true,
        })
      ),
      http.get('*/api/v1/game-nights/:id/summary', () =>
        HttpResponse.json({ error: 'Summary unavailable' }, { status: 500 })
      ),
    ];
  }
  return [
    http.get('*/api/v1/auth/me', () =>
      HttpResponse.json({
        id: 'usr-marco',
        email: 'marco@example.com',
        displayName: 'Marco R.',
        role: 'User' as const,
        emailVerified: true,
      })
    ),
    http.get('*/api/v1/game-nights/:id/summary', () =>
      HttpResponse.json({
        night: MOCK_SP7_SUMMARY_NIGHT,
        mvp: MOCK_SP7_SUMMARY_MVP,
        games: MOCK_SP7_SUMMARY_GAMES_FULL,
        photos: MOCK_SP7_SUMMARY_PHOTOS_FULL,
        eventsCount: MOCK_SP7_SUMMARY_EVENTS_COUNT_FULL,
        archived: false,
      })
    ),
    http.post('*/api/v1/game-nights/:id/share', () =>
      HttpResponse.json({
        url: 'https://meepleai.app/r/gn-padovani-may17',
        token: 'tok_xyz',
      })
    ),
    http.post('*/api/v1/game-nights/:id/archive', () => HttpResponse.json({ archived: true })),
    http.post('*/api/v1/game-nights/:id/unarchive', () => HttpResponse.json({ archived: false })),
    http.post('*/api/v1/game-nights/:id/photos', () =>
      HttpResponse.json({ id: 'ph-new-001', url: 'https://r2.example.com/photos/ph-new-001.jpg' })
    ),
  ];
}
