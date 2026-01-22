/**
 * ContributorsSection Component Tests
 * Issue #2746: Frontend - Contributor Display su SharedGame
 *
 * Tests:
 * - Loading state with skeletons
 * - Primary and additional contributors separation
 * - Empty state (null return)
 * - Responsive grid layout
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ContributorsSection } from '../ContributorsSection';
import type { GameContributorDto } from '@/lib/api';

// Mock useGameContributors hook
const mockUseGameContributors = vi.fn();
vi.mock('@/hooks/queries', () => ({
  useGameContributors: () => mockUseGameContributors(),
}));

// Test data
const mockPrimaryContributor: GameContributorDto = {
  userId: 'user-1',
  userName: 'JohnDoe',
  avatarUrl: 'https://example.com/john.jpg',
  isPrimaryContributor: true,
  contributionCount: 15,
  firstContributionAt: '2023-10-01T10:00:00Z',
  topBadges: [{ id: 'badge-1', name: 'Pioneer', tier: 'Gold', iconUrl: 'https://example.com/gold.png' }],
};

const mockAdditionalContributors: GameContributorDto[] = [
  {
    userId: 'user-2',
    userName: 'JaneSmith',
    avatarUrl: null,
    isPrimaryContributor: false,
    contributionCount: 3,
    firstContributionAt: '2024-01-10T14:00:00Z',
    topBadges: [],
  },
  {
    userId: 'user-3',
    userName: 'BobJones',
    avatarUrl: 'https://example.com/bob.jpg',
    isPrimaryContributor: false,
    contributionCount: 5,
    firstContributionAt: '2023-12-01T09:00:00Z',
    topBadges: [{ id: 'badge-2', name: 'Helper', tier: 'Bronze', iconUrl: null }],
  },
];

describe('ContributorsSection', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('displays loading skeletons when data is loading', () => {
    mockUseGameContributors.mockReturnValue({
      data: undefined,
      isLoading: true,
      error: null,
    });

    render(<ContributorsSection gameId="game-123" />);
    expect(screen.getByText('Contributors')).toBeInTheDocument();
    expect(screen.getByText('This game was contributed by the community')).toBeInTheDocument();
  });

  it('returns null when no contributors are available', () => {
    mockUseGameContributors.mockReturnValue({
      data: [],
      isLoading: false,
      error: null,
    });

    const { container } = render(<ContributorsSection gameId="game-123" />);
    expect(container.firstChild).toBeNull();
  });

  it('displays primary contributor in featured section', () => {
    mockUseGameContributors.mockReturnValue({
      data: [mockPrimaryContributor],
      isLoading: false,
      error: null,
    });

    render(<ContributorsSection gameId="game-123" />);
    expect(screen.getByText('Original Contributor')).toBeInTheDocument();
    expect(screen.getByText('JohnDoe')).toBeInTheDocument();
  });

  it('displays additional contributors in grid', () => {
    mockUseGameContributors.mockReturnValue({
      data: [mockPrimaryContributor, ...mockAdditionalContributors],
      isLoading: false,
      error: null,
    });

    render(<ContributorsSection gameId="game-123" />);
    expect(screen.getByText('Additional Contributors (2)')).toBeInTheDocument();
    expect(screen.getByText('JaneSmith')).toBeInTheDocument();
    expect(screen.getByText('BobJones')).toBeInTheDocument();
  });

  it('displays only primary contributor when no additional contributors exist', () => {
    mockUseGameContributors.mockReturnValue({
      data: [mockPrimaryContributor],
      isLoading: false,
      error: null,
    });

    render(<ContributorsSection gameId="game-123" />);
    expect(screen.getByText('Original Contributor')).toBeInTheDocument();
    expect(screen.queryByText(/Additional Contributors/)).not.toBeInTheDocument();
  });

  it('separates primary and additional contributors correctly', () => {
    mockUseGameContributors.mockReturnValue({
      data: [mockAdditionalContributors[0], mockPrimaryContributor, mockAdditionalContributors[1]],
      isLoading: false,
      error: null,
    });

    render(<ContributorsSection gameId="game-123" />);

    // Primary should be in its own section
    expect(screen.getByText('Original Contributor')).toBeInTheDocument();

    // Others should be in additional section
    expect(screen.getByText('Additional Contributors (2)')).toBeInTheDocument();
  });
});
