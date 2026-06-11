/**
 * @mockup admin-mockups/design_files/sp7-game-night-summary.html
 *
 * Game Night Summary argTypes matrix story — DS-17 Phase C-1 (sub-issue #2166).
 *
 * Canonical pick (P245): single route `/game-nights/[id]/summary` with internal
 * state variants (post-completion recap):
 *   HERO COMPONENT: `NightSummaryView` (Screen M primitive at
 *   `apps/web/src/components/features/game-nights/summary/NightSummaryView.tsx`)
 *   exposes ALL axis values as props directly.
 *
 * Stage axis (sp7-game-night-summary.jsx STATES — 6 frames):
 *   summary-full / summary-no-photos / summary-single-game /
 *   share-success-toast / archived / mobile-single-col
 *
 * Refs: spec docs/superpowers/specs/2026-06-11-ds-17-11-sp6-7-nano-cluster-design.md,
 *       umbrella #2063, sub-issue #2166.
 *
 * NOTE: this is a SCAFFOLD draft. Move to
 *   apps/web/src/components/features/game-nights/summary/NightSummaryView.stories.tsx
 * when wiring into the Storybook tree.
 */

import { fn } from 'storybook/test';

import {
  mswForSp7SummaryState,
  MOCK_SP7_SUMMARY_NIGHT,
  MOCK_SP7_SUMMARY_MVP,
  MOCK_SP7_SUMMARY_GAMES_FULL,
  MOCK_SP7_SUMMARY_GAMES_SINGLE,
  MOCK_SP7_SUMMARY_PHOTOS_FULL,
  MOCK_SP7_SUMMARY_PHOTOS_EMPTY,
  MOCK_SP7_SUMMARY_EVENTS_COUNT_FULL,
  MOCK_SP7_SUMMARY_EVENTS_COUNT_SINGLE,
} from '@/__tests__/fixtures/mockup-pilots/sp6-7-nano/sp7-game-night-summary';
import { NightSummaryView } from '@/components/features/game-nights/summary';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof NightSummaryView> = {
  title: 'Pages/SP7/Game Night Summary',
  component: NightSummaryView,
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component:
          'Pixel-faithful matrix di sp7-game-night-summary.jsx 6 frames (full recap 3 games + 28 diary + 6 photos / no-photos placeholder / single-game / share-success toast / archived banner / mobile single-col). Hero usa NightSummaryView (Screen M). Desktop primary; mobile-single-col frame DEFERRED a Phase 4.',
      },
    },
  },
  argTypes: {
    archived: {
      control: 'boolean',
      description: 'Renders ArchivedBanner + suppresses share/archive CTAs.',
    },
    mobile: {
      control: 'boolean',
      description: 'Mobile vertical stack layout (Frame 06 single-col).',
    },
    eventsCount: {
      control: { type: 'number', min: 0, max: 100, step: 1 },
      description: 'Total diary events count for KPI grid.',
    },
  },
  args: {
    onShare: fn(),
    onArchive: fn(),
    onUnarchive: fn(),
    onGoToList: fn(),
    onJumpToSession: fn(),
    onAddPhoto: fn(),
    night: MOCK_SP7_SUMMARY_NIGHT,
    mvp: MOCK_SP7_SUMMARY_MVP,
    games: MOCK_SP7_SUMMARY_GAMES_FULL,
    eventsCount: MOCK_SP7_SUMMARY_EVENTS_COUNT_FULL,
    photos: MOCK_SP7_SUMMARY_PHOTOS_FULL,
    mobile: false,
    archived: false,
    shareSuccess: undefined,
  },
};
export default meta;

type Story = StoryObj<typeof NightSummaryView>;

// ── Stage frame canonicals (mapped 1:1 ai 6 frame mockup) ───────────────────

export const Frame01_SummaryFull: Story = {
  name: '01 · Recap completo · 3 games · 28 diary · 6 foto · MVP Davide',
  args: {
    games: MOCK_SP7_SUMMARY_GAMES_FULL,
    photos: MOCK_SP7_SUMMARY_PHOTOS_FULL,
    eventsCount: MOCK_SP7_SUMMARY_EVENTS_COUNT_FULL,
    mvp: MOCK_SP7_SUMMARY_MVP,
  },
  parameters: { msw: { handlers: mswForSp7SummaryState('default') } },
};

