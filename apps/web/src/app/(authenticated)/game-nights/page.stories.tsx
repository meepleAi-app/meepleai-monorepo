/**
 * sp4-game-nights-index — DS-17-14 #2228.
 * Mockup parity: `admin-mockups/design_files/sp4-game-nights-index.{html,jsx}`.
 */

import GameNightsPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof GameNightsPage> = {
  title: 'Authenticated / sp4-game-nights-index',
  component: GameNightsPage,
  parameters: {
    // DS-17 #2063: heuristic can't read named exports/http.get; declare states explicitly.
    canonicalStates: ['default'],
    layout: 'fullscreen',
    nextjs: { appDirectory: true },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: { component: '#2228 DS-17-14. Game nights index list.' },
    },
  },
};

export default meta;

type Story = StoryObj<typeof GameNightsPage>;

export const Default: Story = {};
