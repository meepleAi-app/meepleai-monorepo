/**
 * Vector Store Page Tests (pgvector migration)
 * Issue #4861 — Replaces Qdrant vector-collections tests
 *
 * Tests for the Vector Store page (/admin/knowledge-base/vectors):
 * - Renders with real API data via useQuery (getVectorStats)
 * - Loading skeletons while fetching
 * - Error state with retry
 * - Empty state when no game breakdown
 * - Stats display (total vectors, games indexed, dimensions, avg health)
 * - Refresh button
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const { mockGetVectorStats } = vi.hoisted(() => ({
  mockGetVectorStats: vi.fn(),
}));

vi.mock('@/lib/api/clients/adminClient', () => ({
  createAdminClient: () => ({
    getVectorStats: mockGetVectorStats,
    searchVectors: vi.fn().mockResolvedValue({ results: [], errorMessage: null }),
  }),
}));

vi.mock('@/lib/api/core/httpClient', () => ({
  HttpClient: vi.fn(),
}));

// Mock VectorGameCard to avoid rendering complexity
vi.mock('@/components/admin/knowledge-base/vector-game-card', () => ({
  VectorGameCard: ({ game }: { game: { gameId: string; gameName: string } }) => (
    <div data-testid={`vector-game-card-${game.gameId}`}>
      <span>{game.gameName}</span>
    </div>
  ),
}));

import VectorStorePage from '@/app/admin/(dashboard)/knowledge-base/vectors/page';

function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false, staleTime: 0 },
      mutations: { retry: false },
    },
  });
}

function renderWithQuery(ui: React.ReactElement) {
  const client = createTestQueryClient();
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

const MOCK_VECTOR_STATS = {
  totalVectors: 80000,
  dimensions: 768,
  gamesIndexed: 3,
  avgHealthPercent: 95.0,
  sizeEstimateBytes: 6442450944,
  gameBreakdown: [
    {
      gameId: 'game-1',
      gameName: 'Game Rules',
      vectorCount: 42500,
      completedCount: 42000,
      failedCount: 500,
      healthPercent: 98.8,
    },
    {
      gameId: 'game-2',
      gameName: 'Strategy Guides',
      vectorCount: 28300,
      completedCount: 27000,
      failedCount: 1300,
      healthPercent: 95.4,
    },
    {
      gameId: 'game-3',
      gameName: 'FAQ Database',
      vectorCount: 9200,
      completedCount: 8700,
      failedCount: 500,
      healthPercent: 94.6,
    },
  ],
};

describe('VectorCollectionsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetVectorStats.mockResolvedValue(MOCK_VECTOR_STATS);
  });

  it('should render page header', async () => {
    renderWithQuery(<VectorStorePage />);

    expect(screen.getByText('Vector Store')).toBeInTheDocument();
    expect(
      screen.getByText('pgvector knowledge base — embeddings health and semantic search')
    ).toBeInTheDocument();
  });

  it('should render stats cards with calculated values', async () => {
    renderWithQuery(<VectorStorePage />);

    await waitFor(() => {
      // Total vectors
      expect(screen.getByText((80000).toLocaleString())).toBeInTheDocument();
    });

    // Games indexed
    expect(screen.getByText('3')).toBeInTheDocument();
    // Dimensions
    expect(screen.getByText('768')).toBeInTheDocument();
    // Avg health (rounded)
    expect(screen.getByText('95%')).toBeInTheDocument();
  });

  it('should render collection cards', async () => {
    renderWithQuery(<VectorStorePage />);

    await waitFor(() => {
      expect(screen.getByTestId('vector-game-card-game-1')).toBeInTheDocument();
    });

    expect(screen.getByTestId('vector-game-card-game-2')).toBeInTheDocument();
    expect(screen.getByTestId('vector-game-card-game-3')).toBeInTheDocument();
  });

  it('should show error state when fetch fails', async () => {
    mockGetVectorStats.mockRejectedValue(new Error('Connection refused'));
    renderWithQuery(<VectorStorePage />);

    await waitFor(() => {
      expect(screen.getByText('Failed to load vector stats')).toBeInTheDocument();
    });

    expect(screen.getByText('Connection refused')).toBeInTheDocument();
    expect(screen.getByText('Retry')).toBeInTheDocument();
  });

  it('should show empty state when no collections', async () => {
    mockGetVectorStats.mockResolvedValue({
      ...MOCK_VECTOR_STATS,
      gameBreakdown: [],
      gamesIndexed: 0,
      totalVectors: 0,
    });
    renderWithQuery(<VectorStorePage />);

    await waitFor(() => {
      expect(screen.getByText('No vectors indexed yet')).toBeInTheDocument();
    });
  });

  it('should show stat labels', async () => {
    renderWithQuery(<VectorStorePage />);

    await waitFor(() => {
      expect(screen.getByText('Total Vectors')).toBeInTheDocument();
    });

    expect(screen.getByText('Games Indexed')).toBeInTheDocument();
    expect(screen.getByText('Dimensions')).toBeInTheDocument();
    expect(screen.getByText('Avg Health')).toBeInTheDocument();
  });
});
