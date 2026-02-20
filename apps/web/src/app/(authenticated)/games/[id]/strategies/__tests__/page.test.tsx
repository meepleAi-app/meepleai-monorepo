/**
 * Game Strategies Page Tests (Issue #4889)
 *
 * Tests for the strategies page with real API integration.
 * Issue #4903: Backend API for game strategies.
 */

import { screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';
import { api } from '@/lib/api';

import GameStrategiesPage from '../page';

const mockUseParams = vi.hoisted(() => vi.fn(() => ({ id: 'game-123' })));

vi.mock('next/navigation', () => ({
  useParams: mockUseParams,
  useRouter: vi.fn(() => ({ push: vi.fn() })),
}));

vi.mock('@/lib/api', () => ({
  api: {
    games: {
      getStrategies: vi.fn(),
    },
  },
}));

const mockGetStrategies = vi.mocked(api.games.getStrategies);

const mockStrategy = {
  id: 'strat-1',
  gameId: 'game-123',
  title: 'Opening Move Strategy',
  content: 'Start by placing your pieces in the center.',
  author: 'Alice',
  upvotes: 5,
  tags: ['beginner', 'opening'],
  createdAt: '2025-06-15T10:00:00.000Z',
};

describe('GameStrategiesPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows page heading', () => {
    mockGetStrategies.mockResolvedValue({
      items: [],
      total: 0,
      page: 1,
      pageSize: 10,
    });
    renderWithQuery(<GameStrategiesPage />);
    expect(screen.getByText('Strategies')).toBeInTheDocument();
  });

  it('shows back navigation link', () => {
    mockGetStrategies.mockResolvedValue({
      items: [],
      total: 0,
      page: 1,
      pageSize: 10,
    });
    renderWithQuery(<GameStrategiesPage />);
    expect(screen.getByText('Back to Game')).toBeInTheDocument();
  });

  it('shows loading skeletons initially', () => {
    mockGetStrategies.mockReturnValue(new Promise(() => {}));
    renderWithQuery(<GameStrategiesPage />);
    // Skeletons are rendered while loading
    const skeletons = document.querySelectorAll('[class*="animate-pulse"]');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('shows empty state when no strategies', async () => {
    mockGetStrategies.mockResolvedValue({
      items: [],
      total: 0,
      page: 1,
      pageSize: 10,
    });
    renderWithQuery(<GameStrategiesPage />);
    await waitFor(() => {
      expect(screen.getByText('No strategies available for this game yet.')).toBeInTheDocument();
    });
  });

  it('shows strategies when API returns data', async () => {
    mockGetStrategies.mockResolvedValue({
      items: [mockStrategy],
      total: 1,
      page: 1,
      pageSize: 10,
    });
    renderWithQuery(<GameStrategiesPage />);
    await waitFor(() => {
      expect(screen.getByText('Opening Move Strategy')).toBeInTheDocument();
      expect(screen.getByText('Start by placing your pieces in the center.')).toBeInTheDocument();
      expect(screen.getByText('Alice')).toBeInTheDocument();
    });
  });

  it('shows strategy tags', async () => {
    mockGetStrategies.mockResolvedValue({
      items: [mockStrategy],
      total: 1,
      page: 1,
      pageSize: 10,
    });
    renderWithQuery(<GameStrategiesPage />);
    await waitFor(() => {
      expect(screen.getByText('beginner')).toBeInTheDocument();
      expect(screen.getByText('opening')).toBeInTheDocument();
    });
  });

  it('shows strategy count', async () => {
    mockGetStrategies.mockResolvedValue({
      items: [mockStrategy],
      total: 1,
      page: 1,
      pageSize: 10,
    });
    renderWithQuery(<GameStrategiesPage />);
    await waitFor(() => {
      expect(screen.getByText('1 Strategy')).toBeInTheDocument();
    });
  });

  it('shows error when API fails', async () => {
    mockGetStrategies.mockRejectedValue(new Error('Network error'));
    renderWithQuery(<GameStrategiesPage />);
    await waitFor(() => {
      expect(screen.getByText(/Failed to load strategies/i)).toBeInTheDocument();
    });
  });

  it('calls API with correct gameId', async () => {
    mockGetStrategies.mockResolvedValue({
      items: [],
      total: 0,
      page: 1,
      pageSize: 10,
    });
    renderWithQuery(<GameStrategiesPage />);
    await waitFor(() => {
      expect(mockGetStrategies).toHaveBeenCalledWith('game-123', 1, 10);
    });
  });
});
