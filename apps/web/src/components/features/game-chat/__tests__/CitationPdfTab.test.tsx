/**
 * CitationPdfTab — integration tests con MSW per ownership API
 * Spec: docs/superpowers/specs/2026-05-10-citation-pdf-viewer-design.md §3.2
 */
import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { ReactNode } from 'react';

// Mock react-pdf to avoid pdfjs worker loading in jsdom
vi.mock('react-pdf', () => ({
  Document: ({ children, onLoadSuccess }: any) => {
    setTimeout(() => onLoadSuccess?.({ numPages: 24 }), 0);
    return <div data-testid="pdf-document">{children}</div>;
  },
  Page: ({ pageNumber }: { pageNumber: number }) => (
    <div data-testid="pdf-page" data-page-number={pageNumber}>
      Page {pageNumber}
    </div>
  ),
  pdfjs: { GlobalWorkerOptions: { workerSrc: '' } },
}));

vi.mock('@/lib/api', () => ({
  api: {
    knowledgeBase: {
      getGameDocuments: vi.fn(),
    },
    pdf: {
      getPdfDownloadUrl: (id: string) => `http://test/api/v1/pdfs/${id}/download`,
    },
  },
}));

// Mock global fetch for blob fetch in viewer
const mockFetch = vi.fn();
vi.stubGlobal('fetch', mockFetch);

import { api } from '@/lib/api';
import { CitationPdfTab } from '../CitationPdfTab';

const sampleDocument = {
  id: 'pdf-wingspan-123',
  title: 'Wingspan Manuale base',
  status: 'indexed' as const,
  pageCount: 24,
  createdAt: '2026-03-08T10:00:00Z',
  category: 'Rulebook' as const,
  versionLabel: 'v1.2',
};

function wrapper({ children }: { children: ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

describe('CitationPdfTab', () => {
  beforeEach(() => {
    vi.mocked(api.knowledgeBase.getGameDocuments).mockReset();
    mockFetch.mockReset();
    mockFetch.mockResolvedValue({
      ok: true,
      blob: () =>
        Promise.resolve(
          new Blob([new Uint8Array([0x25, 0x50, 0x44, 0x46])], { type: 'application/pdf' })
        ),
    });
  });

  it('renders PDF viewer immediately when isPublic=true (no ownership fetch)', async () => {
    render(<CitationPdfTab documentId="pdf-public-1" gameId="game-1" initialPage={12} isPublic />, {
      wrapper,
    });
    await waitFor(() => expect(screen.getByTestId('pdf-page')).toBeInTheDocument());
    expect(api.knowledgeBase.getGameDocuments).not.toHaveBeenCalled();
  });

  it('renders PDF viewer when ownership confirmed (canView=true)', async () => {
    vi.mocked(api.knowledgeBase.getGameDocuments).mockResolvedValueOnce([sampleDocument]);
    render(
      <CitationPdfTab documentId="pdf-wingspan-123" gameId="game-wingspan" initialPage={12} />,
      { wrapper }
    );
    await waitFor(() => expect(screen.getByTestId('pdf-page')).toBeInTheDocument());
    expect(screen.getByTestId('pdf-page')).toHaveAttribute('data-page-number', '12');
  });

  it('renders upsell card when ownership denied (canView=false)', async () => {
    vi.mocked(api.knowledgeBase.getGameDocuments).mockResolvedValueOnce([
      { ...sampleDocument, id: 'pdf-OTHER' },
    ]);
    render(
      <CitationPdfTab documentId="pdf-wingspan-123" gameId="game-wingspan" initialPage={12} />,
      { wrapper }
    );
    await waitFor(() =>
      expect(screen.getByText(/PDF originale protetto da copyright/i)).toBeInTheDocument()
    );
    expect(screen.queryByTestId('pdf-page')).not.toBeInTheDocument();
  });

  it('renders loading state while ownership check in flight', () => {
    vi.mocked(api.knowledgeBase.getGameDocuments).mockReturnValueOnce(new Promise(() => {}));
    render(
      <CitationPdfTab documentId="pdf-wingspan-123" gameId="game-wingspan" initialPage={12} />,
      { wrapper }
    );
    expect(screen.getByRole('status', { name: /caricamento|loading/i })).toBeInTheDocument();
  });

  it('renders upsell as fallback when ownership API errors', async () => {
    vi.mocked(api.knowledgeBase.getGameDocuments).mockRejectedValueOnce(new Error('500'));
    render(
      <CitationPdfTab documentId="pdf-wingspan-123" gameId="game-wingspan" initialPage={12} />,
      { wrapper }
    );
    await waitFor(() =>
      expect(screen.getByText(/PDF originale protetto da copyright/i)).toBeInTheDocument()
    );
  });
});
