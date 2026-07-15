/**
 * sp4-player-detail — DS-17-14 #2228.
 * Mockup parity: `admin-mockups/design_files/sp4-player-detail.{html,jsx}`.
 * Multi-sub-routes per MOCKUPS_INDEX: includes /players/[id]/{achievements,games,sessions,stats} (canonical = base).
 */

import PlayerDetailPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof PlayerDetailPage> = {
  title: 'Authenticated / sp4-player-detail',
  component: PlayerDetailPage,
  parameters: {
    // DS-17 #2063: heuristic can't read named exports/http.get; declare states explicitly.
    canonicalStates: ['default'],
    layout: 'fullscreen',
    nextjs: {
      appDirectory: true,
      navigation: { pathname: '/players/sp4-fixture-id' },
    },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component:
          '#2228 DS-17-14. Player detail view (canonical for achievements/games/sessions/stats sub-routes per P254 multi-route).',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof PlayerDetailPage>;

export const Default: Story = {};
