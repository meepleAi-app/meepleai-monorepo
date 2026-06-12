/**
 * sp4-kb-detail — DS-17-13 #2220 sub-issue.
 *
 * Mockup parity: `admin-mockups/design_files/sp4-kb-detail.{html,jsx}`.
 * design_intent: forward-refactor (G4 v3 pivot deferred per MOCKUPS_INDEX).
 * Designer review tracking #2223.
 */

import KnowledgeBaseDetailPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof KnowledgeBaseDetailPage> = {
  title: 'Authenticated / sp4-kb-detail',
  component: KnowledgeBaseDetailPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: {
      appDirectory: true,
      navigation: {
        pathname: '/knowledge-base/sp4-kb-detail-fixture',
      },
    },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component:
          '#2220 DS-17-13. Knowledge base detail page (forward-refactor, designer review #2223).',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof KnowledgeBaseDetailPage>;

export const Default: Story = {};
