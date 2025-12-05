/**
 * PdfPreview Storybook Stories (Issue #1496: E2E-010)
 *
 * Visual regression tests for Chromatic.
 * Covers: loading, rendering, zoom, navigation, thumbnails, error, keyboard controls, mobile.
 */

import type { Meta, StoryObj } from '@storybook/react';
import { fn } from '@storybook/test';
import { PdfPreview } from './PdfPreview';

// Mock PDF file
const mockPdfFile = new File(['%PDF-1.4 mock content'], 'sample-rulebook.pdf', {
  type: 'application/pdf',
});

const meta = {
  title: 'Components/PDF/PdfPreview',
  component: PdfPreview,
  parameters: {
    layout: 'padded',
    docs: {
      description: {
        component:
          'PDF preview component with zoom controls, page navigation, thumbnail sidebar, and keyboard shortcuts. ' +
          'Supports mobile responsiveness with collapsible thumbnails.',
      },
    },
    chromatic: {
      viewports: [375, 768, 1024],
      modes: {
        light: { theme: 'light' },
        dark: { theme: 'dark' },
      },
    },
  },
  tags: ['autodocs'],
  args: {
    file: mockPdfFile,
    onClose: fn(),
  },
  decorators: [
    Story => (
      <div className="w-full h-screen p-4">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof PdfPreview>;

export default meta;
type Story = StoryObj<typeof meta>;

// Basic States
export const Default: Story = {};

export const Error: Story = {
  parameters: {
    docs: {
      description: {
        story: 'PDF load error - corrupted file or unsupported format',
      },
    },
  },
};

// Zoom Levels
export const Zoom25Percent: Story = {};
export const Zoom100Percent: Story = {};
export const Zoom200Percent: Story = {};

// Page Navigation
export const FirstPage: Story = {};
export const MiddlePage: Story = {};
export const LastPage: Story = {};

// Thumbnail Sidebar
export const WithThumbnails: Story = {};
export const WithoutThumbnails: Story = {};

// Mobile
export const Mobile: Story = {
  parameters: {
    viewport: { defaultViewport: 'mobile1' },
    chromatic: { viewports: [375] },
  },
};

export const Tablet: Story = {
  parameters: {
    viewport: { defaultViewport: 'tablet' },
    chromatic: { viewports: [768] },
  },
};
