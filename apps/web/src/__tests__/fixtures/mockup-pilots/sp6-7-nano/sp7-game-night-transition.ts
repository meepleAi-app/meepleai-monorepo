/**
 * Game Night Transition Dialog page-mock fixtures (DS-17 Phase D-1 — argTypes matrix pattern).
 *
 * Consumed by `sp7-game-night-transition` cluster Storybook story con axis matrix:
 *   rulesStatus: 'ok' | 'loading' | 'error'
 *   submodal:    null | 'skip' | 'end'
 *   mobile:      boolean
 *   open:        boolean
 *
 * Stage axis discovery: 6 frames in sp7-game-night-transition.jsx
 * (state-01-transition-default through state-06-mobile-fullscreen, lines 866-927).
 *
 * `sp7-game-night-transition.html` is classified `component-mock` in
 * `admin-mockups/MOCKUPS_INDEX.md` — the dialog is mounted from
 * `/game-nights/[id]/live` via `NightLiveClientView` rather than its own route.
 * We export pure-data fixtures (no MSW handlers) because `GameTransitionDialog`
 * is a presentational primitive — the parent orchestrator owns the network.
 *
 * Refs: spec docs/superpowers/specs/2026-06-11-ds-17-11-sp6-7-nano-cluster-design.md,
 *       umbrella #2063, sub-issue #2174 (Phase D-1).
 */

import type {
  TransitionLastGame,
  TransitionNextGame,
} from '@/components/features/game-nights/transition';

// ── LAST GAME — Brass: Birmingham winner Davide 178pt · 2h 45m ───────────
export const MOCK_SP7_TRANSITION_LAST_GAME: TransitionLastGame = {
  id: 'gs-brass-1',
  title: 'Brass: Birmingham',
  publisher: 'Roxley',
  emoji: '🏭',
  cover: ['hsl(220 35% 28%)', 'hsl(28 60% 38%)'],
  duration: '2h 45m',
  endedAt: '22:55',
  winner: {
    id: 'p-davide',
    initials: 'DC',
    name: 'Davide',
    color: 200,
    score: 178,
  },
  topThree: [
    {
      id: 'p-davide',
      initials: 'DC',
      name: 'Davide',
      color: 200,
      score: 178,
      delta: '+50 industria',
    },
    { id: 'p-marco', initials: 'MR', name: 'Marco', color: 262, score: 142, delta: '+12 network' },
    { id: 'p-giulia', initials: 'GM', name: 'Giulia', color: 10, score: 128, delta: '+8 carbone' },
  ],
};

// ── NEXT GAME — Spirit Island weight 4.08 · 3 rules · setup 2/4 done ─────
export const MOCK_SP7_TRANSITION_NEXT_GAME: TransitionNextGame = {
  id: 'gs-spirit-1',
  title: 'Spirit Island',
  publisher: 'GMT · co-op',
  emoji: '🌋',
  cover: ['hsl(210 50% 30%)', 'hsl(150 50% 38%)'],
  estimated: '90m',
  weight: 4.08,
  rules: [
    { icon: '🔁', text: 'Phase order · Growth → Fast → Slow', src: '§ 4.2' },
    { icon: '😱', text: 'Fear deck · 9 carte iniziali (level 1/2/3)', src: '§ 5.1' },
    { icon: '⚡', text: 'Power growth · 1 minor + 1 major per round', src: '§ 6.3' },
  ],
  setup: [
    { icon: '🗺️', text: 'Tabellone · 4 isole', done: true },
    { icon: '🧝', text: 'Spirit panels (4 spiriti scelti)', done: true },
    { icon: '🃏', text: 'Invader deck shuffled · stage I', done: false },
    { icon: '💀', text: 'Fear pool · 9 token', done: false },
  ],
};

// ── Night code (matches sp7-game-night-live fixture #GN-042) ─────────────
export const MOCK_SP7_TRANSITION_NIGHT_CODE = '#GN-042';
