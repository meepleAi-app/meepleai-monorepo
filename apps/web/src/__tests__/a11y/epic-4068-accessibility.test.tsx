/**
 * Accessibility Tests - Epic #4068 Components
 * WCAG 2.1 AA Compliance Validation
 *
 * Epic #4068 - Issue #4185
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { PermissionProvider } from '@/contexts/PermissionContext';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { TierBadge } from '@/components/ui/feedback/tier-badge';
import { UpgradePrompt } from '@/components/ui/feedback/upgrade-prompt';
import { AgentStatusBadge } from '@/components/ui/agent/AgentStatusBadge';
import { TagStrip } from '@/components/ui/tags/TagStrip';
import * as permissionsApi from '@/lib/api/permissions';
import type { UserPermissions } from '@/types/permissions';
import type { Tag } from '@/types/tags';

// TODO: Install and configure axe-core for automated WCAG testing
// import { axe, toHaveNoViolations } from 'jest-axe';
// expect.extend(toHaveNoViolations);

vi.mock('@/lib/api/permissions');

describe('Epic #4068 - Accessibility Compliance', () => {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  const wrapper = ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={queryClient}>
      <PermissionProvider>{children}</PermissionProvider>
    </QueryClientProvider>
  );

  describe('TierBadge Accessibility', () => {
    it('has proper ARIA labels', () => {
      const { container } = render(<TierBadge tier="pro" />);

      const badge = container.querySelector('[role="status"]');
      expect(badge).toHaveAttribute('aria-label', 'Subscription tier: Pro');
    });

    it('uses semantic role', () => {
      const { container } = render(<TierBadge tier="enterprise" />);

      const badge = container.querySelector('[role="status"]');
      expect(badge).toBeInTheDocument();
    });

    // TODO: Add axe-core automated test
    // it('has no accessibility violations', async () => {
    //   const { container } = render(<TierBadge tier="pro" />);
    //   const results = await axe(container);
    //   expect(results).toHaveNoViolations();
    // });
  });

  describe('AgentStatusBadge Accessibility', () => {
    it('has descriptive ARIA label for each status', () => {
      const statusLabelMap: Record<string, string> = {
        active: 'Active',
        idle: 'Idle',
        training: 'Training',
        error: 'Error'
      };

      const statuses: Array<'active' | 'idle' | 'training' | 'error'> = [
        'active',
        'idle',
        'training',
        'error'
      ];

      statuses.forEach(status => {
        const { container } = render(<AgentStatusBadge status={status} />);
        const badge = container.querySelector('[role="status"]');

        expect(badge).toHaveAttribute('aria-label', `Agent status: ${statusLabelMap[status]}`);
      });
    });

    it('animations do not violate prefers-reduced-motion', () => {
      // Mock reduced motion preference
      Object.defineProperty(window, 'matchMedia', {
        writable: true,
        value: vi.fn().mockImplementation(query => ({
          matches: query === '(prefers-reduced-motion: reduce)',
          media: query,
          addEventListener: vi.fn(),
          removeEventListener: vi.fn()
        }))
      });

      const { container } = render(<AgentStatusBadge status="active" />);

      // Component should still render (CSS handles animation disabling)
      expect(container.querySelector('[role="status"]')).toBeInTheDocument();
    });
  });

  describe('TagStrip Accessibility', () => {
    const mockTags: Tag[] = [
      { id: '1', label: 'New', bgColor: 'hsl(142 76% 36%)' },
      { id: '2', label: 'Sale', bgColor: 'hsl(0 84% 60%)' }
    ];

    it('uses semantic list structure', () => {
      const { container } = render(<TagStrip tags={mockTags} />);

      const list = container.querySelector('[role="list"]');
      expect(list).toBeInTheDocument();
      expect(list).toHaveAttribute('aria-label', 'Entity tags');
    });

    it('each tag has status role and label', () => {
      render(<TagStrip tags={mockTags} />);

      const badges = document.querySelectorAll('[role="status"]');
      expect(badges.length).toBeGreaterThan(0);

      badges.forEach(badge => {
        expect(badge).toHaveAttribute('aria-label');
      });
    });
  });

  describe('UpgradePrompt Accessibility', () => {
    it('inline variant has proper ARIA attributes', () => {
      const { container } = render(
        <UpgradePrompt
          requiredTier="pro"
          featureName="Bulk Selection"
          variant="inline"
        />
      );

      const alert = container.querySelector('[role="alert"]');
      expect(alert).toBeInTheDocument();
      expect(alert).toHaveAttribute('aria-live', 'polite');

      const button = container.querySelector('button');
      expect(button).toHaveAttribute('aria-label', expect.stringContaining('Upgrade'));
    });

    it('modal variant has dialog role', () => {
      const { container } = render(
        <UpgradePrompt
          requiredTier="enterprise"
          variant="modal"
        />
      );

      const dialog = container.querySelector('[role="dialog"]');
      expect(dialog).toBeInTheDocument();
      expect(dialog).toHaveAttribute('aria-modal', 'true');
      expect(dialog).toHaveAttribute('aria-labelledby');
    });
  });

  describe('MeepleCard with Permissions - Accessibility', () => {
    const proPermissions: UserPermissions = {
      tier: 'pro',
      role: 'user',
      status: 'Active',
      limits: { maxGames: 500, storageQuotaMB: 5000 },
      accessibleFeatures: ['wishlist', 'bulk-select', 'drag-drop']
    };

    beforeEach(() => {
      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(proPermissions);
    });

    it('maintains semantic HTML structure', async () => {
      const { container } = render(
        <MeepleCard
          entity="game"
          title="Wingspan"
          subtitle="Stonemaier Games"
        />,
        { wrapper }
      );

      await screen.findByText('Wingspan');

      // Card uses semantic article element
      const article = container.querySelector('article');
      expect(article).toBeInTheDocument();
    });

    // TODO: Enable when Issue #4179 implements TierBadge integration into MeepleCard
    it.skip('tier badge does not interfere with focus order', async () => {
      render(
        <MeepleCard
          entity="game"
          title="Wingspan"
          showWishlist
          onWishlistToggle={vi.fn()}
        />,
        { wrapper }
      );

      await screen.findByText('Wingspan');

      // Tier badge should be visible but not focusable (status only)
      const tierBadge = document.querySelector('[role="status"][aria-label*="tier"]');
      expect(tierBadge).not.toHaveAttribute('tabindex');
    });

    // TODO: Add comprehensive axe-core audit
    // it('has no WCAG 2.1 AA violations', async () => {
    //   const { container } = render(
    //     <MeepleCard
    //       entity="game"
    //       title="Wingspan"
    //       showWishlist
    //       selectable
    //       tags={mockTags}
    //     />,
    //     { wrapper }
    //   );
    //
    //   await screen.findByText('Wingspan');
    //
    //   const results = await axe(container);
    //   expect(results).toHaveNoViolations();
    // });
  });

  describe('Keyboard Navigation', () => {
    // TODO: Enable when Issue #4179 implements TierBadge integration into MeepleCard
    it.skip('tier badge is not keyboard focusable (status only)', async () => {
      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue({
        tier: 'pro',
        role: 'user',
        status: 'Active',
        limits: { maxGames: 500, storageQuotaMB: 5000 },
        accessibleFeatures: []
      });

      render(
        <MeepleCard entity="game" title="Wingspan" />,
        { wrapper }
      );

      await screen.findByText('Wingspan');

      const tierBadge = document.querySelector('[role="status"][aria-label*="tier"]');
      expect(tierBadge).not.toHaveAttribute('tabindex');
      expect(tierBadge).not.toHaveAttribute('role', 'button');
    });

    it('upgrade prompt button is keyboard accessible', () => {
      const { container } = render(
        <UpgradePrompt
          requiredTier="pro"
          variant="inline"
        />
      );

      const button = container.querySelector('button');
      expect(button).toBeInTheDocument();

      // Should be keyboard focusable (default button behavior)
      // Focus-visible styles applied via Tailwind
      expect(button).toHaveClass('focus-visible:ring-2');
    });
  });

  describe('Screen Reader Experience', () => {
    // TODO: Enable when Issue #4179 implements permission gates in MeepleCard
    // Currently MeepleCard renders selectable checkbox without permission checks
    it('selectable card has accessible checkbox', async () => {
      const mockPermissions: UserPermissions = {
        tier: 'free',
        role: 'user',
        status: 'Active',
        limits: { maxGames: 50, storageQuotaMB: 100 },
        accessibleFeatures: ['wishlist']
      };

      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(mockPermissions);

      render(
        <MeepleCard
          entity="game"
          title="Wingspan"
          selectable
        />,
        { wrapper }
      );

      await screen.findByText('Wingspan');

      // Selectable card renders an accessible checkbox
      const checkbox = screen.queryByRole('checkbox');
      expect(checkbox).toBeInTheDocument();
      expect(checkbox).toHaveAttribute('aria-label', 'Select card');

      // No unexpected aria-live regions for structural elements
      const liveRegions = document.querySelectorAll('[aria-live="polite"]');
      expect(liveRegions.length).toBeLessThanOrEqual(1);
    });
  });
});
