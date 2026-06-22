/**
 * sp4-session-skeleton-live — DS-17-15 #2231.
 * Mockup parity: `admin-mockups/design_files/sp4-session-skeleton-live.{html,jsx}`.
 */

import LiveSessionPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof LiveSessionPage> = {
  title: 'Authenticated / sp4-session-skeleton-live',
  component: LiveSessionPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: {
      appDirectory: true,
      navigation: { pathname: '/sessions/live/sp4-fixture-session-id' },
    },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component: '#2231 DS-17-15. Live session skeleton (Phase C-2 skeleton-first base).',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof LiveSessionPage>;

export const Default: Story = {};
