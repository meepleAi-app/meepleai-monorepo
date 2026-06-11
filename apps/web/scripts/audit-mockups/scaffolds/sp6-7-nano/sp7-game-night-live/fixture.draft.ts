/**
 * Game Night Live page-mock fixtures (DS-17 Phase C-1 — argTypes matrix pattern).
 *
 * Consumed by `sp7-game-night-live` cluster Storybook story con axis matrix:
 *   status: 'live' | 'paused' | 'transition'
 *   mobile: boolean + initialMobileTab: 'current' | 'planned' | 'diary'
 *   state:  game-1-in-progress | mid-night-game-2 | transition-pending | paused |
 *           diary-empty | diary-widget-embedded | mobile-tabs | auto-save-toast
 *
 * Stage axis discovery: grep `<PhoneShell id=` + `<DesktopShell id=` in
 * sp7-game-night-live.jsx lines 1469-1581 (10 frames total).
 *
 * Refs: spec docs/superpowers/specs/2026-06-11-ds-17-11-sp6-7-nano-cluster-design.md,
 *       umbrella #2063, sub-issue #2166.
 *
 * NOTE: this is a SCAFFOLD draft. Move to
 *   apps/web/src/__tests__/fixtures/mockup-pilots/sp6-7-nano/sp7-game-night-live.ts
 * when wiring into the Storybook tree.
 */

import { http, HttpResponse } from 'msw';

import type {
  DiaryEvent,
  DiaryGameRef,
  DiaryPlayerRef,
  PlannedGame,
  NightLiveHubCurrentGame,
  NightLiveHubNight,
} from '@/components/features/game-nights/live';

export type Sp7LiveState = 'live' | 'paused' | 'transition' | 'loading' | 'error';

// ── Night metadata ───────────────────────────────────────────────────────
export const MOCK_SP7_LIVE_NIGHT: NightLiveHubNight = {
  title: 'Sabato boardgame con i Padovani',
  shortTitle: 'Padovani · 17 mag',
  nightCode: '#GN-042',
};

// ── Diary refs (shared) ──────────────────────────────────────────────────
export const MOCK_SP7_LIVE_DIARY_PLAYERS: ReadonlyArray<DiaryPlayerRef> = [
  { id: 'p-marco', initials: 'MR', color: 262 },
  { id: 'p-giulia', initials: 'GM', color: 10 },
  { id: 'p-davide', initials: 'DC', color: 200 },
  { id: 'p-luca', initials: 'LB', color: 180 },
  { id: 'p-sara', initials: 'ST', color: 320 },
  { id: 'p-aaron', initials: 'AK', color: 140 },
];

export const MOCK_SP7_LIVE_DIARY_GAMES: ReadonlyArray<DiaryGameRef> = [
  { id: 'gs-brass-1', title: 'Brass: Birmingham', emoji: '🏭' },
  { id: 'gs-spirit-1', title: 'Spirit Island', emoji: '🌋' },
];

// ── Current games (per frame variant) ────────────────────────────────────
export const MOCK_SP7_LIVE_CURRENT_GAME_BRASS: NightLiveHubCurrentGame = {
  id: 'gs-brass-1',
  sessionId: 's-brass-may17',
  title: 'Brass: Birmingham',
  emoji: '🏭',
  cover: ['hsl(220 35% 28%)', 'hsl(28 60% 38%)'],
  score: 'Era 1 · canal building',
};

export const MOCK_SP7_LIVE_CURRENT_GAME_SPIRIT: NightLiveHubCurrentGame = {
  id: 'gs-spirit-1',
  sessionId: 's-spirit-may17',
  title: 'Spirit Island',
  emoji: '🌋',
  cover: ['hsl(210 50% 30%)', 'hsl(150 50% 38%)'],
  score: 'round 2 · in corso',
};

// ── Planned games per frame (early in night vs mid-night) ────────────────
// Frame 01 — Game 1 in-progress, 2 upcoming
export const MOCK_SP7_LIVE_PLANNED_GAMES_EARLY: ReadonlyArray<PlannedGame> = [
  {
    id: 'gs-brass-1',
    title: 'Brass: Birmingham',
    publisher: 'Roxley',
    emoji: '🏭',
    cover: ['hsl(220 35% 28%)', 'hsl(28 60% 38%)'],
    status: 'inprogress',
    order: 1,
    actual: '1h 12m elapsed',
    estimated: '120m',
    score: 'Era 1 · canal building',
  },
  {
    id: 'gs-spirit-1',
    title: 'Spirit Island',
    publisher: 'GMT · co-op',
    emoji: '🌋',
    cover: ['hsl(210 50% 30%)', 'hsl(150 50% 38%)'],
    status: 'upcoming',
    order: 2,
    estimated: '90m',
  },
  {
    id: 'gs-wing-1',
    title: 'Wingspan',
    publisher: 'Stonemaier',
    emoji: '🦜',
    cover: ['hsl(85 40% 45%)', 'hsl(35 60% 50%)'],
    status: 'upcoming',
    order: 3,
    estimated: '60m',
  },
];

