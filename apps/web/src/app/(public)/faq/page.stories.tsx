/**
 * sp3-faq-enhanced — DS-17-10 #2208 sub-issue.
 *
 * Multi-route mockup with /faq + /games/[id]/faqs (reuse).
 * Canonical story target = /faq base. Post Stage 0 BGG cleanup (commit 564d854b9).
 *
 * Mockup parity: `admin-mockups/design_files/sp3-faq-enhanced.{html,jsx}`.
 */

import FaqPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof FaqPage> = {
  title: 'Public / sp3-faq-enhanced',
  component: FaqPage,
  parameters: {
    // DS-17 #2063: heuristic can't read named exports/http.get; declare states explicitly.
    canonicalStates: ['default'],
    layout: 'fullscreen',
    nextjs: { appDirectory: true },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component:
          '#2208 DS-17-10. Canonical story for sp3-faq-enhanced.html mockup base route (game-specific /games/[id]/faqs reuses same component). BGG references removed in Stage 0 cleanup.',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof FaqPage>;

export const Default: Story = {};
