/**
 * PageViewerPanel — Unit tests
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

import { PageViewerPanel } from '@/components/chat/viewer/PageViewerPanel';

describe('PageViewerPanel', () => {
  const defaultProps = {
    pdfId: 'abc-123',
    pageNumber: 5,
    isOpen: true,
    onClose: vi.fn(),
  };

  const mockPageResponse = {
    pageNumber: 5,
    text: 'Ogni giocatore inizia con due insediamenti e due strade.',
    documentTitle: 'Catan - Regolamento',
    totalPages: 20,
  };

  beforeEach(() => {
    vi.clearAllMocks();
    mockGetPageText.mockResolvedValue(mockPageResponse);
  });

  it('returns null when isOpen is false', () => {
    const { container } = render(<PageViewerPanel {...defaultProps} isOpen={false} />);
    expect(container.innerHTML).toBe('');
  });

  it('renders page text when open', async () => {
    render(<PageViewerPanel {...defaultProps} />);

    expect(screen.getByTestId('page-viewer-panel')).toBeInTheDocument();

    // Should show loading initially
    expect(screen.getByTestId('page-viewer-loading')).toBeInTheDocument();

    // Wait for text to appear
    await waitFor(() => {
      expect(screen.getByTestId('page-viewer-text')).toBeInTheDocument();
    });

    expect(screen.getByTestId('page-viewer-text')).toHaveTextContent(
      'Ogni giocatore inizia con due insediamenti e due strade.'
    );
    expect(mockGetPageText).toHaveBeenCalledWith('abc-123', 5);
  });

  it('shows document title in the header', async () => {
    render(<PageViewerPanel {...defaultProps} />);

    await waitFor(() => {
      expect(screen.getByTestId('page-viewer-text')).toBeInTheDocument();
    });

    expect(screen.getByText('Catan - Regolamento')).toBeInTheDocument();
  });

  it('shows prev/next navigation buttons', async () => {
    render(<PageViewerPanel {...defaultProps} />);

    await waitFor(() => {
      expect(screen.getByTestId('page-viewer-text')).toBeInTheDocument();
    });

    expect(screen.getByTestId('page-viewer-prev')).toBeInTheDocument();
    expect(screen.getByTestId('page-viewer-next')).toBeInTheDocument();
    expect(screen.getByTestId('page-viewer-nav-label')).toHaveTextContent('p.5 / 20');
  });

  it('calls onClose when X button is clicked', async () => {
    render(<PageViewerPanel {...defaultProps} />);

    await waitFor(() => {
      expect(screen.getByTestId('page-viewer-text')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId('page-viewer-close'));
    expect(defaultProps.onClose).toHaveBeenCalledTimes(1);
  });

  it('calls onClose on Escape key', async () => {
    render(<PageViewerPanel {...defaultProps} />);

    await waitFor(() => {
      expect(screen.getByTestId('page-viewer-text')).toBeInTheDocument();
    });

    fireEvent.keyDown(document, { key: 'Escape' });
    expect(defaultProps.onClose).toHaveBeenCalledTimes(1);
  });

  it('navigates to next page', async () => {
    render(<PageViewerPanel {...defaultProps} />);

    await waitFor(() => {
      expect(screen.getByTestId('page-viewer-text')).toBeInTheDocument();
    });

    mockGetPageText.mockResolvedValue({
      ...mockPageResponse,
      pageNumber: 6,
      text: 'Contenuto pagina 6.',
    });

    fireEvent.click(screen.getByTestId('page-viewer-next'));

    await waitFor(() => {
      expect(mockGetPageText).toHaveBeenCalledWith('abc-123', 6);
    });
  });

  it('navigates to previous page', async () => {
    render(<PageViewerPanel {...defaultProps} />);

    await waitFor(() => {
      expect(screen.getByTestId('page-viewer-text')).toBeInTheDocument();
    });

    mockGetPageText.mockResolvedValue({
      ...mockPageResponse,
      pageNumber: 4,
      text: 'Contenuto pagina 4.',
    });

    fireEvent.click(screen.getByTestId('page-viewer-prev'));

    await waitFor(() => {
      expect(mockGetPageText).toHaveBeenCalledWith('abc-123', 4);
    });
  });

  it('disables prev button on page 1', async () => {
    render(<PageViewerPanel {...defaultProps} pageNumber={1} />);

    mockGetPageText.mockResolvedValue({
      ...mockPageResponse,
      pageNumber: 1,
    });

    await waitFor(() => {
      expect(screen.getByTestId('page-viewer-text')).toBeInTheDocument();
    });

    expect(screen.getByTestId('page-viewer-prev')).toBeDisabled();
  });

  it('disables next button on last page', async () => {
    mockGetPageText.mockResolvedValue({
      ...mockPageResponse,
      pageNumber: 20,
      totalPages: 20,
    });

    render(<PageViewerPanel {...defaultProps} pageNumber={20} />);

    await waitFor(() => {
      expect(screen.getByTestId('page-viewer-text')).toBeInTheDocument();
    });

    expect(screen.getByTestId('page-viewer-next')).toBeDisabled();
  });

  it('shows error when API returns null', async () => {
    mockGetPageText.mockResolvedValue(null);

    render(<PageViewerPanel {...defaultProps} />);

    await waitFor(() => {
      expect(screen.getByTestId('page-viewer-error')).toBeInTheDocument();
    });

    expect(screen.getByTestId('page-viewer-error')).toHaveTextContent('Pagina non trovata');
  });

  it('shows error on fetch failure', async () => {
    mockGetPageText.mockRejectedValue(new Error('Network error'));

    render(<PageViewerPanel {...defaultProps} />);

    await waitFor(() => {
      expect(screen.getByTestId('page-viewer-error')).toBeInTheDocument();
    });

    expect(screen.getByTestId('page-viewer-error')).toHaveTextContent(
      'Errore nel caricamento del testo'
    );
  });
});
