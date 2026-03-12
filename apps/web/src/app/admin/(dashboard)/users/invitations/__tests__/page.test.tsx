import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import InvitationsPage from '../page';

const mockGetInvitations = vi.hoisted(() => vi.fn());
const mockGetInvitationStats = vi.hoisted(() => vi.fn());
const mockResendInvitation = vi.hoisted(() => vi.fn());
const mockSendInvitation = vi.hoisted(() => vi.fn());
const mockBulkSendInvitations = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    invitations: {
      getInvitations: mockGetInvitations,
      getInvitationStats: mockGetInvitationStats,
      resendInvitation: mockResendInvitation,
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
      pageSize: 50,
    });
  });

  it('renders page title and action buttons', async () => {
    renderWithQuery(<InvitationsPage />);

    expect(screen.getByText('Invitations')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /invite user/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /bulk invite/i })).toBeInTheDocument();
  });

  it('renders filter tabs with counts', async () => {
    renderWithQuery(<InvitationsPage />);

    await waitFor(() => {
      expect(screen.getByRole('tab', { name: /all/i })).toBeInTheDocument();
    });

    expect(screen.getByRole('tab', { name: /pending/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /accepted/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /expired/i })).toBeInTheDocument();
  });

  it('renders invitation rows after loading', async () => {
    renderWithQuery(<InvitationsPage />);

    await waitFor(() => {
      expect(screen.getByText('alice@example.com')).toBeInTheDocument();
    });

    expect(screen.getByText('bob@example.com')).toBeInTheDocument();
  });

  it('switches filter tabs and refetches', async () => {
    renderWithQuery(<InvitationsPage />);

    await waitFor(() => {
      expect(screen.getByText('alice@example.com')).toBeInTheDocument();
    });

    // Click "Pending" filter
    await user.click(screen.getByRole('tab', { name: /pending/i }));

    await waitFor(() => {
      expect(mockGetInvitations).toHaveBeenCalledWith(
        expect.objectContaining({ status: 'Pending' })
      );
    });
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
      pageSize: 50,
    });

    renderWithQuery(<InvitationsPage />);

    await waitFor(() => {
      expect(screen.getByText('No invitations found.')).toBeInTheDocument();
    });
  });
});
