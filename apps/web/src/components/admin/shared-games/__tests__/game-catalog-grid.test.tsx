/**
 * GameCatalogGrid Component Tests
 * Issue #4909 - Uniform MeepleCard UI across /dashboard, /games and /admin/shared-games
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { GameCatalogGrid } from '../game-catalog-grid';
import type { SharedGame } from '@/lib/api';

// ============================================================================
// Mock Data
// ============================================================================

const mockGames: SharedGame[] = [
  {
    id: 'game-1',
    bggId: 1,
    title: 'Catan',
    yearPublished: 1995,
    description: 'A classic board game',
    minPlayers: 3,
    maxPlayers: 4,
    playingTimeMinutes: 90,
    minAge: 10,
    complexityRating: 2.3,
    averageRating: 7.5,
    imageUrl: 'https://example.com/catan.jpg',
    thumbnailUrl: 'https://example.com/catan-thumb.jpg',
    status: 'Published',
    createdAt: '2024-01-01T00:00:00Z',
    modifiedAt: null,
  },
  {
    id: 'game-2',
    bggId: 2,
    title: 'Wingspan',
    yearPublished: 2019,
    description: 'A bird game',
    minPlayers: 1,
    maxPlayers: 5,
    playingTimeMinutes: 70,
    minAge: 10,
    complexityRating: 2.4,
    averageRating: 8.0,
    imageUrl: 'https://example.com/wingspan.jpg',
    thumbnailUrl: 'https://example.com/wingspan-thumb.jpg',
    status: 'Draft',
    createdAt: '2024-01-02T00:00:00Z',
    modifiedAt: null,
  },
  {
    id: 'game-3',
    bggId: 3,
    title: 'Azul',
    yearPublished: 2017,
    description: 'A tile game',
    minPlayers: 2,
    maxPlayers: 4,
    playingTimeMinutes: 45,
    minAge: 8,
    complexityRating: 1.8,
    averageRating: 7.8,
    imageUrl: 'https://example.com/azul.jpg',
    thumbnailUrl: 'https://example.com/azul-thumb.jpg',
    status: 'Published',
    createdAt: '2024-01-03T00:00:00Z',
    modifiedAt: null,
  },
];

const mockPagedResponse = {
  items: mockGames,
  totalCount: 3,
  pageSize: 50,
  page: 1,
  totalPages: 1,
  hasNextPage: false,
  hasPreviousPage: false,
};

// Mock window.matchMedia (MeepleCard uses it for mobile detection)
vi.stubGlobal('matchMedia', (query: string) => ({
  matches: false,
  media: query,
  onchange: null,
  addListener: vi.fn(),
  removeListener: vi.fn(),
  addEventListener: vi.fn(),
  removeEventListener: vi.fn(),
  dispatchEvent: vi.fn(),
}));

// ============================================================================
// Mock Hooks
// ============================================================================

vi.mock('@/hooks/queries', () => ({
  useSharedGames: vi.fn(() => ({
    data: mockPagedResponse,
    isLoading: false,
    error: null,
  })),
  sharedGamesKeys: {
    all: ['sharedGames'],
    lists: () => ['sharedGames', 'list'],
    list: () => ['sharedGames', 'list', {}],
  },
}));

vi.mock('@tanstack/react-query', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@tanstack/react-query')>();
  return {
    ...actual,
    useMutation: vi.fn(() => ({
      mutate: vi.fn(),
      isPending: false,
    })),
  };
});

// Mock next/navigation
vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: vi.fn(),
  }),
}));

// ============================================================================
// Test Utilities
// ============================================================================

function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false, gcTime: 0 },
    },
  });
}

function renderWithProviders(ui: React.ReactElement) {
  const client = createTestQueryClient();
  return render(
    <QueryClientProvider client={client}>{ui}</QueryClientProvider>
  );
}

// ============================================================================
// Tests
// ============================================================================

describe('GameCatalogGrid', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders stats summary with correct counts', () => {
    renderWithProviders(<GameCatalogGrid />);

    expect(screen.getByText('Totale')).toBeInTheDocument();
    expect(screen.getByText('Pubblicati')).toBeInTheDocument();
    expect(screen.getByText('Bozze')).toBeInTheDocument();

    // 3 total, 2 published, 1 draft
    expect(screen.getByText('3')).toBeInTheDocument();
    expect(screen.getByText('2')).toBeInTheDocument();
    expect(screen.getByText('1')).toBeInTheDocument();
  });

  it('displays game titles in the grid', () => {
    renderWithProviders(<GameCatalogGrid />);

    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
    expect(screen.getByText('Azul')).toBeInTheDocument();
  });

  it('shows status badges on cards', () => {
    renderWithProviders(<GameCatalogGrid />);

    const publishedBadges = screen.getAllByText('Pubblicato');
    expect(publishedBadges.length).toBeGreaterThanOrEqual(1);

    expect(screen.getByText('Bozza')).toBeInTheDocument();
  });

  it('shows loading skeletons when isLoading', async () => {
    const { useSharedGames } = await import('@/hooks/queries');
    vi.mocked(useSharedGames).mockReturnValueOnce({
      data: undefined,
      isLoading: true,
      error: null,
    } as ReturnType<typeof useSharedGames>);

    renderWithProviders(<GameCatalogGrid />);

    // MeepleCard renders a skeleton with data-testid="meeple-card-skeleton" when loading
    const skeletons = screen.getAllByTestId('meeple-card-skeleton');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('shows empty state when no games', async () => {
    const { useSharedGames } = await import('@/hooks/queries');
    vi.mocked(useSharedGames).mockReturnValueOnce({
      data: { ...mockPagedResponse, items: [], totalCount: 0 },
      isLoading: false,
      error: null,
    } as ReturnType<typeof useSharedGames>);

    renderWithProviders(<GameCatalogGrid />);

    expect(screen.getByText('Nessun gioco nel catalogo')).toBeInTheDocument();
  });

  it('renders admin-specific testids on game cards', () => {
    renderWithProviders(<GameCatalogGrid />);

    expect(screen.getByTestId('admin-game-card-game-1')).toBeInTheDocument();
    expect(screen.getByTestId('admin-game-card-game-2')).toBeInTheDocument();
    expect(screen.getByTestId('admin-game-card-game-3')).toBeInTheDocument();
  });
});
