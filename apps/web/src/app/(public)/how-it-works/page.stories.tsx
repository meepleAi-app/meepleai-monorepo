/**
 * sp3-how-it-works — DS-17-10 #2208 sub-issue.
 *
 * Post Stage 0 BGG cleanup (commit 564d854b9).
 *
 * Mockup parity: `admin-mockups/design_files/sp3-how-it-works.{html,jsx}`.
 */

import HowItWorksPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof HowItWorksPage> = {
  title: 'Public / sp3-how-it-works',
  component: HowItWorksPage,
  parameters: {
    // DS-17 #2063: heuristic can't read named exports/http.get; declare states explicitly.
    canonicalStates: ['default'],
    layout: 'fullscreen',
    nextjs: { appDirectory: true },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component:
          '#2208 DS-17-10. Public how-it-works onboarding page. BGG references removed in Stage 0 cleanup (commit 564d854b9).',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof HowItWorksPage>;

export const Default: Story = {};
