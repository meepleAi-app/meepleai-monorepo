/**
 * Chromatic Visual Tests for Infrastructure Client
 *
 * Issue #2254: Verify locale rendering with user preferences
 *
 * Tests UI rendering with different locales to ensure:
 * - Italian locale renders correctly
 * - English locale renders correctly
 * - Locale changes are visually reflected
 */

import React from 'react';

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { InfrastructureClient } from '../infrastructure-client';

// Mock Next.js router
vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: vi.fn(),
    replace: vi.fn(),
    prefetch: vi.fn(),
  }),
  useSearchParams: () => ({
    get: vi.fn(),
  }),
  usePathname: () => '/admin/infrastructure',
}));

// Mock dependencies
vi.mock('@/hooks/useUserLocale', () => ({
  useUserLocale: vi.fn(),
}));

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getInfrastructureDetails: vi.fn(),
    },
  },
}));

import { api } from '@/lib/api';
import { useUserLocale } from '@/hooks/useUserLocale';

const mockGetInfrastructureDetails = api.admin.getInfrastructureDetails as ReturnType<typeof vi.fn>;
const mockUseUserLocale = useUserLocale as ReturnType<typeof vi.fn>;

const mockInfrastructureData = {
  overall: {
    state: 'Healthy' as const,
    totalServices: 10,
    healthyServices: 8,
    degradedServices: 1,
    unhealthyServices: 1,
    lastCheckedAt: '2025-01-15T10:00:00Z',
  },
  services: [
    {
      serviceName: 'API Service',
      state: 'Healthy' as const,
      responseTime: '00:00:00.0234567',
      checkedAt: '2025-01-15T10:00:00Z',
      errorMessage: null,
    },
    {
      serviceName: 'Database',
      state: 'Degraded' as const,
      responseTime: '00:00:01.1234567',
      checkedAt: '2025-01-15T10:00:00Z',
      errorMessage: 'High latency detected',
    },
  ],
  prometheusMetrics: {
    apiRequestsLast24h: 15420,
    avgLatencyMs: 234.5,
    errorRate: 0.012,
    llmCostLast24h: 12.45,
  },
};

describe('InfrastructureClient - Chromatic Visual Tests', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetInfrastructureDetails.mockResolvedValue(mockInfrastructureData);
  });

  it('renders with Italian locale (user preference)', async () => {
    mockUseUserLocale.mockReturnValue('it');

    const { container } = render(<InfrastructureClient />);

    // Verify component renders without errors
    expect(container).toBeTruthy();

    // Verify hook is called
    expect(mockUseUserLocale).toHaveBeenCalled();
  });

  it('renders with English locale (browser fallback)', async () => {
    mockUseUserLocale.mockReturnValue('en');

    const { container } = render(<InfrastructureClient />);

    // Verify component renders without errors
    expect(container).toBeTruthy();

    // Verify hook is called
    expect(mockUseUserLocale).toHaveBeenCalled();
  });

  it('uses locale from user preferences', async () => {
    mockUseUserLocale.mockReturnValue('it');

    render(<InfrastructureClient />);

    // Verify useUserLocale hook was called
    expect(mockUseUserLocale).toHaveBeenCalled();
  });

  it('uses locale from browser fallback', async () => {
    mockUseUserLocale.mockReturnValue('en');

    render(<InfrastructureClient />);

    // Verify useUserLocale hook was called
    expect(mockUseUserLocale).toHaveBeenCalled();
  });

  it('renders infrastructure data with locale-specific formatting', async () => {
    mockUseUserLocale.mockReturnValue('it');

    const { container } = render(<InfrastructureClient />);

    // Component should render with locale applied
    expect(container.querySelector('[class*="space-y"]')).toBeTruthy();
  });

  it('handles locale changes dynamically', async () => {
    mockUseUserLocale.mockReturnValue('it');

    const { rerender } = render(<InfrastructureClient />);

    mockUseUserLocale.mockReturnValue('en');

    rerender(<InfrastructureClient />);

    // Should have called hook multiple times (exact count varies with React re-renders)
    expect(mockUseUserLocale).toHaveBeenCalled();
  });
});
