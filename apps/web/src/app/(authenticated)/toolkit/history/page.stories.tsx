/**
 * sp4-toolkit-history — DS-17-13 #2220.
 * Mockup parity: `admin-mockups/design_files/sp4-toolkit-history.{html,jsx}`.
 */

import ToolkitHistoryPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof ToolkitHistoryPage> = {
  title: 'Authenticated / sp4-toolkit-history',
  component: ToolkitHistoryPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: { appDirectory: true },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: { component: '#2220 DS-17-13. Toolkit session history.' },
    },
  },
};

export default meta;

type Story = StoryObj<typeof ToolkitHistoryPage>;

export const Default: Story = {};
