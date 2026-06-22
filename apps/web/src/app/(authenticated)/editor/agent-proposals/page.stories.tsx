/**
 * sp4-editor-proposals-index — DS-17-13 #2220.
 * Mockup parity: `admin-mockups/design_files/sp4-editor-proposals-index.{html,jsx}`.
 */

import AgentProposalsIndexPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof AgentProposalsIndexPage> = {
  title: 'Authenticated / sp4-editor-proposals-index',
  component: AgentProposalsIndexPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: { appDirectory: true },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: { component: '#2220 DS-17-13. Editor agent proposals index list.' },
    },
  },
};

export default meta;

type Story = StoryObj<typeof AgentProposalsIndexPage>;

export const Default: Story = {};
