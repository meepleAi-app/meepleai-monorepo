/**
 * @mockup admin-mockups/design_files/sp4-game-detail.html
 *
 * GameDetailView argTypes matrix story — DS-17 Phase 2.5 (DEC-P3-3).
 *
 * Stage axis del mockup (3 Desktop frame, Mobile m1-m6 DEFERRED a Phase 4):
 *   variant: 'own' | 'community'
 *   tab: 'info' | 'sessions' | 'chat' | 'stats'
 *   state: 'default' | 'loading' | 'error'
 *
 * Refs: spec docs/superpowers/specs/2026-06-10-ds-17-phase-2.5-and-3-redesign.md, umbrella #2063.
 */

import { http, HttpResponse } from 'msw';

import { MOCK_GAME_DETAIL } from '@/__tests__/fixtures/mockup-pilots';

import { GameDetailView } from './GameDetailView';

import type { Meta, StoryObj } from '@storybook/react';

const GAME_ID = '11111111-1111-1111-1111-111111111111';

type GameDetailState = 'default' | 'loading' | 'error';

function mswForState(state: GameDetailState) {
  if (state === 'loading') {
    return [http.get(`*/api/v1/library/games/${GAME_ID}`, () => new Promise<Response>(() => {}))];
  }
  if (state === 'error') {
    return [
      http.get(`*/api/v1/library/games/${GAME_ID}`, () =>
        HttpResponse.json({ error: 'server error' }, { status: 500 })
      ),
    ];
  }
  return [http.get(`*/api/v1/library/games/${GAME_ID}`, () => HttpResponse.json(MOCK_GAME_DETAIL))];
}

const meta: Meta<typeof GameDetailView> = {
  title: 'Pages/SP4/GameDetail / Mockup Matrix',
  component: GameDetailView,
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component:
          'Pixel-faithful matrix di sp4-game-detail.jsx stage frames 07-09 (Desktop only Phase 2.5; Mobile m1-m6 deferred Phase 4). Use Storybook controls per esplorare axis variant × tab × state.',
      },
    },
    nextjs: {
      appDirectory: true,
      navigation: { pathname: `/games/${GAME_ID}`, query: {} },
    },
  },
  // Code-reviewer I1: argTypes populated for designer review (variant × tab axis from mockup JSX twin).
  argTypes: {
    variant: {
      control: 'select',
      options: ['own', 'community'],
      description: 'Documentation only — variant axis from mockup.',
    },
    tab: {
      control: 'select',
      options: ['info', 'sessions', 'chat', 'stats'],
      description: 'Documentation only — tab axis from mockup.',
    },
    state: {
      control: 'select',
      options: ['default', 'loading', 'error'],
      description: 'Drives MSW handler scenario.',
    },
  },
  args: {
    gameId: GAME_ID,
    variant: 'own',
    tab: 'info',
    state: 'default',
  },
};
export default meta;

type Story = StoryObj<typeof GameDetailView>;

// ── Desktop stage frames (07-09) ───────────────────────────────────────────
// Mobile frames m1-m6 DEFERRED to Phase 4 hardening (Code-reviewer C1+C2).

export const Frame07_DesktopOwnInfo: Story = {
  name: '07 · Desktop · Own · Info',
  parameters: { msw: { handlers: mswForState('default') } },
};

export const Frame08_DesktopCommunityLocked: Story = {
  name: '08 · Desktop · Community · locked',
  parameters: { msw: { handlers: mswForState('default') } },
};

export const Frame09_DesktopLoading: Story = {
  name: '09 · Desktop · Loading',
  parameters: { msw: { handlers: mswForState('loading') } },
};
