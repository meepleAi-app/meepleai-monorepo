import { vi, describe, it, expect, beforeEach } from 'vitest';

const mockGetInfrastructureDetails = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getInfrastructureDetails: mockGetInfrastructureDetails,
    },
  },
}));

vi.mock('@/components/admin/ServiceHealthMatrix', () => ({
  ServiceHealthMatrix: (props: Record<string, unknown>) => (
    <div
      data-testid="service-health-matrix"
      data-loading={String(props.loading)}
      data-services-count={Array.isArray(props.services) ? (props.services as unknown[]).length : 0}
    />
  ),
}));

import { render, screen, waitFor } from '@testing-library/react';

import { InfrastructureTab } from '../InfrastructureTab';

describe('InfrastructureTab', () => {
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
  });

  it('renders "Infrastructure Health" heading', async () => {
    render(<InfrastructureTab />);

    await waitFor(() => {
      expect(screen.getByText('Infrastructure Health')).toBeInTheDocument();
    });
  });

  it('renders ServiceHealthMatrix', async () => {
    render(<InfrastructureTab />);

    await waitFor(() => {
      expect(screen.getByTestId('service-health-matrix')).toBeInTheDocument();
    });
  });

  it('passes fetched services to ServiceHealthMatrix', async () => {
    mockGetInfrastructureDetails.mockResolvedValue({
      overall: { state: 'Healthy', healthyCount: 2, totalCount: 2 },
      services: [
        {
          serviceName: 'postgres',
          state: 'Healthy',
          responseTimeMs: 10,
          checkedAt: '2026-01-01T00:00:00Z',
        },
        {
          serviceName: 'redis',
          state: 'Healthy',
          responseTimeMs: 2,
          checkedAt: '2026-01-01T00:00:00Z',
        },
      ],
      prometheusMetrics: {
        apiRequestsLast24h: 1000,
        avgLatencyMs: 50,
        errorRate: 0.01,
        llmCostLast24h: 5.5,
      },
    });

    render(<InfrastructureTab />);

    await waitFor(() => {
      expect(screen.getByTestId('service-health-matrix')).toHaveAttribute(
        'data-services-count',
        '2'
      );
    });
  });

  it('handles API errors gracefully', async () => {
    mockGetInfrastructureDetails.mockRejectedValue(new Error('Network error'));

    render(<InfrastructureTab />);

    await waitFor(() => {
      expect(screen.getByTestId('service-health-matrix')).toHaveAttribute(
        'data-services-count',
        '0'
      );
    });
  });
});
