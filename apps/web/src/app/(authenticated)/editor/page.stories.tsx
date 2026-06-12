/**
 * sp4-editor-index — DS-17-13 #2220.
 * Mockup parity: `admin-mockups/design_files/sp4-editor-index.{html,jsx}`.
 *
 * NOTE: editor page uses RequireRole — story may render loading/blocked state without auth mock.
 */

import EditorPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof EditorPage> = {
  title: 'Authenticated / sp4-editor-index',
  component: EditorPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: { appDirectory: true },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component: '#2220 DS-17-13. Editor index landing page (Admin/Editor role-gated).',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof EditorPage>;

export const Default: Story = {};
