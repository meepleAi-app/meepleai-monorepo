/**
 * MeepleCard Permission Integration Tests
 * Epic #4068 - Issue #4179
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';
import { MeepleCard } from '../meeple-card';
import { PermissionProvider } from '@/contexts/PermissionContext';
import * as permissionsApi from '@/lib/api/permissions';
import type { UserPermissions } from '@/types/permissions';

vi.mock('@/lib/api/permissions');

describe('MeepleCard Permission Integration', () => {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } }
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

      // Wait for permissions to load
      await screen.findByText('Wingspan');

      // Wishlist button visible (free tier can access)
      expect(screen.getByLabelText(/wishlist/i)).toBeInTheDocument();
    });

    it('hides bulk select checkbox (requires Pro tier)', async () => {
      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(freePermissions);

      render(
        <MeepleCard
          entity="game"
          id="game-1"
          title="Wingspan"
          selectable
          selected={false}
          onSelect={vi.fn()}
        />,
        { wrapper }
      );

      await screen.findByText('Wingspan');

      // Bulk select hidden (free tier cannot access)
      expect(screen.queryByRole('checkbox')).not.toBeInTheDocument();
    });

    it('hides drag handle (requires Normal tier)', async () => {
      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(freePermissions);

      render(
        <MeepleCard
          entity="game"
          variant="list"
          title="Wingspan"
          draggable
          dragData={{ id: 'game-1', type: 'game', index: 0 }}
          onDragStart={vi.fn()}
        />,
        { wrapper }
      );

      await screen.findByText('Wingspan');

      // Drag handle hidden
      expect(screen.queryByLabelText(/drag/i)).not.toBeInTheDocument();
    });

    it('does NOT show tier badge for free tier', async () => {
      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(freePermissions);

      render(
        <MeepleCard entity="game" title="Wingspan" />,
        { wrapper }
      );

      await screen.findByText('Wingspan');

      // No tier badge for free tier
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

    it('shows all standard features', async () => {
      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(proPermissions);

      render(
        <MeepleCard
          entity="game"
          id="game-1"
          title="Wingspan"
          showWishlist
          isWishlisted={false}
          onWishlistToggle={vi.fn()}
          selectable
          selected={false}
          onSelect={vi.fn()}
        />,
        { wrapper }
      );

      await screen.findByText('Wingspan');

      // Both wishlist and bulk select visible
      expect(screen.getByLabelText(/wishlist/i)).toBeInTheDocument();
      expect(screen.getByRole('checkbox')).toBeInTheDocument();
    });

    it('shows Pro tier badge', async () => {
      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(proPermissions);

      render(
        <MeepleCard entity="game" title="Wingspan" />,
        { wrapper }
      );

      await screen.findByText('Wingspan');

      // Pro tier badge visible
      const tierBadge = screen.getByRole('status', { name: /subscription tier.*pro/i });
      expect(tierBadge).toBeInTheDocument();
      expect(tierBadge).toHaveTextContent('Pro');
    });

    it('shows drag handle in list variant', async () => {
      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(proPermissions);

      render(
        <MeepleCard
          entity="game"
          variant="list"
          title="Wingspan"
          draggable
          dragData={{ id: 'game-1', type: 'game', index: 0 }}
          onDragStart={vi.fn()}
        />,
        { wrapper }
      );

      await screen.findByText('Wingspan');

      // Drag handle visible (pro can access drag-drop)
      expect(screen.getByLabelText(/drag/i)).toBeInTheDocument();
    });
  });

  describe('Admin Role User', () => {
    const adminPermissions: UserPermissions = {
      tier: 'normal',
      role: 'admin',
      status: 'Active',
      limits: { maxGames: 100, storageQuotaMB: 500 },
      accessibleFeatures: ['wishlist', 'drag-drop', 'analytics.view']
    };

    it('shows admin-only quick actions', async () => {
      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(adminPermissions);

      render(
        <MeepleCard
          entity="game"
          title="Wingspan"
          quickActions={[
            { icon: vi.fn(), label: 'Edit', onClick: vi.fn(), adminOnly: false },
            { icon: vi.fn(), label: 'Delete', onClick: vi.fn(), adminOnly: true }
          ]}
        />,
        { wrapper }
      );

      await screen.findByText('Wingspan');

      // Both actions visible (admin can see adminOnly actions)
      expect(screen.getByLabelText(/edit/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/delete/i)).toBeInTheDocument();
    });

    it('filters out admin actions for non-admin user', async () => {
      const userPermissions: UserPermissions = {
        ...adminPermissions,
        role: 'user'
      };

      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(userPermissions);

      render(
        <MeepleCard
          entity="game"
          title="Wingspan"
          quickActions={[
            { icon: vi.fn(), label: 'Edit', onClick: vi.fn(), adminOnly: false },
            { icon: vi.fn(), label: 'Delete', onClick: vi.fn(), adminOnly: true }
          ]}
        />,
        { wrapper }
      );

      await screen.findByText('Wingspan');

      // Only Edit visible (user cannot see Delete)
      expect(screen.getByLabelText(/edit/i)).toBeInTheDocument();
      expect(screen.queryByLabelText(/delete/i)).not.toBeInTheDocument();
    });
  });

  describe('Permission Override Prop', () => {
    it('uses override permissions instead of context', async () => {
      // Context has Pro tier
      const contextPermissions: UserPermissions = {
        tier: 'pro',
        role: 'user',
        status: 'Active',
        limits: { maxGames: 500, storageQuotaMB: 5000 },
        accessibleFeatures: ['wishlist', 'bulk-select']
      };

      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(contextPermissions);

      // Override to Free tier (for testing/preview)
      const overridePermissions = {
        tier: 'free' as const,
        role: 'user' as const,
        canAccess: (feature: string) => feature === 'wishlist',
        isAdmin: () => false
      };

      render(
        <MeepleCard
          entity="game"
          id="game-1"
          title="Wingspan"
          selectable
          selected={false}
          onSelect={vi.fn()}
          permissions={overridePermissions}
        />,
        { wrapper }
      );

      await screen.findByText('Wingspan');

      // Uses override (free tier) NOT context (pro tier)
      expect(screen.queryByRole('checkbox')).not.toBeInTheDocument(); // Free cannot bulk select
      expect(screen.queryByRole('status', { name: /tier/i })).not.toBeInTheDocument(); // No badge for free
    });

    it('override allows testing different tier scenarios', async () => {
      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue({
        tier: 'free',
        role: 'user',
        status: 'Active',
        limits: { maxGames: 50, storageQuotaMB: 100 },
        accessibleFeatures: ['wishlist']
      });

      const enterpriseOverride = {
        tier: 'enterprise' as const,
        role: 'admin' as const,
        canAccess: () => true, // All features
        isAdmin: () => true
      };

      render(
        <MeepleCard
          entity="game"
          title="Wingspan"
          permissions={enterpriseOverride}
        />,
        { wrapper }
      );

      await screen.findByText('Wingspan');

      // Uses override (enterprise)
      const tierBadge = screen.getByRole('status', { name: /subscription tier/i });
      expect(tierBadge).toHaveTextContent('Enterprise');
    });
  });

  describe('Feature Combinations', () => {
    it('shows multiple features when all permissions granted', async () => {
      const allAccessPermissions: UserPermissions = {
        tier: 'enterprise',
        role: 'admin',
        status: 'Active',
        limits: { maxGames: 2147483647, storageQuotaMB: 2147483647 },
        accessibleFeatures: ['wishlist', 'bulk-select', 'drag-drop', 'agent.create', 'analytics.view']
      };

      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(allAccessPermissions);

      render(
        <MeepleCard
          entity="game"
          id="game-1"
          variant="list"
          title="Wingspan"
          showWishlist
          isWishlisted={false}
          onWishlistToggle={vi.fn()}
          selectable
          selected={false}
          onSelect={vi.fn()}
          draggable
          dragData={{ id: 'game-1', type: 'game', index: 0 }}
          onDragStart={vi.fn()}
        />,
        { wrapper }
      );

      await screen.findByText('Wingspan');

      // All features visible
      expect(screen.getByLabelText(/wishlist/i)).toBeInTheDocument();
      expect(screen.getByRole('checkbox')).toBeInTheDocument();
      expect(screen.getByLabelText(/drag/i)).toBeInTheDocument();
      expect(screen.getByRole('status', { name: /tier.*enterprise/i })).toBeInTheDocument();
    });

    it('respects permission priority: quickActions > wishlist', async () => {
      const proPermissions: UserPermissions = {
        tier: 'pro',
        role: 'user',
        status: 'Active',
        limits: { maxGames: 500, storageQuotaMB: 5000 },
        accessibleFeatures: ['wishlist']
      };

      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(proPermissions);

      render(
        <MeepleCard
          entity="game"
          title="Wingspan"
          showWishlist
          onWishlistToggle={vi.fn()}
          quickActions={[
            { icon: vi.fn(), label: 'View', onClick: vi.fn() }
          ]}
        />,
        { wrapper }
      );

      await screen.findByText('Wingspan');

      // QuickActions visible, wishlist hidden (priority)
      expect(screen.getByLabelText(/view/i)).toBeInTheDocument();
      expect(screen.queryByLabelText(/wishlist/i)).not.toBeInTheDocument();
    });
  });

  describe('Error Handling', () => {
    it('falls back to safe defaults (no features) on permission load error', async () => {
      const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});

      vi.mocked(permissionsApi.getUserPermissions).mockRejectedValue(
        new Error('API error')
      );

      render(
        <MeepleCard
          entity="game"
          id="game-1"
          title="Wingspan"
          showWishlist
          selectable
          draggable
          onWishlistToggle={vi.fn()}
          onSelect={vi.fn()}
          dragData={{ id: 'game-1', type: 'game', index: 0 }}
        />,
        { wrapper }
      );

      await screen.findByText('Wingspan');

      // All features hidden (safe default: deny all)
      expect(screen.queryByLabelText(/wishlist/i)).not.toBeInTheDocument();
      expect(screen.queryByRole('checkbox')).not.toBeInTheDocument();
      expect(screen.queryByLabelText(/drag/i)).not.toBeInTheDocument();

      consoleSpy.mockRestore();
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

      // Skeleton shown
      expect(screen.getByTestId('meeple-card-skeleton')).toBeInTheDocument();
    });
  });

  describe('TierBadge Display', () => {
    it('shows tier badge for Normal tier', async () => {
      const normalPermissions: UserPermissions = {
        tier: 'normal',
        role: 'user',
        status: 'Active',
        limits: { maxGames: 100, storageQuotaMB: 500 },
        accessibleFeatures: ['wishlist', 'drag-drop']
      };

      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(normalPermissions);

      render(
        <MeepleCard entity="game" title="Wingspan" />,
        { wrapper }
      );

      await screen.findByText('Wingspan');

      const tierBadge = screen.getByRole('status', { name: /subscription tier/i });
      expect(tierBadge).toHaveTextContent('Normal');
    });

    it('shows tier badge for Enterprise tier with gold styling', async () => {
      const enterprisePermissions: UserPermissions = {
        tier: 'enterprise',
        role: 'user',
        status: 'Active',
        limits: { maxGames: 2147483647, storageQuotaMB: 2147483647 },
        accessibleFeatures: ['wishlist', 'bulk-select', 'drag-drop']
      };

      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(enterprisePermissions);

      render(
        <MeepleCard entity="game" title="Wingspan" />,
        { wrapper }
      );

      await screen.findByText('Wingspan');

      const tierBadge = screen.getByRole('status', { name: /subscription tier/i });
      expect(tierBadge).toHaveTextContent('Enterprise');
      // Gold color applied via inline styles
      expect(tierBadge).toHaveStyle({ color: 'hsl(38 92% 50%)' });
    });
  });

  describe('Quick Actions Filtering', () => {
    it('filters adminOnly actions for regular user', async () => {
      const userPermissions: UserPermissions = {
        tier: 'pro',
        role: 'user',
        status: 'Active',
        limits: { maxGames: 500, storageQuotaMB: 5000 },
        accessibleFeatures: ['wishlist', 'bulk-select']
      };

      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(userPermissions);

      render(
        <MeepleCard
          entity="game"
          title="Wingspan"
          quickActions={[
            { icon: vi.fn(), label: 'View', onClick: vi.fn(), adminOnly: false },
            { icon: vi.fn(), label: 'Edit', onClick: vi.fn(), adminOnly: false },
            { icon: vi.fn(), label: 'Delete', onClick: vi.fn(), adminOnly: true }, // Admin only
            { icon: vi.fn(), label: 'Publish', onClick: vi.fn(), adminOnly: true } // Admin only
          ]}
        />,
        { wrapper }
      );

      await screen.findByText('Wingspan');

      // Only non-admin actions visible
      expect(screen.getByLabelText(/view/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/edit/i)).toBeInTheDocument();
      expect(screen.queryByLabelText(/delete/i)).not.toBeInTheDocument();
      expect(screen.queryByLabelText(/publish/i)).not.toBeInTheDocument();
    });

    it('shows all actions for admin user', async () => {
      const adminPermissions: UserPermissions = {
        tier: 'normal',
        role: 'admin',
        status: 'Active',
        limits: { maxGames: 100, storageQuotaMB: 500 },
        accessibleFeatures: ['wishlist']
      };

      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(adminPermissions);

      render(
        <MeepleCard
          entity="game"
          title="Wingspan"
          quickActions={[
            { icon: vi.fn(), label: 'View', onClick: vi.fn(), adminOnly: false },
            { icon: vi.fn(), label: 'Delete', onClick: vi.fn(), adminOnly: true }
          ]}
        />,
        { wrapper }
      );

      await screen.findByText('Wingspan');

      // Both actions visible (admin sees all)
      expect(screen.getByLabelText(/view/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/delete/i)).toBeInTheDocument();
    });

    it('respects hidden flag regardless of permissions', async () => {
      const adminPermissions: UserPermissions = {
        tier: 'enterprise',
        role: 'admin',
        status: 'Active',
        limits: { maxGames: 2147483647, storageQuotaMB: 2147483647 },
        accessibleFeatures: []
      };

      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(adminPermissions);

      render(
        <MeepleCard
          entity="game"
          title="Wingspan"
          quickActions={[
            { icon: vi.fn(), label: 'View', onClick: vi.fn(), hidden: false },
            { icon: vi.fn(), label: 'Hidden', onClick: vi.fn(), hidden: true } // Explicitly hidden
          ]}
        />,
        { wrapper }
      );

      await screen.findByText('Wingspan');

      // Only View visible (hidden flag respected)
      expect(screen.getByLabelText(/view/i)).toBeInTheDocument();
      expect(screen.queryByLabelText(/hidden/i)).not.toBeInTheDocument();
    });
  });

  describe('Enterprise Tier', () => {
    it('shows Enterprise tier badge with crown icon', async () => {
      const enterprisePermissions: UserPermissions = {
        tier: 'enterprise',
        role: 'user',
        status: 'Active',
        limits: { maxGames: 2147483647, storageQuotaMB: 2147483647 },
        accessibleFeatures: ['wishlist', 'bulk-select', 'drag-drop', 'agent.create', 'analytics.view']
      };

      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(enterprisePermissions);

      render(
        <MeepleCard entity="game" title="Wingspan" />,
        { wrapper }
      );

      await screen.findByText('Wingspan');

      // Enterprise badge with crown
      const tierBadge = screen.getByRole('status', { name: /tier.*enterprise/i });
      expect(tierBadge).toBeInTheDocument();

      // Crown icon present
      const crown = tierBadge.querySelector('svg.lucide-crown');
      expect(crown).toBeInTheDocument();
    });
  });
});
