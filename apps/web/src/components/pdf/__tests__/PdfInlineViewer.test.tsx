/**
 * PdfInlineViewer — shared inline PDF viewer with feature-flagged toolbar.
 *
 * Spec: docs/superpowers/specs/2026-05-30-sp5-admin-kb-f3-fu5-preview-tab-design.md
 * Plan: docs/superpowers/plans/2026-05-30-sp5-admin-kb-f3-fu5-preview-tab.md (Task 1)
 *
 * Mock pattern source: apps/web/src/components/features/game-chat/__tests__/CitationPdfTab.test.tsx
 */
import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('react-pdf', () => ({
  Document: ({ children, onLoadSuccess }: any) => {
    setTimeout(() => onLoadSuccess?.({ numPages: 5 }), 0);
    return <div data-testid="pdf-document">{children}</div>;
  },
  Page: ({ pageNumber, scale, renderTextLayer, customTextRenderer, children }: any) => (
    <div
      data-testid="pdf-page"
      data-page-number={pageNumber}
      data-scale={scale ?? 1}
      data-render-text-layer={String(!!renderTextLayer)}
      data-has-custom-renderer={String(!!customTextRenderer)}
    >
      Page {pageNumber}
      {children}
    </div>
  ),
  pdfjs: { GlobalWorkerOptions: { workerSrc: '' } },
}));

vi.mock('@/lib/api', () => ({
  api: {
    pdf: {
      getPdfDownloadUrl: (id: string) => `http://test/api/v1/pdfs/${id}/download`,
      // #3447 slice: default no regions so existing tests are unaffected; specific tests override.
      getImageRegions: vi.fn().mockResolvedValue([]),
    },
  },
}));

const mockFetch = vi.fn();
vi.stubGlobal('fetch', mockFetch);

import { PdfInlineViewer } from '../PdfInlineViewer';
import { api } from '@/lib/api';

function mockBlobSuccess() {
  mockFetch.mockResolvedValue({
    ok: true,
    blob: () =>
      Promise.resolve(
        new Blob([new Uint8Array([0x25, 0x50, 0x44, 0x46])], { type: 'application/pdf' })
      ),
  });
}

