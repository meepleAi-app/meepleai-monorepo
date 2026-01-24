/**
 * BadgeEarnedModal Component Tests (Issue #2747)
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { BadgeEarnedModal } from '../BadgeEarnedModal';
import { BadgeTier, type BadgeNotificationData } from '@/types/badges';

// Mock react-confetti to avoid canvas issues in tests
vi.mock('react-confetti', () => ({
  default: () => null,
}));

describe('BadgeEarnedModal', () => {
  const mockBadge: BadgeNotificationData = {
    id: '1',
    name: 'First Contribution',
    description: 'Made your first contribution to the community',
    tier: BadgeTier.Gold,
    iconUrl: '/icons/first.png',
    earnedAt: '2026-01-20T10:00:00Z',
  };

  it('should render modal when badge is provided', () => {
    const handleClose = vi.fn();

    render(<BadgeEarnedModal badge={mockBadge} onClose={handleClose} />);

    expect(screen.getByText(/Badge Earned.*🥇/i)).toBeInTheDocument();
    expect(screen.getByText('First Contribution')).toBeInTheDocument();
    expect(screen.getByText(/Made your first contribution/i)).toBeInTheDocument();
  });

  it('should not render when badge is null', () => {
    const handleClose = vi.fn();

    const { container } = render(<BadgeEarnedModal badge={null} onClose={handleClose} />);

    expect(container.firstChild).toBeNull();
  });

  it('should call onClose when Continue button is clicked', async () => {
    const handleClose = vi.fn();
    const user = userEvent.setup();

    render(<BadgeEarnedModal badge={mockBadge} onClose={handleClose} />);

    const continueBtn = screen.getByRole('button', { name: /continue/i });
    await user.click(continueBtn);

    expect(handleClose).toHaveBeenCalledOnce();
  });

  it('should call onShare when Share button is clicked', async () => {
    const handleClose = vi.fn();
    const handleShare = vi.fn();
    const user = userEvent.setup();

    render(
      <BadgeEarnedModal badge={mockBadge} onClose={handleClose} onShare={handleShare} />
    );

    const shareBtn = screen.getByRole('button', { name: /share/i });
    await user.click(shareBtn);

    expect(handleShare).toHaveBeenCalledWith(mockBadge);
  });

  it('should not show Share button when onShare is not provided', () => {
    const handleClose = vi.fn();

    render(<BadgeEarnedModal badge={mockBadge} onClose={handleClose} />);

    expect(screen.queryByRole('button', { name: /share/i })).not.toBeInTheDocument();
  });

  it('should display different celebratory titles for different tiers', () => {
    const handleClose = vi.fn();

    const { rerender } = render(
      <BadgeEarnedModal badge={{ ...mockBadge, tier: BadgeTier.Diamond }} onClose={handleClose} />
    );
    expect(screen.getByText(/Legendary Badge.*💎/i)).toBeInTheDocument();

    rerender(
      <BadgeEarnedModal badge={{ ...mockBadge, tier: BadgeTier.Bronze }} onClose={handleClose} />
    );
    expect(screen.getByText(/Badge Earned.*🥉/i)).toBeInTheDocument();
  });
});
