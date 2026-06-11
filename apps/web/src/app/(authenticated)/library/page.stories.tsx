/**
 * @mockup admin-mockups/design_files/sp4-library-desktop.html
 * @mockup admin-mockups/design_files/sp4-library-mobile.html
 *
 * sp4-library-desktop + sp4-library-mobile — DS-17-12 #2214.
 *
 * Multi-variant story covering both sp4-library-desktop (Default) and
 * sp4-library-mobile (MobileVariant forward-refactor) mockups.
 * Mockup parity: admin-mockups/design_files/sp4-library-{desktop,mobile}.{html,jsx}.
 * Post Stage 0 BGG cleanup (commit 79bcbda0e).
 *
 * sp4-library-mobile design_intent: forward-refactor → designer review tracking issue #2216.
 *
 * Note: Detailed desktop frame matrix (frames 09-17) lives in _content.stories.tsx.
 * This file covers the page-level entry point at both viewport breakpoints.
 *
 * Refs: spec docs/superpowers/specs/2026-06-11-ds-17-phase-c-2-sp4-split-and-ds-17-12-design.md,
 *       umbrella #2214, tracking #2216.
 */

import LibraryPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof LibraryPage> = {
  title: 'Pages/SP4/Library / Page Variants',
  component: LibraryPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: { appDirectory: true },
    docs: {
      description: {
        component:
          '#2214 DS-17-12. Authenticated library catalog page entry point. Default = desktop primary (sp4-library-desktop). MobileVariant = forward-refactor mobile <768px (sp4-library-mobile, designer review tracking #2216).',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof LibraryPage>;

/**
 * Desktop primary — sp4-library-desktop frames 09-17.
 * Full desktop layout with MiniNav, LibraryHybridGrid, and RecentActivityRail.
 * Detailed frame matrix available in _content.stories.tsx.
 */
export const Default: Story = {
  name: 'Desktop (sp4-library-desktop)',
  parameters: {
    viewport: { defaultViewport: 'desktop' },
  },
};

/**
 * Mobile forward-refactor — sp4-library-mobile <768px.
 * Compact hero + simplified tabs (Games/Sessions/Chat + overflow) + 1-col MeepleCard list.
 * design_intent: forward-refactor → designer review in progress (tracking #2216).
 * Components render responsively; mobile breakpoint forces narrow layout.
 */
export const MobileVariant: Story = {
  name: 'Mobile forward-refactor (sp4-library-mobile, tracking #2216)',
  parameters: {
    viewport: { defaultViewport: 'mobile1' },
  },
};
