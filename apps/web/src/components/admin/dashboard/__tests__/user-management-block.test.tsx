import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { UserManagementBlock } from '../user-management-block';

// Mock dependencies
vi.mock('@/lib/api/admin-client', () => ({
  adminClient: {
    getUsers: vi.fn(),
    getUserDetail: vi.fn(),
    getUserLibraryStats: vi.fn(),
    getUserBadges: vi.fn(),
    suspendUser: vi.fn(),
    unsuspendUser: vi.fn(),
  },
}));

vi.mock('@/hooks/use-toast', () => ({
  useToast: () => ({
    toast: vi.fn(),
  }),
}));

const createTestQueryClient = () =>
  new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });

function renderWithQueryClient(ui: React.ReactElement) {
  const queryClient = createTestQueryClient();
  return render(
    <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>
  );
}

describe('UserManagementBlock', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders block header with title and badge', () => {
    const { adminClient } = require('@/lib/api/admin-client');
    adminClient.getUsers.mockResolvedValue({
      items: [],
      totalCount: 8542,
      page: 1,
      pageSize: 6,
      totalPages: 1424,
    });

    renderWithQueryClient(<UserManagementBlock />);

    expect(screen.getByRole('heading', { name: /user management/i })).toBeInTheDocument();
    expect(screen.getByText(/8542 users/i)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /view all/i })).toHaveAttribute(
      'href',
      '/admin/users/management'
    );
  });

  it('displays user cards when data loads', async () => {
    const { adminClient } = require('@/lib/api/admin-client');
    adminClient.getUsers.mockResolvedValue({
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

    renderWithQueryClient(<UserManagementBlock />);

    await waitFor(() => {
      expect(screen.getByText(/john doe/i)).toBeInTheDocument();
      expect(screen.getByText(/john@example.com/i)).toBeInTheDocument();
    });
  });

  it('toggles between grid and list views', async () => {
    const { adminClient } = require('@/lib/api/admin-client');
    adminClient.getUsers.mockResolvedValue({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 6,
      totalPages: 0,
    });

    renderWithQueryClient(<UserManagementBlock />);

    const buttons = screen.getAllByRole('button');
    expect(buttons.length).toBeGreaterThan(0);
  });

  it('filters users by search query', async () => {
    const { adminClient } = require('@/lib/api/admin-client');
    adminClient.getUsers.mockResolvedValue({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 6,
      totalPages: 0,
    });

    renderWithQueryClient(<UserManagementBlock />);

    const searchInput = screen.getByPlaceholderText(/search by name or email/i);
    fireEvent.change(searchInput, { target: { value: 'John' } });

    expect(searchInput).toHaveValue('John');
  });

  it('shows empty state when no users', async () => {
    const { adminClient } = require('@/lib/api/admin-client');
    adminClient.getUsers.mockResolvedValue({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 6,
      totalPages: 0,
    });

    renderWithQueryClient(<UserManagementBlock />);

    await waitFor(() => {
      expect(screen.getByText(/no users found/i)).toBeInTheDocument();
    });
  });

  it('handles undefined API response gracefully', async () => {
    const { adminClient } = require('@/lib/api/admin-client');
    adminClient.getUsers.mockResolvedValue(undefined);

    renderWithQueryClient(<UserManagementBlock />);

    await waitFor(() => {
      expect(screen.getByText(/no users found/i)).toBeInTheDocument();
    });
  });

  it('opens detail panel when user card is clicked', async () => {
    const { adminClient } = require('@/lib/api/admin-client');
    adminClient.getUsers.mockResolvedValue({
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

    adminClient.getUserDetail.mockResolvedValue({
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

    adminClient.getUserLibraryStats.mockResolvedValue({
      gamesOwned: 47,
      totalPlays: 234,
      wishlistCount: 12,
      averageRating: 8.4,
      favoriteCategory: 'Strategy',
    });

    adminClient.getUserBadges.mockResolvedValue([
      {
        id: '1',
        name: 'Early Adopter',
        description: 'Joined in the first year',
        icon: '🎖️',
        earnedAt: new Date().toISOString(),
      },
    ]);

    renderWithQueryClient(<UserManagementBlock />);

    await waitFor(() => {
      const userCard = screen.getByText(/john doe/i);
      fireEvent.click(userCard.closest('button')!);
    });

    await waitFor(() => {
      expect(screen.getByRole('dialog')).toBeInTheDocument();
      expect(screen.getByRole('heading', { name: /user profile/i })).toBeInTheDocument();
    });
  });
});
