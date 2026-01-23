/**
 * BadgeGrid Component Tests (Issue #2747)
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { BadgeGrid } from '../BadgeGrid';
import { BadgeTier, type UserBadgeDto } from '@/types/badges';

describe('BadgeGrid', () => {
  const mockBadges: UserBadgeDto[] = [
    {
      id: '1',
      name: 'First Contribution',
      description: 'Made your first contribution',
      tier: BadgeTier.Bronze,
      iconUrl: '/icons/first.png',
      earnedAt: '2026-01-20T10:00:00Z',
      isDisplayed: true,
    },
    {
      id: '2',
      name: 'Power Contributor',
      description: '10 contributions',
      tier: BadgeTier.Gold,
      iconUrl: '/icons/power.png',
      earnedAt: '2026-01-21T10:00:00Z',
      isDisplayed: true,
    },
    {
      id: '3',
      name: 'Hidden Badge',
      description: 'Secret achievement',
      tier: BadgeTier.Silver,
      iconUrl: '/icons/secret.png',
      earnedAt: '2026-01-19T10:00:00Z',
      isDisplayed: false,
    },
  ];

  it('should render badges grouped by tier', () => {
    render(<BadgeGrid badges={mockBadges} />);

    expect(screen.getByText(/Gold Badges/i)).toBeInTheDocument();
    expect(screen.getByText(/Silver Badges/i)).toBeInTheDocument();
    expect(screen.getByText(/Bronze Badges/i)).toBeInTheDocument();
  });

  it('should hide non-displayed badges by default', () => {
    render(<BadgeGrid badges={mockBadges} />);

    expect(screen.getByText('First Contribution')).toBeInTheDocument();
    expect(screen.getByText('Power Contributor')).toBeInTheDocument();
    expect(screen.queryByText('Hidden Badge')).not.toBeInTheDocument();
  });

  it('should show hidden badges when showHidden is true', () => {
    render(<BadgeGrid badges={mockBadges} showHidden />);

    expect(screen.getByText('Hidden Badge')).toBeInTheDocument();
  });

  it('should sort badges by earned date (most recent first) within tier', () => {
    const badges = [
      { ...mockBadges[0], earnedAt: '2026-01-15T10:00:00Z' }, // Older
      { ...mockBadges[0], id: '4', earnedAt: '2026-01-22T10:00:00Z' }, // Newer
    ];

    render(<BadgeGrid badges={badges} />);

    const badgeElements = screen.getAllByRole('button');
    expect(badgeElements[0]).toHaveTextContent('First Contribution'); // Newer first (id '4')
  });

  it('should call onBadgeClick when badge is clicked', async () => {
    const handleClick = vi.fn();
    const user = userEvent.setup();

    render(<BadgeGrid badges={mockBadges} onBadgeClick={handleClick} />);

    const badge = screen.getByText('Power Contributor').closest('button')!;
    await user.click(badge);

    expect(handleClick).toHaveBeenCalledWith(mockBadges[1]);
  });

  it('should display "No badges" message when empty', () => {
    render(<BadgeGrid badges={[]} />);

    expect(screen.getByText('No badges to display')).toBeInTheDocument();
  });

  it('should display tier icons correctly', () => {
    render(<BadgeGrid badges={mockBadges} />);

    expect(screen.getByText(/🥇/)).toBeInTheDocument(); // Gold
    expect(screen.getByText(/🥈/)).toBeInTheDocument(); // Silver
    expect(screen.getByText(/🥉/)).toBeInTheDocument(); // Bronze
  });

  it('should show badge count per tier', () => {
    render(<BadgeGrid badges={mockBadges} />);

    expect(screen.getByText(/Gold Badges \(1\)/i)).toBeInTheDocument();
  });
});
