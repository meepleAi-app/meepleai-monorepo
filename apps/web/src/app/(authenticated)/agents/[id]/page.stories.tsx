/**
 * sp4-agent-detail — DS-17-12 #2214 sub-issue.
 *
 * Mockup parity: `admin-mockups/design_files/sp4-agent-detail.{html,jsx}`.
 */

import AgentDetailPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof AgentDetailPage> = {
  title: 'Authenticated / sp4-agent-detail',
  component: AgentDetailPage,
  parameters: {
    // DS-17 #2063: heuristic can't read named exports/http.get; declare states explicitly.
    canonicalStates: ['default'],
    layout: 'fullscreen',
    nextjs: {
      appDirectory: true,
      navigation: {
        pathname: '/agents/sp4-agent-detail-fixture',
      },
    },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component: '#2214 DS-17-12. Authenticated agent detail view.',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof AgentDetailPage>;

export const Default: Story = {};
