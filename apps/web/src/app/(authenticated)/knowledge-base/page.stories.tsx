/**
 * sp4-kb-hub — DS-17-13 #2220.
 * Mockup parity: `admin-mockups/design_files/sp4-kb-hub.{html,jsx}`.
 */

import KbHubPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof KbHubPage> = {
  title: 'Authenticated / sp4-kb-hub',
  component: KbHubPage,
  parameters: {
    // DS-17 #2063: heuristic can't read named exports/http.get; declare states explicitly.
    canonicalStates: ['default'],
    layout: 'fullscreen',
    nextjs: { appDirectory: true },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: { component: '#2220 DS-17-13. Knowledge base hub catalog index.' },
    },
  },
};

export default meta;

type Story = StoryObj<typeof KbHubPage>;

export const Default: Story = {};
