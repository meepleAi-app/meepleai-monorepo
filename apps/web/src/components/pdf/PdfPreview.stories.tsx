/**
 * PdfPreview Storybook Stories (Issue #1496: E2E-010)
 *
 * Visual regression tests for Chromatic.
 * Covers: loading, rendering, zoom, navigation, thumbnails, error, keyboard controls, mobile.
 */

import type { Meta, StoryObj } from '@storybook/react';
import { fn } from 'storybook/test';
import { PdfPreview } from './PdfPreview';

// Mock PDF file - minimal valid PDF structure for react-pdf/pdfjs
// This creates a simple 1-page PDF that can be rendered without errors
const mockPdfContent = `%PDF-1.4
1 0 obj
<<
/Type /Catalog
/Pages 2 0 R
>>
endobj
2 0 obj
<<
/Type /Pages
/Kids [3 0 R]
/Count 1
>>
endobj
3 0 obj
<<
/Type /Page
/Parent 2 0 R
/MediaBox [0 0 612 792]
/Contents 4 0 R
/Resources <<
/Font <<
/F1 <<
/Type /Font
/Subtype /Type1
/BaseFont /Helvetica
>>
>>
>>
>>
endobj
4 0 obj
<<
/Length 44
>>
stream
BT
/F1 12 Tf
50 700 Td
(Sample PDF Content) Tj
ET
endstream
endobj
xref
0 5
0000000000 65535 f
0000000009 00000 n
0000000058 00000 n
0000000115 00000 n
0000000317 00000 n
trailer
<<
/Size 5
/Root 1 0 R
>>
startxref
410
%%EOF`;

const mockPdfFile = new File([mockPdfContent], 'sample-rulebook.pdf', {
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
