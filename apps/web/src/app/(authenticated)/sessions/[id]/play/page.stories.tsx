/**
 * sp4-session-play — DS-17-15 #2231.
 * Mockup parity: `admin-mockups/design_files/sp4-session-play.{html,jsx}`.
 */

import LiveSessionPlayPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof LiveSessionPlayPage> = {
  title: 'Authenticated / sp4-session-play',
  component: LiveSessionPlayPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: {
      appDirectory: true,
      navigation: { pathname: '/sessions/sp4-fixture-session-id/play' },
    },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component: '#2231 DS-17-15. Session play live view (Phase C-2 skeleton-first base).',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof LiveSessionPlayPage>;

export const Default: Story = {};
