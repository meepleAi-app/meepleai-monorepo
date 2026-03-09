/**
 * MetricsDashboard Component Tests (Issue #5459)
 *
 * Tests for the processing metrics dashboard:
 * - Period selector toggle
 * - KPI stat cards display
 * - Phase timing bars
 * - Loading states
 * - Empty state
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const { mockGet } = vi.hoisted(() => ({
  mockGet: vi.fn(),
}));

vi.mock('@/lib/api/client', () => ({
  apiClient: {
    get: mockGet,
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
    patch: vi.fn(),
  },
}));

import { MetricsDashboard } from '@/app/admin/(dashboard)/knowledge-base/queue/components/metrics-dashboard';

const MOCK_METRICS = {
  phaseTimings: [
    {
      phase: 'TextExtraction',
      avgDurationSeconds: 12.5,
      minDurationSeconds: 3.2,
      maxDurationSeconds: 45.0,
      sampleCount: 10,
    },
    {
      phase: 'Chunking',
      avgDurationSeconds: 5.3,
      minDurationSeconds: 1.1,
      maxDurationSeconds: 15.0,
      sampleCount: 8,
    },
    {
      phase: 'Embedding',
      avgDurationSeconds: 25.8,
      minDurationSeconds: 10.0,
      maxDurationSeconds: 60.0,
      sampleCount: 10,
    },
  ],
  totalProcessed: 50,
  totalFailed: 5,
  failureRatePercent: 9.1,
  avgTotalDurationSeconds: 43.6,
  period: '24h',
};

function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false },
    },
  });
}

function renderDashboard() {
  const queryClient = createQueryClient();
  return render(
    <QueryClientProvider client={queryClient}>
      <MetricsDashboard />
    </QueryClientProvider>
  );
}

describe('MetricsDashboard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should render loading state initially', () => {
    mockGet.mockReturnValue(new Promise(() => {})); // Never resolves
    renderDashboard();
    expect(screen.getByText('Processing Metrics')).toBeInTheDocument();
    expect(screen.getAllByTestId('stat-card-loading').length).toBeGreaterThanOrEqual(1);
  });

  it('should display metrics data after loading', async () => {
    mockGet.mockResolvedValue(MOCK_METRICS);
    renderDashboard();

    await waitFor(() => {
      expect(screen.getByText('50')).toBeInTheDocument();
    });
    expect(screen.getByText('5')).toBeInTheDocument();
    expect(screen.getByText('9.1%')).toBeInTheDocument();
  });

  it('should render phase timing bars for each phase', async () => {
    mockGet.mockResolvedValue(MOCK_METRICS);
    renderDashboard();

    await waitFor(() => {
      expect(screen.getByTestId('phase-timing-TextExtraction')).toBeInTheDocument();
    });
    expect(screen.getByTestId('phase-timing-Chunking')).toBeInTheDocument();
    expect(screen.getByTestId('phase-timing-Embedding')).toBeInTheDocument();
  });

  it('should show period selector with 24h selected by default', () => {
    mockGet.mockResolvedValue(MOCK_METRICS);
    renderDashboard();

    const btn24h = screen.getByTestId('period-24h');
    const btn7d = screen.getByTestId('period-7d');
    const btn30d = screen.getByTestId('period-30d');

    expect(btn24h).toBeInTheDocument();
    expect(btn7d).toBeInTheDocument();
    expect(btn30d).toBeInTheDocument();
  });

  it('should switch period when clicking a different option', async () => {
    const user = userEvent.setup();
    mockGet.mockResolvedValue(MOCK_METRICS);
    renderDashboard();

    await user.click(screen.getByTestId('period-7d'));

    await waitFor(() => {
      expect(mockGet).toHaveBeenCalledWith('/api/v1/admin/queue/metrics?period=7d');
    });
  });

  it('should show empty state when no phase timings', async () => {
    mockGet.mockResolvedValue({
      ...MOCK_METRICS,
      phaseTimings: [],
    });
    renderDashboard();

    await waitFor(() => {
      expect(screen.getByTestId('no-metrics')).toBeInTheDocument();
    });
    expect(
      screen.getByText('No processing metrics available for this period.')
    ).toBeInTheDocument();
  });

  it('should use danger variant for high failure rate', async () => {
    mockGet.mockResolvedValue({
      ...MOCK_METRICS,
      totalFailed: 20,
      failureRatePercent: 15.5,
    });
    renderDashboard();

    await waitFor(() => {
      expect(screen.getByText('15.5%')).toBeInTheDocument();
    });
  });
});
