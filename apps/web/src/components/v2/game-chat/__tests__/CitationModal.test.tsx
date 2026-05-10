import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import type { Citation } from '@/types';

import { CitationModal } from '../CitationModal';

vi.mock('../CitationPdfTab', () => ({
  CitationPdfTab: (props: any) => (
    <div
      data-testid="citation-pdf-tab-mounted"
      data-document-id={props.documentId}
      data-game-id={props.gameId}
      data-initial-page={props.initialPage}
    >
      PDF tab mock
    </div>
  ),
}));

const sampleCitation: Citation = {
  documentId: 'd1',
  pageNumber: 12,
  snippet: 'Ogni potere "quando attivato" si attiva ogni volta…',
  relevanceScore: 0.95,
  copyrightTier: 'full',
};

describe('CitationModal', () => {
  it('renders nothing when closed', () => {
    render(<CitationModal citation={sampleCitation} open={false} onClose={vi.fn()} />);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('renders dialog with snippet when open', () => {
    render(<CitationModal citation={sampleCitation} open onClose={vi.fn()} />);
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByText(/Ogni potere/)).toBeInTheDocument();
    expect(screen.getByText(/p\. ?12/)).toBeInTheDocument();
  });

  it('calls onClose when close button clicked', () => {
    const onClose = vi.fn();
    render(<CitationModal citation={sampleCitation} open onClose={onClose} />);
    const closeButtons = screen.getAllByRole('button', { name: /chiudi/i });
    fireEvent.click(closeButtons[closeButtons.length - 1]);
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('renders snippet tab content by default', () => {
    render(<CitationModal citation={sampleCitation} open onClose={vi.fn()} gameId="game-1" />);
    expect(screen.getByRole('tab', { name: /snippet/i })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByText(/Ogni potere/)).toBeInTheDocument();
  });

  it('renders both tabs (Snippet + PDF originale) always visible', () => {
    render(<CitationModal citation={sampleCitation} open onClose={vi.fn()} gameId="game-1" />);
    expect(screen.getByRole('tab', { name: /snippet/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /pdf originale/i })).toBeInTheDocument();
  });

  it('switching to PDF tab triggers lazy mount of CitationPdfTab', () => {
    render(<CitationModal citation={sampleCitation} open onClose={vi.fn()} gameId="game-1" />);
    expect(screen.queryByTestId('citation-pdf-tab-mounted')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('tab', { name: /pdf originale/i }));
    expect(screen.getByRole('tab', { name: /pdf originale/i })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByTestId('citation-pdf-tab-mounted')).toBeInTheDocument();
  });

  it('passes gameId and citation fields to CitationPdfTab', () => {
    render(<CitationModal citation={sampleCitation} open onClose={vi.fn()} gameId="wingspan" />);
    fireEvent.click(screen.getByRole('tab', { name: /pdf originale/i }));
    const tab = screen.getByTestId('citation-pdf-tab-mounted');
    expect(tab).toHaveAttribute('data-document-id', sampleCitation.documentId);
    expect(tab).toHaveAttribute('data-game-id', 'wingspan');
    expect(tab).toHaveAttribute('data-initial-page', String(sampleCitation.pageNumber));
  });
});
