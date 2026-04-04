import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import AdminUsersPage from '../page';

const mockGetAllUsers = vi.hoisted(() => vi.fn());
const mockGetInvitations = vi.hoisted(() => vi.fn());
const mockResendInvitation = vi.hoisted(() => vi.fn());
const mockRevokeInvitation = vi.hoisted(() => vi.fn());
const mockSendInvitation = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getAllUsers: mockGetAllUsers,
    },
    invitations: {
      getInvitations: mockGetInvitations,
      resendInvitation: mockResendInvitation,
      revokeInvitation: mockRevokeInvitation,
      sendInvitation: mockSendInvitation,
    },
  },
}));

vi.mock('@/hooks/useSetNavConfig', () => ({
  useSetNavConfig: () => vi.fn(),
}));

vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

// InlineRoleSelect requires its own mutation — mock to avoid complexity
vi.mock('@/components/admin/users/InlineRoleSelect', () => ({
  InlineRoleSelect: ({ currentRole }: { currentRole: string }) => (
    <span data-testid="role-badge">{currentRole}</span>
  ),
}));

const mockUsers = [
  {
    id: 'user-1',
    email: 'alice@example.com',
    displayName: 'Alice',
    role: 'user',
    isSuspended: false,
    createdAt: '2026-01-01T00:00:00Z',
  },
  {
    id: 'user-2',
    email: 'bob@example.com',
    displayName: 'Bob',
    role: 'admin',
    isSuspended: true,
    createdAt: '2026-01-02T00:00:00Z',
  },
];

const mockPendingInvitations = [
  {
    id: 'inv-1',
    email: 'charlie@example.com',
    role: 'User',
    status: 'Pending' as const,
    createdAt: '2026-03-20T00:00:00Z',
    expiresAt: '2026-03-27T00:00:00Z',
    acceptedAt: null,
    invitedByUserId: 'user-1',
  },
];

describe('AdminUsersPage', () => {
  const user = userEvent.setup();

  beforeEach(() => {
    vi.clearAllMocks();
    mockGetAllUsers.mockResolvedValue({
      items: mockUsers,
      total: 2,
      page: 1,
      limit: 20,
    });
    mockGetInvitations.mockResolvedValue({
      items: mockPendingInvitations,
      totalCount: 1,
      page: 1,
      pageSize: 20,
    });
  });

  it('renders page title and header', async () => {
    renderWithQuery(<AdminUsersPage />);

    expect(screen.getByRole('heading', { name: 'Utenti' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /invita utente/i })).toBeInTheDocument();
  });

  it('renders breadcrumb with Admin link', async () => {
    renderWithQuery(<AdminUsersPage />);

    const nav = screen.getByRole('navigation', { name: /breadcrumb/i });
    expect(within(nav).getByRole('link', { name: 'Admin' })).toBeInTheDocument();
    expect(within(nav).getByText('Utenti')).toBeInTheDocument();
  });

  it('shows user rows with correct active badge', async () => {
    renderWithQuery(<AdminUsersPage />);

    await waitFor(() => {
      expect(screen.getByText('Alice')).toBeInTheDocument();
    });

    const rows = screen.getAllByRole('row');
    // Find Alice's row — should show "Attivo"
    const aliceRow = rows.find(r => within(r).queryByText('Alice'));
    expect(within(aliceRow!).getByText('Attivo')).toBeInTheDocument();
  });

  it('shows suspended badge for suspended user', async () => {
    renderWithQuery(<AdminUsersPage />);

    await waitFor(() => {
      expect(screen.getByText('Bob')).toBeInTheDocument();
    });

    const rows = screen.getAllByRole('row');
    const bobRow = rows.find(r => within(r).queryByText('Bob'));
    expect(within(bobRow!).getByText('Sospeso')).toBeInTheDocument();
  });

  it('renders pending invitations as amber rows', async () => {
    renderWithQuery(<AdminUsersPage />);

    await waitFor(() => {
      expect(screen.getByText('charlie@example.com')).toBeInTheDocument();
    });

    const invRow = screen.getByText('charlie@example.com').closest('tr');
    expect(invRow).toHaveClass('bg-amber-50');
  });

  it('shows "Aggiorna" refresh button', async () => {
    renderWithQuery(<AdminUsersPage />);

    expect(screen.getByRole('button', { name: /aggiorna/i })).toBeInTheDocument();
  });

  it('shows header counter with user and invitation counts', async () => {
    renderWithQuery(<AdminUsersPage />);

    await waitFor(() => {
      expect(screen.getByText(/2 utenti/i)).toBeInTheDocument();
    });

    expect(screen.getByText(/1 invit/i)).toBeInTheDocument();
  });

  it('calls revokeInvitation when Revoca clicked on pending invitation row', async () => {
    mockRevokeInvitation.mockResolvedValueOnce(undefined);

    renderWithQuery(<AdminUsersPage />);

    await waitFor(() => {
      expect(screen.getByText('charlie@example.com')).toBeInTheDocument();
    });

    const revokeBtn = screen.getByRole('button', {
      name: /revoca invito per charlie@example.com/i,
    });
    await user.click(revokeBtn);

    await waitFor(() => {
      expect(mockRevokeInvitation).toHaveBeenCalledWith('inv-1');
    });
  });

  it('calls resendInvitation when Reinvia clicked on pending invitation row', async () => {
    mockResendInvitation.mockResolvedValueOnce(undefined);

    renderWithQuery(<AdminUsersPage />);

    await waitFor(() => {
      expect(screen.getByText('charlie@example.com')).toBeInTheDocument();
    });

    const resendBtn = screen.getByRole('button', {
      name: /reinvia invito a charlie@example.com/i,
    });
    await user.click(resendBtn);

    await waitFor(() => {
      expect(mockResendInvitation).toHaveBeenCalledWith('inv-1');
    });
  });
});
