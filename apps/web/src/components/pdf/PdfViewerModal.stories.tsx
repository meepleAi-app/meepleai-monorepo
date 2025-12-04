import type { Meta, StoryObj } from '@storybook/react';
import { useState } from 'react';
import { PdfViewerModal } from './PdfViewerModal';
import { Button } from '@/components/ui/button';

/**
 * PdfViewerModal - Modal dialog for viewing PDF files (BGAI-073, BGAI-074)
 *
 * ## Features
 * - **PDF Loading**: Loads PDF from URL with authentication (cookies)
 * - **Page Navigation**: Jump to specific page on load, previous/next buttons
 * - **Zoom Controls**: 25%, 50%, 100%, 150%, 200% zoom levels
 * - **Thumbnail Sidebar**: Virtualized page thumbnails for quick navigation
 * - **Keyboard Shortcuts**: +/- for zoom, arrows for page navigation
 * - **Mobile Responsive**: Adapts to viewport with collapsible thumbnails
 * - **Citation Integration**: Opens to specific page when clicking citations
 *
 * ## Accessibility
 * - ✅ ARIA labels on all controls
 * - ✅ Keyboard navigation support
 * - ✅ Focus management in modal
 * - ✅ Screen reader announcements for page changes
 * - ✅ Escape key to close modal
 *
 * ## Usage
 * Primary use case: Display PDF when user clicks citation to jump to specific page
 *
 * @see PdfPreview for local file preview (upload workflow)
 */
const meta = {
  title: 'PDF/PdfViewerModal',
  component: PdfViewerModal,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Modal dialog for viewing PDF documents with page navigation, zoom controls, and thumbnail sidebar. Used for citation navigation in Q&A workflow.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    open: {
      control: 'boolean',
      description: 'Controls modal visibility',
    },
    pdfUrl: {
      control: 'text',
      description: 'URL to load PDF from (supports authenticated endpoints)',
    },
    initialPage: {
      control: { type: 'number', min: 1 },
      description: 'Page to display when modal opens (1-indexed)',
    },
    documentName: {
      control: 'text',
      description: 'Document name displayed in modal title',
    },
  },
} satisfies Meta<typeof PdfViewerModal>;

export default meta;
type Story = StoryObj<typeof meta>;

// Sample PDF URL (public domain test PDF)
const SAMPLE_PDF_URL = 'https://www.africau.edu/images/default/sample.pdf';

/**
 * Wrapper component for interactive stories that manage modal state
 */
function PdfViewerModalWrapper({
  pdfUrl = SAMPLE_PDF_URL,
  initialPage = 1,
  documentName = 'Sample PDF Document',
}: {
  pdfUrl?: string;
  initialPage?: number;
  documentName?: string;
}) {
  const [open, setOpen] = useState(false);

  return (
    <div className="flex flex-col gap-4 items-center">
      <p className="text-sm text-muted-foreground">Click to open the PDF viewer modal</p>
      <Button onClick={() => setOpen(true)}>Open PDF Viewer</Button>
      <PdfViewerModal
        open={open}
        onOpenChange={setOpen}
        pdfUrl={pdfUrl}
        initialPage={initialPage}
        documentName={documentName}
      />
    </div>
  );
}

/**
 * Default PDF viewer with sample document.
 * Opens to page 1 with 100% zoom.
 */
export const Default: Story = {
  render: () => <PdfViewerModalWrapper />,
};

/**
 * Opens directly to page 5.
 * Demonstrates citation jump-to-page functionality (BGAI-074).
 */
export const JumpToPage: Story = {
  render: () => <PdfViewerModalWrapper initialPage={5} documentName="Gloomhaven Rules - Page 5" />,
  parameters: {
    docs: {
      description: {
        story:
          'Opens to a specific page, simulating click-to-jump from a citation in chat response.',
      },
    },
  },
};

/**
 * Custom document name displayed in header.
 * Shows how document identification appears in the modal.
 */
export const CustomDocumentName: Story = {
  render: () => <PdfViewerModalWrapper documentName="Board Game Rules - Terraforming Mars v1.2" />,
};

/**
 * Error state when PDF fails to load.
 * Shows error message with retry guidance.
 */
export const ErrorState: Story = {
  render: () => <PdfViewerModalWrapper pdfUrl="https://invalid-url.example/nonexistent.pdf" />,
  parameters: {
    docs: {
      description: {
        story: 'Displays error message when PDF cannot be loaded from URL.',
      },
    },
  },
};

/**
 * Modal in initially open state.
 * Useful for visual testing of modal appearance.
 */
export const InitiallyOpen: Story = {
  render: () => {
    const [open, setOpen] = useState(true);
    return (
      <PdfViewerModal
        open={open}
        onOpenChange={setOpen}
        pdfUrl={SAMPLE_PDF_URL}
        initialPage={1}
        documentName="PDF Viewer - Initially Open"
      />
    );
  },
  parameters: {
    docs: {
      description: {
        story: 'Modal starts in open state for visual snapshot testing.',
      },
    },
  },
};

/**
 * Dark theme variant.
 * Shows modal appearance on dark background.
 */
export const DarkTheme: Story = {
  render: () => <PdfViewerModalWrapper documentName="Dark Theme PDF Viewer" />,
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    Story => (
      <div className="dark p-8 bg-background min-h-[200px] flex items-center justify-center">
        <Story />
      </div>
    ),
  ],
};

/**
 * Citation click workflow (BGAI-074).
 * Simulates the complete user flow: click citation → open PDF at specific page.
 * This demonstrates the integration between CitationLink and PdfViewerModal.
 */
export const CitationClickWorkflow: Story = {
  render: () => {
    const [open, setOpen] = useState(false);
    const [targetPage, setTargetPage] = useState(1);

    // Simulate citation click handler from ChatContent
    const handleCitationClick = (pageNumber: number) => {
      setTargetPage(pageNumber);
      setOpen(true);
    };

    return (
      <div className="flex flex-col gap-6 items-center p-6 max-w-lg">
        <div className="text-center">
          <h3 className="font-semibold mb-2">Citation Click → Jump to Page</h3>
          <p className="text-sm text-muted-foreground mb-4">
            Click any citation below to open the PDF at that page
          </p>
        </div>

        {/* Simulated chat message with citations */}
        <div className="bg-slate-50 rounded-lg p-4 w-full">
          <p className="text-sm mb-3">
            According to the rulebook, setup requires 5 steps. See{' '}
            <button
              onClick={() => handleCitationClick(3)}
              className="inline-flex items-center px-2 py-0.5 bg-orange-100 text-orange-800 rounded text-xs font-medium hover:bg-orange-200 transition-colors cursor-pointer"
            >
              p.3
            </button>{' '}
            for initial placement and{' '}
            <button
              onClick={() => handleCitationClick(7)}
              className="inline-flex items-center px-2 py-0.5 bg-orange-100 text-orange-800 rounded text-xs font-medium hover:bg-orange-200 transition-colors cursor-pointer"
            >
              p.7
            </button>{' '}
            for advanced rules.
          </p>
        </div>

        <PdfViewerModal
          open={open}
          onOpenChange={setOpen}
          pdfUrl={SAMPLE_PDF_URL}
          initialPage={targetPage}
          documentName="Game Rulebook"
        />
      </div>
    );
  },
  parameters: {
    docs: {
      description: {
        story:
          'Complete citation click workflow: User clicks citation badge in chat → PDF viewer opens at referenced page. ' +
          'This is the primary use case implemented in BGAI-074.',
      },
    },
  },
};
