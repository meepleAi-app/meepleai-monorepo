/**
 * sp4-kb-globale — DS-17-13 #2220 sub-issue.
 *
 * Mockup parity: `admin-mockups/design_files/sp4-kb-globale.{html,jsx}`.
 * Post Stage 0 BGG cleanup (lines 2549-2550 "Connetti BGG" import card removed, commit 37260a9e7).
 */

import KnowledgeBaseGlobalPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof KnowledgeBaseGlobalPage> = {
  title: 'Authenticated / sp4-kb-globale',
  component: KnowledgeBaseGlobalPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: { appDirectory: true },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component:
          '#2220 DS-17-13. Knowledge base globale page (community KB catalog). Route /knowledge-base/global EXISTS pre-DS-17-13; story scaffold only.',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof KnowledgeBaseGlobalPage>;

export const Default: Story = {};
