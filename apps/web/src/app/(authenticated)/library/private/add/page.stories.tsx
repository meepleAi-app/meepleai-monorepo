/**
 * sp4-add-game-pdf-dedup — DS-17-12 #2214 sub-issue.
 *
 * Mockup parity: `admin-mockups/design_files/sp4-add-game-pdf-dedup.{html,jsx}`.
 * Canonical story for /library/private/add route (PDF dedup wizard step).
 */

import PrivateAddPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof PrivateAddPage> = {
  title: 'Authenticated / sp4-add-game-pdf-dedup',
  component: PrivateAddPage,
  parameters: {
    // DS-17 #2063: heuristic can't read named exports/http.get; declare states explicitly.
    canonicalStates: ['default'],
    layout: 'fullscreen',
    nextjs: { appDirectory: true },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component:
          '#2214 DS-17-12. Add game wizard (PDF dedup step). Canonical route for sp4-add-game-pdf-dedup mockup.',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof PrivateAddPage>;

export const Default: Story = {};
