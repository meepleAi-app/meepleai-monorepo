/**
 * PdfViewerIntegration Component Tests
 * Issue #3251: [FRONT-015] PDF viewer wrapper for agent chat
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import React from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

// Mock dependencies
vi.mock('@/stores/agentStore', () => ({
  useAgentStore: vi.fn(() => ({
    pdfViewerOpen: false,
    pdfViewerPage: 1,
    pdfViewerDocumentId: null,
    closePdfViewer: vi.fn(),
    setPdfViewerPage: vi.fn(),
    openPdfViewer: vi.fn(),
  })),
}));

vi.mock('sonner', () => ({
  toast: {
    info: vi.fn(),
  },
}));

vi.mock('@/components/pdf-viewer/PdfViewer', () => ({
  PdfViewer: ({ pdfUrl, initialPage }: { pdfUrl: string; initialPage: number }) => (
    <div data-testid="pdf-viewer" data-url={pdfUrl} data-page={initialPage}>
      PDF Viewer Content
    </div>
  ),
}));

// Import after mocks
import { PdfViewerIntegration } from '../PdfViewerIntegration';
import { useAgentStore } from '@/stores/agentStore';
import { toast } from 'sonner';

// Helper to wrap component with QueryClient
function renderWithQueryClient(ui: React.ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
      },
    },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      {ui}
    </QueryClientProvider>
  );
}

describe('PdfViewerIntegration', () => {
  const mockClosePdfViewer = vi.fn();
  const mockSetPdfViewerPage = vi.fn();
  const mockOpenPdfViewer = vi.fn();

  const mockDocuments = [
    {
      id: 'doc-1',
      fileName: 'rulebook.pdf',
      pdfUrl: 'https://example.com/rulebook.pdf',
      isActive: true,
      pageCount: 50,
    },
    {
      id: 'doc-2',
      fileName: 'reference.pdf',
      pdfUrl: 'https://example.com/reference.pdf',
      isActive: false,
      pageCount: 10,
    },
  ];

  beforeEach(() => {
    vi.clearAllMocks();
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: () => Promise.resolve(mockDocuments),
    });

    vi.mocked(useAgentStore).mockReturnValue({
      pdfViewerOpen: false,
      pdfViewerPage: 1,
      pdfViewerDocumentId: null,
      closePdfViewer: mockClosePdfViewer,
      setPdfViewerPage: mockSetPdfViewerPage,
      openPdfViewer: mockOpenPdfViewer,
    } as unknown as ReturnType<typeof useAgentStore>);
  });

  describe('Rendering', () => {
    it('renders nothing when no documents are loaded', () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: () => Promise.resolve([]),
      });

      const { container } = renderWithQueryClient(
        <PdfViewerIntegration gameId="game-123" />
      );
      expect(container.firstChild).toBeNull();
    });

    it('renders nothing when viewer is closed', () => {
      const { container } = renderWithQueryClient(
        <PdfViewerIntegration gameId="game-123" />
      );
      // Even with documents, should not render visible content when closed
      expect(screen.queryByTestId('pdf-viewer')).not.toBeInTheDocument();
    });
  });

  describe('PDF Viewer Dialog', () => {
    it('renders PDF viewer when open and documents loaded', async () => {
      vi.mocked(useAgentStore).mockReturnValue({
        pdfViewerOpen: true,
        pdfViewerPage: 1,
        pdfViewerDocumentId: 'doc-1',
        closePdfViewer: mockClosePdfViewer,
        setPdfViewerPage: mockSetPdfViewerPage,
        openPdfViewer: mockOpenPdfViewer,
      } as unknown as ReturnType<typeof useAgentStore>);

      renderWithQueryClient(<PdfViewerIntegration gameId="game-123" />);

      // Wait for document fetch
      await vi.waitFor(() => {
        expect(screen.getByTestId('pdf-viewer')).toBeInTheDocument();
      });
    });
  });

  describe('Keyboard Shortcuts', () => {
    it('does not open PDF viewer when no documents loaded', async () => {
      // Return empty documents
      global.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: () => Promise.resolve([]),
      });

      renderWithQueryClient(<PdfViewerIntegration gameId="game-123" />);

      // Wait for fetch to complete
      await vi.waitFor(() => {
        expect(global.fetch).toHaveBeenCalled();
      });

      // Simulate P key press
      fireEvent.keyDown(window, { key: 'p' });

      // Should not attempt to open viewer since no documents
      expect(mockOpenPdfViewer).not.toHaveBeenCalled();
    });

    it('closes PDF viewer on P key when open', async () => {
      vi.mocked(useAgentStore).mockReturnValue({
        pdfViewerOpen: true,
        pdfViewerPage: 5,
        pdfViewerDocumentId: 'doc-1',
        closePdfViewer: mockClosePdfViewer,
        setPdfViewerPage: mockSetPdfViewerPage,
        openPdfViewer: mockOpenPdfViewer,
      } as unknown as ReturnType<typeof useAgentStore>);

      renderWithQueryClient(<PdfViewerIntegration gameId="game-123" />);

      await vi.waitFor(() => {
        expect(global.fetch).toHaveBeenCalled();
      });

      fireEvent.keyDown(window, { key: 'P' });

      expect(mockClosePdfViewer).toHaveBeenCalled();
    });

    it('ignores P key when typing in input', () => {
      renderWithQueryClient(<PdfViewerIntegration gameId="game-123" />);

      const input = document.createElement('input');
      document.body.appendChild(input);
      input.focus();

      fireEvent.keyDown(input, { key: 'p', target: input });

      expect(mockOpenPdfViewer).not.toHaveBeenCalled();
      document.body.removeChild(input);
    });

    it('ignores P key when typing in textarea', () => {
      renderWithQueryClient(<PdfViewerIntegration gameId="game-123" />);

      const textarea = document.createElement('textarea');
      document.body.appendChild(textarea);
      textarea.focus();

      fireEvent.keyDown(textarea, { key: 'p', target: textarea });

      expect(mockOpenPdfViewer).not.toHaveBeenCalled();
      document.body.removeChild(textarea);
    });

    it('ignores P key with ctrl modifier', () => {
      renderWithQueryClient(<PdfViewerIntegration gameId="game-123" />);

      fireEvent.keyDown(window, { key: 'p', ctrlKey: true });

      expect(mockOpenPdfViewer).not.toHaveBeenCalled();
    });

    it('ignores P key with meta modifier', () => {
      renderWithQueryClient(<PdfViewerIntegration gameId="game-123" />);

      fireEvent.keyDown(window, { key: 'p', metaKey: true });

      expect(mockOpenPdfViewer).not.toHaveBeenCalled();
    });

    it('ignores P key with alt modifier', () => {
      renderWithQueryClient(<PdfViewerIntegration gameId="game-123" />);

      fireEvent.keyDown(window, { key: 'p', altKey: true });

      expect(mockOpenPdfViewer).not.toHaveBeenCalled();
    });
  });

  describe('Toast Notifications', () => {
    it('has toast module available for notifications', () => {
      // Verify toast mock is available
      expect(toast.info).toBeDefined();
    });
  });

  describe('Document Selection', () => {
    it('fetches documents for the given gameId', async () => {
      renderWithQueryClient(<PdfViewerIntegration gameId="game-456" />);

      await vi.waitFor(() => {
        expect(global.fetch).toHaveBeenCalledWith(
          '/api/v1/admin/shared-games/game-456/documents'
        );
      });
    });

    it('selects active document when no specific document is set', async () => {
      vi.mocked(useAgentStore).mockReturnValue({
        pdfViewerOpen: true,
        pdfViewerPage: 1,
        pdfViewerDocumentId: null, // No specific document
        closePdfViewer: mockClosePdfViewer,
        setPdfViewerPage: mockSetPdfViewerPage,
        openPdfViewer: mockOpenPdfViewer,
      } as unknown as ReturnType<typeof useAgentStore>);

      renderWithQueryClient(<PdfViewerIntegration gameId="game-123" />);

      await vi.waitFor(() => {
        const viewer = screen.getByTestId('pdf-viewer');
        // Should use active document's URL
        expect(viewer.getAttribute('data-url')).toBe('https://example.com/rulebook.pdf');
      });
    });

    it('selects specific document when documentId is set', async () => {
      vi.mocked(useAgentStore).mockReturnValue({
        pdfViewerOpen: true,
        pdfViewerPage: 1,
        pdfViewerDocumentId: 'doc-2', // Specific non-active document
        closePdfViewer: mockClosePdfViewer,
        setPdfViewerPage: mockSetPdfViewerPage,
        openPdfViewer: mockOpenPdfViewer,
      } as unknown as ReturnType<typeof useAgentStore>);

      renderWithQueryClient(<PdfViewerIntegration gameId="game-123" />);

      await vi.waitFor(() => {
        const viewer = screen.getByTestId('pdf-viewer');
        // Should use specific document's URL
        expect(viewer.getAttribute('data-url')).toBe('https://example.com/reference.pdf');
      });
    });
  });

  describe('Page Navigation', () => {
    it('passes initial page to PDF viewer', async () => {
      vi.mocked(useAgentStore).mockReturnValue({
        pdfViewerOpen: true,
        pdfViewerPage: 25,
        pdfViewerDocumentId: 'doc-1',
        closePdfViewer: mockClosePdfViewer,
        setPdfViewerPage: mockSetPdfViewerPage,
        openPdfViewer: mockOpenPdfViewer,
      } as unknown as ReturnType<typeof useAgentStore>);

      renderWithQueryClient(<PdfViewerIntegration gameId="game-123" />);

      await vi.waitFor(() => {
        const viewer = screen.getByTestId('pdf-viewer');
        expect(viewer.getAttribute('data-page')).toBe('25');
      });
    });

    it('defaults to page 1 when no page is set', async () => {
      vi.mocked(useAgentStore).mockReturnValue({
        pdfViewerOpen: true,
        pdfViewerPage: 0, // Falsy value
        pdfViewerDocumentId: 'doc-1',
        closePdfViewer: mockClosePdfViewer,
        setPdfViewerPage: mockSetPdfViewerPage,
        openPdfViewer: mockOpenPdfViewer,
      } as unknown as ReturnType<typeof useAgentStore>);

      renderWithQueryClient(<PdfViewerIntegration gameId="game-123" />);

      await vi.waitFor(() => {
        const viewer = screen.getByTestId('pdf-viewer');
        expect(viewer.getAttribute('data-page')).toBe('1');
      });
    });
  });
});
