/**
 * game-comprehension — ME-M2.2 (Issue #531), parent ADR-051.
 *
 * Public explainer for the AI game-comprehension trust chain (PDF → AI rewords →
 * human reviews per-claim → cited card) with a live citation demo.
 */

import GameComprehensionPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof GameComprehensionPage> = {
  title: 'Public / how-it-works / game-comprehension',
  component: GameComprehensionPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: { appDirectory: true },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component:
          '#531 ME-M2.2. Public landing explaining the AI game-comprehension trust chain, with a hardcoded live citation demo (sample Catan claim + `[p.7]` badge).',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof GameComprehensionPage>;

export const Default: Story = {};
