/**
 * sp3-legal (canonical /terms) — DS-17-10 #2208 sub-issue.
 *
 * Multi-route mockup: `sp3-legal.html` serves /privacy + /terms + /cookies +
 * /cookie-settings routes. Canonical story target = /terms (most representative).
 *
 * Mockup parity: `admin-mockups/design_files/sp3-legal.{html,jsx}`.
 */

import TermsPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof TermsPage> = {
  title: 'Public / sp3-legal',
  component: TermsPage,
  parameters: {
    // DS-17 #2063: heuristic can't read named exports/http.get; declare states explicitly.
    canonicalStates: ['default'],
    layout: 'fullscreen',
    nextjs: { appDirectory: true },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component:
          '#2208 DS-17-10. Canonical story for sp3-legal.html mockup (serves /privacy, /terms, /cookies, /cookie-settings).',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof TermsPage>;

export const Default: Story = {};