describe('PdfInlineViewer', () => {
  beforeEach(() => {
    mockFetch.mockReset();
    mockBlobSuccess();
  });

  it('shows loading skeleton while fetching blob', () => {
    mockFetch.mockReturnValue(new Promise(() => {})); // never resolves
    render(<PdfInlineViewer documentId="doc-1" />);
    expect(screen.getByRole('status', { name: /caricamento/i })).toBeInTheDocument();
  });

  it('renders Page at initialPage after fetch success', async () => {
    render(<PdfInlineViewer documentId="doc-1" initialPage={3} />);
    await waitFor(() => {
      expect(screen.getByTestId('pdf-page')).toHaveAttribute('data-page-number', '3');
    });
  });

  it('draws the image-region overlay for the current page (#3447)', async () => {
    vi.mocked(api.pdf.getImageRegions).mockResolvedValueOnce([
      { page: 1, x: 0.1, y: 0.2, width: 0.3, height: 0.1, elementType: 'Image' },
    ]);
    render(<PdfInlineViewer documentId="doc-1" initialPage={1} />);
    await waitFor(() => expect(screen.getByTestId('pdf-page')).toBeInTheDocument());
    await waitFor(() => expect(screen.getByTestId('pdf-image-region-rect')).toBeInTheDocument());
  });

  it('does not draw image regions belonging to other pages (#3447)', async () => {
    vi.mocked(api.pdf.getImageRegions).mockResolvedValueOnce([
      { page: 3, x: 0.1, y: 0.2, width: 0.3, height: 0.1, elementType: 'Image' },
    ]);
    render(<PdfInlineViewer documentId="doc-1" initialPage={1} />);
    await waitFor(() => expect(screen.getByTestId('pdf-page')).toBeInTheDocument());
    expect(screen.queryByTestId('pdf-image-region-rect')).not.toBeInTheDocument();
  });

  it('shows error banner on fetch HTTP 500', async () => {
    mockFetch.mockResolvedValue({ ok: false, status: 500, statusText: 'Server Error' });
    render(<PdfInlineViewer documentId="doc-1" />);
    await waitFor(() => {
      expect(screen.getByText(/HTTP 500/i)).toBeInTheDocument();
    });
  });

  it('ignores AbortError silently (no banner)', async () => {
    mockFetch.mockImplementation((_: unknown, opts: { signal?: AbortSignal }) => {
      return new Promise((_resolve, reject) => {
        opts.signal?.addEventListener('abort', () => {
          const err = new Error('aborted');
          err.name = 'AbortError';
          reject(err);
        });
      });
    });
    const { unmount } = render(<PdfInlineViewer documentId="doc-1" />);
    unmount();
    // no error banner expected; the test passes if no exception thrown
  });

  it('prev/next clamps to [1, numPages]', async () => {
    render(<PdfInlineViewer documentId="doc-1" initialPage={1} />);
    // Wait for Document onLoadSuccess (mock fires via setTimeout 0) to populate numPages
    // — Next button becomes enabled only when numPages > currentPage AND loading=false
    await waitFor(() => expect(screen.getByRole('button', { name: /next/i })).not.toBeDisabled());
    // Prev at page 1 is still disabled (currentPage <= 1)
    expect(screen.getByRole('button', { name: /prev/i })).toBeDisabled();
    // Next advances
    fireEvent.click(screen.getByRole('button', { name: /next/i }));
    await waitFor(() =>
      expect(screen.getByTestId('pdf-page')).toHaveAttribute('data-page-number', '2')
    );
  });

  it('antiLeak=true prevents contextmenu + applies select-none', async () => {
    render(<PdfInlineViewer documentId="doc-1" features={{ antiLeak: true }} />);
    await waitFor(() => expect(screen.getByTestId('pdf-page')).toBeInTheDocument());
    const canvas = screen.getByTestId('pdf-canvas-container');
    expect(canvas.className).toContain('select-none');
    const ev = new MouseEvent('contextmenu', { bubbles: true, cancelable: true });
    const prevented = !canvas.dispatchEvent(ev);
    expect(prevented).toBe(true);
  });

  it('antiLeak=false (default) allows contextmenu, no select-none', async () => {
    render(<PdfInlineViewer documentId="doc-1" />);
    await waitFor(() => expect(screen.getByTestId('pdf-page')).toBeInTheDocument());
    const canvas = screen.getByTestId('pdf-canvas-container');
    expect(canvas.className).not.toContain('select-none');
  });

  it('features.download=true renders <a download> with download URL', async () => {
    render(<PdfInlineViewer documentId="doc-1" features={{ download: true }} />);
    await waitFor(() => expect(screen.getByTestId('pdf-page')).toBeInTheDocument());
    const a = screen.getByRole('link', { name: /download/i });
    expect(a).toHaveAttribute('href', 'http://test/api/v1/pdfs/doc-1/download');
    expect(a).toHaveAttribute('download');
  });

  it('features.openInTab=true renders <a target="_blank" rel="noopener noreferrer">', async () => {
    render(<PdfInlineViewer documentId="doc-1" features={{ openInTab: true }} />);
    await waitFor(() => expect(screen.getByTestId('pdf-page')).toBeInTheDocument());
    const a = screen.getByRole('link', { name: /apri in tab/i });
    expect(a).toHaveAttribute('target', '_blank');
    expect(a).toHaveAttribute('rel', 'noopener noreferrer');
  });

  it('features.jumpToPage=true clamps input to [1, numPages]', async () => {
    render(<PdfInlineViewer documentId="doc-1" features={{ jumpToPage: true }} />);
    await waitFor(() => expect(screen.getByTestId('pdf-page')).toBeInTheDocument());
    const input = screen.getByRole('spinbutton', { name: /vai a pagina/i });
    fireEvent.change(input, { target: { value: '99' } });
    fireEvent.submit(input.closest('form')!);
    await waitFor(() =>
      expect(screen.getByTestId('pdf-page')).toHaveAttribute('data-page-number', '5')
    );
  });

  it('features.jumpToPage=true ignores NaN input silently', async () => {
    render(<PdfInlineViewer documentId="doc-1" features={{ jumpToPage: true }} />);
    await waitFor(() =>
      expect(screen.getByTestId('pdf-page')).toHaveAttribute('data-page-number', '1')
    );
    const input = screen.getByRole('spinbutton', { name: /vai a pagina/i });
    fireEvent.change(input, { target: { value: 'abc' } });
    fireEvent.submit(input.closest('form')!);
    // page should stay at 1 (NaN ignored)
    await new Promise(r => setTimeout(r, 10));
    expect(screen.getByTestId('pdf-page')).toHaveAttribute('data-page-number', '1');
  });

  it('features.zoom=true switches preset → applies scale to Page', async () => {
    render(<PdfInlineViewer documentId="doc-1" defaultZoom={100} features={{ zoom: true }} />);
    await waitFor(() => expect(screen.getByTestId('pdf-page')).toHaveAttribute('data-scale', '1'));
    const zoomSelect = screen.getByRole('combobox', { name: /zoom/i });
    fireEvent.change(zoomSelect, { target: { value: '150' } });
    await waitFor(() =>
      expect(screen.getByTestId('pdf-page')).toHaveAttribute('data-scale', '1.5')
    );
  });

  it('features.zoom=false (default) does not render zoom controls', async () => {
    render(<PdfInlineViewer documentId="doc-1" />);
    await waitFor(() => expect(screen.getByTestId('pdf-page')).toBeInTheDocument());
    expect(screen.queryByRole('combobox', { name: /zoom/i })).not.toBeInTheDocument();
  });

  // ── SP-D #3408: region overlay (Pattern B) via highlightRects ────────────────

  it('renders the bbox overlay for highlightRects on the current page', async () => {
    render(
      <PdfInlineViewer
        documentId="doc-1"
        initialPage={3}
        highlightRects={[{ page: 3, x: 0.1, y: 0.2, width: 0.3, height: 0.05 }]}
      />
    );
    await waitFor(() => expect(screen.getByTestId('pdf-page')).toBeInTheDocument());
    expect(screen.getByTestId('pdf-bbox-rect')).toBeInTheDocument();
  });

  it('does not render overlay rects that belong to another page', async () => {
    render(
      <PdfInlineViewer
        documentId="doc-1"
        initialPage={3}
        highlightRects={[{ page: 7, x: 0.1, y: 0.2, width: 0.3, height: 0.05 }]}
      />
    );
    await waitFor(() => expect(screen.getByTestId('pdf-page')).toBeInTheDocument());
    expect(screen.queryByTestId('pdf-bbox-rect')).not.toBeInTheDocument();
  });

  it('suppresses the quote text layer when highlightRects are present (Pattern B over A)', async () => {
    render(
      <PdfInlineViewer
        documentId="doc-1"
        initialPage={3}
        highlightQuote="regola X"
        highlightRects={[{ page: 3, x: 0.1, y: 0.2, width: 0.3, height: 0.05 }]}
      />
    );
    await waitFor(() => expect(screen.getByTestId('pdf-page')).toBeInTheDocument());
    expect(screen.getByTestId('pdf-page')).toHaveAttribute('data-render-text-layer', 'false');
    expect(screen.getByTestId('pdf-page')).toHaveAttribute('data-has-custom-renderer', 'false');
    expect(screen.getByTestId('pdf-bbox-rect')).toBeInTheDocument();
  });

  it('keeps the quote text layer active when only highlightQuote is provided (Pattern A)', async () => {
    render(<PdfInlineViewer documentId="doc-1" initialPage={3} highlightQuote="regola X" />);
    await waitFor(() => expect(screen.getByTestId('pdf-page')).toBeInTheDocument());
    expect(screen.getByTestId('pdf-page')).toHaveAttribute('data-render-text-layer', 'true');
    expect(screen.getByTestId('pdf-page')).toHaveAttribute('data-has-custom-renderer', 'true');
    expect(screen.queryByTestId('pdf-bbox-rect')).not.toBeInTheDocument();
  });

  // Geometry contract (SP-D #3408): the bbox overlay is a child of <Page> and anchors to its
  // box, so that box MUST shrink-wrap the canvas. The canvas renders at scale×pageWidth (A4
  // denominator) and is left-aligned; if the Page div filled the full container width the
  // %-based rects would misalign for pages narrower than A4. A `justify-center` flex wrapper
  // shrinks the Page ancestor to the canvas (mirrors PdfPageModal). Guard against its removal.
  it('renders the PDF page inside a centering wrapper so the overlay anchors to the canvas', async () => {
    render(<PdfInlineViewer documentId="doc-1" initialPage={1} />);
    await waitFor(() => expect(screen.getByTestId('pdf-document')).toBeInTheDocument());
    const wrapper = screen.getByTestId('pdf-document').parentElement;
    expect(wrapper?.className).toContain('justify-center');
  });
});
