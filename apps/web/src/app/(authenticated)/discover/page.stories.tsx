/**
 * sp4-discover — DS-17-12 #2214 sub-issue.
 *
 * Mockup parity: `admin-mockups/design_files/sp4-discover.{html,jsx}`.
 */

import DiscoverPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof DiscoverPage> = {
  title: 'Authenticated / sp4-discover',
  component: DiscoverPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: { appDirectory: true },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component: '#2214 DS-17-12. Discover surface for community content.',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof DiscoverPage>;

export const Default: Story = {};
