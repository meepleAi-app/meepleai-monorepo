/**
 * sp4-toolkit-play — DS-17-13 #2220.
 * Mockup parity: `admin-mockups/design_files/sp4-toolkit-play.{html,jsx}`.
 * Maps to `/toolkit/[sessionId]/` ActiveSessionPage (dynamic route).
 */

import ActiveSessionPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof ActiveSessionPage> = {
  title: 'Authenticated / sp4-toolkit-play',
  component: ActiveSessionPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: {
      appDirectory: true,
      navigation: { pathname: '/toolkit/sp4-fixture-session-id' },
    },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: { component: '#2220 DS-17-13. Toolkit active session live view.' },
    },
  },
};

export default meta;

type Story = StoryObj<typeof ActiveSessionPage>;

export const Default: Story = {};
