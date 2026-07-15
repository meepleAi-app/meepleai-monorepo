/**
 * sp3-join — DS-17-10 #2208 sub-issue.
 *
 * Multi-route mockup with /join + /join/event + /join/session variants.
 * Canonical story target = /join base.
 *
 * Mockup parity: `admin-mockups/design_files/sp3-join.{html,jsx}`.
 */

import JoinPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof JoinPage> = {
  title: 'Public / sp3-join',
  component: JoinPage,
  parameters: {
    // DS-17 #2063: heuristic can't read named exports/http.get; declare states explicitly.
    canonicalStates: ['default'],
    layout: 'fullscreen',
    nextjs: { appDirectory: true },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component:
          '#2208 DS-17-10. Canonical story for sp3-join.html mockup base route (event + session variants live in subroutes).',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof JoinPage>;

export const Default: Story = {};
