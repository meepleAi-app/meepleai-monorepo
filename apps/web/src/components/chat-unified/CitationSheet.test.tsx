/**
 * CitationSheet Tests
 *
 * Tests:
 * 1. Renders snippet when open
 * 2. Renders page number in title
 * 3. Returns null when no citation provided
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import React from 'react';

import { CitationSheet } from './CitationSheet';
import type { CitationData } from './CitationSheet';

// Mock BottomSheet to simplify rendering in tests
vi.mock('@/components/ui/overlays/BottomSheet', () => ({
  BottomSheet: ({
    open,
    title,
    children,
  }: {
    open: boolean;
    title?: string;
    children: React.ReactNode;
  }) =>
    open ? (
      <div data-testid="bottom-sheet">
        {title && <h2 data-testid="sheet-title">{title}</h2>}
        {children}
      </div>
    ) : null,
}));

function makeCitation(overrides: Partial<CitationData> = {}): CitationData {
  return {
    documentId: 'doc-001',
    pageNumber: 12,
    snippet: 'Il giocatore che raccoglie più punti vince la partita.',
    relevanceScore: 0.88,
    copyrightTier: 'full',
    ...overrides,
  };
}

describe('CitationSheet', () => {
  it('renders snippet when open', () => {
    const citation = makeCitation();
    render(<CitationSheet open={true} citation={citation} onOpenChange={vi.fn()} />);
    expect(screen.getByText(citation.snippet)).toBeInTheDocument();
  });

  it('renders page number in the sheet title', () => {
    const citation = makeCitation({ pageNumber: 42 });
    render(<CitationSheet open={true} citation={citation} onOpenChange={vi.fn()} />);
    expect(screen.getByTestId('sheet-title')).toHaveTextContent('Pagina 42');
  });

  it('returns null when no citation is provided', () => {
    const { container } = render(
      <CitationSheet open={true} citation={null} onOpenChange={vi.fn()} />
    );
    expect(container.firstChild).toBeNull();
  });

  it('renders paraphrasedSnippet when copyrightTier is protected', () => {
    const citation = makeCitation({
      copyrightTier: 'protected',
      snippet: 'Testo originale protetto.',
      paraphrasedSnippet: 'Versione parafrasata del testo.',
    });
    render(<CitationSheet open={true} citation={citation} onOpenChange={vi.fn()} />);
    expect(screen.getByText('Versione parafrasata del testo.')).toBeInTheDocument();
    expect(screen.queryByText('Testo originale protetto.')).not.toBeInTheDocument();
  });

  it('renders relevance percentage', () => {
    const citation = makeCitation({ relevanceScore: 0.75 });
    render(<CitationSheet open={true} citation={citation} onOpenChange={vi.fn()} />);
    expect(screen.getByText('75%')).toBeInTheDocument();
  });
});
