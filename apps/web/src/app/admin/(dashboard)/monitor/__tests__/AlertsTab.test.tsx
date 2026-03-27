import { vi, describe, it, expect, beforeEach } from 'vitest';

const mockGetAll = vi.hoisted(() => vi.fn());
const mockGetAnalytics = vi.hoisted(() => vi.fn());
const mockGetInfrastructureDetails = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api/alert-rules.api', () => ({
  alertRulesApi: {
    getAll: mockGetAll,
    toggle: vi.fn().mockResolvedValue(undefined),
  },
}));

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getAnalytics: mockGetAnalytics,
      getInfrastructureDetails: mockGetInfrastructureDetails,
    },
  },
}));

vi.mock('@/components/admin/AlertsBanner', () => ({
  AlertsBanner: (props: Record<string, unknown>) => (
    <div data-testid="alerts-banner" data-metrics={JSON.stringify(props.metrics)} />
  ),
}));

vi.mock('@/components/admin/alert-rules/AlertRuleList', () => ({
  AlertRuleList: (props: Record<string, unknown>) => (
    <div
      data-testid="alert-rule-list"
      data-rules-count={Array.isArray(props.rules) ? (props.rules as unknown[]).length : 0}
    />
  ),
}));

import { render, screen, waitFor } from '@testing-library/react';

import { AlertsTab } from '../AlertsTab';

describe('AlertsTab', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetAll.mockResolvedValue([]);
    mockGetAnalytics.mockResolvedValue(null);
    mockGetInfrastructureDetails.mockResolvedValue({
      overall: {
        state: 'Healthy',
        healthyServices: 5,
        totalServices: 5,
        degradedServices: 0,
        unhealthyServices: 0,
        checkedAt: '2026-01-01T00:00:00Z',
      },
      services: [],
      prometheusMetrics: {
        apiRequestsLast24h: 100,
        avgLatencyMs: 50,
        errorRate: 0.01,
        llmCostLast24h: 1.5,
      },
    });
  });

  it('renders AlertsBanner and AlertRuleList after loading', async () => {
    render(<AlertsTab />);

    await waitFor(() => {
      expect(screen.getByTestId('alerts-banner')).toBeInTheDocument();
    });

    expect(screen.getByTestId('alert-rule-list')).toBeInTheDocument();
  });

  it('displays "Alert Rules" heading', async () => {
    render(<AlertsTab />);

    await waitFor(() => {
      expect(screen.getByText('Regole di Alert')).toBeInTheDocument();
    });
  });

  it('passes fetched rules to AlertRuleList', async () => {
    const mockRules = [
      {
        id: '1',
        name: 'Test Rule',
        alertType: 'cpu',
        severity: 'Warning' as const,
        thresholdValue: 90,
        thresholdUnit: '%',
        durationMinutes: 5,
        isEnabled: true,
        description: null,
        createdAt: '2026-01-01T00:00:00Z',
        updatedAt: '2026-01-01T00:00:00Z',
      },
    ];
    mockGetAll.mockResolvedValue(mockRules);

    render(<AlertsTab />);

    await waitFor(() => {
      expect(screen.getByTestId('alert-rule-list')).toHaveAttribute('data-rules-count', '1');
    });
  });

  it('handles API errors gracefully', async () => {
    mockGetAll.mockRejectedValue(new Error('Network error'));
    mockGetAnalytics.mockRejectedValue(new Error('Network error'));

    render(<AlertsTab />);

    await waitFor(() => {
      expect(screen.getByTestId('alerts-banner')).toBeInTheDocument();
    });

    expect(screen.getByTestId('alert-rule-list')).toHaveAttribute('data-rules-count', '0');
  });
});
