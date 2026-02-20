/**
 * Player Achievements Page Tests (Issue #4890)
 */

import { screen, waitFor } from '@testing-library/react';
import { useParams } from 'next/navigation';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import PlayerAchievementsPage from '../page';

const mockGetMyBadges = vi.hoisted(() => vi.fn());

vi.mock('next/navigation', () => ({
  useParams: vi.fn(),
  useRouter: vi.fn(() => ({ push: vi.fn() })),
}));

vi.mock('@/lib/api', () => ({
  api: {
    badges: {
      getMyBadges: mockGetMyBadges,
    },
  },
}));

const mockBadges = [
  {
    id: 'badge-1',
    name: 'First Victory',
    description: 'Win your first game',
    tier: 'Bronze',
    iconUrl: '',
    earnedAt: '2026-01-10T10:00:00Z',
    isDisplayed: true,
    category: 'gameplay',
  },
  {
    id: 'badge-2',
    name: 'Collector',
    description: 'Own 10 games',
    tier: 'Silver',
    iconUrl: '',
    earnedAt: '2026-01-15T10:00:00Z',
    isDisplayed: true,
    category: 'collection',
  },
];

describe('PlayerAchievementsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    (useParams as ReturnType<typeof vi.fn>).mockReturnValue({ id: 'alice' });
  });

  it('renders badges when loaded', async () => {
    mockGetMyBadges.mockResolvedValue(mockBadges);

    renderWithQuery(<PlayerAchievementsPage />);

    await waitFor(() => {
      expect(screen.getByText('First Victory')).toBeInTheDocument();
    });

    expect(screen.getByText('Collector')).toBeInTheDocument();
  });

  it('shows badge count', async () => {
    mockGetMyBadges.mockResolvedValue(mockBadges);

    renderWithQuery(<PlayerAchievementsPage />);

    await waitFor(() => {
      expect(screen.getByText('2 earned')).toBeInTheDocument();
    });
  });

  it('shows tier badges', async () => {
    mockGetMyBadges.mockResolvedValue(mockBadges);

    renderWithQuery(<PlayerAchievementsPage />);

    await waitFor(() => {
      expect(screen.getByText('Bronze')).toBeInTheDocument();
    });

    expect(screen.getByText('Silver')).toBeInTheDocument();
  });

  it('shows empty state when no badges', async () => {
    mockGetMyBadges.mockResolvedValue([]);

    renderWithQuery(<PlayerAchievementsPage />);

    await waitFor(() => {
      expect(screen.getByText(/No badges yet/i)).toBeInTheDocument();
    });
  });

  it('shows error state on failure', async () => {
    mockGetMyBadges.mockRejectedValue(new Error('API error'));

    renderWithQuery(<PlayerAchievementsPage />);

    await waitFor(() => {
      expect(screen.getByText('API error')).toBeInTheDocument();
    });
  });

  it('shows loading skeletons', () => {
    mockGetMyBadges.mockReturnValue(new Promise(() => {}));

    renderWithQuery(<PlayerAchievementsPage />);

    const skeletons = document.querySelectorAll('.animate-pulse');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('renders page heading', async () => {
    mockGetMyBadges.mockResolvedValue(mockBadges);

    renderWithQuery(<PlayerAchievementsPage />);

    await waitFor(() => {
      expect(screen.getByText('Achievements')).toBeInTheDocument();
    });
  });
});
