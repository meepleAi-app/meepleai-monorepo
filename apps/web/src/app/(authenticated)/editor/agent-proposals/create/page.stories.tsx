/**
 * sp4-editor-proposals-create — DS-17-13 #2220.
 * Mockup parity: `admin-mockups/design_files/sp4-editor-proposals-create.{html,jsx}`.
 */

import AgentProposalsCreatePage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof AgentProposalsCreatePage> = {
  title: 'Authenticated / sp4-editor-proposals-create',
  component: AgentProposalsCreatePage,
  parameters: {
    layout: 'fullscreen',
    nextjs: { appDirectory: true },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: { component: '#2220 DS-17-13. Editor agent proposal create wizard.' },
    },
  },
};

export default meta;

type Story = StoryObj<typeof AgentProposalsCreatePage>;

export const Default: Story = {};
