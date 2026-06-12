/**
 * sp4-toolkit-templates — DS-17-13 #2220.
 * Mockup parity: `admin-mockups/design_files/sp4-toolkit-templates.{html,jsx}`.
 */

import ToolkitTemplatesPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof ToolkitTemplatesPage> = {
  title: 'Authenticated / sp4-toolkit-templates',
  component: ToolkitTemplatesPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: { appDirectory: true },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: { component: '#2220 DS-17-13. Toolkit templates library.' },
    },
  },
};

export default meta;

type Story = StoryObj<typeof ToolkitTemplatesPage>;

export const Default: Story = {};
