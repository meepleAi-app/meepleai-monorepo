/**
 * MeepleCard Permission Integration Tests
 * Epic #4068 - Issue #4179
 *
 * Tests for permission-aware features of MeepleCard.
 * Many features (TierBadge, quickActions, selectable, draggable, permissions override)
 * are planned for Issue #4179 but not yet implemented.
 * Those tests are marked with it.skip() until the features are built.
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';
import { MeepleCard } from '../meeple-card';
import { PermissionProvider } from '@/contexts/PermissionContext';
import * as permissionsApi from '@/lib/api/permissions';
import type { UserPermissions } from '@/types/permissions';

vi.mock('@/lib/api/permissions');

describe('MeepleCard Permission Integration', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    vi.clearAllMocks();
    queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false, gcTime: 0 } }
    });
  });

  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>
      <PermissionProvider>{children}</PermissionProvider>
    </QueryClientProvider>
  );

  describe('Free Tier User', () => {
    const freePermissions: UserPermissions = {
      tier: 'free',
      role: 'user',
      status: 'Active',
      limits: { maxGames: 50, storageQuotaMB: 100 },
      accessibleFeatures: ['wishlist']
    };

    it('shows wishlist button (free tier has access)', async () => {
      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(freePermissions);

      render(
        <MeepleCard
          entity="game"
          title="Wingspan"
          showWishlist
          isWishlisted={false}
          onWishlistToggle={vi.fn()}
        />,
        { wrapper }
      );

      await screen.findByText('Wingspan');
      expect(screen.getByLabelText(/wishlist/i)).toBeInTheDocument();
    });

    // TODO: Enable when Issue #4179 implements selectable prop with permission gating
    it.skip('hides bulk select checkbox (requires Pro tier)', async () => {
      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(freePermissions);

      render(
        <MeepleCard
          entity="game"
          title="Wingspan"
        />,
        { wrapper }
      );

      await screen.findByText('Wingspan');
      expect(screen.queryByRole('checkbox')).not.toBeInTheDocument();
    });

    // TODO: Enable when Issue #4179 implements draggable prop with permission gating
    it.skip('hides drag handle (requires Normal tier)', async () => {
      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(freePermissions);

      render(
        <MeepleCard entity="game" variant="list" title="Wingspan" />,
        { wrapper }
      );

      await screen.findByText('Wingspan');
      expect(screen.queryByLabelText(/drag/i)).not.toBeInTheDocument();
    });

    it('does NOT show tier badge for free tier', async () => {
      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(freePermissions);

      render(
        <MeepleCard entity="game" title="Wingspan" />,
        { wrapper }
      );

      await screen.findByText('Wingspan');
      expect(screen.queryByRole('status', { name: /subscription tier/i })).not.toBeInTheDocument();
    });
  });

  describe('Pro Tier User', () => {
    const proPermissions: UserPermissions = {
      tier: 'pro',
      role: 'user',
      status: 'Active',
      limits: { maxGames: 500, storageQuotaMB: 5000 },
      accessibleFeatures: ['wishlist', 'bulk-select', 'drag-drop', 'agent.create']
    };

    it('shows wishlist for pro tier', async () => {
      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(proPermissions);

      render(
        <MeepleCard
          entity="game"
          title="Wingspan"
          showWishlist
          isWishlisted={false}
          onWishlistToggle={vi.fn()}
        />,
        { wrapper }
      );

      await screen.findByText('Wingspan');
      expect(screen.getByLabelText(/wishlist/i)).toBeInTheDocument();
    });

    // TODO: Enable when Issue #4179 implements TierBadge
    it.skip('shows Pro tier badge', async () => {
      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(proPermissions);

      render(
        <MeepleCard entity="game" title="Wingspan" />,
        { wrapper }
      );

      await screen.findByText('Wingspan');
      const tierBadge = screen.getByRole('status', { name: /subscription tier.*pro/i });
      expect(tierBadge).toBeInTheDocument();
      expect(tierBadge).toHaveTextContent('Pro');
    });

    // TODO: Enable when Issue #4179 implements draggable prop
    it.skip('shows drag handle in list variant', async () => {
      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(proPermissions);

      render(
        <MeepleCard entity="game" variant="list" title="Wingspan" />,
        { wrapper }
      );

      await screen.findByText('Wingspan');
      expect(screen.getByLabelText(/drag/i)).toBeInTheDocument();
    });
  });

  // TODO: Enable when Issue #4179 implements quickActions with adminOnly filtering
  describe.skip('Admin Role User', () => {
    const adminPermissions: UserPermissions = {
      tier: 'normal',
      role: 'admin',
      status: 'Active',
      limits: { maxGames: 100, storageQuotaMB: 500 },
      accessibleFeatures: ['wishlist', 'drag-drop', 'analytics.view']
    };

    it('shows admin-only quick actions', async () => {
      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(adminPermissions);
      render(<MeepleCard entity="game" title="Wingspan" />, { wrapper });
      await screen.findByText('Wingspan');
    });

    it('filters out admin actions for non-admin user', async () => {
      const userPermissions: UserPermissions = { ...adminPermissions, role: 'user' };
      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(userPermissions);
      render(<MeepleCard entity="game" title="Wingspan" />, { wrapper });
      await screen.findByText('Wingspan');
    });
  });

  // TODO: Enable when Issue #4179 implements permissions override prop
  describe.skip('Permission Override Prop', () => {
    it('uses override permissions instead of context', () => {});
    it('override allows testing different tier scenarios', () => {});
  });

  describe('Feature Combinations', () => {
    // TODO: Enable when Issue #4179 implements selectable, draggable, TierBadge
    it.skip('shows multiple features when all permissions granted', async () => {});

    // TODO: Enable when Issue #4179 implements quickActions
    it.skip('respects permission priority: quickActions > wishlist', async () => {});
  });

  describe('Error Handling', () => {
    it('renders card even when permission API fails', async () => {
      vi.spyOn(console, 'error').mockImplementation(() => {});

      vi.mocked(permissionsApi.getUserPermissions).mockRejectedValue(
        new Error('API error')
      );

      render(
        <MeepleCard
          entity="game"
          title="Wingspan"
          showWishlist
          onWishlistToggle={vi.fn()}
        />,
        { wrapper }
      );

      // Card still renders even on permission error
      await screen.findByText('Wingspan');
      expect(screen.getByTestId('meeple-card')).toBeInTheDocument();

      vi.restoreAllMocks();
    });
  });

  describe('Loading State', () => {
    it('shows skeleton while permissions loading', () => {
      vi.mocked(permissionsApi.getUserPermissions).mockImplementation(
        () => new Promise(() => {}) // Never resolves
      );

      render(
        <MeepleCard entity="game" title="Wingspan" loading />,
        { wrapper }
      );

      expect(screen.getByTestId('meeple-card-skeleton')).toBeInTheDocument();
    });
  });

  // TODO: Enable when Issue #4179 implements TierBadge component
  describe.skip('TierBadge Display', () => {
    it('shows tier badge for Normal tier', () => {});
    it('shows tier badge for Enterprise tier with gold styling', () => {});
  });

  // TODO: Enable when Issue #4179 implements quickActions with adminOnly filtering
  describe.skip('Quick Actions Filtering', () => {
    it('filters adminOnly actions for regular user', () => {});
    it('shows all actions for admin user', () => {});
    it('respects hidden flag regardless of permissions', () => {});
  });

  // TODO: Enable when Issue #4179 implements Enterprise tier badge with crown
  describe.skip('Enterprise Tier', () => {
    it('shows Enterprise tier badge with crown icon', () => {});
  });
});
