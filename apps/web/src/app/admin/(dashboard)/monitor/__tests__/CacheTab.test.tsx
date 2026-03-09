import { vi, describe, it, expect, beforeEach } from 'vitest';

const mockGetInfrastructureDetails = vi.hoisted(() => vi.fn());
const mockClearKBCache = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getInfrastructureDetails: mockGetInfrastructureDetails,
      clearKBCache: mockClearKBCache,
    },
  },
}));

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { CacheTab } from '../CacheTab';

describe('CacheTab', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetInfrastructureDetails.mockResolvedValue({
      overall: { state: 'Healthy', healthyCount: 5, totalCount: 5 },
      services: [],
      prometheusMetrics: {
        apiRequestsLast24h: 1000,
        avgLatencyMs: 50,
        errorRate: 0.01,
        llmCostLast24h: 5.5,
      },
    });
    mockClearKBCache.mockResolvedValue({
      success: true,
      message: 'Cache cleared',
      clearedAt: '2026-01-01T00:00:00Z',
    });
  });

  it('renders heading', async () => {
    render(<CacheTab />);

    await waitFor(() => {
      expect(screen.getByText('Cache & System Metrics')).toBeInTheDocument();
    });
  });

  it('renders "Clear Cache" button', async () => {
    render(<CacheTab />);

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /clear cache/i })).toBeInTheDocument();
    });
  });

  it('renders metric cards', async () => {
    render(<CacheTab />);

    await waitFor(() => {
      expect(screen.getByText('Avg Latency')).toBeInTheDocument();
    });

    expect(screen.getByText('API Requests (24h)')).toBeInTheDocument();
    expect(screen.getByText('Error Rate')).toBeInTheDocument();
    expect(screen.getByText('LLM Cost (24h)')).toBeInTheDocument();
  });

  it('calls clearKBCache when button is clicked', async () => {
    const user = userEvent.setup();

    render(<CacheTab />);

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /clear cache/i })).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /clear cache/i }));

    expect(mockClearKBCache).toHaveBeenCalledOnce();
  });

  it('handles API errors gracefully', async () => {
    mockGetInfrastructureDetails.mockRejectedValue(new Error('Network error'));

    render(<CacheTab />);

    await waitFor(() => {
      expect(screen.getByText('Cache & System Metrics')).toBeInTheDocument();
    });
  });
});
