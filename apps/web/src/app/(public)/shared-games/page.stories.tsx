/**
 * sp3-shared-games — DS-17-10 #2208 sub-issue.
 * Mockup parity: `admin-mockups/design_files/sp3-shared-games.{html,jsx}`.
 */

import SharedGamesPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof SharedGamesPage> = {
  title: 'Public / sp3-shared-games',
  component: SharedGamesPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: { appDirectory: true },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component:
          '#2208 DS-17-10. Public catalog of community shared games. Renders existing `(public)/shared-games/page.tsx` server component.',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof SharedGamesPage>;

export const Default: Story = {};
