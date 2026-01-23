/**
 * RateLimitWarningBanner Component Tests
 * Issue #2749: Frontend - Rate Limit Feedback UI
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { RateLimitWarningBanner } from '../RateLimitWarningBanner';

vi.mock('@/hooks/queries/useShareRequests', () => ({
  useRateLimitStatus: vi.fn(),
}));

const { useRateLimitStatus } = await import('@/hooks/queries/useShareRequests');

function renderWithProviders(ui: React.ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>
  );
}

describe('RateLimitWarningBanner', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('should not show when usage < 80%', () => {
    vi.mocked(useRateLimitStatus).mockReturnValue({
      data: {
        currentPendingCount: 2,
        maxPendingAllowed: 5,
        currentMonthlyCount: 3,
        maxMonthlyAllowed: 10,
        isInCooldown: false,
        cooldownEndsAt: null,
        monthResetAt: '2024-12-31T23:59:59Z',
      },
    } as any);

    renderWithProviders(<RateLimitWarningBanner />);

    expect(screen.queryByText(/Rate Limit Warning/)).not.toBeInTheDocument();
  });

  it('should show when monthly >= 80%', () => {
    vi.mocked(useRateLimitStatus).mockReturnValue({
      data: {
        currentPendingCount: 2,
        maxPendingAllowed: 5,
        currentMonthlyCount: 8,
        maxMonthlyAllowed: 10,
        isInCooldown: false,
        cooldownEndsAt: null,
        monthResetAt: '2024-12-31T23:59:59Z',
      },
    } as any);

    renderWithProviders(<RateLimitWarningBanner />);

    expect(screen.getByText(/Rate Limit Warning/)).toBeInTheDocument();
    expect(screen.getByText(/80%/)).toBeInTheDocument();
  });

  it('should show when in cooldown', () => {
    vi.mocked(useRateLimitStatus).mockReturnValue({
      data: {
        currentPendingCount: 0,
        maxPendingAllowed: 5,
        currentMonthlyCount: 0,
        maxMonthlyAllowed: 10,
        isInCooldown: true,
        cooldownEndsAt: '2024-12-31T23:59:59Z',
        monthResetAt: '2024-12-31T23:59:59Z',
      },
    } as any);

    renderWithProviders(<RateLimitWarningBanner />);

    expect(screen.getByText(/Cooldown active/)).toBeInTheDocument();
  });
});
