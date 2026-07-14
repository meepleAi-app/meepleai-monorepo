/**
 * sp4-upload-wizard-extended — DS-17-12 #2214 sub-issue.
 *
 * Mockup parity: `admin-mockups/design_files/sp4-upload-wizard-extended.{html,jsx}`.
 * Post Stage 0 BGG cleanup (BGG upload option block removed, commit 79bcbda0e).
 *
 * MOCKUPS_INDEX line 151: `sp4-upload-wizard-extended.html` → `/upload`, `/gamebook/upload (partial)`.
 * Canonical story target = `/gamebook/upload`.
 */

import GamebookUploadPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof GamebookUploadPage> = {
  title: 'Authenticated / sp4-upload-wizard-extended',
  component: GamebookUploadPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: { appDirectory: true },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component:
          '#2214 DS-17-12. Upload wizard extended flow (gamebook upload). BGG upload option removed Stage 0.',
      },
    },
    // `Default` export (empty args) covers the canonical `default` state — no
    // `mswForState('default')` string literal for lint:storybook-states to detect
    // (#2342 Task 4 bonifica).
    canonicalStates: ['default'],
  },
};

export default meta;

type Story = StoryObj<typeof GamebookUploadPage>;

export const Default: Story = {};
