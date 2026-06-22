/**
 * sp4-games-index — DS-17-12 #2214 sub-issue.
 *
 * Mockup parity: `admin-mockups/design_files/sp4-games-index.{html,jsx}`.
 */

import GamesIndexPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof GamesIndexPage> = {
  title: 'Authenticated / sp4-games-index',
  component: GamesIndexPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: { appDirectory: true },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component: '#2214 DS-17-12. Authenticated games catalog index.',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof GamesIndexPage>;

export const Default: Story = {};
