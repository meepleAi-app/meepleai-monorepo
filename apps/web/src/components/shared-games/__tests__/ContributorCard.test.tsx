/**
 * ContributorCard Component Tests
 * Issue #2746: Frontend - Contributor Display su SharedGame
 *
 * Tests:
 * - Contributor info display (avatar, name, stats)
 * - Primary contributor badge
 * - Badge list rendering
 * - Featured styling
 * - Link navigation
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { ContributorCard } from '../ContributorCard';
import type { GameContributorDto } from '@/lib/api';

// Mock next/link
vi.mock('next/link', () => ({
  default: ({ href, children, className }: { href: string; children: React.ReactNode; className?: string }) => (
    <a href={href} className={className}>{children}</a>
  ),
}));

// Mock date-fns
vi.mock('date-fns', () => ({
  formatDistanceToNow: vi.fn(() => '3 months ago'),
}));

// Test data
const mockPrimaryContributor: GameContributorDto = {
  userId: 'user-1',
  userName: 'JohnDoe',
  avatarUrl: 'https://example.com/john.jpg',
  isPrimaryContributor: true,
  contributionCount: 15,
  firstContributionAt: '2023-10-01T10:00:00Z',
  topBadges: [
    { id: 'badge-1', name: 'Pioneer', tier: 'Gold', iconUrl: 'https://example.com/gold.png' },
    { id: 'badge-2', name: 'Mentor', tier: 'Silver', iconUrl: null },
  ],
};

const mockAdditionalContributor: GameContributorDto = {
  userId: 'user-2',
  userName: 'JaneSmith',
  avatarUrl: null,
  isPrimaryContributor: false,
  contributionCount: 3,
  firstContributionAt: '2024-01-10T14:00:00Z',
  topBadges: [],
};

describe('ContributorCard', () => {
  it('renders contributor name and avatar', () => {
    render(<ContributorCard contributor={mockPrimaryContributor} />);
    expect(screen.getByText('JohnDoe')).toBeInTheDocument();
    expect(screen.getByAltText('JohnDoe')).toHaveAttribute('src', 'https://example.com/john.jpg');
  });

  it('renders fallback initial when no avatar provided', () => {
    render(<ContributorCard contributor={mockAdditionalContributor} />);
    expect(screen.getByText('J')).toBeInTheDocument(); // 'J' for JaneSmith
  });

  it('displays contribution count correctly', () => {
    render(<ContributorCard contributor={mockPrimaryContributor} />);
    expect(screen.getByText('15 contributions')).toBeInTheDocument();

    render(<ContributorCard contributor={{ ...mockAdditionalContributor, contributionCount: 1 }} />);
    expect(screen.getByText('1 contribution')).toBeInTheDocument();
  });

  it('displays time since first contribution', () => {
    render(<ContributorCard contributor={mockPrimaryContributor} />);
    expect(screen.getByText('3 months ago')).toBeInTheDocument();
  });

  it('shows "Original" badge for primary contributor', () => {
    render(<ContributorCard contributor={mockPrimaryContributor} />);
    expect(screen.getByText('Original')).toBeInTheDocument();
  });

  it('does not show "Original" badge for non-primary contributors', () => {
    render(<ContributorCard contributor={mockAdditionalContributor} />);
    expect(screen.queryByText('Original')).not.toBeInTheDocument();
  });

  it('renders badges section when contributor has badges', () => {
    render(<ContributorCard contributor={mockPrimaryContributor} />);
    expect(screen.getByText('Badges:')).toBeInTheDocument();
    expect(screen.getByText('Pioneer')).toBeInTheDocument();
    expect(screen.getByText('Mentor')).toBeInTheDocument();
  });

  it('does not render badges section when contributor has no badges', () => {
    render(<ContributorCard contributor={mockAdditionalContributor} />);
    expect(screen.queryByText('Badges:')).not.toBeInTheDocument();
  });

  it('applies featured styling when featured prop is true', () => {
    const { container } = render(<ContributorCard contributor={mockPrimaryContributor} featured />);
    const card = container.querySelector('.ring-2');
    expect(card).toBeInTheDocument();
  });

  it('links to contributor profile', () => {
    render(<ContributorCard contributor={mockPrimaryContributor} />);
    const link = screen.getByRole('link');
    expect(link).toHaveAttribute('href', '/contributors/user-1');
  });
});
