import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import InvitationsPage from '../page';

const mockGetInvitations = vi.hoisted(() => vi.fn());
const mockGetInvitationStats = vi.hoisted(() => vi.fn());
const mockResendInvitation = vi.hoisted(() => vi.fn());
const mockRevokeInvitation = vi.hoisted(() => vi.fn());
const mockSendInvitation = vi.hoisted(() => vi.fn());
const mockBulkSendInvitations = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    invitations: {
      getInvitations: mockGetInvitations,
      getInvitationStats: mockGetInvitationStats,
      resendInvitation: mockResendInvitation,
      revokeInvitation: mockRevokeInvitation,
      sendInvitation: mockSendInvitation,
      bulkSendInvitations: mockBulkSendInvitations,
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

const mockStats = {
  pending: 3,
  accepted: 5,
  expired: 1,
  revoked: 0,
  total: 9,
};

const mockInvitations = [
  {
    id: 'inv-1',
    email: 'alice@example.com',
    role: 'User',
    status: 'Pending' as const,
    createdAt: '2026-03-01T00:00:00Z',
    expiresAt: '2026-03-08T00:00:00Z',
    acceptedAt: null,
    invitedByUserId: '00000000-0000-0000-0000-000000000001',
  },
  {
    id: 'inv-2',
    email: 'bob@example.com',
    role: 'Admin',
    status: 'Accepted' as const,
    createdAt: '2026-02-15T00:00:00Z',
    expiresAt: '2026-02-22T00:00:00Z',
    acceptedAt: '2026-02-16T00:00:00Z',
    invitedByUserId: '00000000-0000-0000-0000-000000000001',
  },
];

describe('InvitationsPage', () => {
  const user = userEvent.setup();

  beforeEach(() => {
    vi.clearAllMocks();
    mockGetInvitationStats.mockResolvedValue(mockStats);
    mockGetInvitations.mockResolvedValue({
      items: mockInvitations,
      totalCount: 2,
      page: 1,
      pageSize: 20,
    });
  });

  it('renders page title and action buttons', async () => {
    renderWithQuery(<InvitationsPage />);

    expect(screen.getByText('Inviti')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /invita utente/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /invito multiplo/i })).toBeInTheDocument();
  });

  it('renders stats cards with data', async () => {
    renderWithQuery(<InvitationsPage />);

    await waitFor(() => {
      expect(screen.getByText('Totale')).toBeInTheDocument();
    });

    expect(screen.getByText('In attesa')).toBeInTheDocument();
    expect(screen.getByText('Accettati')).toBeInTheDocument();
    expect(screen.getByText('Scaduti')).toBeInTheDocument();
    expect(screen.getByText('Revocati')).toBeInTheDocument();
  });

  it('renders invitation rows after loading', async () => {
    renderWithQuery(<InvitationsPage />);

    await waitFor(() => {
      expect(screen.getByText('alice@example.com')).toBeInTheDocument();
    });

    expect(screen.getByText('bob@example.com')).toBeInTheDocument();
  });

  it('calls resend API when resend button clicked', async () => {
    mockResendInvitation.mockResolvedValueOnce(undefined);

    renderWithQuery(<InvitationsPage />);

    await waitFor(() => {
      expect(screen.getByText('alice@example.com')).toBeInTheDocument();
    });

    // Alice is Pending, so should have a Resend button
    const resendButton = screen.getByRole('button', {
      name: /resend invitation to alice@example.com/i,
    });
    await user.click(resendButton);

    await waitFor(() => {
      expect(mockResendInvitation).toHaveBeenCalledWith('inv-1');
    });
  });

  it('shows empty state when no invitations', async () => {
    mockGetInvitations.mockResolvedValue({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 20,
    });

    renderWithQuery(<InvitationsPage />);

    await waitFor(() => {
      expect(screen.getByText('Nessun invito inviato finora')).toBeInTheDocument();
    });
  });

  it('shows generic error message (not raw error) when query fails', async () => {
    mockGetInvitations.mockRejectedValueOnce(new Error('Internal Server Error: DB timeout'));

    renderWithQuery(<InvitationsPage />);

    await waitFor(() => {
      expect(screen.getByText('Caricamento inviti fallito')).toBeInTheDocument();
    });

    expect(screen.queryByText(/DB timeout/i)).not.toBeInTheDocument();
    expect(screen.getByText(/impossibile caricare gli inviti/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /riprova/i })).toBeInTheDocument();
  });

  it('resets page to 1 when status filter changes', async () => {
    renderWithQuery(<InvitationsPage />);

    await waitFor(() => {
      expect(screen.getByText('alice@example.com')).toBeInTheDocument();
    });

    const trigger = screen.getByRole('combobox');
    await user.click(trigger);

    const pendingOption = screen.getByRole('option', { name: 'In attesa' });
    await user.click(pendingOption);

    await waitFor(() => {
      expect(mockGetInvitations).toHaveBeenCalledWith(
        expect.objectContaining({ status: 'Pending', page: 1 })
      );
    });
  });

  it('filters invitations by email search', async () => {
    renderWithQuery(<InvitationsPage />);

    await waitFor(() => {
      expect(screen.getByText('alice@example.com')).toBeInTheDocument();
    });

    const searchInput = screen.getByPlaceholderText('Cerca per email...');
    await user.type(searchInput, 'alice');

    await waitFor(() => {
      expect(screen.getByText('alice@example.com')).toBeInTheDocument();
      expect(screen.queryByText('bob@example.com')).not.toBeInTheDocument();
    });
  });

  it('shows search empty state when no results match email search', async () => {
    renderWithQuery(<InvitationsPage />);

    await waitFor(() => {
      expect(screen.getByText('alice@example.com')).toBeInTheDocument();
    });

    const searchInput = screen.getByPlaceholderText('Cerca per email...');
    await user.type(searchInput, 'xyz@notfound.com');

    await waitFor(() => {
      expect(screen.getByText(/nessun invito trovato per/i)).toBeInTheDocument();
    });
  });

  it('opens revoke confirmation dialog when Revoke is clicked', async () => {
    renderWithQuery(<InvitationsPage />);

    await waitFor(() => {
      expect(screen.getByText('alice@example.com')).toBeInTheDocument();
    });

    const revokeButton = screen.getByRole('button', {
      name: /revoke invitation for alice@example.com/i,
    });
    await user.click(revokeButton);

    await waitFor(() => {
      expect(screen.getByRole('alertdialog')).toBeInTheDocument();
    });
    expect(screen.getByText('Revoke Invitation')).toBeInTheDocument();
  });

  it('calls revokeInvitation API when dialog confirm is clicked', async () => {
    mockRevokeInvitation.mockResolvedValueOnce(undefined);

    renderWithQuery(<InvitationsPage />);

    await waitFor(() => {
      expect(screen.getByText('alice@example.com')).toBeInTheDocument();
    });

    await user.click(
      screen.getByRole('button', { name: /revoke invitation for alice@example.com/i })
    );

    await waitFor(() => {
      expect(screen.getByRole('alertdialog')).toBeInTheDocument();
    });

    // Click the confirm "Revoke" button inside the AlertDialog
    const allRevokeButtons = screen.getAllByRole('button', { name: /^revoke$/i });
    const confirmButton = allRevokeButtons.find(btn => btn.closest('[role="alertdialog"]'));
    await user.click(confirmButton!);

    await waitFor(() => {
      expect(mockRevokeInvitation).toHaveBeenCalledWith('inv-1');
    });
  });

  it('does NOT call revokeInvitation when dialog is cancelled', async () => {
    renderWithQuery(<InvitationsPage />);

    await waitFor(() => {
      expect(screen.getByText('alice@example.com')).toBeInTheDocument();
    });

    await user.click(
      screen.getByRole('button', { name: /revoke invitation for alice@example.com/i })
    );

    await waitFor(() => {
      expect(screen.getByRole('alertdialog')).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /cancel/i }));

    await waitFor(() => {
      expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    });
    expect(mockRevokeInvitation).not.toHaveBeenCalled();
  });

  it('shows filtered empty state when no results match filter', async () => {
    mockGetInvitations.mockResolvedValue({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 20,
    });

    renderWithQuery(<InvitationsPage />);

    const trigger = screen.getByRole('combobox');
    await user.click(trigger);
    const expiredOption = screen.getByRole('option', { name: 'Scaduti' });
    await user.click(expiredOption);

    await waitFor(() => {
      expect(screen.getByText('Nessun invito corrisponde al filtro')).toBeInTheDocument();
    });
  });
});
