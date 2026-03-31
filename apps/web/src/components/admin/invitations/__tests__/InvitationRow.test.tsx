import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

import type { InvitationDto } from '@/lib/api/schemas/invitation.schemas';

import { InvitationRow } from '../InvitationRow';

const pendingInvitation: InvitationDto = {
  id: 'inv-1',
  email: 'alice@example.com',
  role: 'User',
  status: 'Pending',
  createdAt: '2026-03-01T00:00:00Z',
  expiresAt: '2026-03-08T00:00:00Z',
  acceptedAt: null,
  invitedByUserId: '00000000-0000-0000-0000-000000000001',
};

const acceptedInvitation: InvitationDto = {
  ...pendingInvitation,
  status: 'Accepted',
  acceptedAt: '2026-03-02T00:00:00Z',
};

describe('InvitationRow', () => {
  const user = userEvent.setup();

  it('renders email, role, and status', () => {
    render(
      <table>
        <tbody>
          <InvitationRow invitation={pendingInvitation} />
        </tbody>
      </table>
    );
    expect(screen.getByText('alice@example.com')).toBeInTheDocument();
    expect(screen.getByText('User')).toBeInTheDocument();
    expect(screen.getByText('Pending')).toBeInTheDocument();
  });

  it('shows Resend and Revoke buttons only for Pending invitations', () => {
    render(
      <table>
        <tbody>
          <InvitationRow invitation={pendingInvitation} onResend={vi.fn()} onRevoke={vi.fn()} />
        </tbody>
      </table>
    );
    expect(
      screen.getByRole('button', { name: /resend invitation to alice@example.com/i })
    ).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: /revoke invitation for alice@example.com/i })
    ).toBeInTheDocument();
  });

  it('does NOT show Resend/Revoke for Accepted invitations', () => {
    render(
      <table>
        <tbody>
          <InvitationRow invitation={acceptedInvitation} onResend={vi.fn()} onRevoke={vi.fn()} />
        </tbody>
      </table>
    );
    expect(screen.queryByRole('button', { name: /resend/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /revoke/i })).not.toBeInTheDocument();
  });

  it('calls onResend with invitation id when Resend is clicked', async () => {
    const onResend = vi.fn();
    render(
      <table>
        <tbody>
          <InvitationRow invitation={pendingInvitation} onResend={onResend} onRevoke={vi.fn()} />
        </tbody>
      </table>
    );
    await user.click(
      screen.getByRole('button', { name: /resend invitation to alice@example.com/i })
    );
    expect(onResend).toHaveBeenCalledWith('inv-1');
  });

  it('disables Resend button when isResending=true', () => {
    render(
      <table>
        <tbody>
          <InvitationRow
            invitation={pendingInvitation}
            onResend={vi.fn()}
            onRevoke={vi.fn()}
            isResending
          />
        </tbody>
      </table>
    );
    expect(
      screen.getByRole('button', { name: /resend invitation to alice@example.com/i })
    ).toBeDisabled();
  });

  it('disables Revoke trigger when isRevoking=true', () => {
    render(
      <table>
        <tbody>
          <InvitationRow
            invitation={pendingInvitation}
            onResend={vi.fn()}
            onRevoke={vi.fn()}
            isRevoking
          />
        </tbody>
      </table>
    );
    expect(
      screen.getByRole('button', { name: /revoke invitation for alice@example.com/i })
    ).toBeDisabled();
  });
});
