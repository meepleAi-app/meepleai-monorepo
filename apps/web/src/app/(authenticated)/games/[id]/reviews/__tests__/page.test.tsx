/**
 * Game Reviews Page Tests (Issue #4889 / #4904)
 *
 * Tests for the reviews page with real API implementation.
 */

import { screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import GameReviewsPage from '../page';

const mockUseParams = vi.hoisted(() => vi.fn(() => ({ id: 'game-123' })));

vi.mock('next/navigation', () => ({
  useParams: mockUseParams,
  useRouter: vi.fn(() => ({ push: vi.fn() })),
}));

const mockGetReviews = vi.fn();
const mockCreateReview = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    games: {
      getReviews: (...args: unknown[]) => mockGetReviews(...args),
      createReview: (...args: unknown[]) => mockCreateReview(...args),
    },
  },
}));

describe('GameReviewsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetReviews.mockResolvedValue({ items: [], total: 0 });
  });

  it('shows page heading', async () => {
    renderWithQuery(<GameReviewsPage />);
    expect(screen.getByText('Reviews')).toBeInTheDocument();
  });

  it('shows back navigation link', async () => {
    renderWithQuery(<GameReviewsPage />);
    const backLink = screen.getByRole('link', { name: /Back to Game/i });
    expect(backLink).toBeInTheDocument();
    expect(backLink).toHaveAttribute('href', '/games/game-123');
  });

  it('shows loading skeletons initially', async () => {
    mockGetReviews.mockReturnValue(new Promise(() => {}));
    const { container } = renderWithQuery(<GameReviewsPage />);
    const skeletons = container.querySelectorAll('.animate-pulse');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('shows empty state when no reviews available', async () => {
    mockGetReviews.mockResolvedValue({ items: [], total: 0 });
    renderWithQuery(<GameReviewsPage />);
    await waitFor(() => {
      expect(
        screen.getByText('No reviews yet. Be the first to review this game!')
      ).toBeInTheDocument();
    });
  });

  it('shows review cards when data is loaded', async () => {
    mockGetReviews.mockResolvedValue({
      items: [
        {
          id: 'review-1',
          gameId: 'game-123',
          authorName: 'BoardGameFan',
          rating: 8,
          content: 'Great game, highly recommended!',
          createdAt: '2024-01-15T10:00:00Z',
        },
      ],
      total: 1,
    });
    renderWithQuery(<GameReviewsPage />);
    await waitFor(() => {
      expect(screen.getByText('BoardGameFan')).toBeInTheDocument();
    });
    expect(screen.getByText('Great game, highly recommended!')).toBeInTheDocument();
  });

  it('shows review count when reviews are loaded', async () => {
    mockGetReviews.mockResolvedValue({
      items: [
        {
          id: 'review-1',
          gameId: 'game-123',
          authorName: 'Player1',
          rating: 7,
          content: 'Fun game!',
          createdAt: '2024-01-15T10:00:00Z',
        },
      ],
      total: 1,
    });
    renderWithQuery(<GameReviewsPage />);
    await waitFor(() => {
      expect(screen.getByText(/1 Review/i)).toBeInTheDocument();
    });
  });

  it('shows write review form', async () => {
    renderWithQuery(<GameReviewsPage />);
    await waitFor(() => {
      expect(screen.getByText('Write a Review')).toBeInTheDocument();
    });
    expect(screen.getByLabelText('Your Name')).toBeInTheDocument();
  });

  it('shows error state when API fails', async () => {
    mockGetReviews.mockRejectedValue(new Error('Network error'));
    renderWithQuery(<GameReviewsPage />);
    await waitFor(() => {
      expect(screen.getByText(/Failed to load reviews/i)).toBeInTheDocument();
    });
  });

  it('calls getReviews with correct gameId', async () => {
    renderWithQuery(<GameReviewsPage />);
    await waitFor(() => {
      expect(mockGetReviews).toHaveBeenCalledWith('game-123', 1, 10);
    });
  });
});
