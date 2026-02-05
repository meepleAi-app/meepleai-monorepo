/**
 * SearchSelectGame Component Tests
 * Issue #3650: Tests for game search with API integration
 */

import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { SearchSelectGame } from '@/components/collection/wizard/steps/SearchSelectGame';
import { useAddGameWizardStore } from '@/stores/addGameWizardStore';
import { api } from '@/lib/api';

// Mock toast
vi.mock('@/components/layout', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
    info: vi.fn(),
    warning: vi.fn(),
  },
}));

// Mock API client
vi.mock('@/lib/api', () => ({
  api: {
    sharedGames: {
      search: vi.fn(),
    },
    library: {
      addGame: vi.fn(),
    },
  },
}));

describe('SearchSelectGame', () => {
  const mockSearchResults = {
    items: [
      {
        id: 'game-1',
        title: 'Catan',
        yearPublished: 1995,
        thumbnailUrl: 'https://example.com/catan.jpg',
        minPlayers: 3,
        maxPlayers: 4,
        playingTimeMinutes: 90,
        averageRating: 8.5,
      },
      {
        id: 'game-2',
        title: 'Ticket to Ride',
        yearPublished: 2004,
        thumbnailUrl: 'https://example.com/ttr.jpg',
        minPlayers: 2,
        maxPlayers: 5,
        playingTimeMinutes: 60,
        averageRating: 7.8,
      },
    ],
    total: 2,
    page: 1,
    pageSize: 10,
  };

  beforeEach(() => {
    useAddGameWizardStore.getState().reset();
    vi.clearAllMocks();
    vi.useFakeTimers({ shouldAdvanceTime: true });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  describe('Rendering', () => {
    it('renders search input', () => {
      render(<SearchSelectGame />);

      expect(screen.getByPlaceholderText(/Search by game title/i)).toBeInTheDocument();
    });

    it('renders create custom game option', () => {
      render(<SearchSelectGame />);

      expect(screen.getByText(/Create Custom Game/i)).toBeInTheDocument();
    });

    it('renders page title and description', () => {
      render(<SearchSelectGame />);

      expect(screen.getByText('Search or Create Game')).toBeInTheDocument();
      expect(screen.getByText(/Search the shared game catalog/i)).toBeInTheDocument();
    });
  });

  describe('Search Functionality', () => {
    it('calls API after debounce when typing', async () => {
      vi.mocked(api.sharedGames.search).mockResolvedValue(mockSearchResults);

      render(<SearchSelectGame />);

      const searchInput = screen.getByPlaceholderText(/Search by game title/i);
      await userEvent.type(searchInput, 'Catan');

      // Should not call API immediately (debounce)
      expect(api.sharedGames.search).not.toHaveBeenCalled();

      // Advance timers past debounce delay (300ms)
      vi.advanceTimersByTime(350);

      await waitFor(() => {
        expect(api.sharedGames.search).toHaveBeenCalledWith({
          searchTerm: 'Catan',
          page: 1,
          pageSize: 10,
          status: 2, // Published only
        });
      });
    });

    it('displays search results', async () => {
      vi.mocked(api.sharedGames.search).mockResolvedValue(mockSearchResults);

      render(<SearchSelectGame />);

      const searchInput = screen.getByPlaceholderText(/Search by game title/i);
      await userEvent.type(searchInput, 'game');
      vi.advanceTimersByTime(350);

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
        expect(screen.getByText('Ticket to Ride')).toBeInTheDocument();
      });
    });

    it('displays total count', async () => {
      vi.mocked(api.sharedGames.search).mockResolvedValue(mockSearchResults);

      render(<SearchSelectGame />);

      const searchInput = screen.getByPlaceholderText(/Search by game title/i);
      await userEvent.type(searchInput, 'game');
      vi.advanceTimersByTime(350);

      await waitFor(() => {
        expect(screen.getByText('2 games found')).toBeInTheDocument();
      });
    });

    it('displays loading skeletons during search', async () => {
      vi.mocked(api.sharedGames.search).mockImplementation(
        () => new Promise(resolve => setTimeout(() => resolve(mockSearchResults), 1000))
      );

      render(<SearchSelectGame />);

      const searchInput = screen.getByPlaceholderText(/Search by game title/i);
      await userEvent.type(searchInput, 'game');
      vi.advanceTimersByTime(350);

      // Should show loading skeletons
      await waitFor(() => {
        expect(document.querySelectorAll('[class*="animate-pulse"]').length).toBeGreaterThan(0);
      });
    });

    it('displays no results message when empty', async () => {
      vi.mocked(api.sharedGames.search).mockResolvedValue({
        items: [],
        total: 0,
        page: 1,
        pageSize: 10,
      });

      render(<SearchSelectGame />);

      const searchInput = screen.getByPlaceholderText(/Search by game title/i);
      await userEvent.type(searchInput, 'nonexistent');
      vi.advanceTimersByTime(350);

      await waitFor(() => {
        expect(screen.getByText(/No games found/i)).toBeInTheDocument();
      });
    });
  });

  describe('Game Selection', () => {
    it('selects a game when clicked', async () => {
      vi.mocked(api.sharedGames.search).mockResolvedValue(mockSearchResults);

      render(<SearchSelectGame />);

      const searchInput = screen.getByPlaceholderText(/Search by game title/i);
      await userEvent.type(searchInput, 'game');
      vi.advanceTimersByTime(350);

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
      });

      // Click on Catan
      fireEvent.click(screen.getByText('Catan'));

      // Check store was updated
      const { selectedGame, isCustomGame } = useAddGameWizardStore.getState();
      expect(selectedGame?.id).toBe('game-1');
      expect(selectedGame?.title).toBe('Catan');
      expect(isCustomGame).toBe(false);
    });

    it('shows Next button after selection', async () => {
      vi.mocked(api.sharedGames.search).mockResolvedValue(mockSearchResults);

      render(<SearchSelectGame />);

      // Initially no Next button
      expect(screen.queryByText(/Next/)).not.toBeInTheDocument();

      const searchInput = screen.getByPlaceholderText(/Search by game title/i);
      await userEvent.type(searchInput, 'game');
      vi.advanceTimersByTime(350);

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
      });

      fireEvent.click(screen.getByText('Catan'));

      // Next button should appear
      expect(screen.getByText(/Next/)).toBeInTheDocument();
    });

    it('shows selected badge on selected game', async () => {
      vi.mocked(api.sharedGames.search).mockResolvedValue(mockSearchResults);

      render(<SearchSelectGame />);

      const searchInput = screen.getByPlaceholderText(/Search by game title/i);
      await userEvent.type(searchInput, 'game');
      vi.advanceTimersByTime(350);

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
      });

      fireEvent.click(screen.getByText('Catan'));

      expect(screen.getByText(/Selected/)).toBeInTheDocument();
    });
  });

  describe('Custom Game Creation', () => {
    it('creates custom game when clicking create button', () => {
      render(<SearchSelectGame />);

      const createButton = screen.getByText(/Create Custom Game/i);
      fireEvent.click(createButton);

      const { isCustomGame, step } = useAddGameWizardStore.getState();
      expect(isCustomGame).toBe(true);
      expect(step).toBe(2); // Should advance to Game Details step
    });
  });

  describe('Error Handling', () => {
    it('displays error message on API failure', async () => {
      vi.mocked(api.sharedGames.search).mockRejectedValue(new Error('Network error'));

      render(<SearchSelectGame />);

      const searchInput = screen.getByPlaceholderText(/Search by game title/i);
      await userEvent.type(searchInput, 'game');
      vi.advanceTimersByTime(350);

      await waitFor(() => {
        expect(screen.getByText(/Network error/i)).toBeInTheDocument();
      });
    });

    it('shows retry button on error', async () => {
      vi.mocked(api.sharedGames.search).mockRejectedValue(new Error('Network error'));

      render(<SearchSelectGame />);

      const searchInput = screen.getByPlaceholderText(/Search by game title/i);
      await userEvent.type(searchInput, 'game');
      vi.advanceTimersByTime(350);

      await waitFor(() => {
        expect(screen.getByText(/Retry/i)).toBeInTheDocument();
      });
    });
  });

  describe('Pagination', () => {
    it('shows load more button when more results available', async () => {
      vi.mocked(api.sharedGames.search).mockResolvedValue({
        ...mockSearchResults,
        total: 25, // More than PAGE_SIZE
      });

      render(<SearchSelectGame />);

      const searchInput = screen.getByPlaceholderText(/Search by game title/i);
      await userEvent.type(searchInput, 'game');
      vi.advanceTimersByTime(350);

      await waitFor(() => {
        expect(screen.getByText(/Load More/i)).toBeInTheDocument();
      });
    });

    it('loads more results when clicking load more', async () => {
      vi.mocked(api.sharedGames.search)
        .mockResolvedValueOnce({
          ...mockSearchResults,
          total: 25,
        })
        .mockResolvedValueOnce({
          items: [
            {
              id: 'game-3',
              title: 'Azul',
              yearPublished: 2017,
              thumbnailUrl: '',
              minPlayers: 2,
              maxPlayers: 4,
              playingTimeMinutes: 45,
              averageRating: 8.2,
            },
          ],
          total: 25,
          page: 2,
          pageSize: 10,
        });

      render(<SearchSelectGame />);

      const searchInput = screen.getByPlaceholderText(/Search by game title/i);
      await userEvent.type(searchInput, 'game');
      vi.advanceTimersByTime(350);

      await waitFor(() => {
        expect(screen.getByText(/Load More/i)).toBeInTheDocument();
      });

      fireEvent.click(screen.getByText(/Load More/i));

      await waitFor(() => {
        expect(api.sharedGames.search).toHaveBeenCalledTimes(2);
        expect(api.sharedGames.search).toHaveBeenLastCalledWith({
          searchTerm: 'game',
          page: 2,
          pageSize: 10,
          status: 2,
        });
      });
    });
  });

  describe('Accessibility', () => {
    it('has proper aria-label on search input', () => {
      render(<SearchSelectGame />);

      const searchInput = screen.getByLabelText(/Search games/i);
      expect(searchInput).toBeInTheDocument();
    });
  });
});