export const Frame02_SummaryNoPhotos: Story = {
  name: '02 · Stesso recap · gallery empty + placeholder CTA "Aggiungi foto"',
  args: {
    games: MOCK_SP7_SUMMARY_GAMES_FULL,
    photos: MOCK_SP7_SUMMARY_PHOTOS_EMPTY,
    eventsCount: MOCK_SP7_SUMMARY_EVENTS_COUNT_FULL,
    mvp: MOCK_SP7_SUMMARY_MVP,
  },
  parameters: { msw: { handlers: mswForSp7SummaryState('default') } },
};

export const Frame03_SummarySingleGame: Story = {
  name: '03 · Serata 1 game · no transition · no per-game multipli · stats ridotte',
  args: {
    games: MOCK_SP7_SUMMARY_GAMES_SINGLE,
    photos: MOCK_SP7_SUMMARY_PHOTOS_FULL.slice(0, 2),
    eventsCount: MOCK_SP7_SUMMARY_EVENTS_COUNT_SINGLE,
    mvp: MOCK_SP7_SUMMARY_MVP,
  },
  parameters: { msw: { handlers: mswForSp7SummaryState('default') } },
};

export const Frame04_ShareSuccessToast: Story = {
  name: '04 · Post-share toast "Link copiato" toolkit',
  args: {
    games: MOCK_SP7_SUMMARY_GAMES_FULL,
    photos: MOCK_SP7_SUMMARY_PHOTOS_FULL,
    eventsCount: MOCK_SP7_SUMMARY_EVENTS_COUNT_FULL,
    mvp: MOCK_SP7_SUMMARY_MVP,
    shareSuccess: {
      visible: true,
      url: 'https://meepleai.app/r/gn-padovani-may17',
      subline: 'Link copiato — condividilo con il gruppo',
    },
  },
  parameters: { msw: { handlers: mswForSp7SummaryState('default') } },
};

export const Frame05_Archived: Story = {
  name: '05 · Post-archive banner muted + CTA "Torna alla lista"',
  args: {
    games: MOCK_SP7_SUMMARY_GAMES_FULL,
    photos: MOCK_SP7_SUMMARY_PHOTOS_FULL,
    eventsCount: MOCK_SP7_SUMMARY_EVENTS_COUNT_FULL,
    mvp: MOCK_SP7_SUMMARY_MVP,
    archived: true,
  },
  parameters: { msw: { handlers: mswForSp7SummaryState('default') } },
};

export const Frame06_MobileSingleCol: Story = {
  name: '06 · Mobile vertical stack (390 fullscreen · padding ridotto)',
  args: {
    games: MOCK_SP7_SUMMARY_GAMES_FULL,
    photos: MOCK_SP7_SUMMARY_PHOTOS_FULL,
    eventsCount: MOCK_SP7_SUMMARY_EVENTS_COUNT_FULL,
    mvp: MOCK_SP7_SUMMARY_MVP,
    mobile: true,
  },
  parameters: {
    msw: { handlers: mswForSp7SummaryState('default') },
    docs: {
      description: {
        story:
          'Mobile-only frame. Renders w/ `mobile=true` prop for current visual check. Viewport sweep DEFERRED a Phase 4.',
      },
    },
  },
};

// ── State variant frames (axis = NoMvp / NoPhotosNoMvp / Loading) ──────────

export const StateNoMvp: Story = {
  name: 'State · No MVP (mvp=null — co-op completata senza singolo vincitore)',
  args: { mvp: null },
  parameters: { msw: { handlers: mswForSp7SummaryState('default') } },
};

export const StateEmptyAll: Story = {
  name: 'State · Empty all (zero games, zero events — edge case)',
  args: {
    games: [],
    photos: [],
    eventsCount: 0,
    mvp: null,
  },
  parameters: { msw: { handlers: mswForSp7SummaryState('default') } },
};
