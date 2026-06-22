/**
 * sp4-sessions-index — DS-17-14 #2228.
 * Mockup parity: `admin-mockups/design_files/sp4-sessions-index.{html,jsx}`.
 */

import SessionsPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof SessionsPage> = {
  title: 'Authenticated / sp4-sessions-index',
  component: SessionsPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: { appDirectory: true },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: { component: '#2228 DS-17-14. Sessions index list.' },
    },
  },
};

export default meta;

type Story = StoryObj<typeof SessionsPage>;

export const Default: Story = {};
