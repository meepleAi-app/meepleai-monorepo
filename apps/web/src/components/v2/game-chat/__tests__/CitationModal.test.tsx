import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import type { Citation } from '@/types';

import { CitationModal } from '../CitationModal';

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
    fireEvent.click(screen.getByRole('button', { name: /chiudi/i }));
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('hides "Apri nella KB" footer when onOpenInKb is undefined', () => {
    render(<CitationModal citation={sampleCitation} open onClose={vi.fn()} />);
    expect(screen.queryByRole('button', { name: /apri nella kb/i })).not.toBeInTheDocument();
  });

  it('shows "Apri nella KB" when onOpenInKb provided + calls it on click', () => {
    const onOpenInKb = vi.fn();
    render(
      <CitationModal citation={sampleCitation} open onClose={vi.fn()} onOpenInKb={onOpenInKb} />
    );
    const kbBtn = screen.getByRole('button', { name: /apri nella kb/i });
    fireEvent.click(kbBtn);
    expect(onOpenInKb).toHaveBeenCalledOnce();
  });
});