// Frame 02 — Game 1 completed (winner), Game 2 in-progress, 1 upcoming
export const MOCK_SP7_LIVE_PLANNED_GAMES_MID: ReadonlyArray<PlannedGame> = [
  {
    id: 'gs-brass-1',
    title: 'Brass: Birmingham',
    publisher: 'Roxley',
    emoji: '🏭',
    cover: ['hsl(220 35% 28%)', 'hsl(28 60% 38%)'],
    status: 'completed',
    order: 1,
    actual: '113m',
    estimated: '120m',
    score: '178–142',
    winner: { name: 'Davide', initials: 'DC', color: 200 },
  },
  {
    id: 'gs-spirit-1',
    title: 'Spirit Island',
    publisher: 'GMT · co-op',
    emoji: '🌋',
    cover: ['hsl(210 50% 30%)', 'hsl(150 50% 38%)'],
    status: 'inprogress',
    order: 2,
    actual: '35m elapsed',
    estimated: '90m',
    score: 'round 2 · in corso',
  },
  {
    id: 'gs-wing-1',
    title: 'Wingspan',
    publisher: 'Stonemaier',
    emoji: '🦜',
    cover: ['hsl(85 40% 45%)', 'hsl(35 60% 50%)'],
    status: 'upcoming',
    order: 3,
    estimated: '60m',
  },
];

// ── Diary events (frame 02 mid-night = 5 representative events) ─────────
export const MOCK_SP7_LIVE_DIARY_EVENTS: ReadonlyArray<DiaryEvent> = [
  {
    id: 'd01',
    time: '21:02',
    gameId: 'gs-brass-1',
    kind: 'turn',
    icon: '🎯',
    actors: ['p-marco'],
    text: 'Marco apre il Canal Era — porto a Stoke-on-Trent',
  },
  {
    id: 'd08',
    time: '22:48',
    gameId: 'gs-brass-1',
    kind: 'score',
    icon: '📊',
    actors: ['p-marco'],
    text: 'Marco completa rete Birmingham (+12 link points)',
  },
  {
    id: 'd09',
    time: '22:55',
    gameId: 'gs-brass-1',
    kind: 'end',
    icon: '🏆',
    actors: ['p-davide'],
    text: '🏆 Davide vince Brass Birmingham 178–142',
  },
  {
    id: 'd10',
    time: '23:00',
    gameId: null,
    kind: 'system',
    icon: '↻',
    actors: [],
    text: 'Setup Spirit Island · 4 spiriti scelti random',
  },
  {
    id: 'd14',
    time: '23:23',
    gameId: 'gs-spirit-1',
    kind: 'score',
    icon: '📊',
    actors: ['p-davide'],
    text: 'Blight su Powerwala — Davide perde 1 presenza',
  },
];

// Frame 05 — diary empty
export const MOCK_SP7_LIVE_DIARY_EVENTS_EMPTY: ReadonlyArray<DiaryEvent> = [];

// ── MSW handlers ─────────────────────────────────────────────────────────
export function mswForSp7LiveState(state: Sp7LiveState) {
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
      http.get('*/api/v1/game-nights/:id/live', () => new Promise<Response>(() => {})),
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
      http.get('*/api/v1/game-nights/:id/live', () =>
        HttpResponse.json({ error: 'Live state unavailable' }, { status: 500 })
      ),
    ];
  }
  // default 'live' / 'paused' / 'transition' all share same MSW baseline
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
    http.get('*/api/v1/game-nights/:id/live', () =>
      HttpResponse.json({
        night: MOCK_SP7_LIVE_NIGHT,
        status: state === 'paused' ? 'paused' : state === 'transition' ? 'transition' : 'live',
        current: 2,
        total: 3,
        elapsed: '2h 23m',
        confirmedPlayers: 6,
        totalPlayers: 6,
        plannedGames: MOCK_SP7_LIVE_PLANNED_GAMES_MID,
        currentGame: state === 'transition' ? null : MOCK_SP7_LIVE_CURRENT_GAME_SPIRIT,
        diaryEvents: MOCK_SP7_LIVE_DIARY_EVENTS,
        diaryGames: MOCK_SP7_LIVE_DIARY_GAMES,
        diaryPlayers: MOCK_SP7_LIVE_DIARY_PLAYERS,
      })
    ),
    http.post('*/api/v1/game-nights/:id/pause', () => HttpResponse.json({ status: 'paused' })),
    http.post('*/api/v1/game-nights/:id/resume', () => HttpResponse.json({ status: 'live' })),
    http.post('*/api/v1/game-nights/:id/transition', () =>
      HttpResponse.json({ status: 'transition' })
    ),
    http.post('*/api/v1/game-nights/:id/end', () => HttpResponse.json({ status: 'completed' })),
  ];
}
