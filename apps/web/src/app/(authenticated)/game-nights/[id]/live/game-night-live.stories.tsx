/**
 * @mockup admin-mockups/design_files/sp7-game-night-live.html
 *
 * Game Night Live Hub argTypes matrix story — DS-17 Phase C-1 (sub-issue #2166).
 *
 * Canonical pick (P245): the mockup covers a SINGLE route `/game-nights/[id]/live`
 * with internal state variants (game progress in-flight, paused, transition).
 *
 *   HERO COMPONENT: `NightLiveHub` (`apps/web/src/components/features/game-nights/live/NightLiveHub.tsx`)
 *   is the canonical hero (Screen K). It exposes props for all axis values:
 *     - status: 'live' | 'paused' | 'transition'
 *     - mobile / desktop
 *     - initialMobileTab: 'current' | 'planned' | 'diary'
 *     - currentGame, plannedGames, diaryEvents (data shape)
 *     - autoSaveToast (frame 10)
 *
 *   `GameTransitionDialog` (Screen L) covers Frame 03 transition state — used as
 *   a sibling primitive that the page-client mounts as a modal. We render it
 *   separately via Frame03 story for visual coverage.
 *
 * Stage axis (sp7-game-night-live.jsx STATES — 10 frames):
 *   game 1 in-progress / game 2 mid-night / transition-pending / paused /
 *   diary-empty / diary-widget-embedded / mobile-tab-current / mobile-tab-planned /
 *   mobile-tab-diary / auto-save-toast
 *
 * Refs: spec docs/superpowers/specs/2026-06-11-ds-17-11-sp6-7-nano-cluster-design.md,
 *       umbrella #2063, sub-issue #2166.
 *
 * NOTE: this is a SCAFFOLD draft. Move to
 *   apps/web/src/components/features/game-nights/live/NightLiveHub.stories.tsx
 * when wiring into the Storybook tree.
 */

import { fn } from 'storybook/test';

import {
  mswForSp7LiveState,
  MOCK_SP7_LIVE_NIGHT,
  MOCK_SP7_LIVE_CURRENT_GAME_SPIRIT,
  MOCK_SP7_LIVE_CURRENT_GAME_BRASS,
  MOCK_SP7_LIVE_DIARY_EVENTS,
  MOCK_SP7_LIVE_DIARY_GAMES,
  MOCK_SP7_LIVE_DIARY_PLAYERS,
  MOCK_SP7_LIVE_PLANNED_GAMES_EARLY,
  MOCK_SP7_LIVE_PLANNED_GAMES_MID,
  MOCK_SP7_LIVE_DIARY_EVENTS_EMPTY,
} from '@/__tests__/fixtures/mockup-pilots/sp6-7-nano/sp7-game-night-live';
import { NightLiveHub } from '@/components/features/game-nights/live';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof NightLiveHub> = {
  title: 'Pages/SP7/Game Night Live',
  component: NightLiveHub,
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component:
          'Pixel-faithful matrix di sp7-game-night-live.jsx 10 frames (game progression, transition, pause, diary embedded, mobile tabs, auto-save toast). Hero usa NightLiveHub (Screen K). Frame 03 transition rendered via GameTransitionDialog sibling primitive. Desktop primary; Mobile tab frames (07-09) DEFERRED a Phase 4 viewport sweep.',
      },
    },
  },
  argTypes: {
    status: {
      control: 'select',
      options: ['live', 'paused', 'transition'],
      description: 'Live status — drives top-bar color + pause overlay.',
    },
    mobile: {
      control: 'boolean',
      description: 'Mobile shell mode (initialMobileTab axis becomes active).',
    },
    initialMobileTab: {
      control: 'select',
      options: ['current', 'planned', 'diary'],
      description: 'Active mobile tab — only meaningful when mobile=true.',
    },
  },
  args: {
    onBack: fn(),
    onPauseToggle: fn(),
    onTransition: fn(),
    onEnd: fn(),
    onJumpToSession: fn(),
    onAddGame: fn(),
    night: MOCK_SP7_LIVE_NIGHT,
    status: 'live',
    current: 2,
    total: 3,
    elapsed: '2h 23m',
    confirmedPlayers: 6,
    totalPlayers: 6,
    plannedGames: MOCK_SP7_LIVE_PLANNED_GAMES_MID,
    currentGame: MOCK_SP7_LIVE_CURRENT_GAME_SPIRIT,
    diaryEvents: MOCK_SP7_LIVE_DIARY_EVENTS,
    diaryGames: MOCK_SP7_LIVE_DIARY_GAMES,
    diaryPlayers: MOCK_SP7_LIVE_DIARY_PLAYERS,
    autoSaveToast: undefined,
    mobile: false,
    initialMobileTab: 'current',
  },
};
export default meta;

type Story = StoryObj<typeof NightLiveHub>;

// ── Stage frame canonicals (mapped 1:1 ai 10 frame mockup) ─────────────────

export const Frame01_Game1InProgress: Story = {
  name: '01 · Game 1 (Brass) in-progress · diary 5 eventi · 2 upcoming',
  args: {
    current: 1,
    total: 3,
    elapsed: '1h 12m',
    currentGame: MOCK_SP7_LIVE_CURRENT_GAME_BRASS,
    plannedGames: MOCK_SP7_LIVE_PLANNED_GAMES_EARLY,
  },
  parameters: { msw: { handlers: mswForSp7LiveState('live') } },
};

