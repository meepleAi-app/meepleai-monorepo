/**
 * PublicLibraryPage Component Tests
 *
 * Tests for the catalog browse page with trending, mechanic filters,
 * paginated grid, and add-to-library actions.
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { PublicLibraryPage } from '@/components/library/PublicLibraryPage';
import { useAddGameToLibrary, useLibrary } from '@/hooks/queries/useLibrary';
import { useCatalogTrending } from '@/hooks/queries/useCatalogTrending';
import { useSharedGames, useGameMechanics } from '@/hooks/queries/useSharedGames';
import type { TrendingGame } from '@/lib/api/catalog';
import type {
  SharedGame,
  PagedSharedGames,
  GameMechanic,
} from '@/lib/api/schemas/shared-games.schemas';
import type { PaginatedLibraryResponse } from '@/lib/api/schemas/library.schemas';

// ── Mocks ────────────────────────────────────────────────────────────────────

vi.mock('@/hooks/queries/useCatalogTrending', () => ({
  useCatalogTrending: vi.fn(),
}));

vi.mock('@/hooks/queries/useSharedGames', () => ({
  useSharedGames: vi.fn(),
  useGameMechanics: vi.fn(),
}));

vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibrary: vi.fn(),
  useAddGameToLibrary: vi.fn(),
}));

vi.mock('@/lib/animations', () => ({
  useReducedMotion: () => false,
}));

// ── Typed mocks ───────────────────────────────────────────────────────────────

const mockUseCatalogTrending = vi.mocked(useCatalogTrending);
const mockUseSharedGames = vi.mocked(useSharedGames);
const mockUseGameMechanics = vi.mocked(useGameMechanics);
const mockUseLibrary = vi.mocked(useLibrary);
const mockUseAddGameToLibrary = vi.mocked(useAddGameToLibrary);

// ── Fixtures ──────────────────────────────────────────────────────────────────

function makeTrendingGame(overrides: Partial<TrendingGame> = {}): TrendingGame {
  return {
    rank: 1,
    gameId: 'trend-1',
    title: 'Trending Game',
    thumbnailUrl: null,
    score: 100,
    searchCount: 10,
    viewCount: 50,
    libraryAddCount: 5,
    playCount: 3,
    ...overrides,
  };
}

function makeSharedGame(overrides: Partial<SharedGame> = {}): SharedGame {
  return {
    id: 'game-1',
    bggId: null,
    title: 'Catan',
    yearPublished: 1995,
    description: 'Classic game',
    minPlayers: 3,
    maxPlayers: 4,
    playingTimeMinutes: 60,
    minAge: 10,
    complexityRating: 2.3,
    averageRating: 7.2,
    imageUrl: '',
    thumbnailUrl: '',
    status: 'Published',
    isRagPublic: false,
    createdAt: '2024-01-01T00:00:00Z',
    modifiedAt: null,
    ...overrides,
  };
}

function makePagedSharedGames(items: SharedGame[], total?: number): PagedSharedGames {
  return {
    items,
    total: total ?? items.length,
    page: 1,
    pageSize: 18,
  };
}

function makePaginatedLibrary(gameIds: string[] = []): PaginatedLibraryResponse {
  return {
    items: gameIds.map((gameId, i) => ({
      id: `entry-${i}`,
      userId: 'user-1',
      gameId,
      gameTitle: `Game ${i}`,
      gamePublisher: null,
      gameYearPublished: null,
      gameIconUrl: null,
      gameImageUrl: null,
      addedAt: '2026-01-01T00:00:00Z',
      notes: null,
      isFavorite: false,
      currentState: 'Owned',
      stateChangedAt: null,
      stateNotes: null,
      hasKb: false,
      kbCardCount: 0,
      kbIndexedCount: 0,
      kbProcessingCount: 0,
      ownershipDeclaredAt: null,
      hasRagAccess: false,
      agentIsOwned: false,
      minPlayers: null,
      maxPlayers: null,
      playingTimeMinutes: null,
      complexityRating: null,
      averageRating: null,
      isPrivateGame: false,
      privateGameId: null,
      canProposeToCatalog: false,
    })),
    page: 1,
    pageSize: 500,
    totalCount: gameIds.length,
    totalPages: 1,
    hasNextPage: false,
    hasPreviousPage: false,
  };
}

function makeMechanic(slug: string, name: string): GameMechanic {
  return { id: `mech-${slug}`, name, slug };
}

const defaultMutate = vi.fn();

// ── Setup ─────────────────────────────────────────────────────────────────────

function setupDefaultMocks() {
  mockUseCatalogTrending.mockReturnValue({
    data: undefined,
    isLoading: false,
  } as ReturnType<typeof useCatalogTrending>);

  mockUseSharedGames.mockReturnValue({
    data: makePagedSharedGames([]),
    isLoading: false,
  } as ReturnType<typeof useSharedGames>);

  mockUseGameMechanics.mockReturnValue({
    data: [],
    isLoading: false,
  } as ReturnType<typeof useGameMechanics>);

  mockUseLibrary.mockReturnValue({
    data: makePaginatedLibrary(),
    isLoading: false,
  } as ReturnType<typeof useLibrary>);

  mockUseAddGameToLibrary.mockReturnValue({
    mutate: defaultMutate,
    isPending: false,
  } as ReturnType<typeof useAddGameToLibrary>);
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('PublicLibraryPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    setupDefaultMocks();
  });

  // --------------------------------------------------------------------------
  // Smoke render
  // --------------------------------------------------------------------------

  it('renders the page root element', () => {
    render(<PublicLibraryPage />);
    expect(screen.getByTestId('public-library-page')).toBeInTheDocument();
  });

  it('renders the search input', () => {
    render(<PublicLibraryPage />);
    expect(screen.getByTestId('catalog-search-input')).toBeInTheDocument();
  });

  it('renders trending section heading', () => {
    render(<PublicLibraryPage />);
    expect(screen.getByText(/trending questa settimana/i)).toBeInTheDocument();
  });

  it('renders all games section heading', () => {
    render(<PublicLibraryPage />);
    expect(screen.getByText(/tutti i giochi/i)).toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // Trending section
  // --------------------------------------------------------------------------

  it('shows loading indicator while trending is loading', () => {
    mockUseCatalogTrending.mockReturnValue({
      data: undefined,
      isLoading: true,
    } as ReturnType<typeof useCatalogTrending>);

    render(<PublicLibraryPage />);
    expect(screen.getByTestId('trending-loading')).toBeInTheDocument();
  });

  it('renders trending ShelfCards when data is available', () => {
    mockUseCatalogTrending.mockReturnValue({
      data: [
        makeTrendingGame({ gameId: 't1', title: 'Ticket to Ride', rank: 1 }),
        makeTrendingGame({ gameId: 't2', title: 'Pandemic', rank: 2 }),
      ],
      isLoading: false,
    } as ReturnType<typeof useCatalogTrending>);

    render(<PublicLibraryPage />);

    expect(screen.getByText('Ticket to Ride')).toBeInTheDocument();
    expect(screen.getByText('Pandemic')).toBeInTheDocument();
  });

  it('shows empty message when no trending games', () => {
    mockUseCatalogTrending.mockReturnValue({
      data: [],
      isLoading: false,
    } as ReturnType<typeof useCatalogTrending>);

    render(<PublicLibraryPage />);
    expect(screen.getByTestId('trending-empty')).toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // Catalog grid
  // --------------------------------------------------------------------------

  it('shows catalog loading indicator', () => {
    mockUseSharedGames.mockReturnValue({
      data: undefined,
      isLoading: true,
    } as ReturnType<typeof useSharedGames>);

    render(<PublicLibraryPage />);
    expect(screen.getByTestId('catalog-loading')).toBeInTheDocument();
  });

  it('renders ShelfCards for catalog games', () => {
    mockUseSharedGames.mockReturnValue({
      data: makePagedSharedGames([
        makeSharedGame({ id: 'g1', title: 'Catan' }),
        makeSharedGame({ id: 'g2', title: '7 Wonders' }),
      ]),
      isLoading: false,
    } as ReturnType<typeof useSharedGames>);

    render(<PublicLibraryPage />);

    const grid = screen.getByTestId('catalog-grid');
    expect(grid).toBeInTheDocument();
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('7 Wonders')).toBeInTheDocument();
  });

  it('shows empty state when catalog has no results', () => {
    mockUseSharedGames.mockReturnValue({
      data: makePagedSharedGames([]),
      isLoading: false,
    } as ReturnType<typeof useSharedGames>);

    render(<PublicLibraryPage />);
    expect(screen.getAllByText(/nessun gioco trovato/i).length).toBeGreaterThan(0);
  });

  // --------------------------------------------------------------------------
  // Library status badge
  // --------------------------------------------------------------------------

  it('marks games already in library as inLibrary', () => {
    const gameId = 'game-owned-1';

    mockUseLibrary.mockReturnValue({
      data: makePaginatedLibrary([gameId]),
      isLoading: false,
    } as ReturnType<typeof useLibrary>);

    mockUseSharedGames.mockReturnValue({
      data: makePagedSharedGames([makeSharedGame({ id: gameId, title: 'Owned Game' })]),
      isLoading: false,
    } as ReturnType<typeof useSharedGames>);

    render(<PublicLibraryPage />);
    expect(screen.getByTestId('in-library-badge')).toBeInTheDocument();
  });

  it('shows add button for games not in library', () => {
    mockUseSharedGames.mockReturnValue({
      data: makePagedSharedGames([makeSharedGame({ id: 'not-owned', title: 'New Game' })]),
      isLoading: false,
    } as ReturnType<typeof useSharedGames>);

    render(<PublicLibraryPage />);
    expect(screen.getByTestId('add-button')).toBeInTheDocument();
  });

  it('calls addToLibrary when add button is clicked', () => {
    mockUseSharedGames.mockReturnValue({
      data: makePagedSharedGames([makeSharedGame({ id: 'catalog-1', title: 'New Game' })]),
      isLoading: false,
    } as ReturnType<typeof useSharedGames>);

    render(<PublicLibraryPage />);

    const addBtn = screen.getByTestId('add-button');
    fireEvent.click(addBtn);

    expect(defaultMutate).toHaveBeenCalledWith({ gameId: 'catalog-1' });
  });

  // --------------------------------------------------------------------------
  // MechanicFilter
  // --------------------------------------------------------------------------

  it('renders mechanic filter chips when mechanics are available', () => {
    mockUseGameMechanics.mockReturnValue({
      data: [
        makeMechanic('deck-building', 'Deck Building'),
        makeMechanic('cooperative', 'Cooperativo'),
      ],
      isLoading: false,
    } as ReturnType<typeof useGameMechanics>);

    render(<PublicLibraryPage />);

    expect(screen.getByTestId('mechanic-filter-row')).toBeInTheDocument();
    expect(screen.getByText('Deck Building')).toBeInTheDocument();
    expect(screen.getByText('Cooperativo')).toBeInTheDocument();
  });

  it('does not render mechanic filter when no mechanics returned', () => {
    mockUseGameMechanics.mockReturnValue({
      data: [],
      isLoading: false,
    } as ReturnType<typeof useGameMechanics>);

    render(<PublicLibraryPage />);

    expect(screen.queryByTestId('mechanic-filter-row')).not.toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // Load More
  // --------------------------------------------------------------------------

  it('renders Load More button when there are more pages', () => {
    mockUseSharedGames.mockReturnValue({
      data: makePagedSharedGames(
        Array.from({ length: 18 }, (_, i) => makeSharedGame({ id: `g${i}`, title: `Game ${i}` })),
        100 // total > 18 * 1 page
      ),
      isLoading: false,
    } as ReturnType<typeof useSharedGames>);

    render(<PublicLibraryPage />);
    expect(screen.getByTestId('load-more-button')).toBeInTheDocument();
  });

  it('does not render Load More when all games are loaded', () => {
    mockUseSharedGames.mockReturnValue({
      data: makePagedSharedGames([makeSharedGame({ id: 'g1', title: 'Only Game' })], 1),
      isLoading: false,
    } as ReturnType<typeof useSharedGames>);

    render(<PublicLibraryPage />);
    expect(screen.queryByTestId('load-more-button')).not.toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // Search interaction
  // --------------------------------------------------------------------------

  it('updates search input value on change', () => {
    render(<PublicLibraryPage />);

    const input = screen.getByTestId('catalog-search-input') as HTMLInputElement;
    fireEvent.change(input, { target: { value: 'catan' } });

    expect(input.value).toBe('catan');
  });
});
