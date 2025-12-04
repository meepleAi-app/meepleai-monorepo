import type { Meta, StoryObj } from '@storybook/react';
import { PdfPreview } from './PdfPreview';

/**
 * PdfPreview - Local file preview for upload workflow (BGAI-073)
 *
 * ## Features
 * - **Local File Input**: Accepts File object for preview before upload
 * - **Page Navigation**: Previous/next buttons, jump to specific page
 * - **Zoom Controls**: 25%, 50%, 100%, 150%, 200% zoom levels
 * - **Thumbnail Sidebar**: Virtualized page thumbnails for quick navigation
 * - **Keyboard Shortcuts**: +/- for zoom, arrows for page navigation, Escape to close
 * - **Mobile Responsive**: Adapts to viewport with collapsible thumbnails
 *
 * ## Accessibility
 * - ✅ ARIA labels on all controls
 * - ✅ Keyboard navigation support
 * - ✅ Screen reader announcements for page changes
 * - ✅ Error state with proper role="alert"
 *
 * ## Usage
 * Primary use case: Preview PDF file before upload in document management workflow
 *
 * @see PdfViewerModal for URL-based PDF viewing (citation workflow)
 */
const meta = {
  title: 'PDF/PdfPreview',
  component: PdfPreview,
  parameters: {
    layout: 'padded',
    docs: {
      description: {
        component:
          'Preview component for local PDF files (File objects) with page navigation, zoom controls, and thumbnail sidebar. Used for document upload workflow.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    file: {
      description: 'File object to preview (local PDF file)',
      control: false, // File objects cannot be controlled via Storybook controls
    },
    onClose: {
      description: 'Callback when close button is clicked',
      action: 'closed',
    },
  },
} satisfies Meta<typeof PdfPreview>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Helper to create a mock PDF File object for Storybook stories.
 * Creates a minimal valid PDF structure for demonstration.
 */
function createMockPdfFile(name: string, pageCount: number = 1): File {
  // Create a minimal valid PDF content
  // This is a simplified PDF that renders a single page with text
  const pdfContent = `%PDF-1.4
1 0 obj
<< /Type /Catalog /Pages 2 0 R >>
endobj
2 0 obj
<< /Type /Pages /Kids [3 0 R] /Count ${pageCount} >>
endobj
3 0 obj
<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>
endobj
4 0 obj
<< /Length 44 >>
stream
BT /F1 24 Tf 100 700 Td (Sample PDF) Tj ET
endstream
endobj
5 0 obj
<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>
endobj
xref
0 6
0000000000 65535 f
0000000009 00000 n
0000000058 00000 n
0000000115 00000 n
0000000266 00000 n
0000000360 00000 n
trailer
<< /Size 6 /Root 1 0 R >>
startxref
435
%%EOF`;

  const blob = new Blob([pdfContent], { type: 'application/pdf' });
  return new File([blob], name, { type: 'application/pdf' });
}

/**
 * Default preview with a sample PDF file.
 * Shows basic preview functionality with all controls visible.
 */
export const Default: Story = {
  args: {
    file: createMockPdfFile('sample-document.pdf'),
  },
};

/**
 * Preview with close button callback.
 * Demonstrates the onClose handler functionality.
 */
export const WithCloseButton: Story = {
  args: {
    file: createMockPdfFile('board-game-rules.pdf'),
    onClose: () => console.log('Close clicked'),
  },
};

/**
 * Preview of a longer document name.
 * Tests filename display with longer names.
 */
export const LongFilename: Story = {
  args: {
    file: createMockPdfFile('terraforming-mars-expansion-prelude-rulebook-v2.1-final.pdf'),
  },
};

/**
 * Error state when PDF fails to load.
 * Shows error handling with close button.
 *
 * Note: This story uses an invalid file to trigger the error state.
 */
export const ErrorState: Story = {
  render: () => {
    // Create an invalid PDF file to trigger error state
    const invalidFile = new File(['not a valid pdf content'], 'invalid.pdf', {
      type: 'application/pdf',
    });
    return <PdfPreview file={invalidFile} onClose={() => console.log('Close clicked')} />;
  },
  parameters: {
    docs: {
      description: {
        story: 'Displays error message when PDF file is corrupted or invalid.',
      },
    },
  },
};

/**
 * Dark theme variant.
 * Shows preview appearance on dark background.
 */
export const DarkTheme: Story = {
  args: {
    file: createMockPdfFile('dark-theme-preview.pdf'),
  },
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    Story => (
      <div className="dark p-8 bg-background min-h-[700px]">
        <Story />
      </div>
    ),
  ],
};

/**
 * Compact container variant.
 * Shows preview in a smaller container to test responsive behavior.
 */
export const CompactContainer: Story = {
  args: {
    file: createMockPdfFile('compact-preview.pdf'),
  },
  decorators: [
    Story => (
      <div className="w-[500px] h-[400px] border border-dashed border-gray-400 p-2">
        <Story />
      </div>
    ),
  ],
  parameters: {
    docs: {
      description: {
        story: 'Preview component in a constrained container to test responsive layout.',
      },
    },
  },
};

/**
 * Full width variant.
 * Shows preview taking full available width.
 */
export const FullWidth: Story = {
  args: {
    file: createMockPdfFile('full-width-preview.pdf'),
  },
  decorators: [
    Story => (
      <div className="w-full">
        <Story />
      </div>
    ),
  ],
};
