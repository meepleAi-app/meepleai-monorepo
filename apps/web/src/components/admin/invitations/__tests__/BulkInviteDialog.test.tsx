import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import { BulkInviteDialog } from '../BulkInviteDialog';

const mockBulkSendInvitations = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    invitations: {
      bulkSendInvitations: mockBulkSendInvitations,
    },
  },
}));

vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

describe('BulkInviteDialog', () => {
  const user = userEvent.setup();
  const defaultProps = {
    open: true,
    onOpenChange: vi.fn(),
    onSuccess: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders the dialog with textarea', () => {
    renderWithQuery(<BulkInviteDialog {...defaultProps} />);

    expect(screen.getByText('Bulk Invite Users')).toBeInTheDocument();
    expect(screen.getByLabelText('CSV Content')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /preview/i })).toBeInTheDocument();
  });

  it('disables preview button when textarea is empty', () => {
    renderWithQuery(<BulkInviteDialog {...defaultProps} />);

    expect(screen.getByRole('button', { name: /preview/i })).toBeDisabled();
  });

  it('shows preview with valid and invalid rows', async () => {
    renderWithQuery(<BulkInviteDialog {...defaultProps} />);

    const textarea = screen.getByLabelText('CSV Content');
    await user.type(textarea, 'alice@example.com,User\nbad-email,Admin');
    await user.click(screen.getByRole('button', { name: /preview/i }));

    expect(screen.getByText('alice@example.com')).toBeInTheDocument();
    expect(screen.getByText('bad-email')).toBeInTheDocument();
    expect(screen.getByText('Valid')).toBeInTheDocument();
    expect(screen.getByText('Invalid email format')).toBeInTheDocument();
  });

  it('submits valid rows and shows results', async () => {
    mockBulkSendInvitations.mockResolvedValueOnce({
      successful: [
        {
          id: '1',
          email: 'alice@example.com',
          role: 'User',
          status: 'Pending',
          expiresAt: '',
          createdAt: '',
          acceptedAt: null,
          invitedByUserId: '00000000-0000-0000-0000-000000000000',
        },
      ],
      failed: [{ email: 'dup@example.com', error: 'Already invited' }],
    });

    renderWithQuery(<BulkInviteDialog {...defaultProps} />);

    const textarea = screen.getByLabelText('CSV Content');
    await user.type(textarea, 'alice@example.com,User');
    await user.click(screen.getByRole('button', { name: /preview/i }));

    // Preview step: send
    await user.click(screen.getByRole('button', { name: /send 1 invitation/i }));

    await waitFor(() => {
      expect(mockBulkSendInvitations).toHaveBeenCalledWith('alice@example.com,User');
    });

    // Results step
    await waitFor(() => {
      expect(screen.getByText(/1 invitation sent/i)).toBeInTheDocument();
    });
    expect(screen.getByText(/1 failed/i)).toBeInTheDocument();

    // "Done" button
    expect(screen.getByRole('button', { name: /done/i })).toBeInTheDocument();
  });

  it('shows error toast on API failure', async () => {
    mockBulkSendInvitations.mockRejectedValueOnce(new Error('Server error'));
    const { toast } = await import('sonner');

    renderWithQuery(<BulkInviteDialog {...defaultProps} />);

    const textarea = screen.getByLabelText('CSV Content');
    await user.type(textarea, 'alice@example.com,User');
    await user.click(screen.getByRole('button', { name: /preview/i }));
    await user.click(screen.getByRole('button', { name: /send 1 invitation/i }));

    await waitFor(() => {
      expect(toast.error).toHaveBeenCalledWith('Server error');
    });
  });

  it('allows going back from preview to input', async () => {
    renderWithQuery(<BulkInviteDialog {...defaultProps} />);

    const textarea = screen.getByLabelText('CSV Content');
    await user.type(textarea, 'alice@example.com,User');
    await user.click(screen.getByRole('button', { name: /preview/i }));

    // Now on preview step
    expect(screen.getByText('alice@example.com')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /back/i }));

    // Back on input step
    expect(screen.getByLabelText('CSV Content')).toBeInTheDocument();
  });
});
