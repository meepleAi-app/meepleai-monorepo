/**
 * @mockup admin-mockups/design_files/sp4-game-detail.html
 *
 * GameDetailView page-mock story — DS-17-6-v2 pilot.
 *
 * Refs: spec, umbrella #2063.
 *
 * Implementer note: `GameDetailView` accepts `gameId: string | null` as a prop
 * (verified GameDetailView.tsx:94, :385). `meta.args.gameId = 'g-1'` flows
 * through to all 3 stories; Empty drives 404 via MSW handler (not gameId
 * mutation), Error drives 500.
 */

import { http, HttpResponse } from 'msw';

import { MOCK_GAME_DETAIL } from '@/__tests__/fixtures/mockup-pilots';

import { GameDetailView } from './GameDetailView';

import type { Meta, StoryObj } from '@storybook/react';

const GAME_ID = '11111111-1111-1111-1111-111111111111';

const meta: Meta<typeof GameDetailView> = {
  title: 'Pages/SP4/GameDetail / Mockup Pilot',
  component: GameDetailView,
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component:
          'Pixel-faithful to `admin-mockups/design_files/sp4-game-detail.html`. Hero + KPI + tabs.',
      },
    },
    nextjs: {
      appDirectory: true,
      navigation: {
        pathname: `/games/${GAME_ID}`,
        query: {},
      },
    },
  },
  args: {
    gameId: GAME_ID,
  },
};
export default meta;

type Story = StoryObj<typeof GameDetailView>;

export const Default: Story = {
  parameters: {
    msw: {
      handlers: [
        http.get(`*/api/v1/library/games/${GAME_ID}`, () => HttpResponse.json(MOCK_GAME_DETAIL)),
      ],
    },
  },
};

export const Empty: Story = {
  parameters: {
    msw: {
      handlers: [
        http.get(`*/api/v1/library/games/${GAME_ID}`, () =>
          HttpResponse.json(null, { status: 404 })
        ),
      ],
    },
  },
  // gameId=GAME_ID (from meta.args) — MSW returns 404 to drive the empty state.
};

export const Error: Story = {
  parameters: {
    msw: {
      handlers: [
        http.get(`*/api/v1/library/games/${GAME_ID}`, () =>
          HttpResponse.json({ error: 'server error' }, { status: 500 })
        ),
      ],
    },
  },
};
