/**
 * sp4-play-records-index — DS-17-13 #2220.
 * Mockup parity: `admin-mockups/design_files/sp4-play-records-index.{html,jsx}`.
 */

import PlayRecordsPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof PlayRecordsPage> = {
  title: 'Authenticated / sp4-play-records-index',
  component: PlayRecordsPage,
  parameters: {
    // DS-17 #2063: heuristic can't read named exports/http.get; declare states explicitly.
    canonicalStates: ['default'],
    layout: 'fullscreen',
    nextjs: { appDirectory: true },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: { component: '#2220 DS-17-13. Play records index list.' },
    },
  },
};

export default meta;

type Story = StoryObj<typeof PlayRecordsPage>;

export const Default: Story = {};
