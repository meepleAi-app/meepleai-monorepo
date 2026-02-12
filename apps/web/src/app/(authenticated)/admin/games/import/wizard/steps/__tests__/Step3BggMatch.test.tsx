/**
 * Step3BggMatch Component Tests - Issue #4164
 *
 * Test coverage:
 * - Initial render with pre-filled title
 * - BGG search results display
 * - Match score calculation
 * - Result selection and store integration
 * - Manual BGG ID input and fetch
 * - Error states
 * - Selected game summary
 *
 * Pattern: Vitest + React Testing Library
 * Target: ≥85% coverage
 */

import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { Step3BggMatch } from '../Step3BggMatch';

import { useGameImportWizardStore } from '@/stores/useGameImportWizardStore';
import { useSearchBggGames } from '@/hooks/queries/useSearchBggGames';
import { api } from '@/lib/api';

// Mock dependencies
vi.mock('@/hooks/queries/useSearchBggGames');
vi.mock('@/lib/api', () => ({
  api: {
    bgg: {
      getGameDetails: vi.fn(),
    },
  },
}));

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

beforeEach(() => {
  vi.clearAllMocks();

  // Reset store
  useGameImportWizardStore.setState({
    currentStep: 3,
    uploadedPdf: { id: 'pdf-123', fileName: 'test.pdf' },
    extractedMetadata: { title: 'Catan', confidence: 85 },
    selectedBggId: null,
    bggGameData: null,
    enrichedData: null,
    isProcessing: false,
    error: null,
  });

  // Default: mock search returns results
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
});

