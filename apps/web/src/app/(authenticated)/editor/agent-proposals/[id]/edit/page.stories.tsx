/**
 * sp4-editor-proposals-edit — DS-17-13 #2220.
 * Mockup parity: `admin-mockups/design_files/sp4-editor-proposals-edit.{html,jsx}`.
 */

import AgentProposalsEditPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof AgentProposalsEditPage> = {
  title: 'Authenticated / sp4-editor-proposals-edit',
  component: AgentProposalsEditPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: {
      appDirectory: true,
      navigation: { pathname: '/editor/agent-proposals/sp4-fixture-id/edit' },
    },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: { component: '#2220 DS-17-13. Editor agent proposal edit form.' },
    },
  },
};

export default meta;

type Story = StoryObj<typeof AgentProposalsEditPage>;

export const Default: Story = {};
