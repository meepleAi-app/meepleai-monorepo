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
// Mocks
// ============================================================================

const mockGetAll = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    sharedGames: {
      getAll: (...args: unknown[]) => mockGetAll(...args),
      publish: vi.fn(),
      archive: vi.fn(),
      delete: vi.fn(),
    },
  },
}));

vi.mock('@/hooks/queries', () => ({
  sharedGamesKeys: {
    all: ['sharedGames'],
    lists: () => ['sharedGames', 'list'],
    list: () => ['sharedGames', 'list', {}],
  },
}));

// Mock next/navigation
vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: vi.fn(),
  }),
}));

// Mock sonner toast
vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
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
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

// ============================================================================
// Tests
// ============================================================================

describe('GameCatalogGrid', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetAll.mockResolvedValue(mockPagedResponse);
  });

  it('renders stats summary with correct counts', async () => {
    renderWithProviders(<GameCatalogGrid />);

    // Wait for data to load (game title appears)
    expect(await screen.findByText('Catan')).toBeInTheDocument();

    expect(screen.getByText('Totale')).toBeInTheDocument();
    expect(screen.getByText('Pubblicati')).toBeInTheDocument();
    expect(screen.getByText('Bozze')).toBeInTheDocument();

    // 3 total, 2 published, 1 draft
    expect(screen.getByText('3')).toBeInTheDocument();
    expect(screen.getByText('2')).toBeInTheDocument();
    expect(screen.getByText('1')).toBeInTheDocument();
  });

  it('displays game titles in the grid', async () => {
    renderWithProviders(<GameCatalogGrid />);

    expect(await screen.findByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
    expect(screen.getByText('Azul')).toBeInTheDocument();
  });

  it('shows status badges on cards', async () => {
    renderWithProviders(<GameCatalogGrid />);

    // Wait for data
    await screen.findByText('Catan');

    const publishedBadges = screen.getAllByText('Pubblicato');
    expect(publishedBadges.length).toBeGreaterThanOrEqual(1);

    expect(screen.getByText('Bozza')).toBeInTheDocument();
  });

  it('shows loading state when query is loading', () => {
    // Return a promise that never resolves to keep loading state
    mockGetAll.mockReturnValue(new Promise(() => {}));

    renderWithProviders(<GameCatalogGrid />);

    // Stats show dash when loading
    const dashes = screen.getAllByText('—');
    expect(dashes.length).toBeGreaterThan(0);
  });

  it('shows empty state when no games', async () => {
    mockGetAll.mockResolvedValue({ ...mockPagedResponse, items: [], totalCount: 0 });

    renderWithProviders(<GameCatalogGrid />);

    expect(await screen.findByText('Nessun gioco nel catalogo')).toBeInTheDocument();
  });

  it('renders admin-specific testids on game cards', async () => {
    renderWithProviders(<GameCatalogGrid />);

    expect(await screen.findByTestId('admin-game-card-game-1')).toBeInTheDocument();
    expect(screen.getByTestId('admin-game-card-game-2')).toBeInTheDocument();
    expect(screen.getByTestId('admin-game-card-game-3')).toBeInTheDocument();
  });
});
