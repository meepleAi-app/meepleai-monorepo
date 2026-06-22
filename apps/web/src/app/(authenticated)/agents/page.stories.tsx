/**
 * sp4-agents-index — DS-17-12 #2214 sub-issue.
 *
 * Mockup parity: `admin-mockups/design_files/sp4-agents-index.{html,jsx}`.
 */

import AgentsIndexPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof AgentsIndexPage> = {
  title: 'Authenticated / sp4-agents-index',
  component: AgentsIndexPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: { appDirectory: true },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component: '#2214 DS-17-12. Authenticated agents catalog.',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof AgentsIndexPage>;

export const Default: Story = {};
