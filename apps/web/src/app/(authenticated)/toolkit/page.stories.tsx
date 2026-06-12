/**
 * sp4-toolkit-detail — DS-17-13 #2220 sub-issue.
 *
 * Mockup parity: `admin-mockups/design_files/sp4-toolkit-detail.{html,jsx}`.
 *
 * Multi-route mockup (P254 canonical pattern):
 * Per MOCKUPS_INDEX line 150, `sp4-toolkit-detail.html` maps to:
 * - `/toolkit` (canonical, this story)
 * - `/toolkit/{sub-routes}`
 * - `/library/[gameId]/toolbox` (shipped via EPIC #2096 M4 GameToolboxTab 1-Card placeholder, PR #2207 — separate concern, NOT rendered by this story)
 * - `/library/[gameId]/toolkit`
 * - `/library/private/[id]/toolkit/configure`
 *
 * POST-EPIC #2096 reconciliation note: M4 GameToolboxTab on /library route is a placeholder
 * (1-Card "In arrivo" pattern). sp4-toolkit-detail mockup represents full Toolkit Hub flow.
 * The two routes/mockups are intentionally divergent — /toolkit (this story) is the
 * standalone Toolkit hub, /library/[gameId]/toolbox is the embedded library tab.
 */

import ToolkitHubPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof ToolkitHubPage> = {
  title: 'Authenticated / sp4-toolkit-detail',
  component: ToolkitHubPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: { appDirectory: true },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component:
          '#2220 DS-17-13. Toolkit hub canonical (P254 multi-route). POST-EPIC #2096 reconciliation: /library/[gameId]/toolbox renders GameToolboxTab placeholder (M4 PR #2207), separate from this Toolkit hub.',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof ToolkitHubPage>;

export const Default: Story = {};
