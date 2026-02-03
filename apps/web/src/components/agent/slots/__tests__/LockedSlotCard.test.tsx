/**
 * LockedSlotCard Unit Tests
 * Issue #3247 (FRONT-011)
 * Issue #3248 (FRONT-012) - Test coverage
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect, beforeEach } from 'vitest';

import { LockedSlotCard } from '../LockedSlotCard';

// Mock sonner toast
vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
    info: vi.fn(),
  },
}));

describe('LockedSlotCard', () => {
  const defaultProps = {
    lockedCount: 3,
    currentTier: 'free',
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('should render locked slot count correctly', () => {
      render(<LockedSlotCard {...defaultProps} />);

      expect(screen.getByText('3 Locked Slots')).toBeInTheDocument();
    });

    it('should render singular form for 1 slot', () => {
      render(<LockedSlotCard {...defaultProps} lockedCount={1} />);

      expect(screen.getByText('1 Locked Slot')).toBeInTheDocument();
    });

    it('should display all premium benefits', () => {
      render(<LockedSlotCard {...defaultProps} />);

      expect(screen.getByText('+3 additional agent slots')).toBeInTheDocument();
      expect(screen.getByText('Priority model access')).toBeInTheDocument();
      expect(screen.getByText('Extended token quota')).toBeInTheDocument();
      expect(screen.getByText('Priority support')).toBeInTheDocument();
    });

    it('should display current tier badge', () => {
      render(<LockedSlotCard {...defaultProps} currentTier="free" />);

      expect(screen.getByText('free')).toBeInTheDocument();
    });

    it('should render upgrade button with sparkles icon', () => {
      render(<LockedSlotCard {...defaultProps} />);

      const upgradeButton = screen.getByRole('button', { name: /upgrade/i });
      expect(upgradeButton).toBeInTheDocument();
      expect(upgradeButton).toHaveTextContent('Upgrade to Premium');
    });

    it('should have correct aria-label for accessibility', () => {
      render(<LockedSlotCard {...defaultProps} />);

      expect(screen.getByRole('region', { name: /premium upgrade/i })).toBeInTheDocument();
    });
  });

  describe('Upgrade Button Interaction', () => {
    it('should open pricing modal on upgrade click', async () => {
      const user = userEvent.setup();
      render(<LockedSlotCard {...defaultProps} />);

      const upgradeButton = screen.getByRole('button', { name: /upgrade/i });
      await user.click(upgradeButton);

      // Modal should be visible
      await waitFor(() => {
        expect(screen.getByText('Premium Tier Coming Soon!')).toBeInTheDocument();
      });
    });

    it('should call onUpgradeClick callback', async () => {
      const user = userEvent.setup();
      const onUpgradeClick = vi.fn();

      render(<LockedSlotCard {...defaultProps} onUpgradeClick={onUpgradeClick} />);

      const upgradeButton = screen.getByRole('button', { name: /upgrade/i });
      await user.click(upgradeButton);

      expect(onUpgradeClick).toHaveBeenCalledTimes(1);
    });
  });

  describe('Pricing Modal', () => {
    it('should display pricing information', async () => {
      const user = userEvent.setup();
      render(<LockedSlotCard {...defaultProps} />);

      const upgradeButton = screen.getByRole('button', { name: /upgrade/i });
      await user.click(upgradeButton);

      await waitFor(() => {
        expect(screen.getByText('€9.99')).toBeInTheDocument();
        expect(screen.getByText('/month')).toBeInTheDocument();
      });
    });

    it('should display benefits in modal', async () => {
      const user = userEvent.setup();
      render(<LockedSlotCard {...defaultProps} />);

      const upgradeButton = screen.getByRole('button', { name: /upgrade/i });
      await user.click(upgradeButton);

      await waitFor(() => {
        // Benefits should be listed in modal too
        const benefitItems = screen.getAllByText(/additional|Priority|Extended/);
        expect(benefitItems.length).toBeGreaterThan(0);
      });
    });

    it('should have waitlist button', async () => {
      const user = userEvent.setup();
      render(<LockedSlotCard {...defaultProps} />);

      const upgradeButton = screen.getByRole('button', { name: /upgrade/i });
      await user.click(upgradeButton);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /join waitlist/i })).toBeInTheDocument();
      });
    });

    it('should close modal on waitlist button click', async () => {
      const user = userEvent.setup();
      render(<LockedSlotCard {...defaultProps} />);

      // Open modal
      const upgradeButton = screen.getByRole('button', { name: /upgrade/i });
      await user.click(upgradeButton);

      // Click waitlist
      const waitlistButton = await screen.findByRole('button', { name: /join waitlist/i });
      await user.click(waitlistButton);

      // Modal should close
      await waitFor(() => {
        expect(screen.queryByText('Premium Tier Coming Soon!')).not.toBeInTheDocument();
      });
    });

    it('should close modal on close button click', async () => {
      const user = userEvent.setup();
      render(<LockedSlotCard {...defaultProps} />);

      // Open modal
      const upgradeButton = screen.getByRole('button', { name: /upgrade/i });
      await user.click(upgradeButton);

      // Click close button (aria-label="Close" with capital C)
      const closeButton = await screen.findByRole('button', { name: 'Close' });
      await user.click(closeButton);

      // Modal should close
      await waitFor(() => {
        expect(screen.queryByText('Premium Tier Coming Soon!')).not.toBeInTheDocument();
      });
    });
  });

  describe('Analytics Tracking', () => {
    it('should track upgrade click via gtag when available', async () => {
      // Mock gtag
      const mockGtag = vi.fn();
      (window as unknown as { gtag: typeof mockGtag }).gtag = mockGtag;

      const user = userEvent.setup();

      render(<LockedSlotCard {...defaultProps} lockedCount={3} currentTier="free" />);

      const upgradeButton = screen.getByRole('button', { name: /upgrade/i });
      await user.click(upgradeButton);

      expect(mockGtag).toHaveBeenCalledWith(
        'event',
        'upgrade_cta_clicked',
        expect.objectContaining({
          source: 'slot_management',
          current_tier: 'free',
          locked_slots_count: 3,
        })
      );

      // Cleanup
      delete (window as unknown as { gtag?: typeof mockGtag }).gtag;
    });

    it('should call onUpgradeClick callback for tracking', async () => {
      const onUpgradeClick = vi.fn();
      const user = userEvent.setup();

      render(<LockedSlotCard {...defaultProps} onUpgradeClick={onUpgradeClick} />);

      const upgradeButton = screen.getByRole('button', { name: /upgrade/i });
      await user.click(upgradeButton);

      expect(onUpgradeClick).toHaveBeenCalledTimes(1);
    });
  });

  describe('Styling', () => {
    it('should have gradient background', () => {
      render(<LockedSlotCard {...defaultProps} />);

      const card = screen.getByRole('region', { name: /premium upgrade/i });
      expect(card).toHaveClass('bg-gradient-to-br');
    });

    it('should have purple border styling', () => {
      render(<LockedSlotCard {...defaultProps} />);

      const card = screen.getByRole('region', { name: /premium upgrade/i });
      expect(card).toHaveClass('border-purple-500/30');
    });

    it('should have pulse animation on upgrade button', () => {
      render(<LockedSlotCard {...defaultProps} />);

      const upgradeButton = screen.getByRole('button', { name: /upgrade/i });
      expect(upgradeButton).toHaveClass('agent-pulse-purple');
    });
  });
});
