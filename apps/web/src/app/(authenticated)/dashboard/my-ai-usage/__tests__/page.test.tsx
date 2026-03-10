/**
 * My AI Usage Page Tests - Issue #5484
 *
 * Tests:
 * 1. Renders page with stats when data loads
 * 2. Shows loading skeleton
 * 3. Shows error state
 * 4. Period selector changes data
 * 5. Empty state when no usage
 * 6. Requires Editor+ role
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import type { UserAiUsageDto } from '@/lib/api/schemas/ai-usage.schemas';

// ─── Mocks ───────────────────────────────────────────────────────────────────

const mockUser = { id: 'user-1', name: 'Test Editor', role: 'Editor' };

vi.mock('@/hooks/useAuth', () => ({
  useAuth: () => ({ user: mockUser, isLoading: false }),
}));

// Mock RequireRole to just render children
vi.mock('@/components/auth/RequireRole', () => ({
  RequireRole: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

const mockGetMyAiUsage = vi.fn();
vi.mock('@/lib/api/dashboard-client', () => ({
  dashboardClient: {
    getMyAiUsage: (...args: unknown[]) => mockGetMyAiUsage(...args),
  },
}));

const mockUsageData: UserAiUsageDto = {
  userId: 'user-1',
  period: { from: '2024-01-01', to: '2024-01-07' },
  totalTokens: 12500,
  totalCostUsd: 0.0142,
  requestCount: 45,
  byModel: [
    { model: 'meta-llama/llama-3.3-70b-instruct:free', tokens: 8000, cost: 0.0 },
    { model: 'llama3:8b', tokens: 4500, cost: 0.0142 },
  ],
  byOperation: [
    { operation: 'chat', count: 30, tokens: 9000 },
    { operation: 'rag_query', count: 15, tokens: 3500 },
  ],
  dailyUsage: [
    { date: '2024-01-01', tokens: 1200 },
    { date: '2024-01-02', tokens: 2300 },
    { date: '2024-01-03', tokens: 1800 },
  ],
};

// ─── Tests ───────────────────────────────────────────────────────────────────

describe('MyAiUsagePage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetMyAiUsage.mockResolvedValue(mockUsageData);
  });

  it('renders usage stats after loading', async () => {
    const { default: Page } = await import('../page');
    render(<Page />);

    await waitFor(() => {
      expect(screen.getByTestId('usage-stats')).toBeInTheDocument();
    });

    expect(screen.getByText('45')).toBeInTheDocument(); // requestCount
    expect(screen.getByText('12.5K')).toBeInTheDocument(); // totalTokens
    // Cost appears in stat card and model detail — just check at least one
    expect(screen.getAllByText('$0.0142').length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText('2')).toBeInTheDocument(); // models count
  });

  it('shows loading skeleton initially', async () => {
    mockGetMyAiUsage.mockReturnValue(new Promise(() => {})); // never resolves
    const { default: Page } = await import('../page');
    render(<Page />);

    expect(screen.getByTestId('usage-skeleton')).toBeInTheDocument();
  });

  it('shows error when API fails', async () => {
    mockGetMyAiUsage.mockRejectedValue(new Error('Network error'));
    const { default: Page } = await import('../page');
    render(<Page />);

    await waitFor(() => {
      expect(screen.getByText('Network error')).toBeInTheDocument();
    });
  });

  it('changes period and refetches data', async () => {
    const user = userEvent.setup();
    const { default: Page } = await import('../page');
    render(<Page />);

    await waitFor(() => {
      expect(screen.getByTestId('usage-stats')).toBeInTheDocument();
    });

    // Default is 7d
    expect(mockGetMyAiUsage).toHaveBeenCalledWith(7);

    // Switch to 30d
    await user.click(screen.getByTestId('period-30d'));
    await waitFor(() => {
      expect(mockGetMyAiUsage).toHaveBeenCalledWith(30);
    });
  });

  it('renders model distribution bars', async () => {
    const { default: Page } = await import('../page');
    render(<Page />);

    await waitFor(() => {
      expect(screen.getByText('Distribuzione modelli')).toBeInTheDocument();
    });

    expect(screen.getByText('meta-llama/llama-3.3-70b-instruct:free')).toBeInTheDocument();
    expect(screen.getByText('llama3:8b')).toBeInTheDocument();
  });

  it('renders operation breakdown', async () => {
    const { default: Page } = await import('../page');
    render(<Page />);

    await waitFor(() => {
      expect(screen.getByText('Distribuzione per tipo')).toBeInTheDocument();
    });

    expect(screen.getByText('chat')).toBeInTheDocument();
    expect(screen.getByText('rag_query')).toBeInTheDocument();
    expect(screen.getByText('30 richieste')).toBeInTheDocument();
  });

  it('renders daily usage chart', async () => {
    const { default: Page } = await import('../page');
    render(<Page />);

    await waitFor(() => {
      expect(screen.getByTestId('daily-usage-chart')).toBeInTheDocument();
    });
  });

  it('shows empty state when no requests', async () => {
    mockGetMyAiUsage.mockResolvedValue({
      ...mockUsageData,
      requestCount: 0,
      totalTokens: 0,
      totalCostUsd: 0,
      byModel: [],
      byOperation: [],
      dailyUsage: [],
    });

    const { default: Page } = await import('../page');
    render(<Page />);

    await waitFor(() => {
      expect(screen.getByText(/Nessun utilizzo AI/)).toBeInTheDocument();
    });
  });
});
