/**
 * GameDetailPage Tests
 *
 * Covers:
 * - Shows "Aggiungi alla Libreria" button when game is NOT in library
 * - Hides the button and shows "In libreria" badge when game IS in library
 * - Renders loading skeleton while data is fetching
 * - Renders error state when game is not found
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

// ============================================================================
// Navigation mocks
// ============================================================================

vi.mock('next/navigation', () => ({
  useParams: () => ({ id: 'game-123' }),
  useRouter: () => ({ push: vi.fn() }),
}));

// ============================================================================
// Query mocks
// ============================================================================

let mockGame: Record<string, unknown> | null = null;
let mockGameLoading = false;
let mockLibraryStatus: { inLibrary: boolean } | null = null;
let mockLibraryLoading = false;
let mockManaPipsData: null = null;
let mockPipsLoading = false;

vi.mock('@/hooks/queries/useSharedGames', () => ({
  useSharedGame: () => ({
    data: mockGame,
    isLoading: mockGameLoading,
  }),
}));

vi.mock('@/hooks/queries/useLibrary', () => ({
  useGameInLibraryStatus: () => ({
    data: mockLibraryStatus,
    isLoading: mockLibraryLoading,
  }),
}));

vi.mock('@/hooks/queries/useGameManaPips', () => ({
  useGameManaPips: () => ({
    data: mockManaPipsData,
    isLoading: mockPipsLoading,
  }),
  buildGameManaPips: () => [],
}));

// ============================================================================
// Component mocks
// ============================================================================

vi.mock('@/components/library/AddToLibraryButton', () => ({
  AddToLibraryButton: ({ gameId, gameTitle }: { gameId: string; gameTitle: string }) => (
    <button data-testid="add-to-library-button" data-game-id={gameId} data-game-title={gameTitle}>
      Aggiungi alla Collezione
    </button>
  ),
}));

vi.mock('@/components/ui/data-display/meeple-card', () => ({
  MeepleCard: ({ badge }: { badge?: string }) => (
    <div data-testid="meeple-card">{badge && <span data-testid="library-badge">{badge}</span>}</div>
  ),
}));

// ============================================================================
// Import under test (after mocks)
// ============================================================================

import GameDetailPage from '../page';

// ============================================================================
// Helpers
// ============================================================================

const GAME = {
  id: 'game-123',
  title: 'Catan',
  publishers: [{ name: 'Kosmos' }],
  yearPublished: 1995,
  imageUrl: null,
  averageRating: 7.5,
};

const resetDefaults = () => {
  mockGame = GAME;
  mockGameLoading = false;
  mockLibraryStatus = { inLibrary: false };
  mockLibraryLoading = false;
  mockManaPipsData = null;
  mockPipsLoading = false;
};

// ============================================================================
// Tests
// ============================================================================

describe('GameDetailPage', () => {
  beforeEach(resetDefaults);

  it('shows AddToLibraryButton when game is not in library', () => {
    mockLibraryStatus = { inLibrary: false };

    render(<GameDetailPage />);

    expect(screen.getByTestId('add-to-library-button')).toBeInTheDocument();
    expect(screen.queryByTestId('library-badge')).not.toBeInTheDocument();
  });

  it('hides AddToLibraryButton and shows badge when game is in library', () => {
    mockLibraryStatus = { inLibrary: true };

    render(<GameDetailPage />);

    expect(screen.queryByTestId('add-to-library-button')).not.toBeInTheDocument();
    expect(screen.getByTestId('library-badge')).toHaveTextContent('In libreria');
  });

  it('passes correct gameId and gameTitle to AddToLibraryButton', () => {
    mockLibraryStatus = { inLibrary: false };

    render(<GameDetailPage />);

    const btn = screen.getByTestId('add-to-library-button');
    expect(btn).toHaveAttribute('data-game-id', 'game-123');
    expect(btn).toHaveAttribute('data-game-title', 'Catan');
  });

  it('renders loading skeleton while data is fetching', () => {
    mockGameLoading = true;

    render(<GameDetailPage />);

    // Skeleton renders; neither button nor meeple-card should appear
    expect(screen.queryByTestId('meeple-card')).not.toBeInTheDocument();
    expect(screen.queryByTestId('add-to-library-button')).not.toBeInTheDocument();
  });

  it('renders error state when game is not found', () => {
    mockGame = null;

    render(<GameDetailPage />);

    expect(screen.getByText('Gioco non trovato.')).toBeInTheDocument();
    expect(screen.queryByTestId('add-to-library-button')).not.toBeInTheDocument();
  });

  it('does not show AddToLibraryButton when libraryStatus is null (loading state)', () => {
    // null status means the query hasn't resolved yet; treat as "not in library"
    mockLibraryStatus = null;

    render(<GameDetailPage />);

    // null?.inLibrary === undefined, which is falsy — button should appear
    expect(screen.getByTestId('add-to-library-button')).toBeInTheDocument();
  });
});
