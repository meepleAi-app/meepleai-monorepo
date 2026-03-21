/**
 * PdfPageModal Tests
 * Issue #4919: Citations as clickable links to source PDF page.
 *
 * Tests:
 * 1. Does not render when open=false
 * 2. Renders dialog when open=true
 * 3. Displays citation page number in header
 * 4. Shows snippet text when available
 * 5. Hides snippet section when no snippet
 * 6. Calls getPdfDownloadUrl with citation documentId
 * 7. Calls onClose when dialog is dismissed
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import React from 'react';

// ─── Mocks ────────────────────────────────────────────────────────────────────

// Mock api to capture getPdfDownloadUrl calls
const mockGetPdfDownloadUrl = vi.fn((id: string) => `http://api/pdfs/${id}/download`);
vi.mock('@/lib/api', () => ({
  api: {
    pdf: {
      getPdfDownloadUrl: (id: string) => mockGetPdfDownloadUrl(id),
    },
  },
}));

// react-pdf is already mocked in vitest.setup.tsx
// (Document renders data-testid="pdf-document", Page renders data-testid="pdf-page")

import { PdfPageModal } from '../PdfPageModal';
import type { Citation } from '@/types';

// ─── Helpers ──────────────────────────────────────────────────────────────────

function makeCitation(overrides: Partial<Citation> = {}): Citation {
  return {
    documentId: 'doc-abc-123',
    pageNumber: 5,
    snippet: 'The player with the most points wins.',
    relevanceScore: 0.9,
    copyrightTier: 'full',
    ...overrides,
  };
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('PdfPageModal', () => {
  const onClose = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('does not render dialog content when open=false', () => {
    render(<PdfPageModal citation={makeCitation()} open={false} onClose={onClose} />);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('renders dialog when open=true', () => {
    render(<PdfPageModal citation={makeCitation()} open={true} onClose={onClose} />);
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });

  it('displays citation page number in the dialog title', () => {
    render(
      <PdfPageModal citation={makeCitation({ pageNumber: 12 })} open={true} onClose={onClose} />
    );
    // Title text includes "Sorgente — Pagina 12"
    expect(screen.getByRole('heading', { name: /pagina 12/i })).toBeInTheDocument();
  });

  it('shows the snippet text', () => {
    const snippet = 'The player with the most points wins.';
    render(<PdfPageModal citation={makeCitation({ snippet })} open={true} onClose={onClose} />);
    expect(screen.getByText(new RegExp(snippet))).toBeInTheDocument();
  });

  it('does not show snippet section when snippet is empty', () => {
    render(<PdfPageModal citation={makeCitation({ snippet: '' })} open={true} onClose={onClose} />);
    // Snippet quote container should not exist
    expect(screen.queryByText(/\u201c/)).not.toBeInTheDocument();
  });

  it('calls getPdfDownloadUrl with the citation documentId', () => {
    render(
      <PdfPageModal
        citation={makeCitation({ documentId: 'my-doc-id' })}
        open={true}
        onClose={onClose}
      />
    );
    expect(mockGetPdfDownloadUrl).toHaveBeenCalledWith('my-doc-id');
  });

  it('renders PDF document and page components', () => {
    render(<PdfPageModal citation={makeCitation()} open={true} onClose={onClose} />);
    // react-pdf is mocked: Document → data-testid="pdf-document"
    expect(screen.getByTestId('pdf-document')).toBeInTheDocument();
  });

  it('shows page navigation controls', () => {
    render(<PdfPageModal citation={makeCitation()} open={true} onClose={onClose} />);
    expect(screen.getByRole('button', { name: /pagina precedente/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /pagina successiva/i })).toBeInTheDocument();
  });

  it('prev page button is disabled on page 1', () => {
    render(
      <PdfPageModal citation={makeCitation({ pageNumber: 1 })} open={true} onClose={onClose} />
    );
    const prevBtn = screen.getByRole('button', { name: /pagina precedente/i });
    expect(prevBtn).toBeDisabled();
  });
});
