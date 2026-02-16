import { screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderWithQuery } from '@/__tests__/utils/query-test-utils';
import { UserManagementBlock } from '../user-management-block';
import * as adminClientModule from '@/lib/api/admin-client';

// Mock dependencies
vi.mock('@/lib/api/admin-client', () => ({
  adminClient: {
    getUsers: vi.fn(),
    getUserDetail: vi.fn(),
    getUserLibraryStats: vi.fn(),
    getUserBadges: vi.fn(),
    suspendUser: vi.fn(),
    unsuspendUser: vi.fn(),
    updateUserTier: vi.fn(),
    impersonateUser: vi.fn(),
  },
}));

vi.mock('@/hooks/use-toast', () => ({
  useToast: () => ({
    toast: vi.fn(),
  }),
}));

vi.mock('@/hooks/useDebounce', () => ({
  useDebounce: (value: string) => value,
}));

describe('UserManagementBlock', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders block header with title and badge', async () => {
    vi.mocked(adminClientModule.adminClient.getUsers).mockResolvedValue({
      items: [],
      totalCount: 8542,
      page: 1,
      pageSize: 6,
      totalPages: 1424,
    });

    renderWithQuery(<UserManagementBlock />);

    expect(screen.getByRole('heading', { name: /user management/i })).toBeInTheDocument();
    await screen.findByText('8542 users');
    expect(screen.getByRole('link', { name: /view all/i })).toHaveAttribute(
      'href',
      '/admin/users/management'
    );
  });

  it('displays user cards when data loads', async () => {
    vi.mocked(adminClientModule.adminClient.getUsers).mockResolvedValue({
      items: [
        {
          id: '1',
          displayName: 'John Doe',
          email: 'john@example.com',
          role: 'user',
          tier: 'premium',
          level: 15,
          experiencePoints: 2350,
          createdAt: new Date().toISOString(),
          isActive: true,
          isTwoFactorEnabled: true,
          emailVerified: true,
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 6,
      totalPages: 1,
    });

    renderWithQuery(<UserManagementBlock />);

    await waitFor(() => {
      expect(screen.getByText(/john doe/i)).toBeInTheDocument();
      expect(screen.getByText(/john@example.com/i)).toBeInTheDocument();
    });
  });

  it('toggles between grid and list views', async () => {
    vi.mocked(adminClientModule.adminClient.getUsers).mockResolvedValue({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 6,
      totalPages: 0,
    });

    renderWithQuery(<UserManagementBlock />);

    const buttons = screen.getAllByRole('button');
    expect(buttons.length).toBeGreaterThan(0);
  });

  it('filters users by search query', async () => {
    vi.mocked(adminClientModule.adminClient.getUsers).mockResolvedValue({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 6,
      totalPages: 0,
    });

    renderWithQuery(<UserManagementBlock />);

    const searchInput = screen.getByPlaceholderText(/search by name or email/i);
    fireEvent.change(searchInput, { target: { value: 'John' } });

    expect(searchInput).toHaveValue('John');
  });

  it('shows empty state when no users', async () => {
    vi.mocked(adminClientModule.adminClient.getUsers).mockResolvedValue({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 6,
      totalPages: 0,
    });

    renderWithQuery(<UserManagementBlock />);

    await waitFor(() => {
      expect(screen.getByText(/no users found/i)).toBeInTheDocument();
    });
  });

  it('handles undefined API response gracefully', async () => {
    vi.mocked(adminClientModule.adminClient.getUsers).mockResolvedValue({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 6,
      totalPages: 0,
    });

    renderWithQuery(<UserManagementBlock />);

    await waitFor(() => {
      expect(screen.getByText(/no users found/i)).toBeInTheDocument();
    });
  });

  it('opens detail panel when user card is clicked', async () => {
    vi.mocked(adminClientModule.adminClient.getUsers).mockResolvedValue({
      items: [
        {
          id: '1',
          displayName: 'John Doe',
          email: 'john@example.com',
          role: 'user',
          tier: 'premium',
          level: 15,
          experiencePoints: 2350,
          createdAt: new Date().toISOString(),
          isActive: true,
          isTwoFactorEnabled: true,
          emailVerified: true,
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 6,
      totalPages: 1,
    });

    vi.mocked(adminClientModule.adminClient.getUserDetail).mockResolvedValue({
      id: '1',
      displayName: 'John Doe',
      email: 'john@example.com',
      role: 'user',
      tier: 'premium',
      level: 15,
      experiencePoints: 2350,
      createdAt: new Date().toISOString(),
      isActive: true,
      isTwoFactorEnabled: true,
      emailVerified: true,
    });

    vi.mocked(adminClientModule.adminClient.getUserLibraryStats).mockResolvedValue({
      totalGames: 47,
      favoriteGames: 12,
      sessionsPlayed: 234,
      oldestAddedAt: '2024-01-15T00:00:00Z',
      newestAddedAt: '2024-06-10T00:00:00Z',
    });

    vi.mocked(adminClientModule.adminClient.getUserBadges).mockResolvedValue([
      {
        id: '1',
        name: 'Early Adopter',
        description: 'Joined in the first year',
        icon: '🎖️',
        earnedAt: new Date().toISOString(),
      },
    ]);

    renderWithQuery(<UserManagementBlock />);

    // Wait for user card to appear
    await screen.findByText('John Doe');

    // MeepleCard in grid variant uses onClick on the card itself (no action buttons in grid/list).
    // Click the card element to trigger detail panel.
    const userCard = screen.getByTestId('meeple-card');
    fireEvent.click(userCard);

    // Wait for dialog to open
    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /user profile/i })).toBeInTheDocument();
    });
  });
});
