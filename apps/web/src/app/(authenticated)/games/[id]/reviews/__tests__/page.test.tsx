/**
 * Game Reviews Page Tests (Issue #4889)
 *
 * Tests for the reviews page with real API integration.
 * Issue #4904: Backend API for game reviews.
 */

import { screen, waitFor, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';
import { api } from '@/lib/api';

import GameReviewsPage from '../page';

const mockUseParams = vi.hoisted(() => vi.fn(() => ({ id: 'game-123' })));

vi.mock('next/navigation', () => ({
  useParams: mockUseParams,
  useRouter: vi.fn(() => ({ push: vi.fn() })),
}));

vi.mock('@/lib/api', () => ({
  api: {
    games: {
      getReviews: vi.fn(),
      createReview: vi.fn(),
    },
  },
}));

const mockGetReviews = vi.mocked(api.games.getReviews);
const mockCreateReview = vi.mocked(api.games.createReview);

const mockReview = {
  id: 'review-1',
  gameId: 'game-123',
  authorName: 'Alice',
  rating: 8,
  content: 'Amazing game with deep strategy.',
  createdAt: '2025-06-15T10:00:00.000Z',
  updatedAt: '2025-06-15T10:00:00.000Z',
};

describe('GameReviewsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows page heading', () => {
    mockGetReviews.mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 10 });
    renderWithQuery(<GameReviewsPage />);
    expect(screen.getByText('Reviews')).toBeInTheDocument();
  });

  it('shows back navigation link', () => {
    mockGetReviews.mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 10 });
    renderWithQuery(<GameReviewsPage />);
    expect(screen.getByText('Back to Game')).toBeInTheDocument();
  });

  it('shows loading skeletons initially', () => {
    mockGetReviews.mockReturnValue(new Promise(() => {}));
    renderWithQuery(<GameReviewsPage />);
    const skeletons = document.querySelectorAll('[class*="animate-pulse"]');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('shows empty state when no reviews', async () => {
    mockGetReviews.mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 10 });
    renderWithQuery(<GameReviewsPage />);
    await waitFor(() => {
      expect(
        screen.getByText('No reviews yet. Be the first to review this game!')
      ).toBeInTheDocument();
    });
  });

  it('shows reviews when API returns data', async () => {
    mockGetReviews.mockResolvedValue({ items: [mockReview], total: 1, page: 1, pageSize: 10 });
    renderWithQuery(<GameReviewsPage />);
    await waitFor(() => {
      expect(screen.getByText('Alice')).toBeInTheDocument();
      expect(screen.getByText('Amazing game with deep strategy.')).toBeInTheDocument();
    });
  });

  it('shows review count', async () => {
    mockGetReviews.mockResolvedValue({ items: [mockReview], total: 1, page: 1, pageSize: 10 });
    renderWithQuery(<GameReviewsPage />);
    await waitFor(() => {
      expect(screen.getByText('1 Review')).toBeInTheDocument();
    });
  });

  it('shows error when API fails', async () => {
    mockGetReviews.mockRejectedValue(new Error('Network error'));
    renderWithQuery(<GameReviewsPage />);
    await waitFor(() => {
      expect(screen.getByText(/Failed to load reviews/i)).toBeInTheDocument();
    });
  });

  it('shows write review form', async () => {
    mockGetReviews.mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 10 });
    renderWithQuery(<GameReviewsPage />);
    await waitFor(() => {
      expect(screen.getByText('Write a Review')).toBeInTheDocument();
      expect(screen.getByLabelText('Your Name')).toBeInTheDocument();
      expect(screen.getByLabelText('Review')).toBeInTheDocument();
    });
  });

  it('submit button is disabled when form is incomplete', async () => {
    mockGetReviews.mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 10 });
    renderWithQuery(<GameReviewsPage />);
    await waitFor(() => {
      const submitButton = screen.getByRole('button', { name: 'Submit Review' });
      expect(submitButton).toBeDisabled();
    });
  });

  it('shows success message after successful submission', async () => {
    mockGetReviews.mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 10 });
    mockCreateReview.mockResolvedValue({
      id: 'new-review',
      gameId: 'game-123',
      authorName: 'Bob',
      rating: 7,
      content: 'Good game',
      createdAt: '2025-06-15T10:00:00.000Z',
      updatedAt: null,
    });

    renderWithQuery(<GameReviewsPage />);

    await waitFor(() => {
      expect(screen.getByText('Write a Review')).toBeInTheDocument();
    });

    fireEvent.change(screen.getByLabelText('Your Name'), { target: { value: 'Bob' } });
    // Select rating 7
    const ratingButtons = screen.getAllByRole('radio');
    fireEvent.click(ratingButtons[6]); // 7th star
    fireEvent.change(screen.getByLabelText('Review'), { target: { value: 'Good game' } });

    const submitButton = screen.getByRole('button', { name: 'Submit Review' });
    fireEvent.click(submitButton);

    await waitFor(() => {
      expect(screen.getByText('Your review has been submitted successfully!')).toBeInTheDocument();
    });
  });

  it('calls API with correct gameId', async () => {
    mockGetReviews.mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 10 });
    renderWithQuery(<GameReviewsPage />);
    await waitFor(() => {
      expect(mockGetReviews).toHaveBeenCalledWith('game-123', 1, 10);
    });
  });
});