export const Frame02_MidNightGame2: Story = {
  name: '02 · Mid-night · Game 1 completed (winner) · Game 2 (Spirit) in-progress · diary 18 eventi',
  args: {
    current: 2,
    total: 3,
    elapsed: '2h 23m',
    currentGame: MOCK_SP7_LIVE_CURRENT_GAME_SPIRIT,
    plannedGames: MOCK_SP7_LIVE_PLANNED_GAMES_MID,
  },
  parameters: { msw: { handlers: mswForSp7LiveState('live') } },
};

export const Frame03_TransitionPending: Story = {
  name: '03 · Transition pending tra Game 1 e Game 2 — current pane "In attesa transition"',
  args: {
    status: 'transition',
    current: 2,
    total: 3,
    elapsed: '2h 55m',
    currentGame: null,
    plannedGames: MOCK_SP7_LIVE_PLANNED_GAMES_MID,
  },
  parameters: {
    msw: { handlers: mswForSp7LiveState('live') },
    docs: {
      description: {
        story:
          'Documentation-only frame. The `GameTransitionDialog` modal (Screen L) renders ON TOP of NightLiveHub via `NightLiveClientView` orchestration. Full transition flow lives in GameTransitionDialog.stories.tsx (sibling primitive).',
      },
    },
  },
};

export const Frame04_Paused: Story = {
  name: '04 · Serata in pausa · overlay agent warning',
  args: {
    status: 'paused',
    current: 2,
    total: 3,
    elapsed: '2h 23m',
    currentGame: MOCK_SP7_LIVE_CURRENT_GAME_SPIRIT,
  },
  parameters: { msw: { handlers: mswForSp7LiveState('live') } },
};

export const Frame05_DiaryEmpty: Story = {
  name: '05 · Inizio serata · diary "Nessun evento ancora"',
  args: {
    current: 1,
    total: 3,
    elapsed: '0m',
    currentGame: MOCK_SP7_LIVE_CURRENT_GAME_BRASS,
    plannedGames: MOCK_SP7_LIVE_PLANNED_GAMES_EARLY,
    diaryEvents: MOCK_SP7_LIVE_DIARY_EVENTS_EMPTY,
  },
  parameters: { msw: { handlers: mswForSp7LiveState('live') } },
};

export const Frame06_DiaryWidgetEmbedded: Story = {
  name: '06 · Diary widget 320x400 isolato (riuso standalone)',
  args: {
    current: 2,
    total: 3,
    elapsed: '2h 23m',
  },
  parameters: {
    msw: { handlers: mswForSp7LiveState('live') },
    docs: {
      description: {
        story:
          'Documentation-only frame. The standalone diary widget is `DiaryInlineWidget` (`apps/web/src/components/features/game-nights/live/DiaryInlineWidget.tsx`) — has its own dedicated story alongside the source file.',
      },
    },
  },
};

export const Frame07_MobileTabCurrent: Story = {
  name: '07 · Mobile · Tab "Current" attivo',
  args: { mobile: true, initialMobileTab: 'current' },
  parameters: {
    msw: { handlers: mswForSp7LiveState('live') },
    docs: {
      description: {
        story:
          'Mobile-only frame. Viewport sweep DEFERRED a Phase 4 — story render Desktop layout with `mobile=true` prop for current visual check.',
      },
    },
  },
};

export const Frame08_MobileTabPlanned: Story = {
  name: '08 · Mobile · Tab "Planned" attivo',
  args: { mobile: true, initialMobileTab: 'planned' },
  parameters: {
    msw: { handlers: mswForSp7LiveState('live') },
    docs: {
      description: {
        story: 'Mobile-only frame. Viewport sweep DEFERRED a Phase 4.',
      },
    },
  },
};

export const Frame09_MobileTabDiary: Story = {
  name: '09 · Mobile · Tab "Diary" attivo',
  args: { mobile: true, initialMobileTab: 'diary' },
  parameters: {
    msw: { handlers: mswForSp7LiveState('live') },
    docs: {
      description: {
        story: 'Mobile-only frame. Viewport sweep DEFERRED a Phase 4.',
      },
    },
  },
};

export const Frame10_AutoSaveToast: Story = {
  name: '10 · Toast bottom-right toolkit ogni 60s ("Salvato")',
  args: {
    autoSaveToast: { visible: true, timestamp: '23:42' },
  },
  parameters: { msw: { handlers: mswForSp7LiveState('live') } },
};

// ── Additional state variants (Phase 4 deferred — high-level coverage) ─────

export const StateENDED: Story = {
  name: 'State · ENDED (serata completata · go to summary)',
  args: {
    status: 'live',
    current: 3,
    total: 3,
    elapsed: '6h 15m',
    currentGame: null,
    plannedGames: MOCK_SP7_LIVE_PLANNED_GAMES_MID.map(g => ({
      ...g,
      status: 'completed' as const,
    })),
  },
  parameters: {
    msw: { handlers: mswForSp7LiveState('live') },
    docs: {
      description: {
        story:
          'Final state — orchestrator (`NightLiveClientView`) redirects to /game-nights/[id]/summary post-onEnd. Documentation-only here.',
      },
    },
  },
};
