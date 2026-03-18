/**
 * @vitest-environment jsdom
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { MeepleContributorCard } from '../MeepleContributorCard';

vi.mock('next/link', () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

vi.mock('date-fns', () => ({
  formatDistanceToNow: () => '3 days ago',
}));

vi.mock('@/components/badges/BadgeIcon', () => ({
  BadgeIcon: () => null,
}));

describe('MeepleContributorCard', () => {
  const mockContributor = {
    userId: '00000000-0000-0000-0000-000000000001',
    userName: 'Marco',
    avatarUrl: null,
    isPrimaryContributor: true,
    contributionCount: 5,
    firstContributionAt: '2026-01-01T00:00:00Z',
    topBadges: [],
  };

  it('renders with correct entity type', () => {
    render(<MeepleContributorCard contributor={mockContributor} />);
    const card = screen.getByTestId('meeple-contributor-card');
    expect(card).toHaveAttribute('data-entity', 'player');
  });

  it('displays contributor name as title', () => {
    render(<MeepleContributorCard contributor={mockContributor} />);
    expect(screen.getByText('Marco')).toBeInTheDocument();
  });
});
