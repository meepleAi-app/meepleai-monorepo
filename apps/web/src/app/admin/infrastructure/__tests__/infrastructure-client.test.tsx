/**
 * InfrastructureClient Component Tests - Issue #900
 *
 * Optimized test suite with modern patterns:
 * - Uses shared test utilities (DRY principle)
 * - Parallel execution where possible
 * - Focused on critical paths
 * - Target: 90%+ coverage, <4s runtime
 *
 * Legacy comprehensive tests: infrastructure-client.legacy.test.tsx.skip
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { InfrastructureClient } from '../infrastructure-client';
import { api } from '@/lib/api';
import {
  createHealthyInfraData,
  createDegradedInfraData,
  TEST_TIMEOUTS,
} from './helpers/test-utils';

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
    warning: vi.fn(),
    info: vi.fn(),
  },
}));

describe('InfrastructureClient', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.runOnlyPendingTimers();
    vi.useRealTimers();
  });

  // ==================== Core Functionality ====================

  describe('Data Fetching & Rendering', () => {
    it('should fetch and display infrastructure data on mount', async () => {
      const mockData = createHealthyInfraData();
      vi.mocked(api.admin.getInfrastructureDetails).mockResolvedValue(mockData);

      render(<InfrastructureClient />);

      await waitFor(
        () => {
          expect(api.admin.getInfrastructureDetails).toHaveBeenCalledTimes(1);
        },
        { timeout: TEST_TIMEOUTS.STANDARD }
      );

      // Verify page title
      expect(screen.getByText('Monitoraggio Infrastruttura')).toBeInTheDocument();

      // Verify services rendered
      expect(screen.getByText('PostgreSQL')).toBeInTheDocument();
      expect(screen.getByText('Redis')).toBeInTheDocument();

      // Verify metrics (locale-agnostic)
      expect(screen.getByText(/15[.,]234/)).toBeInTheDocument();
      expect(screen.getByText(/125\.4\s*ms/)).toBeInTheDocument();
    });

    it('should display error message on fetch failure', async () => {
      vi.mocked(api.admin.getInfrastructureDetails).mockRejectedValue(new Error('Network error'));

      render(<InfrastructureClient />);

      await waitFor(
        () => {
          expect(screen.getByText(/errore caricamento dati infrastruttura/i)).toBeInTheDocument();
        },
        { timeout: TEST_TIMEOUTS.STANDARD }
      );
    });
  });

  // ==================== Circuit Breaker ====================

  describe('Circuit Breaker Pattern', () => {
    it(
      'should open circuit after 5 consecutive failures',
      async () => {
        const user = userEvent.setup({ delay: null });
        vi.mocked(api.admin.getInfrastructureDetails).mockRejectedValue(new Error('Network error'));

        render(<InfrastructureClient />);

        // Wait for initial failure
        await waitFor(
          () => {
            expect(api.admin.getInfrastructureDetails).toHaveBeenCalledTimes(1);
          },
          { timeout: TEST_TIMEOUTS.STANDARD }
        );

        // Trigger 4 more failures via manual refresh
        const refreshButton = screen.getByRole('button', { name: /aggiorna/i });

        for (let i = 0; i < 4; i++) {
          await user.click(refreshButton);
          await waitFor(
            () => {
              expect(api.admin.getInfrastructureDetails).toHaveBeenCalledTimes(i + 2);
            },
            { timeout: TEST_TIMEOUTS.FAST }
          );
        }

        // Circuit should be open
        await waitFor(
          () => {
            expect(
              screen.getByText(/troppe richieste fallite.*aggiornamento automatico sospeso/i)
            ).toBeInTheDocument();
          },
          { timeout: TEST_TIMEOUTS.STANDARD }
        );

        expect(refreshButton).toBeDisabled();
      },
      { timeout: TEST_TIMEOUTS.INTEGRATION }
    );
  });

  // ==================== Auto-Refresh ====================

  describe('Auto-Refresh Mechanism', () => {
    it('should auto-refresh every 30 seconds by default', async () => {
      const mockData = createHealthyInfraData();
      vi.mocked(api.admin.getInfrastructureDetails).mockResolvedValue(mockData);

      render(<InfrastructureClient />);

      // Initial fetch
      await waitFor(() => {
        expect(api.admin.getInfrastructureDetails).toHaveBeenCalledTimes(1);
      });

      // Advance 30 seconds
      vi.advanceTimersByTime(30000);

      await waitFor(() => {
        expect(api.admin.getInfrastructureDetails).toHaveBeenCalledTimes(2);
      });
    });

    it('should allow disabling auto-refresh', async () => {
      const user = userEvent.setup({ delay: null });
      const mockData = createHealthyInfraData();
      vi.mocked(api.admin.getInfrastructureDetails).mockResolvedValue(mockData);

      render(<InfrastructureClient />);

      await waitFor(() => {
        expect(api.admin.getInfrastructureDetails).toHaveBeenCalledTimes(1);
      });

      // Disable auto-refresh
      const autoRefreshSwitch = screen.getByRole('switch', {
        name: /aggiornamento automatico/i,
      });
      await user.click(autoRefreshSwitch);

      // Advance time - should not fetch again
      vi.advanceTimersByTime(60000);

      expect(api.admin.getInfrastructureDetails).toHaveBeenCalledTimes(1);
    });
  });

  // ==================== Service Filtering ====================

  describe('Service Filtering', () => {
    it('should filter services by search query', async () => {
      const user = userEvent.setup({ delay: null });
      const mockData = createHealthyInfraData();
      vi.mocked(api.admin.getInfrastructureDetails).mockResolvedValue(mockData);

      render(<InfrastructureClient />);

      await waitFor(() => {
        expect(screen.getByText('PostgreSQL')).toBeInTheDocument();
      });

      // Search for "redis"
      const searchInput = screen.getByPlaceholderText(/cerca servizio/i);
      await user.type(searchInput, 'redis');

      await waitFor(() => {
        expect(screen.getByText('Redis')).toBeInTheDocument();
        expect(screen.queryByText('PostgreSQL')).not.toBeInTheDocument();
      });
    });
  });

  // ==================== Export Functionality ====================

  describe('Export Functionality', () => {
    beforeEach(() => {
      // Mock URL.createObjectURL and revokeObjectURL
      global.URL.createObjectURL = vi.fn(() => 'blob:mock-url');
      global.URL.revokeObjectURL = vi.fn();

      // Mock HTMLAnchorElement click
      const mockClick = vi.fn();
      const originalCreateElement = document.createElement.bind(document);
      vi.spyOn(document, 'createElement').mockImplementation((tag: string) => {
        const element = originalCreateElement(tag);
        if (tag === 'a') {
          element.click = mockClick;
        }
        return element;
      });
    });

    it('should export data as CSV', async () => {
      const user = userEvent.setup({ delay: null });
      const mockData = createHealthyInfraData();
      vi.mocked(api.admin.getInfrastructureDetails).mockResolvedValue(mockData);

      render(<InfrastructureClient />);

      await waitFor(() => {
        expect(screen.getByText('PostgreSQL')).toBeInTheDocument();
      });

      const csvButton = screen.getByRole('button', { name: /csv/i });
      await user.click(csvButton);

      expect(global.URL.createObjectURL).toHaveBeenCalled();
      expect(global.URL.revokeObjectURL).toHaveBeenCalled();
    });
  });

  // ==================== Accessibility ====================

  describe('Accessibility', () => {
    it('should have proper ARIA labels', async () => {
      const mockData = createHealthyInfraData();
      vi.mocked(api.admin.getInfrastructureDetails).mockResolvedValue(mockData);

      render(<InfrastructureClient />);

      await waitFor(() => {
        expect(screen.getByText('PostgreSQL')).toBeInTheDocument();
      });

      // Check for role="list"
      expect(screen.getByRole('list', { name: /stato servizi/i })).toBeInTheDocument();

      // Check for service cards
      const serviceCards = screen.getAllByRole('listitem');
      expect(serviceCards.length).toBeGreaterThan(0);
    });
  });
});
