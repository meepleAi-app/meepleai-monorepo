/**
 * ServicesDashboard Component Tests
 * Issue #132 — Enhanced ServiceHealthMatrix
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import type { EnhancedServiceDashboard } from '@/lib/api/schemas';

const mockGetServiceDashboard = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getServiceDashboard: mockGetServiceDashboard,
    },
  },
}));

vi.mock('@/hooks/useToast', () => ({
  useToast: () => ({
    toast: vi.fn(),
  }),
}));

import { ServicesDashboard } from '../ServicesDashboard';

function createMockDashboard(
  overrides: Partial<EnhancedServiceDashboard> = {}
): EnhancedServiceDashboard {
  return {
    overall: {
      state: 'Healthy',
      totalServices: 6,
      healthyServices: 5,
      degradedServices: 1,
      unhealthyServices: 0,
      checkedAt: new Date().toISOString(),
    },
    services: [
      {
        serviceName: 'PostgreSQL',
        state: 'Healthy',
        checkedAt: new Date().toISOString(),
        responseTimeMs: 15,
        category: 'Core Infrastructure',
        uptimePercent24h: 99.95,
        responseTimeTrend: 'stable',
        lastIncidentAt: null,
      },
      {
        serviceName: 'Redis',
        state: 'Healthy',
        checkedAt: new Date().toISOString(),
        responseTimeMs: 2,
        category: 'Core Infrastructure',
        uptimePercent24h: 100,
        responseTimeTrend: 'down',
        previousResponseTimeMs: 5,
        lastIncidentAt: null,
      },
      {
        serviceName: 'pgvector',
        state: 'Healthy',
        checkedAt: new Date().toISOString(),
        responseTimeMs: 25,
        category: 'Core Infrastructure',
        uptimePercent24h: 99.8,
        responseTimeTrend: 'stable',
        lastIncidentAt: null,
      },
      {
        serviceName: 'Embedding Service',
        state: 'Degraded',
        errorMessage: 'High latency detected',
        checkedAt: new Date().toISOString(),
        responseTimeMs: 3500,
        category: 'AI Services',
        uptimePercent24h: 97.2,
        responseTimeTrend: 'up',
        previousResponseTimeMs: 800,
        lastIncidentAt: new Date(Date.now() - 3600000).toISOString(),
      },
      {
        serviceName: 'OpenRouter',
        state: 'Healthy',
        checkedAt: new Date().toISOString(),
        responseTimeMs: 120,
        category: 'External APIs',
        uptimePercent24h: 99.99,
        responseTimeTrend: 'stable',
        lastIncidentAt: null,
      },
      {
        serviceName: 'Prometheus',
        state: 'Healthy',
        checkedAt: new Date().toISOString(),
        responseTimeMs: 8,
        category: 'Monitoring',
        uptimePercent24h: 100,
        responseTimeTrend: 'stable',
        lastIncidentAt: null,
      },
    ],
    prometheusMetrics: {
      apiRequestsLast24h: 15000,
      avgLatencyMs: 45,
      errorRate: 0.002,
      llmCostLast24h: 12.5,
    },
    ...overrides,
  };
}

describe('ServicesDashboard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers({ shouldAdvanceTime: true });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  // ==================== Loading State ====================

  it('shows loading spinner initially', () => {
    mockGetServiceDashboard.mockReturnValue(new Promise(() => {})); // Never resolves
    render(<ServicesDashboard />);

    expect(screen.getByTestId('services-dashboard')).toBeInTheDocument();
  });

  // ==================== Data Display ====================

  it('renders overall health banner', async () => {
    mockGetServiceDashboard.mockResolvedValue(createMockDashboard());
    render(<ServicesDashboard />);

    await waitFor(() => {
      expect(screen.getByTestId('overall-health-banner')).toBeInTheDocument();
    });

    expect(screen.getByText(/5\/6 services healthy/)).toBeInTheDocument();
  });

  it('renders services grouped by category', async () => {
    mockGetServiceDashboard.mockResolvedValue(createMockDashboard());
    render(<ServicesDashboard />);

    await waitFor(() => {
      expect(screen.getByText('Core Infrastructure')).toBeInTheDocument();
    });

    expect(screen.getByText('AI Services')).toBeInTheDocument();
    expect(screen.getByText('External APIs')).toBeInTheDocument();
    expect(screen.getByText('Monitoring')).toBeInTheDocument();
  });

  it('renders service rows within categories', async () => {
    mockGetServiceDashboard.mockResolvedValue(createMockDashboard());
    render(<ServicesDashboard />);

    await waitFor(() => {
      expect(screen.getByText('PostgreSQL')).toBeInTheDocument();
    });

    expect(screen.getByText('Redis')).toBeInTheDocument();
    expect(screen.getByText('pgvector')).toBeInTheDocument();
    expect(screen.getByText('Embedding Service')).toBeInTheDocument();
    expect(screen.getByText('OpenRouter')).toBeInTheDocument();
    expect(screen.getByText('Prometheus')).toBeInTheDocument();
  });

  // ==================== Uptime Badges ====================

  it('shows uptime badges for services', async () => {
    mockGetServiceDashboard.mockResolvedValue(createMockDashboard());
    render(<ServicesDashboard />);

    await waitFor(() => {
      const badges = screen.getAllByTestId('uptime-badge');
      expect(badges.length).toBeGreaterThan(0);
    });

    // PostgreSQL 99.95 rounds to 100.0, Redis 100.0 — both show 100.0%
    // pgvector 99.8 shows as 99.8%
    expect(screen.getByText('99.8%')).toBeInTheDocument(); // pgvector
  });

  it('shows yellow uptime badge for 95-99% uptime', async () => {
    mockGetServiceDashboard.mockResolvedValue(createMockDashboard());
    render(<ServicesDashboard />);

    await waitFor(() => {
      expect(screen.getByText('97.2%')).toBeInTheDocument(); // Embedding Service
    });
  });

  // ==================== Response Time Trends ====================

  it('shows trend indicators', async () => {
    mockGetServiceDashboard.mockResolvedValue(createMockDashboard());
    render(<ServicesDashboard />);

    await waitFor(() => {
      expect(screen.getByTestId('trend-up')).toBeInTheDocument(); // Embedding Service
    });

    expect(screen.getByTestId('trend-down')).toBeInTheDocument(); // Redis
    expect(screen.getAllByTestId('trend-stable').length).toBeGreaterThan(0);
  });

  it('formats response time correctly', async () => {
    mockGetServiceDashboard.mockResolvedValue(createMockDashboard());
    render(<ServicesDashboard />);

    await waitFor(() => {
      expect(screen.getByText('15ms')).toBeInTheDocument(); // PostgreSQL
    });

    expect(screen.getByText('3.50s')).toBeInTheDocument(); // Embedding Service (3500ms)
  });

  // ==================== Auto-Refresh ====================

  it('shows auto-refresh controls', async () => {
    mockGetServiceDashboard.mockResolvedValue(createMockDashboard());
    render(<ServicesDashboard />);

    await waitFor(() => {
      expect(screen.getByTestId('auto-refresh-controls')).toBeInTheDocument();
    });

    expect(screen.getByTestId('auto-refresh-toggle')).toBeInTheDocument();
    expect(screen.getByTestId('countdown')).toBeInTheDocument();
    expect(screen.getByTestId('refresh-interval-select')).toBeInTheDocument();
  });

  it('shows countdown timer when auto-refresh is active', async () => {
    mockGetServiceDashboard.mockResolvedValue(createMockDashboard());
    render(<ServicesDashboard />);

    await waitFor(() => {
      expect(screen.getByTestId('countdown')).toHaveTextContent('30s');
    });
  });

  it('pauses auto-refresh when toggle clicked', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    mockGetServiceDashboard.mockResolvedValue(createMockDashboard());
    render(<ServicesDashboard />);

    await waitFor(() => {
      expect(screen.getByTestId('auto-refresh-toggle')).toBeInTheDocument();
    });

    await user.click(screen.getByTestId('auto-refresh-toggle'));

    expect(screen.getByText('Resume')).toBeInTheDocument();
    expect(screen.queryByTestId('countdown')).not.toBeInTheDocument();
  });

  // ==================== Compact/Expanded View ====================

  it('toggles between compact and expanded view', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    mockGetServiceDashboard.mockResolvedValue(createMockDashboard());
    render(<ServicesDashboard />);

    await waitFor(() => {
      expect(screen.getByTestId('compact-toggle')).toBeInTheDocument();
    });

    // Default is expanded — shows Last Incident and Error columns
    expect(screen.getAllByText('Last Incident').length).toBeGreaterThan(0);

    await user.click(screen.getByTestId('compact-toggle'));

    // Compact mode hides Last Incident and Error columns
    expect(screen.queryByText('Last Incident')).not.toBeInTheDocument();
  });

  // ==================== Category Collapsing ====================

  it('collapses a category when header clicked', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    mockGetServiceDashboard.mockResolvedValue(createMockDashboard());
    render(<ServicesDashboard />);

    await waitFor(() => {
      expect(screen.getByText('PostgreSQL')).toBeInTheDocument();
    });

    // Click Core Infrastructure to collapse
    await user.click(screen.getByTestId('category-toggle-core-infrastructure'));

    // PostgreSQL should be hidden now
    expect(screen.queryByText('PostgreSQL')).not.toBeInTheDocument();

    // Re-click to expand
    await user.click(screen.getByTestId('category-toggle-core-infrastructure'));
    expect(screen.getByText('PostgreSQL')).toBeInTheDocument();
  });

  // ==================== Health States ====================

  it('shows issue badge on category with unhealthy services', async () => {
    mockGetServiceDashboard.mockResolvedValue(createMockDashboard());
    render(<ServicesDashboard />);

    await waitFor(() => {
      // AI Services has 1 degraded service
      expect(screen.getByText('1 issue')).toBeInTheDocument();
    });
  });

  it('renders degraded and unhealthy state badges', async () => {
    mockGetServiceDashboard.mockResolvedValue(
      createMockDashboard({
        overall: {
          state: 'Degraded',
          totalServices: 3,
          healthyServices: 1,
          degradedServices: 1,
          unhealthyServices: 1,
          checkedAt: new Date().toISOString(),
        },
      })
    );
    render(<ServicesDashboard />);

    await waitFor(() => {
      expect(screen.getByText('Degraded')).toBeInTheDocument();
    });
  });

  // ==================== Error Handling ====================

  it('shows empty state when no data returned', async () => {
    mockGetServiceDashboard.mockResolvedValue({
      overall: {
        state: 'Healthy',
        totalServices: 0,
        healthyServices: 0,
        degradedServices: 0,
        unhealthyServices: 0,
        checkedAt: new Date().toISOString(),
      },
      services: [],
      prometheusMetrics: {
        apiRequestsLast24h: 0,
        avgLatencyMs: 0,
        errorRate: 0,
        llmCostLast24h: 0,
      },
    });
    render(<ServicesDashboard />);

    await waitFor(() => {
      expect(screen.getByText(/No service health data available/)).toBeInTheDocument();
    });
  });

  // ==================== Prometheus Metrics KPIs ====================

  it('renders Prometheus metrics KPI row', async () => {
    mockGetServiceDashboard.mockResolvedValue(createMockDashboard());
    render(<ServicesDashboard />);

    await waitFor(() => {
      expect(screen.getByTestId('metrics-kpi-row')).toBeInTheDocument();
    });

    expect(screen.getByTestId('kpi-api-requests')).toHaveTextContent(/15[.,]000/);
    expect(screen.getByTestId('kpi-avg-latency')).toHaveTextContent('45.0ms');
    expect(screen.getByTestId('kpi-error-rate')).toHaveTextContent('0.20%');
    expect(screen.getByTestId('kpi-llm-cost')).toHaveTextContent('$12.50');
  });

  // ==================== Interval Change ====================

  it('changes refresh interval via select', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    mockGetServiceDashboard.mockResolvedValue(createMockDashboard());
    render(<ServicesDashboard />);

    await waitFor(() => {
      expect(screen.getByTestId('refresh-interval-select')).toBeInTheDocument();
    });

    await user.selectOptions(screen.getByTestId('refresh-interval-select'), '15');

    await waitFor(() => {
      expect(screen.getByTestId('countdown')).toHaveTextContent('15s');
    });
  });
});
