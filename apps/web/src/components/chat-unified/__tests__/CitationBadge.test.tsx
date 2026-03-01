/**
 * CitationBadge Tests
 * Issue #4919: Citations as clickable links to source PDF page.
 *
 * Tests:
 * 1. Renders page number
 * 2. Shows snippet as tooltip title
 * 3. Shows truncated snippet when > 120 chars
 * 4. Shows page fallback tooltip when no snippet
 * 5. Opens PdfPageModal on click
 * 6. Applies custom className
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import React from 'react';

// Mock PdfPageModal to isolate CitationBadge
vi.mock('../PdfPageModal', () => ({
  PdfPageModal: ({ open, citation, onClose }: { open: boolean; citation: { pageNumber: number }; onClose: () => void }) =>
    open ? (
      <div data-testid="pdf-modal" data-page={citation.pageNumber}>
        <button onClick={onClose}>Close</button>
      </div>
    ) : null,
}));

import { CitationBadge } from '../CitationBadge';
import type { Citation } from '@/types';

// ─── Helpers ──────────────────────────────────────────────────────────────────

function makeCitation(overrides: Partial<Citation> = {}): Citation {
  return {
    documentId: 'doc-uuid-1234',
    pageNumber: 42,
    snippet: 'This is a snippet from the rulebook.',
    relevanceScore: 0.95,
    ...overrides,
  };
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('CitationBadge', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders the page number', () => {
    render(<CitationBadge citation={makeCitation({ pageNumber: 7 })} />);
    expect(screen.getByText('p.7')).toBeInTheDocument();
  });

  it('shows snippet as button title (tooltip)', () => {
    const snippet = 'Rolling dice determines movement.';
    render(<CitationBadge citation={makeCitation({ snippet })} />);
    const button = screen.getByRole('button');
    expect(button).toHaveAttribute('title', `"${snippet}"`);
  });

  it('truncates long snippets to 120 chars in tooltip', () => {
    const longSnippet = 'A'.repeat(150);
    render(<CitationBadge citation={makeCitation({ snippet: longSnippet })} />);
    const button = screen.getByRole('button');
    const title = button.getAttribute('title') ?? '';
    // Should be truncated with ellipsis inside quotes
    expect(title).toContain('...');
    // Total: 2 (quotes) + 120 (chars) + 3 (...)
    expect(title.length).toBeLessThanOrEqual(130);
  });

  it('shows page fallback tooltip when no snippet provided', () => {
    render(<CitationBadge citation={makeCitation({ snippet: '' })} />);
    const button = screen.getByRole('button');
    expect(button.getAttribute('title')).toBe('Pagina 42');
  });

  it('opens the PDF modal on click', () => {
    render(<CitationBadge citation={makeCitation({ pageNumber: 3 })} />);
    expect(screen.queryByTestId('pdf-modal')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button'));
    const modal = screen.getByTestId('pdf-modal');
    expect(modal).toBeInTheDocument();
    expect(modal).toHaveAttribute('data-page', '3');
  });

  it('closes the PDF modal when onClose is called', () => {
    render(<CitationBadge citation={makeCitation()} />);
    fireEvent.click(screen.getByRole('button', { name: /apri citazione/i }));
    expect(screen.getByTestId('pdf-modal')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /close/i }));
    expect(screen.queryByTestId('pdf-modal')).not.toBeInTheDocument();
  });

  it('applies additional className', () => {
    render(<CitationBadge citation={makeCitation()} className="extra-class" />);
    const button = screen.getByRole('button');
    expect(button.className).toContain('extra-class');
  });

  it('has accessible aria-label with page number', () => {
    render(<CitationBadge citation={makeCitation({ pageNumber: 15 })} />);
    const button = screen.getByRole('button');
    expect(button).toHaveAttribute('aria-label', 'Apri citazione — pagina 15');
  });
});
