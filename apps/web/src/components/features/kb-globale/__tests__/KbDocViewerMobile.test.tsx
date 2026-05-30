import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { KbDocViewerMobile } from '../KbDocViewerMobile';

vi.mock('react-pdf', () => ({
  Document: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="pdf-document">{children}</div>
  ),
  Page: ({ pageNumber }: { pageNumber: number }) => (
    <div data-testid={`pdf-page-${pageNumber}`}>p {pageNumber}</div>
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
  closeLabel: 'Chiudi',
  pageOfTotal: (c: number, t: number) => `${c} / ${t}`,
  tabPdf: 'PDF',
  tabCitations: 'Citazioni',
};

describe('KbDocViewerMobile', () => {
  it('renders sticky header with title + page indicator', () => {
    render(
      <KbDocViewerMobile
        doc={docMock}
        activePage={14}
        citations={[]}
        labels={labels}
        onPageChange={vi.fn()}
        onClose={vi.fn()}
      />
    );
    expect(screen.getByText(/gloomhaven rulebook v2/i)).toBeInTheDocument();
    expect(screen.getByText('14 / 24')).toBeInTheDocument();
  });

  it('shows PDF tab by default', () => {
    render(
      <KbDocViewerMobile
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

  it('switches to citations tab', async () => {
    const citations = [{ n: 1, docId: docMock.id, page: 14, refText: 'p.14', snippet: 'scout' }];
    render(
      <KbDocViewerMobile
        doc={docMock}
        activePage={14}
        citations={citations}
        labels={labels}
        onPageChange={vi.fn()}
        onClose={vi.fn()}
      />
    );
    await userEvent.click(screen.getByRole('tab', { name: /citazioni/i }));
    expect(screen.getByText(/scout/)).toBeInTheDocument();
  });

  it('calls onClose when close button pressed', async () => {
    const onClose = vi.fn();
    render(
      <KbDocViewerMobile
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
});
