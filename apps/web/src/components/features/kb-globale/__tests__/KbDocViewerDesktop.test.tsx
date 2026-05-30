import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { KbDocViewerDesktop } from '../KbDocViewerDesktop';

// Mock react-pdf so jsdom can render without a real worker
vi.mock('react-pdf', () => ({
  Document: ({
    children,
    onLoadSuccess,
  }: {
    children: React.ReactNode;
    onLoadSuccess?: (p: { numPages: number }) => void;
  }) => {
    onLoadSuccess?.({ numPages: 24 });
    return <div data-testid="pdf-document">{children}</div>;
  },
  Page: ({ pageNumber }: { pageNumber: number }) => (
    <div data-testid={`pdf-page-${pageNumber}`}>page {pageNumber}</div>
  ),
  pdfjs: { GlobalWorkerOptions: { workerSrc: '' }, version: '5.7.284' },
}));

const docMock = {
  id: 'doc-1',
  title: 'Gloomhaven Rulebook v2',
  fileUrl: '/api/v1/pdfs/doc-1/download',
  pageCount: 24,
};
const labels = {
  pageLabel: (n: number) => `Pagina ${n}`,
  zoomIn: 'Zoom in',
  zoomOut: 'Zoom out',
  zoomReset: 'Zoom reset',
  thumbnailsLabel: 'Pagine',
  closeLabel: 'Chiudi',
  pageOfTotal: (cur: number, total: number) => `${cur} / ${total}`,
};

describe('KbDocViewerDesktop', () => {
  it('renders the active page', () => {
    render(
      <KbDocViewerDesktop
        doc={docMock}
        activePage={14}
        citations={[]}
        labels={labels}
        onPageChange={vi.fn()}
        onClose={vi.fn()}
      />
    );
    expect(screen.getByTestId('pdf-page-14')).toBeInTheDocument();
  });

  it('shows page X / Y indicator', () => {
    render(
      <KbDocViewerDesktop
        doc={docMock}
        activePage={14}
        citations={[]}
        labels={labels}
        onPageChange={vi.fn()}
        onClose={vi.fn()}
      />
    );
    expect(screen.getByText('14 / 24')).toBeInTheDocument();
  });

  it('calls onPageChange when thumbnail clicked', async () => {
    const onPageChange = vi.fn();
    render(
      <KbDocViewerDesktop
        doc={docMock}
        activePage={14}
        citations={[]}
        labels={labels}
        onPageChange={onPageChange}
        onClose={vi.fn()}
      />
    );
    const thumb = await screen.findByRole('button', { name: /pagina 15/i });
    await userEvent.click(thumb);
    expect(onPageChange).toHaveBeenCalledWith(15);
  });

  it('calls onClose when close button clicked', async () => {
    const onClose = vi.fn();
    render(
      <KbDocViewerDesktop
        doc={docMock}
        activePage={14}
        citations={[]}
        labels={labels}
        onPageChange={vi.fn()}
        onClose={onClose}
      />
    );
    await userEvent.click(screen.getByRole('button', { name: /chiudi/i }));
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('highlights the active page in citations panel when citation matches activePage (D-E page-level)', () => {
    const citations = [
      { n: 1, docId: docMock.id, page: 14, refText: 'p.14', snippet: 'scout abilities' },
      { n: 2, docId: docMock.id, page: 21, refText: 'p.21', snippet: 'perks' },
    ];
    render(
      <KbDocViewerDesktop
        doc={docMock}
        activePage={14}
        citations={citations}
        labels={labels}
        onPageChange={vi.fn()}
        onClose={vi.fn()}
      />
    );
    const activeCite = screen.getByText(/scout abilities/);
    expect(activeCite.closest('[data-active="true"]')).not.toBeNull();
  });
});
