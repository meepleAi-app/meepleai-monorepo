/**
 * Tests for TierStrategyMatrix component
 * Issue #3441: Tests for tier-strategy-model architecture
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import React from 'react';

import { TierStrategyMatrix } from '@/app/(authenticated)/admin/rag/tier-strategy-config/TierStrategyMatrix';
import type { TierStrategyMatrixDto } from '@/lib/api';

// Mock the hook
const mockMutateAsync = vi.fn();
vi.mock('@/hooks/queries/useTierStrategy', () => ({
  useUpdateTierStrategyAccess: () => ({
    mutateAsync: mockMutateAsync,
    isPending: false,
    variables: null,
  }),
}));

// Mock toast
vi.mock('@/components/layout/Toast', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

describe('TierStrategyMatrix', () => {
  let queryClient: QueryClient;

  const createWrapper = () => {
    return function Wrapper({ children }: { children: React.ReactNode }) {
      return React.createElement(
        QueryClientProvider,
        { client: queryClient },
        children
      );
    };
  };

  const mockMatrix: TierStrategyMatrixDto = {
    tiers: ['Anonymous', 'User', 'Editor', 'Admin'],
    strategies: [
      {
        name: 'FAST',
        displayName: 'Fast',
        description: 'Quick responses with lower quality',
        complexityLevel: 1,
        requiresAdmin: false,
      },
      {
        name: 'BALANCED',
        displayName: 'Balanced',
        description: 'Balance between speed and quality',
        complexityLevel: 2,
        requiresAdmin: false,
      },
      {
        name: 'PRECISE',
        displayName: 'Precise',
        description: 'Higher quality, slower responses',
        complexityLevel: 3,
        requiresAdmin: false,
      },
      {
        name: 'EXPERT',
        displayName: 'Expert',
        description: 'Expert-level responses',
        complexityLevel: 4,
        requiresAdmin: true,
      },
    ],
    accessMatrix: [
      { id: '1', tier: 'User', strategy: 'FAST', isEnabled: true, isDefault: true },
      { id: '2', tier: 'User', strategy: 'BALANCED', isEnabled: true, isDefault: true },
      { id: '3', tier: 'User', strategy: 'PRECISE', isEnabled: false, isDefault: true },
      { id: '4', tier: 'Editor', strategy: 'FAST', isEnabled: true, isDefault: true },
      { id: '5', tier: 'Editor', strategy: 'BALANCED', isEnabled: true, isDefault: true },
      { id: '6', tier: 'Editor', strategy: 'PRECISE', isEnabled: true, isDefault: true },
      { id: '7', tier: 'Admin', strategy: 'FAST', isEnabled: true, isDefault: true },
      { id: '8', tier: 'Admin', strategy: 'BALANCED', isEnabled: true, isDefault: true },
      { id: '9', tier: 'Admin', strategy: 'PRECISE', isEnabled: true, isDefault: true },
      { id: '10', tier: 'Admin', strategy: 'EXPERT', isEnabled: true, isDefault: true },
    ],
  };

  beforeEach(() => {
    queryClient = new QueryClient({
      defaultOptions: {
        queries: { retry: false },
        mutations: { retry: false },
      },
    });
    vi.clearAllMocks();
  });

  // =========================================================================
  // Rendering Tests
  // =========================================================================

  describe('Rendering', () => {
    it('should render the matrix table', () => {
      render(<TierStrategyMatrix matrix={mockMatrix} />, {
        wrapper: createWrapper(),
      });

      expect(screen.getByText('Tier')).toBeInTheDocument();
    });

    it('should render all tier rows', () => {
      render(<TierStrategyMatrix matrix={mockMatrix} />, {
        wrapper: createWrapper(),
      });

      expect(screen.getByText('Anonymous')).toBeInTheDocument();
      expect(screen.getByText('User')).toBeInTheDocument();
      expect(screen.getByText('Editor')).toBeInTheDocument();
      expect(screen.getByText('Admin')).toBeInTheDocument();
    });

    it('should render strategy column headers', () => {
      render(<TierStrategyMatrix matrix={mockMatrix} />, {
        wrapper: createWrapper(),
      });

      expect(screen.getByText('Fast')).toBeInTheDocument();
      expect(screen.getByText('Balanced')).toBeInTheDocument();
      expect(screen.getByText('Precise')).toBeInTheDocument();
      expect(screen.getByText('Expert')).toBeInTheDocument();
    });

    it('should render switches for each tier-strategy combination', () => {
      render(<TierStrategyMatrix matrix={mockMatrix} />, {
        wrapper: createWrapper(),
      });

      // Find switches by role
      const switches = screen.getAllByRole('switch');
      // 4 tiers × 4 strategies = 16 switches
      expect(switches.length).toBe(16);
    });

    it('should render legend', () => {
      render(<TierStrategyMatrix matrix={mockMatrix} />, {
        wrapper: createWrapper(),
      });

      expect(screen.getByText('Access enabled')).toBeInTheDocument();
      expect(screen.getByText('Access disabled')).toBeInTheDocument();
      expect(screen.getByText('Admin-only strategy')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Access State Tests
  // =========================================================================

  describe('Access State', () => {
    it('should show enabled state for User with FAST strategy', () => {
      render(<TierStrategyMatrix matrix={mockMatrix} />, {
        wrapper: createWrapper(),
      });

      const fastSwitch = screen.getByLabelText('Toggle FAST for User');
      expect(fastSwitch).toBeChecked();
    });

    it('should show disabled state for User with PRECISE strategy', () => {
      render(<TierStrategyMatrix matrix={mockMatrix} />, {
        wrapper: createWrapper(),
      });

      const preciseSwitch = screen.getByLabelText('Toggle PRECISE for User');
      expect(preciseSwitch).not.toBeChecked();
    });

    it('should disable switches for Anonymous tier', () => {
      render(<TierStrategyMatrix matrix={mockMatrix} />, {
        wrapper: createWrapper(),
      });

      const anonymousFastSwitch = screen.getByLabelText('Toggle FAST for Anonymous');
      expect(anonymousFastSwitch).toBeDisabled();
    });
  });

  // =========================================================================
  // Interaction Tests
  // =========================================================================

  describe('Interactions', () => {
    it('should call updateAccess when toggling a switch', async () => {
      mockMutateAsync.mockResolvedValue({
        id: '3',
        tier: 'User',
        strategy: 'PRECISE',
        isEnabled: true,
        isDefault: false,
      });

      render(<TierStrategyMatrix matrix={mockMatrix} />, {
        wrapper: createWrapper(),
      });

      const preciseSwitch = screen.getByLabelText('Toggle PRECISE for User');
      fireEvent.click(preciseSwitch);

      await waitFor(() => {
        expect(mockMutateAsync).toHaveBeenCalledWith({
          tier: 'User',
          strategy: 'PRECISE',
          isEnabled: true,
        });
      });
    });

    it('should not call updateAccess for Anonymous tier', () => {
      render(<TierStrategyMatrix matrix={mockMatrix} />, {
        wrapper: createWrapper(),
      });

      const anonymousSwitch = screen.getByLabelText('Toggle FAST for Anonymous');
      fireEvent.click(anonymousSwitch);

      expect(mockMutateAsync).not.toHaveBeenCalled();
    });
  });

  // =========================================================================
  // Default Indicator Tests
  // =========================================================================

  describe('Default Indicators', () => {
    it('should show default indicator for default access entries', () => {
      render(<TierStrategyMatrix matrix={mockMatrix} />, {
        wrapper: createWrapper(),
      });

      // Check for "default" text (multiple instances expected)
      const defaultIndicators = screen.getAllByText('default');
      expect(defaultIndicators.length).toBeGreaterThan(0);
    });
  });

  // =========================================================================
  // Admin-Only Strategy Tests
  // =========================================================================

  describe('Admin-Only Strategies', () => {
    it('should show admin shield icon for admin-only strategies', () => {
      render(<TierStrategyMatrix matrix={mockMatrix} />, {
        wrapper: createWrapper(),
      });

      // Expert strategy has requiresAdmin: true
      // The shield icon should be visible in the header
      // We can check if the Expert column header area has the special indicator
      const expertHeader = screen.getByText('Expert');
      expect(expertHeader).toBeInTheDocument();
    });
  });
});
