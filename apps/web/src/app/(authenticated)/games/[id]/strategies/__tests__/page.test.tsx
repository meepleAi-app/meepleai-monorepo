/**
 * Game Strategies Page Tests (Issue #4889 / #4903)
 *
 * Tests for the strategies page with real API implementation.
 */

import { screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import GameStrategiesPage from '../page';

const mockUseParams = vi.hoisted(() => vi.fn(() => ({ id: 'game-123' })));

vi.mock('next/navigation', () => ({
  useParams: mockUseParams,
  useRouter: vi.fn(() => ({ push: vi.fn() })),
}));

const mockGetStrategies = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    games: {
      getStrategies: (...args: unknown[]) => mockGetStrategies(...args),
    },
  },
}));

describe('GameStrategiesPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetStrategies.mockResolvedValue({ items: [], total: 0 });
  });

  it('shows page heading', () => {
    renderWithQuery(<GameStrategiesPage />);
    expect(screen.getByText('Strategies')).toBeInTheDocument();
  });

  it('shows back navigation link', () => {
    renderWithQuery(<GameStrategiesPage />);
    const backLink = screen.getByRole('link', { name: /Back to Game/i });
    expect(backLink).toBeInTheDocument();
    expect(backLink).toHaveAttribute('href', '/games/game-123');
  });

  it('shows loading skeletons initially', () => {
    mockGetStrategies.mockReturnValue(new Promise(() => {}));
    const { container } = renderWithQuery(<GameStrategiesPage />);
    const skeletons = container.querySelectorAll('.animate-pulse');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('shows empty state when no strategies available', async () => {
    mockGetStrategies.mockResolvedValue({ items: [], total: 0 });
    renderWithQuery(<GameStrategiesPage />);
    await waitFor(() => {
      expect(
        screen.getByText('No strategies available for this game yet.')
      ).toBeInTheDocument();
    });
  });

  it('shows strategy cards when data is loaded', async () => {
    mockGetStrategies.mockResolvedValue({
      items: [
        {
          id: 'strategy-1',
          gameId: 'game-123',
          title: 'Opening Strategy',
          content: 'Start by placing your first settlement near resources.',
          author: 'ProPlayer',
          upvotes: 42,
          tags: ['beginner', 'opening'],
          createdAt: '2024-01-15T10:00:00Z',
        },
      ],
      total: 1,
    });
    renderWithQuery(<GameStrategiesPage />);
    await waitFor(() => {
      expect(screen.getByText('Opening Strategy')).toBeInTheDocument();
    });
    expect(screen.getByText('Start by placing your first settlement near resources.')).toBeInTheDocument();
    expect(screen.getByText('ProPlayer')).toBeInTheDocument();
  });

  it('shows error state when API fails', async () => {
    mockGetStrategies.mockRejectedValue(new Error('Network error'));
    renderWithQuery(<GameStrategiesPage />);
    await waitFor(() => {
      expect(screen.getByText(/Failed to load strategies/i)).toBeInTheDocument();
    });
  });

  it('calls getStrategies with correct gameId', async () => {
    renderWithQuery(<GameStrategiesPage />);
    await waitFor(() => {
      expect(mockGetStrategies).toHaveBeenCalledWith('game-123', 1, 10);
    });
  });
});
