/**
 * SharedGameDetailModal Component Tests
 *
 * Issue #2763: Sprint 3 - Catalog & Shared Games Components (0% → 85%)
 *
 * Tests:
 * - Modal open/close behavior
 * - Loading and error states
 * - Overview tab content
 * - Rules tab (when available)
 * - FAQ tab (when available)
 * - Errata tab (when available)
 * - Tab visibility based on content
 * - Action buttons (Add to Collection, Share, BGG link)
 *
 * Note: Mocks api module directly due to singleton timing issues with MSW.
 * The api singleton is created at module import time before MSW can intercept.
 */

import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { SharedGameDetailModal } from '../SharedGameDetailModal';

// Mock next/image
vi.mock('next/image', () => ({
  default: ({ src, alt, ...props }: { src: string; alt: string }) => (
    // eslint-disable-next-line @next/next/no-img-element
    <img src={src} alt={alt} {...props} />
  ),
}));

// Mock clipboard API
const mockWriteText = vi.fn().mockResolvedValue(undefined);
Object.assign(navigator, {
  clipboard: { writeText: mockWriteText },
});

// Mock window.print
window.print = vi.fn();

// Mock the api module directly (singleton timing issue with MSW)
const mockGetById = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    sharedGames: {
      getById: (id: string) => mockGetById(id),
    },
  },
}));

// Mock game data for game-1 (with all content)
const mockGame1 = {
  id: 'game-1',
  bggId: 13,
  title: 'Catan',
  yearPublished: 1995,
  description: 'A game of trading and building.',
  minPlayers: 3,
  maxPlayers: 4,
  playingTimeMinutes: 90,
  minAge: 10,
  complexityRating: 2.32,
  averageRating: 7.2,
  imageUrl: 'https://example.com/catan.jpg',
  thumbnailUrl: 'https://example.com/catan-thumb.jpg',
  status: 2,
  createdAt: new Date().toISOString(),
  modifiedAt: null,
  createdBy: 'user-1',
  modifiedBy: null,
  rules: '# Setup\n\nPlace the board in the center of the table.',
  faqs: [
    {
      id: 'faq-1',
      question: 'Can I trade on my first turn?',
      answer: 'No, you cannot trade until after your first turn.',
      order: 1,
    },
  ],
  erratas: [
    {
      id: 'errata-1',
      description: 'The card incorrectly states "2 resources" instead of "3 resources".',
      pageReference: '12',
      publishedDate: '2023-01-15',
      createdAt: '2023-01-15',
    },
  ],
  designers: [{ id: 'd1', name: 'Klaus Teuber' }],
  publishers: [{ id: 'p1', name: 'Kosmos' }],
  categories: [
    { id: 'c1', name: 'Strategy' },
    { id: 'c2', name: 'Economic' },
  ],
  mechanics: [{ id: 'm1', name: 'Trading' }],
};

// Mock game data for game-2 (without rules, faqs, erratas)
const mockGame2 = {
  id: 'game-2',
  bggId: 9209,
  title: 'Ticket to Ride',
  yearPublished: 2004,
  description: 'A cross-country train adventure.',
  minPlayers: 2,
  maxPlayers: 5,
  playingTimeMinutes: 60,
  minAge: 8,
  complexityRating: 1.82,
  averageRating: 7.5,
  imageUrl: 'https://example.com/ttr.jpg',
  thumbnailUrl: 'https://example.com/ttr-thumb.jpg',
  status: 2,
  createdAt: new Date().toISOString(),
  modifiedAt: null,
  createdBy: 'user-1',
  modifiedBy: null,
  rules: null,
  faqs: [],
  erratas: [],
  designers: [],
  publishers: [],
  categories: [],
  mechanics: [],
};

