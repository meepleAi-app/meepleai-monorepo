/**
 * sp4-players-index — DS-17-14 #2228.
 * Mockup parity: `admin-mockups/design_files/sp4-players-index.{html,jsx}`.
 */

import PlayersPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof PlayersPage> = {
  title: 'Authenticated / sp4-players-index',
  component: PlayersPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: { appDirectory: true },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: { component: '#2228 DS-17-14. Players catalog index.' },
    },
  },
};

export default meta;

type Story = StoryObj<typeof PlayersPage>;

export const Default: Story = {};
