/**
 * sp4-citation-pdf-viewer — DS-17-12 #2214 sub-issue.
 *
 * Component-mock: citation PDF viewer tab used by chat citations.
 * Mockup parity: `admin-mockups/design_files/sp4-citation-pdf-viewer.{html,jsx}`.
 */

import { CitationPdfTab } from './CitationPdfTab';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof CitationPdfTab> = {
  title: 'Component-mocks / sp4-citation-pdf-viewer',
  component: CitationPdfTab,
  parameters: {
    layout: 'padded',
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component:
          '#2214 DS-17-12. Citation PDF viewer tab embedded in chat citations. Component-mock (no standalone route).',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof CitationPdfTab>;

export const Default: Story = {
  args: {
    documentId: 'a1b2c3d4-e5f6-7890-abcd-ef1234567890',
    gameId: 'b2c3d4e5-f6a7-8901-bcde-f12345678901',
    initialPage: 1,
  },
};
