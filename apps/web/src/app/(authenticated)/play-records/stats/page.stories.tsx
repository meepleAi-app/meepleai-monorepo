/**
 * sp4-play-records-stats — DS-17-13 #2220.
 * Mockup parity: `admin-mockups/design_files/sp4-play-records-stats.{html,jsx}`.
 */

import StatisticsPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof StatisticsPage> = {
  title: 'Authenticated / sp4-play-records-stats',
  component: StatisticsPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: { appDirectory: true },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: { component: '#2220 DS-17-13. Play records statistics dashboard.' },
    },
  },
};

export default meta;

type Story = StoryObj<typeof StatisticsPage>;

export const Default: Story = {};
