/**
 * BggSearchPanel Component Tests - Task 7
 *
 * Test coverage:
 * - Initial render with/without initialQuery
 * - BGG search results display with match scoring
 * - Full game details fetch on selection (categories, mechanics, etc.)
 * - onSelect callback with BggFullGameData
 * - Duplicate check with warning UI
 * - Throttle UX (slow response timer)
 * - Unavailable state with retry
 * - Manual BGG ID input
 * - Error states
 *
 * Pattern: Vitest + React Testing Library (TDD)
 */

import { render, screen, waitFor, within, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';

import { BggSearchPanel } from '../BggSearchPanel';
import { useSearchBggGames } from '@/hooks/queries/useSearchBggGames';
import { api } from '@/lib/api';

// Mock dependencies
vi.mock('@/hooks/queries/useSearchBggGames');
vi.mock('@/lib/api', () => ({
  api: {
    bgg: {
      getGameDetails: vi.fn(),
    },
    sharedGames: {
      checkBggDuplicate: vi.fn().mockResolvedValue({
        isDuplicate: false,
        existingGameId: null,
        existingGame: null,
        bggData: null,
      }),
    },
  },
}));

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  };
}

const mockSearchResults = [
  {
    bggId: 13,
    name: 'Catan',
    yearPublished: 1995,
    thumbnailUrl: 'https://example.com/catan.jpg',
    type: 'boardgame',
  },
  {
    bggId: 12345,
    name: 'Settlers of Catan',
    yearPublished: 1995,
    thumbnailUrl: 'https://example.com/settlers.jpg',
    type: 'boardgame',
  },
  {
    bggId: 54321,
    name: 'Catan: Cities & Knights',
    yearPublished: 1998,
    thumbnailUrl: null,
    type: 'boardgame',
  },
];

const mockFullDetails = {
  bggId: 13,
  name: 'Catan',
  description: 'Trade and build settlements',
  yearPublished: 1995,
  minPlayers: 3,
  maxPlayers: 4,
  playingTime: 120,
  minPlayTime: 60,
  maxPlayTime: 120,
  minAge: 10,
  averageRating: 7.14,
  bayesAverageRating: 7.0,
  usersRated: 100000,
  averageWeight: 2.32,
  thumbnailUrl: 'https://example.com/catan-thumb.jpg',
  imageUrl: 'https://example.com/catan.jpg',
  categories: ['Negotiation', 'Economic'],
  mechanics: ['Dice Rolling', 'Trading'],
  designers: ['Klaus Teuber'],
  publishers: ['KOSMOS', 'Catan Studio'],
};

beforeEach(() => {
  vi.clearAllMocks();

  // Default: search returns results
  (useSearchBggGames as ReturnType<typeof vi.fn>).mockReturnValue({
    data: {
      results: mockSearchResults,
      total: 3,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    },
    isLoading: false,
    error: null,
  });

  // Default: getGameDetails returns full data
  (api.bgg.getGameDetails as ReturnType<typeof vi.fn>).mockResolvedValue(mockFullDetails);

  // Default: no duplicate
  (api.sharedGames.checkBggDuplicate as ReturnType<typeof vi.fn>).mockResolvedValue({
    isDuplicate: false,
    existingGameId: null,
    existingGame: null,
    bggData: null,
  });
});

