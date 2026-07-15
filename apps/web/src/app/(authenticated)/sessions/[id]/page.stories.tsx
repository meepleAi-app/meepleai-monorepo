/**
 * sp4-session-summary-skeleton — DS-17-15 #2231.
 * Mockup parity: `admin-mockups/design_files/sp4-session-summary-skeleton.{html,jsx}`.
 */

import SessionDetailPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof SessionDetailPage> = {
  title: 'Authenticated / sp4-session-summary-skeleton',
  component: SessionDetailPage,
  parameters: {
    // DS-17 #2063: heuristic can't read named exports/http.get; declare states explicitly.
    canonicalStates: ['default'],
    layout: 'fullscreen',
    nextjs: {
      appDirectory: true,
      navigation: { pathname: '/sessions/sp4-fixture-session-id' },
    },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component: '#2231 DS-17-15. Session summary skeleton (Phase C-2 skeleton-first base).',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof SessionDetailPage>;

export const Default: Story = {};
