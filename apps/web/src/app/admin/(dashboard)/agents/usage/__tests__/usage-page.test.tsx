/**
 * Unit tests for /admin/agents/usage page (Issue #5077).
 * Covers KPI rendering, loading skeleton, and error state.
 */

import { screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import UsagePage from '../page';

// ─── Mocks ───────────────────────────────────────────────────────────────────

const mockGetOpenRouterStatus = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api/clients/adminClient', () => ({
  createAdminClient: () => ({
    getOpenRouterStatus: mockGetOpenRouterStatus,
  }),
  HttpClient: vi.fn(),
}));

vi.mock('@/lib/api/core/httpClient', () => ({
  HttpClient: vi.fn(() => ({})),
}));

vi.mock('@/hooks/useSetNavConfig', () => ({
  useSetNavConfig: () => vi.fn(),
}));

// ─── Fixtures ────────────────────────────────────────────────────────────────

const mockStatus = {
  balanceUsd: 4.5,
  dailySpendUsd: 0.0025,
  todayRequestCount: 42,
  currentRpm: 80,
  limitRpm: 200,
  utilizationPercent: 0.4,
  isThrottled: false,
  isFreeTier: false,
  rateLimitInterval: 'minute',
  lastUpdated: '2026-02-22T10:00:00Z',
};

// ─── Tests ───────────────────────────────────────────────────────────────────

describe('UsagePage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders page heading', async () => {
    mockGetOpenRouterStatus.mockResolvedValue(mockStatus);

    renderWithQuery(<UsagePage />);

    await waitFor(() => {
      expect(screen.getByText('Usage & Costs')).toBeInTheDocument();
    });
  });

  it('renders KPI card labels', async () => {
    mockGetOpenRouterStatus.mockResolvedValue(mockStatus);

    renderWithQuery(<UsagePage />);

    await waitFor(() => {
      expect(screen.getByText('Spend Today')).toBeInTheDocument();
    });

    expect(screen.getByText('Requests Today')).toBeInTheDocument();
    expect(screen.getByText('RPM')).toBeInTheDocument();
    expect(screen.getByText('Balance')).toBeInTheDocument();
  });

  it('displays formatted spend value', async () => {
    mockGetOpenRouterStatus.mockResolvedValue(mockStatus);

    renderWithQuery(<UsagePage />);

    // dailySpendUsd = 0.0025 → "$0.0025"
    await waitFor(() => {
      expect(screen.getByText('$0.0025')).toBeInTheDocument();
    });
  });

  it('displays today request count', async () => {
    mockGetOpenRouterStatus.mockResolvedValue(mockStatus);

    renderWithQuery(<UsagePage />);

    await waitFor(() => {
      expect(screen.getByText('42')).toBeInTheDocument();
    });
  });

  it('shows RPM current / limit', async () => {
    mockGetOpenRouterStatus.mockResolvedValue(mockStatus);

    renderWithQuery(<UsagePage />);

    await waitFor(() => {
      expect(screen.getByText('80')).toBeInTheDocument();
    });

    expect(screen.getByText('/ 200')).toBeInTheDocument();
  });

  it('does not show throttled badge when not throttled', async () => {
    mockGetOpenRouterStatus.mockResolvedValue(mockStatus);

    renderWithQuery(<UsagePage />);

    await waitFor(() => {
      expect(screen.getByText('40.0% utilization')).toBeInTheDocument();
    });

    expect(screen.queryByText('Throttled')).not.toBeInTheDocument();
  });

  it('shows throttled badge when isThrottled = true', async () => {
    mockGetOpenRouterStatus.mockResolvedValue({ ...mockStatus, isThrottled: true });

    renderWithQuery(<UsagePage />);

    await waitFor(() => {
      expect(screen.getByText('Throttled')).toBeInTheDocument();
    });
  });

  it('shows Free Tier badge when isFreeTier = true', async () => {
    mockGetOpenRouterStatus.mockResolvedValue({ ...mockStatus, isFreeTier: true });

    renderWithQuery(<UsagePage />);

    await waitFor(() => {
      expect(screen.getByText('Free Tier')).toBeInTheDocument();
    });
  });

  it('shows loading skeletons initially', () => {
    mockGetOpenRouterStatus.mockReturnValue(new Promise(() => {})); // never resolves

    renderWithQuery(<UsagePage />);

    const skeletons = document.querySelectorAll('.animate-pulse');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('shows error banner on failure', async () => {
    mockGetOpenRouterStatus.mockRejectedValue(new Error('Redis unavailable'));

    renderWithQuery(<UsagePage />);

    await waitFor(() => {
      expect(screen.getByRole('alert')).toBeInTheDocument();
    });

    expect(screen.getByText(/Redis unavailable/i)).toBeInTheDocument();
  });

  it('shows placeholder text for charts section', async () => {
    mockGetOpenRouterStatus.mockResolvedValue(mockStatus);

    renderWithQuery(<UsagePage />);

    await waitFor(() => {
      expect(screen.getByText(/Timeline chart/i)).toBeInTheDocument();
    });

    expect(screen.getByText(/Cost breakdown chart/i)).toBeInTheDocument();
    expect(screen.getByText(/Rate gauge/i)).toBeInTheDocument();
    expect(screen.getByText(/Quota display/i)).toBeInTheDocument();
    expect(screen.getByText(/Requests table/i)).toBeInTheDocument();
  });
});
