/**
 * sp3-shared-game-detail — DS-17-10 #2208 sub-issue.
 *
 * Mockup parity: `admin-mockups/design_files/sp3-shared-game-detail.{html,jsx}`.
 * Post Stage 0 BGG cleanup (commit 564d854b9) — KB entry boardgamegeek.com removed.
 *
 * NOTE: this route renders the PUBLIC community view of a shared game
 * (`page-client.tsx` uses `useSharedGameDetail` + `ContributorsSection`),
 * NOT the private library view at `/library/[gameId]` which uses
 * `GameDetailDesktop` (M1-M7 EPIC #2096 deliverables shipped via PR #2207).
 * The two routes are separate concerns despite the mockup name.
 */

import SharedGameDetailPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof SharedGameDetailPage> = {
  title: 'Public / sp3-shared-game-detail',
  component: SharedGameDetailPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: {
      appDirectory: true,
      navigation: {
        pathname: '/shared-games/sp3-shared-game-detail-fixture',
      },
    },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component:
          '#2208 DS-17-10. Public community view of a shared game. Renders `(public)/shared-games/[id]/page.tsx` server wrapper that delegates to `page-client.tsx`.',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof SharedGameDetailPage>;

export const Default: Story = {};
