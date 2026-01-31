/**
 * ShareButton Component Tests
 * Issue #2749: Frontend - Rate Limit Feedback UI
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ShareButton } from '../ShareButton';

// Mock useRateLimitStatus
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

describe('ShareButton', () => {
  const mockOnShare = vi.fn();
  const mockGameId = '123e4567-e89b-12d3-a456-426614174000';

  it('should be enabled when rate limit allows', () => {
    vi.mocked(useRateLimitStatus).mockReturnValue({
      data: {
        currentPendingCount: 1,
        maxPendingAllowed: 5,
        currentMonthlyCount: 3,
        maxMonthlyAllowed: 10,
        isInCooldown: false,
        cooldownEndsAt: null,
        monthResetAt: '2024-12-31T23:59:59Z',
      },
    } as any);

    renderWithProviders(
      <ShareButton gameId={mockGameId} onShare={mockOnShare} />
    );

    const button = screen.getByRole('button', { name: /Share with Community/i });
    expect(button).not.toBeDisabled();
  });

  it('should be disabled when in cooldown', () => {
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

    renderWithProviders(
      <ShareButton gameId={mockGameId} onShare={mockOnShare} />
    );

    const button = screen.getByRole('button', { name: /Share with Community/i });
    expect(button).toBeDisabled();
  });

  it('should be disabled when existing pending request', () => {
    vi.mocked(useRateLimitStatus).mockReturnValue({
      data: {
        currentPendingCount: 1,
        maxPendingAllowed: 5,
        currentMonthlyCount: 3,
        maxMonthlyAllowed: 10,
        isInCooldown: false,
        cooldownEndsAt: null,
        monthResetAt: '2024-12-31T23:59:59Z',
      },
    } as any);

    renderWithProviders(
      <ShareButton
        gameId={mockGameId}
        onShare={mockOnShare}
        existingPendingRequest={true}
      />
    );

    const button = screen.getByRole('button', { name: /Share with Community/i });
    expect(button).toBeDisabled();
  });

  it('should be disabled when monthly limit reached', () => {
    vi.mocked(useRateLimitStatus).mockReturnValue({
      data: {
        currentPendingCount: 1,
        maxPendingAllowed: 5,
        currentMonthlyCount: 10,
        maxMonthlyAllowed: 10,
        isInCooldown: false,
        cooldownEndsAt: null,
        monthResetAt: '2024-12-31T23:59:59Z',
      },
    } as any);

    renderWithProviders(
      <ShareButton gameId={mockGameId} onShare={mockOnShare} />
    );

    const button = screen.getByRole('button', { name: /Share with Community/i });
    expect(button).toBeDisabled();
  });
});
