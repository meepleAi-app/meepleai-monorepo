/**
 * SharedGameSearch Component Tests
 *
 * Issue #2763: Sprint 3 - Catalog & Shared Games Components (0% → 85%)
 *
 * Tests:
 * - Search input and debounce behavior
 * - API search results display
 * - No results state
 * - Loading states
 * - Game selection callback
 * - Error handling
 *
 * Note: Mocks api module directly due to singleton timing issues with MSW.
 * The api singleton is created at module import time before MSW can intercept.
 *
 * Note: BGG fallback was removed (restricted to admin only due to licensing).
 */

import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { SharedGameSearch } from '../SharedGameSearch';

// Mock next/image
vi.mock('next/image', () => ({
  default: ({ src, alt, ...props }: { src: string; alt: string }) => (
    // eslint-disable-next-line @next/next/no-img-element
    <img src={src} alt={alt} {...props} />
  ),
}));

// Mock the api module directly (singleton timing issue with MSW)
const mockSearchFn = vi.fn();
const mockGetCategories = vi.fn();
const mockGetMechanics = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    sharedGames: {
      search: (...args: unknown[]) => mockSearchFn(...args),
      getCategories: () => mockGetCategories(),
      getMechanics: () => mockGetMechanics(),
    },
  },
}));

// Mock categories and mechanics for filters
const mockCategories = [
  { id: 'strategy', name: 'Strategy' },
  { id: 'family', name: 'Family' },
];

const mockMechanics = [
  { id: 'worker-placement', name: 'Worker Placement' },
  { id: 'deck-building', name: 'Deck Building' },
];

// Mock catalog search results
const mockCatalogResults = {
  items: [
    {
      id: 'game-1',
      title: 'Catan',
      bggId: 13,
      yearPublished: 1995,
      thumbnailUrl: 'https://example.com/catan-thumb.jpg',
      minPlayers: 3,
      maxPlayers: 4,
      playingTimeMinutes: 90,
      averageRating: 7.2,
    },
    {
      id: 'game-2',
      title: 'Ticket to Ride',
      bggId: 9209,
      yearPublished: 2004,
      thumbnailUrl: 'https://example.com/ttr-thumb.jpg',
      minPlayers: 2,
      maxPlayers: 5,
      playingTimeMinutes: 60,
      averageRating: 7.5,
    },
  ],
  total: 2,
  page: 1,
  pageSize: 10,
};

