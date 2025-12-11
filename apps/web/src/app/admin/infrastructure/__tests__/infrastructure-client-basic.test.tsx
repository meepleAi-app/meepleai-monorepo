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
import { createHealthyInfraData } from './helpers/test-utils';

// Mock next/navigation
vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: vi.fn(),
    replace: vi.fn(),
    back: vi.fn(),
  }),
  usePathname: () => '/admin/infrastructure',
}));

// Mock AdminLayout
vi.mock('@/components/admin/AdminLayout', () => ({
  AdminLayout: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
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

const mockData = createHealthyInfraData();

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
      // Use getAllByText to verify at least one exists
      const healthElements = screen.getAllByText('Sano');
      expect(healthElements.length).toBeGreaterThan(0);
    });
  });

  it('should display Prometheus metrics', async () => {
    vi.mocked(api.admin.getInfrastructureDetails).mockResolvedValue(mockData);

    render(<InfrastructureClient />);

    await waitFor(() => {
      // Component uses .toLocaleString() which formats differently per locale
      // Italian locale uses "." as thousands separator: 15.234
      expect(screen.getByText(/15[.,]234/)).toBeInTheDocument(); // API requests
      expect(screen.getByText(/125\.4\s*ms/)).toBeInTheDocument(); // Latency
    });
  });
});
