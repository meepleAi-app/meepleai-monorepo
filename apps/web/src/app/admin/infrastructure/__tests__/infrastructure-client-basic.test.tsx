/**
 * InfrastructureClient Basic Tests - Issue #899
 *
 * Simplified test suite focusing on core functionality and rendering.
 * Full test suite in infrastructure-client.test.tsx (requires optimization).
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { InfrastructureClient } from '../infrastructure-client';
import { api } from '@/lib/api';
import type { InfrastructureDetails } from '@/lib/api';

// Mock next/navigation
vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: vi.fn(),
    replace: vi.fn(),
    back: vi.fn(),
  }),
  usePathname: () => '/admin/infrastructure',
}));

// Mock API
vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getInfrastructureDetails: vi.fn(),
    },
  },
}));

// Mock sonner
vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

const mockData: InfrastructureDetails = {
  overall: {
    state: 'Healthy',
    totalServices: 2,
    healthyServices: 2,
    degradedServices: 0,
    unhealthyServices: 0,
    checkedAt: new Date().toISOString(),
  },
  services: [
    {
      serviceName: 'postgres',
      state: 'Healthy',
      errorMessage: null,
      checkedAt: new Date().toISOString(),
      responseTime: '00:00:00.0150000',
    },
    {
      serviceName: 'redis',
      state: 'Healthy',
      errorMessage: null,
      checkedAt: new Date().toISOString(),
      responseTime: '00:00:00.0020000',
    },
  ],
  prometheusMetrics: {
    apiRequestsLast24h: 15234,
    avgLatencyMs: 125.4,
    errorRate: 0.012,
    llmCostLast24h: 3.45,
  },
};

describe('InfrastructureClient - Basic Tests', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should render without crashing', async () => {
    vi.mocked(api.admin.getInfrastructureDetails).mockResolvedValue(mockData);

    render(<InfrastructureClient />);

    await waitFor(() => {
      expect(screen.getByText('Monitoraggio Infrastruttura')).toBeInTheDocument();
    });
  });

  it('should display infrastructure data', async () => {
    vi.mocked(api.admin.getInfrastructureDetails).mockResolvedValue(mockData);

    render(<InfrastructureClient />);

    await waitFor(() => {
      expect(screen.getByText('PostgreSQL')).toBeInTheDocument();
      expect(screen.getByText('Redis')).toBeInTheDocument();
    });
  });

  it('should show overall health status', async () => {
    vi.mocked(api.admin.getInfrastructureDetails).mockResolvedValue(mockData);

    render(<InfrastructureClient />);

    await waitFor(() => {
      // "Sano" appears multiple times (overall health + service badges)
      const healthElements = screen.getAllByText('Sano');
      expect(healthElements.length).toBeGreaterThan(0);
    });
  });

  it('should display Prometheus metrics', async () => {
    vi.mocked(api.admin.getInfrastructureDetails).mockResolvedValue(mockData);

    render(<InfrastructureClient />);

    await waitFor(() => {
      // Component uses .toLocaleString() - Italian locale uses "." as thousands separator
      expect(screen.getByText(/15[.,]234/)).toBeInTheDocument(); // API requests
      expect(screen.getByText(/125\.4\s*ms/)).toBeInTheDocument(); // Latency
    });
  });
});