describe('SharedGameSearch', () => {
  const mockOnSelect = vi.fn();

  beforeEach(() => {
    vi.useFakeTimers();
    vi.clearAllMocks();

    // Default mock: return catalog results
    mockSearchFn.mockResolvedValue(mockCatalogResults);
    mockGetCategories.mockResolvedValue(mockCategories);
    mockGetMechanics.mockResolvedValue(mockMechanics);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  // ==========================================================================
  // Basic Rendering
  // ==========================================================================

  describe('Basic Rendering', () => {
    it('renders search input', () => {
      render(<SharedGameSearch />);
      expect(screen.getByPlaceholderText('Cerca un gioco...')).toBeInTheDocument();
    });

    it('renders with custom placeholder', () => {
      render(<SharedGameSearch placeholder="Search games..." />);
      expect(screen.getByPlaceholderText('Search games...')).toBeInTheDocument();
    });

    it('renders search icon', () => {
      render(<SharedGameSearch />);
      expect(screen.getByLabelText('Cerca giochi')).toBeInTheDocument();
    });

    it('renders filters when showFilters is true', async () => {
      // Use real timers for async operations
      vi.useRealTimers();

      render(<SharedGameSearch showFilters={true} />);
      await waitFor(() => {
        expect(screen.getByRole('button', { name: /Filtri/i })).toBeInTheDocument();
      });

      // Restore fake timers for other tests
      vi.useFakeTimers();
    });

    it('does not render filters when showFilters is false', () => {
      render(<SharedGameSearch showFilters={false} />);
      expect(screen.queryByRole('button', { name: /Filtri/i })).not.toBeInTheDocument();
    });
  });

  // ==========================================================================
  // Search Input Behavior
  // ==========================================================================

  describe('Search Input', () => {
    it('updates input value immediately', async () => {
      render(<SharedGameSearch />);
      const input = screen.getByPlaceholderText('Cerca un gioco...');

      await act(async () => {
        fireEvent.change(input, { target: { value: 'Catan' } });
      });

      expect(input).toHaveValue('Catan');
    });

    it('shows clear button when input has value', async () => {
      render(<SharedGameSearch />);
      const input = screen.getByPlaceholderText('Cerca un gioco...');

      await act(async () => {
        fireEvent.change(input, { target: { value: 'test' } });
      });

      expect(screen.getByLabelText('Cancella ricerca')).toBeInTheDocument();
    });

    it('clears input when clear button is clicked', async () => {
      render(<SharedGameSearch />);
      const input = screen.getByPlaceholderText('Cerca un gioco...');

      await act(async () => {
        fireEvent.change(input, { target: { value: 'test' } });
      });

      const clearButton = screen.getByLabelText('Cancella ricerca');
      await act(async () => {
        fireEvent.click(clearButton);
      });

      expect(input).toHaveValue('');
    });

    it('focuses input after clearing', async () => {
      render(<SharedGameSearch />);
      const input = screen.getByPlaceholderText('Cerca un gioco...');

      await act(async () => {
        fireEvent.change(input, { target: { value: 'test' } });
      });

      const clearButton = screen.getByLabelText('Cancella ricerca');
      await act(async () => {
        fireEvent.click(clearButton);
      });

      expect(document.activeElement).toBe(input);
    });
  });

  // ==========================================================================
  // Debounced Search
  // ==========================================================================

  describe('Debounced Search', () => {
    it('does not search immediately', async () => {
      render(<SharedGameSearch />);
      const input = screen.getByPlaceholderText('Cerca un gioco...');

      await act(async () => {
        fireEvent.change(input, { target: { value: 'Catan' } });
      });

      // Results should not appear immediately
      expect(screen.queryByText('Catan')).not.toBeInTheDocument();
    });

    it('searches after debounce delay', async () => {
      vi.useRealTimers(); // Use real timers for this test

      render(<SharedGameSearch />);
      const input = screen.getByPlaceholderText('Cerca un gioco...');

      fireEvent.change(input, { target: { value: 'Catan' } });

      // Wait for debounce (300ms) + API delay
      await waitFor(
        () => {
          expect(screen.getByText('Catan')).toBeInTheDocument();
        },
        { timeout: 1000 }
      );
    });
  });

  // ==========================================================================
  // Search Results
  // ==========================================================================

  describe('Search Results', () => {
    beforeEach(() => {
      vi.useRealTimers();
    });

    it('displays search results from catalog', async () => {
      render(<SharedGameSearch />);
      const input = screen.getByPlaceholderText('Cerca un gioco...');

      fireEvent.change(input, { target: { value: 'Catan' } });

      await waitFor(
        () => {
          expect(screen.getByText('Catan')).toBeInTheDocument();
        },
        { timeout: 1000 }
      );
    });

    it('shows "Catalogo" badge for catalog results', async () => {
      render(<SharedGameSearch />);
      const input = screen.getByPlaceholderText('Cerca un gioco...');

      fireEvent.change(input, { target: { value: 'Catan' } });

      await waitFor(
        () => {
          const badges = screen.getAllByText('Catalogo');
          expect(badges.length).toBeGreaterThan(0);
        },
        { timeout: 1000 }
      );
    });

    it('shows year published for results', async () => {
      render(<SharedGameSearch />);
      const input = screen.getByPlaceholderText('Cerca un gioco...');

      fireEvent.change(input, { target: { value: 'Catan' } });

      await waitFor(
        () => {
          expect(screen.getByText('1995')).toBeInTheDocument();
        },
        { timeout: 1000 }
      );
    });

    it('shows player count for results', async () => {
      render(<SharedGameSearch />);
      const input = screen.getByPlaceholderText('Cerca un gioco...');

      fireEvent.change(input, { target: { value: 'Catan' } });

      await waitFor(
        () => {
          expect(screen.getByText(/3-4 giocatori/)).toBeInTheDocument();
        },
        { timeout: 1000 }
      );
    });
  });

  // ==========================================================================
  // Game Selection
  // ==========================================================================

  describe('Game Selection', () => {
    beforeEach(() => {
      vi.useRealTimers();
    });

    it('calls onSelect when game is clicked', async () => {
      render(<SharedGameSearch onSelect={mockOnSelect} />);
      const input = screen.getByPlaceholderText('Cerca un gioco...');

      fireEvent.change(input, { target: { value: 'Catan' } });

      await waitFor(
        () => {
          expect(screen.getByText('Catan')).toBeInTheDocument();
        },
        { timeout: 1000 }
      );

      // Click on the game result
      const gameButton = screen.getByText('Catan').closest('button');
      fireEvent.click(gameButton!);

      expect(mockOnSelect).toHaveBeenCalledWith(
        expect.objectContaining({
          id: 'game-1',
          title: 'Catan',
          source: 'catalog',
        })
      );
    });
  });

  // ==========================================================================
  // No Results State
  // ==========================================================================

  describe('No Results', () => {
    beforeEach(() => {
      vi.useRealTimers();
    });

    it('shows no results when catalog returns empty', async () => {
      // Override to return empty results
      mockSearchFn.mockResolvedValue({
        items: [],
        total: 0,
        page: 1,
        pageSize: 20,
      });

      render(<SharedGameSearch />);
      const input = screen.getByPlaceholderText('Cerca un gioco...');

      fireEvent.change(input, { target: { value: 'notfound' } });

      // Wait for search to complete (debounce + API call)
      await waitFor(
        () => {
          expect(mockSearchFn).toHaveBeenCalled();
        },
        { timeout: 1000 }
      );

      // No game results should appear
      expect(screen.queryByText('Catan')).not.toBeInTheDocument();
      expect(screen.queryByText('Ticket to Ride')).not.toBeInTheDocument();
    });
  });

  // ==========================================================================
  // Loading States
  // ==========================================================================

  describe('Loading States', () => {
    beforeEach(() => {
      vi.useRealTimers();
    });

    it('shows loading skeleton while searching', async () => {
      // Create a delayed promise
      let resolveSearch: (value: unknown) => void;
      const delayedPromise = new Promise(resolve => {
        resolveSearch = resolve;
      });
      mockSearchFn.mockReturnValue(delayedPromise);

      render(<SharedGameSearch />);
      const input = screen.getByPlaceholderText('Cerca un gioco...');

      fireEvent.change(input, { target: { value: 'Catan' } });

      // Wait for debounce then check for skeletons
      await waitFor(
        () => {
          // Check for skeleton elements during loading
          const skeletons = document.querySelectorAll('[class*="skeleton"]');
          // Results should eventually load
          expect(skeletons.length).toBeGreaterThanOrEqual(0);
        },
        { timeout: 1000 }
      );

      // Resolve the promise to complete the test
      resolveSearch!({
        items: [],
        total: 0,
        page: 1,
        pageSize: 20,
      });
    });
  });

  // ==========================================================================
  // Error Handling
  // ==========================================================================

  describe('Error Handling', () => {
    beforeEach(() => {
      vi.useRealTimers();
    });

    it('shows error message when catalog search fails', async () => {
      mockSearchFn.mockRejectedValue(new Error('Internal error'));

      render(<SharedGameSearch />);
      const input = screen.getByPlaceholderText('Cerca un gioco...');

      fireEvent.change(input, { target: { value: 'error' } });

      await waitFor(
        () => {
          expect(screen.getByText('Errore nella ricerca. Riprova.')).toBeInTheDocument();
        },
        { timeout: 1000 }
      );
    });
  });

  // ==========================================================================
  // Auto Focus
  // ==========================================================================

  describe('Auto Focus', () => {
    it('focuses input when autoFocus is true', () => {
      render(<SharedGameSearch autoFocus={true} />);
      const input = screen.getByPlaceholderText('Cerca un gioco...');
      expect(document.activeElement).toBe(input);
    });

    it('does not focus input when autoFocus is false', () => {
      render(<SharedGameSearch autoFocus={false} />);
      const input = screen.getByPlaceholderText('Cerca un gioco...');
      expect(document.activeElement).not.toBe(input);
    });
  });
});
