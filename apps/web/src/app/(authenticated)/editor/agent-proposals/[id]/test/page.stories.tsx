/**
 * sp4-editor-proposals-test — DS-17-13 #2220.
 * Mockup parity: `admin-mockups/design_files/sp4-editor-proposals-test.{html,jsx}`.
 */

import AgentProposalsTestPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof AgentProposalsTestPage> = {
  title: 'Authenticated / sp4-editor-proposals-test',
  component: AgentProposalsTestPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: {
      appDirectory: true,
      navigation: { pathname: '/editor/agent-proposals/sp4-fixture-id/test' },
    },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: { component: '#2220 DS-17-13. Editor agent proposal test runner.' },
    },
  },
};

export default meta;

type Story = StoryObj<typeof AgentProposalsTestPage>;

export const Default: Story = {};
