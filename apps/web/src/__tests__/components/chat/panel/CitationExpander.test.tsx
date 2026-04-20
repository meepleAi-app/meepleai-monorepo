/**
 * CitationExpander — Unit tests
 */

import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

// Mock api before importing component
const mockGetPageText = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    pdf: {
      getPageText: (...args: unknown[]) => mockGetPageText(...args),
    },
  },
}));

import { CitationExpander } from '@/components/chat/panel/CitationExpander';

describe('CitationExpander', () => {
  const defaultProps = {
    pdfId: 'abc-123',
    pageNumber: 5,
    docName: 'Regolamento Catan',
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders collapsed by default with badge visible', () => {
    render(<CitationExpander {...defaultProps} />);

    const badge = screen.getByTestId('citation-badge');
    expect(badge).toBeInTheDocument();
    expect(badge).toHaveTextContent('Regolamento p.5');
    expect(screen.queryByTestId('citation-expanded')).not.toBeInTheDocument();
  });

  it('expands on click and shows loading, then page text', async () => {
    mockGetPageText.mockResolvedValue({
      pageNumber: 5,
      text: 'Ogni giocatore inizia con due insediamenti.',
      documentTitle: 'Catan - Regolamento',
      totalPages: 12,
    });

    render(<CitationExpander {...defaultProps} />);

    fireEvent.click(screen.getByTestId('citation-badge'));

    // Should show expanded area
    expect(screen.getByTestId('citation-expanded')).toBeInTheDocument();

    // Should show loading initially
    expect(screen.getByTestId('citation-loading')).toBeInTheDocument();

    // Wait for text to appear
    await waitFor(() => {
      expect(screen.getByTestId('citation-text')).toBeInTheDocument();
    });

    expect(screen.getByTestId('citation-text')).toHaveTextContent(
      'Ogni giocatore inizia con due insediamenti.'
    );
    expect(mockGetPageText).toHaveBeenCalledWith('abc-123', 5);
  });

  it('collapses on second click', async () => {
    mockGetPageText.mockResolvedValue({
      pageNumber: 5,
      text: 'Some text',
      documentTitle: 'Doc',
      totalPages: 10,
    });

    render(<CitationExpander {...defaultProps} />);

    const badge = screen.getByTestId('citation-badge');

    // Expand
    fireEvent.click(badge);
    await waitFor(() => {
      expect(screen.getByTestId('citation-text')).toBeInTheDocument();
    });

    // Collapse
    fireEvent.click(badge);
    expect(screen.queryByTestId('citation-expanded')).not.toBeInTheDocument();
  });

  it('does not re-fetch on re-expand', async () => {
    mockGetPageText.mockResolvedValue({
      pageNumber: 5,
      text: 'Cached text',
      documentTitle: 'Doc',
      totalPages: 10,
    });

    render(<CitationExpander {...defaultProps} />);

    const badge = screen.getByTestId('citation-badge');

    // Expand
    fireEvent.click(badge);
    await waitFor(() => {
      expect(screen.getByTestId('citation-text')).toBeInTheDocument();
    });
    expect(mockGetPageText).toHaveBeenCalledTimes(1);

    // Collapse
    fireEvent.click(badge);

    // Re-expand
    fireEvent.click(badge);
    await waitFor(() => {
      expect(screen.getByTestId('citation-text')).toBeInTheDocument();
    });

    // Should NOT have fetched again
    expect(mockGetPageText).toHaveBeenCalledTimes(1);
  });

  it('shows error message on fetch failure', async () => {
    mockGetPageText.mockRejectedValue(new Error('Network error'));

    render(<CitationExpander {...defaultProps} />);

    fireEvent.click(screen.getByTestId('citation-badge'));

    await waitFor(() => {
      expect(screen.getByTestId('citation-error')).toBeInTheDocument();
    });

    expect(screen.getByTestId('citation-error')).toHaveTextContent(
      'Errore nel caricamento del testo'
    );
  });

  it('shows error when API returns null', async () => {
    mockGetPageText.mockResolvedValue(null);

    render(<CitationExpander {...defaultProps} />);

    fireEvent.click(screen.getByTestId('citation-badge'));

    await waitFor(() => {
      expect(screen.getByTestId('citation-error')).toBeInTheDocument();
    });

    expect(screen.getByTestId('citation-error')).toHaveTextContent('Pagina non trovata');
  });

  it('displays document title from API response over docName prop', async () => {
    mockGetPageText.mockResolvedValue({
      pageNumber: 5,
      text: 'Page content',
      documentTitle: 'Catan - Regolamento Ufficiale',
      totalPages: 12,
    });

    render(<CitationExpander {...defaultProps} />);

    fireEvent.click(screen.getByTestId('citation-badge'));

    await waitFor(() => {
      expect(screen.getByTestId('citation-text')).toBeInTheDocument();
    });

    // Should use the API documentTitle, not the prop docName
    expect(screen.getByText(/Catan - Regolamento Ufficiale/)).toBeInTheDocument();
  });
});
