/**
 * sp4-toolkit-stats — DS-17-13 #2220.
 * Mockup parity: `admin-mockups/design_files/sp4-toolkit-stats.{html,jsx}`.
 */

import ToolkitStatsPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof ToolkitStatsPage> = {
  title: 'Authenticated / sp4-toolkit-stats',
  component: ToolkitStatsPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: { appDirectory: true },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: { component: '#2220 DS-17-13. Toolkit statistics dashboard.' },
    },
  },
};

export default meta;

type Story = StoryObj<typeof ToolkitStatsPage>;

export const Default: Story = {};
