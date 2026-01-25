/**
 * MyActiveReviewsButton Component Tests
 *
 * Test Coverage:
 * - Badge count display
 * - Empty state
 * - Active reviews list
 * - Sheet open/close
 * - Navigation on review click
 *
 * Issue #2748: Frontend - Admin Review Lock UI
 * Target: ≥85% coverage
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { MyActiveReviewsButton } from '../MyActiveReviewsButton';
import * as useQueriesModule from '@/hooks/queries';
import type { ActiveReviewDto } from '@/lib/api/schemas/admin-share-requests.schemas';

// Mock hooks
vi.mock('@/hooks/queries', () => ({
  useMyReviews: vi.fn(),
}));

// Mock next/navigation
const mockPush = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
}));

const mockActiveReviews: ActiveReviewDto[] = [
  {
    shareRequestId: 'share-1',
    sourceGameId: 'game-1',
    gameTitle: 'Wingspan',
    contributorId: 'user-1',
    contributorName: 'Mario Rossi',
    reviewStartedAt: new Date(Date.now() - 10 * 60 * 1000).toISOString(),
    reviewLockExpiresAt: new Date(Date.now() + 20 * 60 * 1000).toISOString(),
    status: 'InReview',
  },
  {
    shareRequestId: 'share-2',
    sourceGameId: 'game-2',
    gameTitle: 'Gloomhaven',
    contributorId: 'user-2',
    contributorName: 'Giulia Bianchi',
    reviewStartedAt: new Date(Date.now() - 25 * 60 * 1000).toISOString(),
    reviewLockExpiresAt: new Date(Date.now() + 5 * 60 * 1000).toISOString(),
    status: 'InReview',
  },
];

describe('MyActiveReviewsButton', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = new QueryClient({
      defaultOptions: {
        queries: { retry: false },
      },
    });
    vi.clearAllMocks();
  });

  const renderComponent = () => {
    return render(
      <QueryClientProvider client={queryClient}>
        <MyActiveReviewsButton />
      </QueryClientProvider>
    );
  };

  it('renders button with zero badge when no active reviews', () => {
    vi.mocked(useQueriesModule.useMyReviews).mockReturnValue({
      data: [],
      isLoading: false,
    } as any);

    renderComponent();

    expect(screen.getByRole('button', { name: /My Reviews/i })).toBeInTheDocument();
    expect(screen.queryByText('0')).not.toBeInTheDocument(); // Badge not shown when count is 0
  });

  it('renders button with badge count when active reviews exist', () => {
    vi.mocked(useQueriesModule.useMyReviews).mockReturnValue({
      data: mockActiveReviews,
      isLoading: false,
    } as any);

    renderComponent();

    expect(screen.getByText('2')).toBeInTheDocument(); // Badge with count
  });

  it('shows empty state when sheet is opened and no active reviews', () => {
    vi.mocked(useQueriesModule.useMyReviews).mockReturnValue({
      data: [],
      isLoading: false,
    } as any);

    renderComponent();

    const button = screen.getByRole('button', { name: /My Reviews/i });
    fireEvent.click(button);

    expect(screen.getByText('No active reviews')).toBeInTheDocument();
  });

  it('shows active reviews list when sheet is opened', () => {
    vi.mocked(useQueriesModule.useMyReviews).mockReturnValue({
      data: mockActiveReviews,
      isLoading: false,
    } as any);

    renderComponent();

    const button = screen.getByRole('button', { name: /My Reviews/i });
    fireEvent.click(button);

    expect(screen.getByText('Wingspan')).toBeInTheDocument();
    expect(screen.getByText('Gloomhaven')).toBeInTheDocument();
  });

  it('navigates to review detail when review item is clicked', () => {
    vi.mocked(useQueriesModule.useMyReviews).mockReturnValue({
      data: mockActiveReviews,
      isLoading: false,
    } as any);

    renderComponent();

    const button = screen.getByRole('button', { name: /My Reviews/i });
    fireEvent.click(button);

    const reviewItem = screen.getByText('Wingspan').closest('button');
    fireEvent.click(reviewItem!);

    expect(mockPush).toHaveBeenCalledWith('/admin/share-requests/share-1');
  });

  it('shows loading state when fetching reviews', () => {
    vi.mocked(useQueriesModule.useMyReviews).mockReturnValue({
      data: undefined,
      isLoading: true,
    } as any);

    renderComponent();

    const button = screen.getByRole('button', { name: /My Reviews/i });
    fireEvent.click(button);

    // Loading spinner should be visible (lucide-loader-2)
    const { container } = screen.getByText('Active Reviews').closest('div')!.parentElement!;
    expect(container.querySelector('svg.lucide-loader-2')).toBeInTheDocument();
  });
});
