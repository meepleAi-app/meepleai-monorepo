/**
 * Profile Landing Page Tests (Issue #4893)
 */

import { screen, waitFor, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import ProfilePage from '../page';

// ─── Hoisted mocks ────────────────────────────────────────────────────────────

const mockGetStats = vi.hoisted(() => vi.fn());
const mockGetProfile = vi.hoisted(() => vi.fn());
const mockUploadAvatar = vi.hoisted(() => vi.fn());
const mockUseAuth = vi.hoisted(() => vi.fn());
const mockUseRecentSessions = vi.hoisted(() => vi.fn());
const mockUseActivityFeed = vi.hoisted(() => vi.fn());
const mockDrawCard = vi.hoisted(() => vi.fn());

vi.mock('@/hooks/useAuth', () => ({
  useAuth: mockUseAuth,
}));

vi.mock('@/hooks/useRecentSessions', () => ({
  useRecentSessions: mockUseRecentSessions,
}));

vi.mock('@/hooks/useActivityFeed', () => ({
  useActivityFeed: mockUseActivityFeed,
}));

vi.mock('@/stores/use-card-hand', () => ({
  useCardHand: () => ({ drawCard: mockDrawCard }),
}));

vi.mock('@/lib/api', () => ({
  api: {
    auth: {
      getProfile: mockGetProfile,
      uploadAvatar: mockUploadAvatar,
    },
    library: {
      getStats: mockGetStats,
    },
  },
}));

// AvatarUpload usa react-image-crop — lo stubbiamo per evitare problemi JSDOM
vi.mock('@/components/profile/AvatarUpload', () => ({
  AvatarUpload: ({ displayName }: { displayName: string }) => (
    <div data-testid="avatar-upload">{displayName.slice(0, 2).toUpperCase()}</div>
  ),
}));

// AchievementsGrid ha un fetch interno — lo stubbiamo
vi.mock('@/components/profile/AchievementsGrid', () => ({
  AchievementsGrid: () => <div data-testid="achievements-grid">Achievements</div>,
}));

// EditProfileSheet ha un Sheet Radix — lo stubbiamo
vi.mock('@/components/profile/EditProfileSheet', () => ({
  EditProfileSheet: ({ currentDisplayName }: { currentDisplayName: string }) => (
    <button data-testid="edit-profile-sheet">Modifica {currentDisplayName}</button>
  ),
}));

// ActivityFeed usa useActivityFeed internamente — lo stubbiamo per isolare il test
vi.mock('@/components/profile/ActivityFeed', () => ({
  ActivityFeed: () => <div data-testid="activity-feed">Activity</div>,
}));

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const mockUser = {
  id: 'user-1',
  email: 'alice@example.com',
  displayName: 'Alice Smith',
  role: 'User',
};

const mockProfile = {
  id: 'user-1',
  email: 'alice@example.com',
  displayName: 'Alice Smith',
  role: 'User',
  createdAt: '2025-01-01T00:00:00Z',
  isTwoFactorEnabled: false,
  twoFactorEnabledAt: null,
  language: 'it',
  theme: 'light',
  emailNotifications: true,
  dataRetentionDays: 30,
  avatarUrl: null,
};

const mockStats = {
  totalGames: 24,
  favoriteGames: 8,
  ownedCount: 10,
  wishlistCount: 5,
  privatePdfs: 2,
  inPrestitoCount: 1,
  oldestAddedAt: null,
  newestAddedAt: null,
};

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('ProfilePage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUseAuth.mockReturnValue({ user: mockUser });
    mockGetStats.mockResolvedValue(mockStats);
    mockGetProfile.mockResolvedValue(mockProfile);
    mockUploadAvatar.mockResolvedValue({ ok: true, avatarUrl: 'https://example.com/avatar.jpg' });
    mockUseRecentSessions.mockReturnValue({ sessions: [], isLoading: false, error: null });
    mockUseActivityFeed.mockReturnValue({ items: [], isLoading: false, error: null });
    mockDrawCard.mockReturnValue(undefined);
  });

  it('shows user display name in header', async () => {
    renderWithQuery(<ProfilePage />);

    await waitFor(() => {
      expect(screen.getByText('Alice Smith')).toBeInTheDocument();
    });
  });

  it('shows user email in header', async () => {
    renderWithQuery(<ProfilePage />);

    await waitFor(() => {
      expect(screen.getByText('alice@example.com')).toBeInTheDocument();
    });
  });

  it('shows avatar component in header', async () => {
    renderWithQuery(<ProfilePage />);

    await waitFor(() => {
      expect(screen.getByTestId('avatar-upload')).toBeInTheDocument();
    });
  });

  it('shows edit profile button in header', async () => {
    renderWithQuery(<ProfilePage />);

    await waitFor(() => {
      expect(screen.getByTestId('edit-profile-sheet')).toBeInTheDocument();
    });
  });

  it('renders tab bar with three tabs', async () => {
    renderWithQuery(<ProfilePage />);

    await waitFor(() => {
      expect(screen.getByRole('tab', { name: /Overview/i })).toBeInTheDocument();
    });

    expect(screen.getByRole('tab', { name: /Achievements/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /Activity/i })).toBeInTheDocument();
  });

  it('shows library stats on Overview tab', async () => {
    renderWithQuery(<ProfilePage />);

    await waitFor(() => {
      expect(screen.getByText('Giochi')).toBeInTheDocument();
    });

    expect(screen.getByText('24')).toBeInTheDocument();
    expect(screen.getByText('8')).toBeInTheDocument();
  });

  it('shows quick action links on Overview tab', async () => {
    renderWithQuery(<ProfilePage />);

    await waitFor(() => {
      expect(screen.getByText('Quick Actions')).toBeInTheDocument();
    });

    expect(screen.getByText('My Library')).toBeInTheDocument();
    expect(screen.getByText('Storia di gioco')).toBeInTheDocument();
  });

  it('switches to Achievements tab and shows achievements grid', async () => {
    renderWithQuery(<ProfilePage />);

    await waitFor(() => {
      expect(screen.getByRole('tab', { name: /Achievements/i })).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole('tab', { name: /Achievements/i }));

    expect(screen.getByTestId('achievements-grid')).toBeInTheDocument();
  });

  it('switches to Activity tab and shows activity feed', async () => {
    renderWithQuery(<ProfilePage />);

    await waitFor(() => {
      expect(screen.getByRole('tab', { name: /Activity/i })).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole('tab', { name: /Activity/i }));

    expect(screen.getByTestId('activity-feed')).toBeInTheDocument();
  });

  it('shows loading skeleton while fetching stats', async () => {
    mockGetStats.mockReturnValue(new Promise(() => {}));

    renderWithQuery(<ProfilePage />);

    await waitFor(() => {
      const skeletons = document.querySelectorAll('.animate-pulse');
      expect(skeletons.length).toBeGreaterThan(0);
    });
  });

  it('works without a logged-in user', async () => {
    mockUseAuth.mockReturnValue({ user: null });
    mockGetProfile.mockResolvedValue(null);

    renderWithQuery(<ProfilePage />);

    await waitFor(() => {
      expect(screen.getByText('Player')).toBeInTheDocument();
    });
  });
});