describe('Step3BggMatch', () => {
  describe('Initial Render', () => {
    it('renders heading and description', () => {
      render(<Step3BggMatch />);

      expect(screen.getByText('Select BoardGameGeek Game')).toBeInTheDocument();
      expect(
        screen.getByText(/search for the matching game on boardgamegeek/i)
      ).toBeInTheDocument();
    });

    it('pre-fills search input with extracted title', () => {
      render(<Step3BggMatch />);

      const searchInput = screen.getByTestId('bgg-search-input');
      expect(searchInput).toHaveValue('Catan');
      expect(screen.getByText(/pre-filled with extracted title/i)).toBeInTheDocument();
    });

    it('renders manual BGG ID section', () => {
      render(<Step3BggMatch />);

      expect(screen.getByText('Manual BGG ID Input')).toBeInTheDocument();
      expect(screen.getByTestId('manual-bgg-id-input')).toBeInTheDocument();
      expect(screen.getByTestId('fetch-manual-bgg-btn')).toBeInTheDocument();
    });
  });

  describe('BGG Search Results', () => {
    it('displays search results with match scores', () => {
      render(<Step3BggMatch />);

      expect(screen.getByText('Found 3 results')).toBeInTheDocument();

      // Check first result (using testid to avoid ambiguity)
      const result1 = screen.getByTestId('bgg-result-13');
      expect(within(result1).getByText('Catan')).toBeInTheDocument();
      expect(within(result1).getAllByText('1995')).toHaveLength(1);
      expect(within(result1).getByText('BGG #13')).toBeInTheDocument();
      expect(within(result1).getByText('100% match')).toBeInTheDocument();

      // Check second result
      const result2 = screen.getByTestId('bgg-result-12345');
      expect(within(result2).getByText('Settlers of Catan')).toBeInTheDocument();
      expect(within(result2).getByText('BGG #12345')).toBeInTheDocument();
    });

    it('displays thumbnails for results with imageUrl', () => {
      render(<Step3BggMatch />);

      const results = screen.getByTestId('bgg-search-results');
      const images = within(results).getAllByRole('img');

      expect(images).toHaveLength(2); // 2 results have thumbnailUrl
      expect(images[0]).toHaveAttribute('src', 'https://example.com/catan.jpg');
      expect(images[1]).toHaveAttribute('src', 'https://example.com/settlers.jpg');
    });

    it('shows placeholder for results without thumbnail', () => {
      render(<Step3BggMatch />);

      const result3 = screen.getByTestId('bgg-result-54321');
      // Placeholder is a div with bg-muted containing Search icon (aria-hidden SVG)
      const placeholder = result3.querySelector('.bg-muted');

      expect(placeholder).toBeInTheDocument();
    });

    it('displays type badges', () => {
      render(<Step3BggMatch />);

      const results = screen.getByTestId('bgg-search-results');
      const giocoTags = within(results).getAllByText('Gioco');
      expect(giocoTags).toHaveLength(3);
    });

    it('sorts results by match score descending', () => {
      render(<Step3BggMatch />);

      const results = screen.getByTestId('bgg-search-results');
      const resultButtons = within(results).getAllByRole('button');

      // First result should be exact match (100%)
      expect(within(resultButtons[0]).getByText(/100% match/i)).toBeInTheDocument();
    });
  });

  describe('Result Selection', () => {
    it('selecting a result updates store', async () => {
      const user = userEvent.setup();
      render(<Step3BggMatch />);

      const firstResult = screen.getByTestId('bgg-result-13');
      await user.click(firstResult);

      await waitFor(() => {
        const state = useGameImportWizardStore.getState();
        expect(state.selectedBggId).toBe(13);
        expect(state.bggGameData).toMatchObject({
          id: 13,
          name: 'Catan',
          yearPublished: 1995,
        });
      });
    });

    it('calls onComplete callback when result selected', async () => {
      const user = userEvent.setup();
      const onComplete = vi.fn();

      render(<Step3BggMatch onComplete={onComplete} />);

      const firstResult = screen.getByTestId('bgg-result-13');
      await user.click(firstResult);

      await waitFor(() => {
        expect(onComplete).toHaveBeenCalledWith(
          13,
          expect.objectContaining({
            id: 13,
            name: 'Catan',
          })
        );
      });
    });

    it('highlights selected result', async () => {
      const user = userEvent.setup();
      render(<Step3BggMatch />);

      const firstResult = screen.getByTestId('bgg-result-13');
      await user.click(firstResult);

      await waitFor(() => {
        expect(firstResult).toHaveClass('border-primary');
        expect(firstResult).toHaveClass('ring-2');
      });
    });

    it('shows checkmark icon on selected result', async () => {
      const user = userEvent.setup();
      render(<Step3BggMatch />);

      const firstResult = screen.getByTestId('bgg-result-13');
      await user.click(firstResult);

      await waitFor(() => {
        // CheckCircle2 icon is aria-hidden SVG, check for its presence via className
        const checkIcon = firstResult.querySelector('.lucide-circle-check');
        expect(checkIcon).toBeInTheDocument();
      });
    });

    it('displays selected game summary alert', async () => {
      const user = userEvent.setup();
      render(<Step3BggMatch />);

      const firstResult = screen.getByTestId('bgg-result-13');
      await user.click(firstResult);

      await waitFor(() => {
        const alert = screen.getByRole('alert');
        expect(within(alert).getByText('Game Selected')).toBeInTheDocument();
        // Text content is split: "Selected: " + <strong>Catan</strong> + " (BGG #13)"
        expect(within(alert).getByText(/selected:/i)).toBeInTheDocument();
        expect(within(alert).getByText('Catan')).toBeInTheDocument();
        expect(within(alert).getByText(/\(BGG #13\)/i)).toBeInTheDocument();
      });
    });
  });

  describe('Search Functionality', () => {
    it('allows editing search query', async () => {
      const user = userEvent.setup();
      render(<Step3BggMatch />);

      const searchInput = screen.getByTestId('bgg-search-input');
      await user.clear(searchInput);
      await user.type(searchInput, 'Monopoly');

      expect(searchInput).toHaveValue('Monopoly');
    });

    it('shows loading state during search', () => {
      (useSearchBggGames as ReturnType<typeof vi.fn>).mockReturnValue({
        data: null,
        isLoading: true,
        error: null,
      });

      const { container } = render(<Step3BggMatch />);

      // Loading spinner is an aria-hidden SVG with animate-spin class
      const spinner = container.querySelector('.animate-spin');
      expect(spinner).toBeInTheDocument();
    });

    it('displays error when search fails', () => {
      (useSearchBggGames as ReturnType<typeof vi.fn>).mockReturnValue({
        data: null,
        isLoading: false,
        error: new Error('BGG API unavailable'),
      });

      render(<Step3BggMatch />);

      expect(screen.getByText('Search Failed')).toBeInTheDocument();
      expect(screen.getByText('BGG API unavailable')).toBeInTheDocument();
    });

    it('shows empty state when no results found', () => {
      (useSearchBggGames as ReturnType<typeof vi.fn>).mockReturnValue({
        data: {
          results: [],
          total: 0,
          page: 1,
          pageSize: 20,
          totalPages: 0,
        },
        isLoading: false,
        error: null,
      });

      render(<Step3BggMatch />);

      expect(
        screen.getByText(/no games found.*try a different search term/i)
      ).toBeInTheDocument();
    });
  });

  describe('Manual BGG ID Input', () => {
    it('allows entering manual BGG ID', async () => {
      const user = userEvent.setup();
      render(<Step3BggMatch />);

      const manualInput = screen.getByTestId('manual-bgg-id-input');
      await user.type(manualInput, '13');

      expect(manualInput).toHaveValue(13);
    });

    it('fetches game details when Fetch button clicked', async () => {
      const user = userEvent.setup();
      const mockGameDetails = {
        bggId: 999,
        name: 'Monopoly',
        yearPublished: 1935,
        minPlayers: 2,
        maxPlayers: 6,
        playingTime: 120,
        minAge: 8,
        description: 'Classic game',
        imageUrl: 'https://example.com/monopoly.jpg',
        thumbnailUrl: 'https://example.com/monopoly_thumb.jpg',
      };

      (api.bgg.getGameDetails as ReturnType<typeof vi.fn>).mockResolvedValue(mockGameDetails);

      render(<Step3BggMatch />);

      const manualInput = screen.getByTestId('manual-bgg-id-input');
      await user.type(manualInput, '999');

      const fetchBtn = screen.getByTestId('fetch-manual-bgg-btn');
      await user.click(fetchBtn);

      await waitFor(() => {
        expect(api.bgg.getGameDetails).toHaveBeenCalledWith(999);
      });

      // Preview should display (unique game to avoid conflicts)
      await waitFor(() => {
        expect(screen.getByText('Monopoly')).toBeInTheDocument();
        expect(screen.getAllByText('1935')).toHaveLength(1);
        expect(screen.getByText('2-6 players')).toBeInTheDocument();
      });
    });

    it('shows error when manual BGG ID not found', async () => {
      const user = userEvent.setup();

      (api.bgg.getGameDetails as ReturnType<typeof vi.fn>).mockRejectedValue(
        new Error('Game with BGG ID 99999 not found')
      );

      render(<Step3BggMatch />);

      const manualInput = screen.getByTestId('manual-bgg-id-input');
      await user.type(manualInput, '99999');

      const fetchBtn = screen.getByTestId('fetch-manual-bgg-btn');
      await user.click(fetchBtn);

      await waitFor(() => {
        expect(screen.getByText(/game with bgg id 99999 not found/i)).toBeInTheDocument();
      });
    });

    it('confirms manual selection and updates store', async () => {
      const user = userEvent.setup();
      const mockGameDetails = {
        bggId: 777,
        name: 'Risk',
        yearPublished: 1959,
        minPlayers: 2,
        maxPlayers: 6,
        playingTime: 120,
        minAge: 10,
        description: 'Strategy game',
        imageUrl: 'https://example.com/risk.jpg',
        thumbnailUrl: 'https://example.com/risk_thumb.jpg',
      };

      (api.bgg.getGameDetails as ReturnType<typeof vi.fn>).mockResolvedValue(mockGameDetails);

      render(<Step3BggMatch />);

      // Enter manual ID and fetch
      const manualInput = screen.getByTestId('manual-bgg-id-input');
      await user.type(manualInput, '777');

      const fetchBtn = screen.getByTestId('fetch-manual-bgg-btn');
      await user.click(fetchBtn);

      // Wait for preview (unique game)
      await waitFor(() => {
        expect(screen.getByText('Risk')).toBeInTheDocument();
      });

      // Confirm selection
      const confirmBtn = screen.getByTestId('confirm-manual-bgg-btn');
      await user.click(confirmBtn);

      await waitFor(() => {
        const state = useGameImportWizardStore.getState();
        expect(state.selectedBggId).toBe(777);
        expect(state.bggGameData).toMatchObject({
          id: 777,
          name: 'Risk',
        });
      });
    });

    it('shows loading state during manual fetch', async () => {
      const user = userEvent.setup();

      // Mock slow API
      (api.bgg.getGameDetails as ReturnType<typeof vi.fn>).mockImplementation(
        () => new Promise(resolve => setTimeout(resolve, 100))
      );

      render(<Step3BggMatch />);

      const manualInput = screen.getByTestId('manual-bgg-id-input');
      await user.type(manualInput, '13');

      const fetchBtn = screen.getByTestId('fetch-manual-bgg-btn');
      await user.click(fetchBtn);

      // Loading state
      expect(screen.getByText('Loading...')).toBeInTheDocument();
      expect(fetchBtn).toBeDisabled();
    });

    it('validates manual BGG ID (positive number only)', async () => {
      const user = userEvent.setup();
      render(<Step3BggMatch />);

      const fetchBtn = screen.getByTestId('fetch-manual-bgg-btn');
      const manualInput = screen.getByTestId('manual-bgg-id-input');

      // Empty input - button should be disabled
      expect(fetchBtn).toBeDisabled();

      // Enter invalid negative number
      await user.type(manualInput, '-5');

      // Button should be enabled now (since manualId is not empty)
      expect(fetchBtn).toBeEnabled();

      // Click should trigger validation error
      await user.click(fetchBtn);

      // Wait for error alert to appear
      await waitFor(() => {
        expect(screen.getByText('Please enter a valid BGG ID (positive number)')).toBeInTheDocument();
      });
    });
  });

  describe('Match Score Calculation', () => {
    it('calculates 100% match for exact title match', () => {
      render(<Step3BggMatch />);

      const exactMatch = screen.getByTestId('bgg-result-13');
      expect(within(exactMatch).getByText('100% match')).toBeInTheDocument();
    });

    it('applies correct badge variant based on match score', () => {
      render(<Step3BggMatch />);

      // Exact match (100%) should use primary variant (bg-primary)
      const exactMatch = screen.getByTestId('bgg-result-13');
      const exactBadge = within(exactMatch).getByText('100% match');
      expect(exactBadge).toBeInTheDocument();
      expect(exactBadge.className).toContain('bg-primary');

      // Low match (<70%) should use destructive variant
      const lowMatch = screen.getByTestId('bgg-result-54321');
      const lowBadge = within(lowMatch).getByText(/22% match/);
      expect(lowBadge.className).toContain('bg-destructive');
    });

    it('sorts results by match score descending', () => {
      render(<Step3BggMatch />);

      const results = screen.getByTestId('bgg-search-results');
      const resultButtons = within(results).getAllByRole('button');

      // First result should have highest match score
      const firstResultScore = within(resultButtons[0]).getByText(/\d+% match/);
      expect(firstResultScore.textContent).toContain('100%');
    });
  });

  describe('Selected Game Summary', () => {
    it('displays summary alert when game is selected', async () => {
      useGameImportWizardStore.setState({
        selectedBggId: 13,
        bggGameData: {
          id: 13,
          name: 'Catan',
          yearPublished: 1995,
        },
      });

      render(<Step3BggMatch />);

      const alert = screen.getByRole('alert');
      expect(within(alert).getByText('Game Selected')).toBeInTheDocument();
      // Text split across elements: "Selected: " + <strong>Catan</strong> + " (BGG #13)"
      expect(within(alert).getByText(/selected:/i)).toBeInTheDocument();
      expect(within(alert).getByText('Catan')).toBeInTheDocument();
      expect(within(alert).getByText(/\(BGG #13\)/i)).toBeInTheDocument();
    });

    it('does not display summary when no game selected', () => {
      render(<Step3BggMatch />);

      expect(screen.queryByText('Game Selected')).not.toBeInTheDocument();
    });
  });

  describe('External Links', () => {
    it('renders BGG external links for each result', () => {
      render(<Step3BggMatch />);

      const links = screen.getAllByTitle('View on BoardGameGeek');
      expect(links).toHaveLength(3);

      expect(links[0]).toHaveAttribute('href', 'https://boardgamegeek.com/boardgame/13');
      expect(links[0]).toHaveAttribute('target', '_blank');
      expect(links[0]).toHaveAttribute('rel', 'noopener noreferrer');
    });

    it('external link click does not trigger result selection', async () => {
      const user = userEvent.setup();
      render(<Step3BggMatch />);

      const externalLink = screen.getAllByTitle('View on BoardGameGeek')[0];

      // Click link (stopPropagation should prevent selection)
      // Note: In test env, we can't prevent default navigation, but we can verify selection didn't happen
      await user.click(externalLink);

      // Selection should not happen
      const state = useGameImportWizardStore.getState();
      expect(state.selectedBggId).toBeNull();
    });
  });
});