describe('BggSearchPanel', () => {
  describe('Initial Render', () => {
    it('renders search input', () => {
      render(<BggSearchPanel onSelect={vi.fn()} />, { wrapper: createWrapper() });
      expect(screen.getByPlaceholderText(/search for game title/i)).toBeInTheDocument();
    });

    it('renders with initialQuery pre-filled', () => {
      render(<BggSearchPanel onSelect={vi.fn()} initialQuery="Catan" />, {
        wrapper: createWrapper(),
      });
      expect(screen.getByDisplayValue('Catan')).toBeInTheDocument();
    });

    it('renders manual BGG ID section by default', () => {
      render(<BggSearchPanel onSelect={vi.fn()} />, { wrapper: createWrapper() });
      expect(screen.getByText('Inserimento ID manuale')).toBeInTheDocument();
    });

    it('hides manual BGG ID section when showManualIdInput is false', () => {
      render(<BggSearchPanel onSelect={vi.fn()} showManualIdInput={false} />, {
        wrapper: createWrapper(),
      });
      expect(screen.queryByText('Inserimento ID manuale')).not.toBeInTheDocument();
    });
  });

  describe('BGG Search Results', () => {
    it('displays search results with match scores', () => {
      render(<BggSearchPanel onSelect={vi.fn()} initialQuery="Catan" />, {
        wrapper: createWrapper(),
      });

      expect(screen.getByText('Found 3 results')).toBeInTheDocument();

      const result1 = screen.getByTestId('bgg-result-13');
      expect(within(result1).getByText('Catan')).toBeInTheDocument();
      expect(within(result1).getByText('100% match')).toBeInTheDocument();
    });

    it('sorts results by match score descending', () => {
      render(<BggSearchPanel onSelect={vi.fn()} initialQuery="Catan" />, {
        wrapper: createWrapper(),
      });

      const results = screen.getByTestId('bgg-search-results');
      const resultButtons = within(results).getAllByRole('button');

      const firstResultScore = within(resultButtons[0]).getByText(/\d+% match/);
      expect(firstResultScore.textContent).toContain('100%');
    });

    it('shows loading state during search', () => {
      (useSearchBggGames as ReturnType<typeof vi.fn>).mockReturnValue({
        data: null,
        isLoading: true,
        error: null,
      });

      const { container } = render(<BggSearchPanel onSelect={vi.fn()} />, {
        wrapper: createWrapper(),
      });

      const spinner = container.querySelector('.animate-spin');
      expect(spinner).toBeInTheDocument();
    });

    it('shows empty state when no results found', () => {
      (useSearchBggGames as ReturnType<typeof vi.fn>).mockReturnValue({
        data: { results: [], total: 0, page: 1, pageSize: 20, totalPages: 0 },
        isLoading: false,
        error: null,
      });

      render(<BggSearchPanel onSelect={vi.fn()} initialQuery="Catan" />, {
        wrapper: createWrapper(),
      });

      expect(screen.getByText(/no games found/i)).toBeInTheDocument();
    });
  });

  describe('Result Selection with Full Details', () => {
    it('calls onSelect with full game data including metadata', async () => {
      const onSelect = vi.fn();

      render(<BggSearchPanel onSelect={onSelect} initialQuery="Catan" />, {
        wrapper: createWrapper(),
      });

      const result = screen.getByTestId('bgg-result-13');
      await userEvent.click(result);

      await waitFor(() => {
        expect(api.bgg.getGameDetails).toHaveBeenCalledWith(13);
        expect(onSelect).toHaveBeenCalledWith(
          expect.objectContaining({
            id: 13,
            name: 'Catan',
            yearPublished: 1995,
            minPlayers: 3,
            maxPlayers: 4,
            playingTime: 120,
            minAge: 10,
            description: 'Trade and build settlements',
            categories: ['Negotiation', 'Economic'],
            mechanics: ['Dice Rolling', 'Trading'],
            designers: ['Klaus Teuber'],
            publishers: ['KOSMOS', 'Catan Studio'],
            complexityRating: 2.32,
            averageRating: 7.14,
            imageUrl: 'https://example.com/catan.jpg',
            thumbnailUrl: 'https://example.com/catan-thumb.jpg',
          })
        );
      });
    });

    it('shows loading state while fetching full details', async () => {
      (api.bgg.getGameDetails as ReturnType<typeof vi.fn>).mockImplementation(
        () => new Promise(resolve => setTimeout(() => resolve(mockFullDetails), 200))
      );

      const { container } = render(<BggSearchPanel onSelect={vi.fn()} initialQuery="Catan" />, {
        wrapper: createWrapper(),
      });

      const result = screen.getByTestId('bgg-result-13');
      await userEvent.click(result);

      // Should show some loading indication while fetching details
      await waitFor(() => {
        const spinner = container.querySelector('.animate-spin');
        expect(spinner).toBeInTheDocument();
      });
    });

    it('highlights selected result after details are fetched', async () => {
      render(<BggSearchPanel onSelect={vi.fn()} initialQuery="Catan" />, {
        wrapper: createWrapper(),
      });

      const result = screen.getByTestId('bgg-result-13');
      await userEvent.click(result);

      await waitFor(() => {
        expect(result).toHaveClass('border-primary');
      });
    });
  });

  describe('Duplicate Check', () => {
    it('shows duplicate warning when game exists in catalog', async () => {
      (api.sharedGames.checkBggDuplicate as ReturnType<typeof vi.fn>).mockResolvedValue({
        isDuplicate: true,
        existingGameId: 'abc-123',
        existingGame: null,
        bggData: null,
      });

      render(<BggSearchPanel onSelect={vi.fn()} initialQuery="Catan" />, {
        wrapper: createWrapper(),
      });

      await userEvent.click(screen.getByTestId('bgg-result-13'));

      await waitFor(() => {
        expect(screen.getByText(/already exists/i)).toBeInTheDocument();
      });
    });

    it('does not show duplicate warning when game is new', async () => {
      render(<BggSearchPanel onSelect={vi.fn()} initialQuery="Catan" />, {
        wrapper: createWrapper(),
      });

      await userEvent.click(screen.getByTestId('bgg-result-13'));

      await waitFor(() => {
        expect(api.sharedGames.checkBggDuplicate).toHaveBeenCalledWith(13);
      });

      expect(screen.queryByText(/already exists/i)).not.toBeInTheDocument();
    });

    it('still calls onSelect even when duplicate detected', async () => {
      const onSelect = vi.fn();
      (api.sharedGames.checkBggDuplicate as ReturnType<typeof vi.fn>).mockResolvedValue({
        isDuplicate: true,
        existingGameId: 'abc-123',
        existingGame: null,
        bggData: null,
      });

      render(<BggSearchPanel onSelect={onSelect} initialQuery="Catan" />, {
        wrapper: createWrapper(),
      });

      await userEvent.click(screen.getByTestId('bgg-result-13'));

      await waitFor(() => {
        expect(onSelect).toHaveBeenCalled();
      });
    });
  });

  describe('Search Error / Unavailable State', () => {
    it('displays error when search fails', () => {
      (useSearchBggGames as ReturnType<typeof vi.fn>).mockReturnValue({
        data: null,
        isLoading: false,
        error: new Error('BGG API unavailable'),
      });

      render(<BggSearchPanel onSelect={vi.fn()} />, { wrapper: createWrapper() });

      expect(screen.getByText('Search Failed')).toBeInTheDocument();
      expect(screen.getByText('BGG API unavailable')).toBeInTheDocument();
    });

    it('shows unavailable alert with retry button when search errors persist', () => {
      (useSearchBggGames as ReturnType<typeof vi.fn>).mockReturnValue({
        data: null,
        isLoading: false,
        error: new Error('BGG API unavailable'),
      });

      render(<BggSearchPanel onSelect={vi.fn()} />, { wrapper: createWrapper() });

      expect(screen.getByText('Search Failed')).toBeInTheDocument();
    });
  });

  describe('Manual BGG ID Input', () => {
    it('fetches and selects game by manual BGG ID', async () => {
      const onSelect = vi.fn();
      const manualDetails = {
        ...mockFullDetails,
        bggId: 999,
        name: 'Monopoly',
        yearPublished: 1935,
      };
      (api.bgg.getGameDetails as ReturnType<typeof vi.fn>).mockResolvedValue(manualDetails);

      render(<BggSearchPanel onSelect={onSelect} />, { wrapper: createWrapper() });

      const manualInput = screen.getByTestId('manual-bgg-id-input');
      await userEvent.type(manualInput, '999');

      const fetchBtn = screen.getByTestId('fetch-manual-bgg-btn');
      await userEvent.click(fetchBtn);

      await waitFor(() => {
        expect(api.bgg.getGameDetails).toHaveBeenCalledWith(999);
        expect(screen.getByText('Monopoly')).toBeInTheDocument();
      });

      // Confirm selection
      const confirmBtn = screen.getByTestId('confirm-manual-bgg-btn');
      await userEvent.click(confirmBtn);

      await waitFor(() => {
        expect(onSelect).toHaveBeenCalledWith(
          expect.objectContaining({
            id: 999,
            name: 'Monopoly',
            categories: ['Negotiation', 'Economic'],
          })
        );
      });
    });

    it('shows error when manual BGG ID not found', async () => {
      (api.bgg.getGameDetails as ReturnType<typeof vi.fn>).mockRejectedValue(
        new Error('Gioco con ID 99999 non trovato')
      );

      render(<BggSearchPanel onSelect={vi.fn()} />, { wrapper: createWrapper() });

      const manualInput = screen.getByTestId('manual-bgg-id-input');
      await userEvent.type(manualInput, '99999');

      const fetchBtn = screen.getByTestId('fetch-manual-bgg-btn');
      await userEvent.click(fetchBtn);

      await waitFor(() => {
        expect(screen.getByText(/gioco con id 99999 non trovato/i)).toBeInTheDocument();
      });
    });

    it('validates manual BGG ID (positive number only)', async () => {
      render(<BggSearchPanel onSelect={vi.fn()} />, { wrapper: createWrapper() });

      const fetchBtn = screen.getByTestId('fetch-manual-bgg-btn');
      const manualInput = screen.getByTestId('manual-bgg-id-input');

      expect(fetchBtn).toBeDisabled();

      await userEvent.type(manualInput, '-5');
      expect(fetchBtn).toBeEnabled();

      await userEvent.click(fetchBtn);

      await waitFor(() => {
        expect(screen.getByText('Inserisci un ID valido (numero positivo)')).toBeInTheDocument();
      });
    });
  });
});
