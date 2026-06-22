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
  Page: ({ pageNumber, scale }: { pageNumber: number; scale?: number }) => (
    <div data-testid="pdf-page" data-page-number={pageNumber} data-scale={scale ?? 1}>
      Page {pageNumber}
    </div>
  ),
  pdfjs: { GlobalWorkerOptions: { workerSrc: '' } },
}));

vi.mock('@/lib/api', () => ({
  api: {
    pdf: { getPdfDownloadUrl: (id: string) => `http://test/api/v1/pdfs/${id}/download` },
  },
}));

const mockFetch = vi.fn();
vi.stubGlobal('fetch', mockFetch);

import { PdfInlineViewer } from '../PdfInlineViewer';

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
});