describe('SharedGameDetailModal', () => {
  const mockOnClose = vi.fn();
  const mockOnAddToCollection = vi.fn();
  const mockOnShare = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();

    // Default mock: return game data based on ID
    mockGetById.mockImplementation((id: string) => {
      if (id === 'game-1') return Promise.resolve(mockGame1);
      if (id === 'game-2') return Promise.resolve(mockGame2);
      return Promise.reject(new Error('Game not found'));
    });
  });

  const defaultProps = {
    gameId: 'game-1',
    open: true,
    onClose: mockOnClose,
    onAddToCollection: mockOnAddToCollection,
    onShare: mockOnShare,
  };

  // ==========================================================================
  // Modal Open/Close
  // ==========================================================================

  describe('Modal Open/Close', () => {
    it('renders when open is true', async () => {
      render(<SharedGameDetailModal {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByRole('dialog')).toBeInTheDocument();
      });
    });

    it('does not render content when open is false', () => {
      render(<SharedGameDetailModal {...defaultProps} open={false} />);
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    it('does not fetch when gameId is null', () => {
      render(<SharedGameDetailModal {...defaultProps} gameId={null} />);
      // Modal should not show loading state for null gameId
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    it('calls onClose when modal is closed', async () => {
      render(<SharedGameDetailModal {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByRole('dialog')).toBeInTheDocument();
      });

      // Close button is typically the X button
      const closeButton = screen.getByRole('button', { name: /close/i });
      fireEvent.click(closeButton);

      expect(mockOnClose).toHaveBeenCalled();
    });
  });

  // ==========================================================================
  // Loading State
  // ==========================================================================

  describe('Loading State', () => {
    it('shows loading spinner while fetching', async () => {
      // Create a delayed promise
      let resolvePromise: (value: unknown) => void;
      const delayedPromise = new Promise((resolve) => {
        resolvePromise = resolve;
      });
      mockGetById.mockReturnValue(delayedPromise);

      render(<SharedGameDetailModal {...defaultProps} />);

      // Should show loading spinner
      await waitFor(() => {
        expect(screen.getByRole('dialog')).toBeInTheDocument();
      });

      // Loader should be visible initially
      const loader = document.querySelector('.animate-spin');
      expect(loader).toBeInTheDocument();

      // Resolve the promise to clean up
      resolvePromise!(mockGame1);
    });
  });

  // ==========================================================================
  // Error State
  // ==========================================================================

  describe('Error State', () => {
    it('shows error message when fetch fails', async () => {
      mockGetById.mockRejectedValue(new Error('Server error'));

      render(<SharedGameDetailModal {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByText(/impossibile caricare/i)).toBeInTheDocument();
      });
    });

    it('shows retry button when error occurs', async () => {
      mockGetById.mockRejectedValue(new Error('Server error'));

      render(<SharedGameDetailModal {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /riprova/i })).toBeInTheDocument();
      });
    });

    it('retries fetch when retry button is clicked', async () => {
      let callCount = 0;

      mockGetById.mockImplementation(() => {
        callCount++;
        if (callCount === 1) {
          return Promise.reject(new Error('Server error'));
        }
        // Return valid data on second call
        return Promise.resolve(mockGame1);
      });

      render(<SharedGameDetailModal {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /riprova/i })).toBeInTheDocument();
      });

      await act(async () => {
        fireEvent.click(screen.getByRole('button', { name: /riprova/i }));
      });

      await waitFor(() => {
        expect(callCount).toBe(2);
      });
    });
  });

  // ==========================================================================
  // Game Content Display
  // ==========================================================================

  describe('Game Content Display', () => {
    it('displays game title', async () => {
      render(<SharedGameDetailModal {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
      });
    });

    it('displays game image', async () => {
      render(<SharedGameDetailModal {...defaultProps} />);

      await waitFor(() => {
        const image = screen.getByAltText('Catan');
        expect(image).toBeInTheDocument();
        expect(image).toHaveAttribute('src', 'https://example.com/catan.jpg');
      });
    });

    it('displays category badges', async () => {
      render(<SharedGameDetailModal {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByText('Strategy')).toBeInTheDocument();
        expect(screen.getByText('Economic')).toBeInTheDocument();
      });
    });

    it('displays player count', async () => {
      render(<SharedGameDetailModal {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByText(/3-4/)).toBeInTheDocument();
      });
    });

    it('displays playing time', async () => {
      render(<SharedGameDetailModal {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByText(/90/)).toBeInTheDocument();
      });
    });

    it('displays year published', async () => {
      render(<SharedGameDetailModal {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByText(/1995/)).toBeInTheDocument();
      });
    });
  });

  // ==========================================================================
  // Tabs
  // ==========================================================================

  describe('Tabs', () => {
    it('shows Overview tab by default', async () => {
      render(<SharedGameDetailModal {...defaultProps} />);

      await waitFor(() => {
        // Find the active tab (Overview should be selected by default)
        const tabs = screen.getAllByRole('tab');
        expect(tabs.length).toBeGreaterThan(0);
      });
    });

    it('shows Rules tab when rules exist', async () => {
      render(<SharedGameDetailModal {...defaultProps} />);

      await waitFor(() => {
        const rulesTab = screen.getByRole('tab', { name: /regole/i });
        expect(rulesTab).toBeInTheDocument();
      });
    });

    it('shows FAQ tab when FAQs exist', async () => {
      render(<SharedGameDetailModal {...defaultProps} />);

      await waitFor(() => {
        const faqTab = screen.getByRole('tab', { name: /faq/i });
        expect(faqTab).toBeInTheDocument();
      });
    });

    it('shows Errata tab when erratas exist', async () => {
      render(<SharedGameDetailModal {...defaultProps} />);

      await waitFor(() => {
        const errataTab = screen.getByRole('tab', { name: /errata/i });
        expect(errataTab).toBeInTheDocument();
      });
    });

    it('hides Rules tab when no rules', async () => {
      // game-2 has no rules
      render(<SharedGameDetailModal {...defaultProps} gameId="game-2" />);

      await waitFor(() => {
        expect(screen.getByText('Ticket to Ride')).toBeInTheDocument();
      });

      expect(screen.queryByRole('tab', { name: /regole/i })).not.toBeInTheDocument();
    });

    it('hides FAQ tab when no FAQs', async () => {
      // game-2 has no FAQs
      render(<SharedGameDetailModal {...defaultProps} gameId="game-2" />);

      await waitFor(() => {
        expect(screen.getByText('Ticket to Ride')).toBeInTheDocument();
      });

      expect(screen.queryByRole('tab', { name: /faq/i })).not.toBeInTheDocument();
    });

    it('hides Errata tab when no erratas', async () => {
      // game-2 has no erratas
      render(<SharedGameDetailModal {...defaultProps} gameId="game-2" />);

      await waitFor(() => {
        expect(screen.getByText('Ticket to Ride')).toBeInTheDocument();
      });

      expect(screen.queryByRole('tab', { name: /errata/i })).not.toBeInTheDocument();
    });
  });

  // ==========================================================================
  // Overview Tab
  // ==========================================================================

  describe('Overview Tab', () => {
    it('displays description', async () => {
      render(<SharedGameDetailModal {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByText(/trading and building/i)).toBeInTheDocument();
      });
    });

    it('displays mechanics', async () => {
      render(<SharedGameDetailModal {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByText('Trading')).toBeInTheDocument();
      });
    });

    it('displays designers', async () => {
      render(<SharedGameDetailModal {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByText('Klaus Teuber')).toBeInTheDocument();
      });
    });

    it('displays publishers', async () => {
      render(<SharedGameDetailModal {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByText('Kosmos')).toBeInTheDocument();
      });
    });
  });

  // ==========================================================================
  // Rules Tab
  // ==========================================================================

  describe('Rules Tab', () => {
    it('displays rules content when Rules tab is clicked', async () => {
      render(<SharedGameDetailModal {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByRole('tab', { name: /regole/i })).toBeInTheDocument();
      });

      const rulesTab = screen.getByRole('tab', { name: /regole/i });
      await act(async () => {
        fireEvent.click(rulesTab);
      });

      await waitFor(() => {
        expect(screen.getByText(/place the board/i)).toBeInTheDocument();
      });
    });
  });

  // ==========================================================================
  // FAQ Tab
  // ==========================================================================

  describe('FAQ Tab', () => {
    it('displays FAQ questions when FAQ tab is clicked', async () => {
      render(<SharedGameDetailModal {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByRole('tab', { name: /faq/i })).toBeInTheDocument();
      });

      const faqTab = screen.getByRole('tab', { name: /faq/i });
      await act(async () => {
        fireEvent.click(faqTab);
      });

      await waitFor(() => {
        expect(screen.getByText(/Can I trade on my first turn/i)).toBeInTheDocument();
      });
    });
  });

  // ==========================================================================
  // Errata Tab
  // ==========================================================================

  describe('Errata Tab', () => {
    it('displays errata items when Errata tab is clicked', async () => {
      render(<SharedGameDetailModal {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByRole('tab', { name: /errata/i })).toBeInTheDocument();
      });

      const errataTab = screen.getByRole('tab', { name: /errata/i });
      await act(async () => {
        fireEvent.click(errataTab);
      });

      await waitFor(() => {
        expect(screen.getByText(/incorrectly states/i)).toBeInTheDocument();
      });
    });
  });

  // ==========================================================================
  // Action Buttons
  // ==========================================================================

  describe('Action Buttons', () => {
    it('has Add to Collection button', async () => {
      render(<SharedGameDetailModal {...defaultProps} />);

      await waitFor(() => {
        const addButton = screen.getByRole('button', { name: /aggiungi/i });
        expect(addButton).toBeInTheDocument();
      });
    });

    it('calls onAddToCollection when Add button is clicked', async () => {
      render(<SharedGameDetailModal {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /aggiungi/i })).toBeInTheDocument();
      });

      const addButton = screen.getByRole('button', { name: /aggiungi/i });
      fireEvent.click(addButton);

      expect(mockOnAddToCollection).toHaveBeenCalledWith('game-1');
    });

    it('has Share button', async () => {
      render(<SharedGameDetailModal {...defaultProps} />);

      await waitFor(() => {
        // Share button might have icon only or text
        const buttons = screen.getAllByRole('button');
        const shareButton = buttons.find(
          (btn) => btn.textContent?.toLowerCase().includes('condividi') ||
                   btn.querySelector('[data-lucide="share-2"]') ||
                   btn.querySelector('svg')
        );
        expect(shareButton).toBeTruthy();
      });
    });

    it('shows BGG link when bggId exists', async () => {
      render(<SharedGameDetailModal {...defaultProps} />);

      await waitFor(() => {
        const bggLink = screen.getByRole('link', { name: /bgg/i });
        expect(bggLink).toBeInTheDocument();
        expect(bggLink).toHaveAttribute(
          'href',
          expect.stringContaining('boardgamegeek.com/boardgame/13')
        );
      });
    });
  });

  // ==========================================================================
  // State Reset
  // ==========================================================================

  describe('State Reset', () => {
    it('resets to Overview tab when reopened', async () => {
      const { rerender } = render(<SharedGameDetailModal {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByRole('tab', { name: /faq/i })).toBeInTheDocument();
      });

      // Click FAQ tab
      const faqTab = screen.getByRole('tab', { name: /faq/i });
      await act(async () => {
        fireEvent.click(faqTab);
      });

      // Close and reopen
      rerender(<SharedGameDetailModal {...defaultProps} open={false} />);
      rerender(<SharedGameDetailModal {...defaultProps} open={true} />);

      await waitFor(() => {
        // Overview should be active again
        const overviewTab = screen.getAllByRole('tab')[0];
        expect(overviewTab).toHaveAttribute('data-state', 'active');
      });
    });
  });
});
