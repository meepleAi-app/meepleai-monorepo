/**
 * GuestInvitationWithdraw — #4721: the organizer withdraws an invitation sent by email.
 *
 * The real mutation hook runs on a fake API, so "in flight" and "failed" are the hook's own states. The
 * rule that matters: the dialog closes only when the withdrawal worked — a Radix `AlertDialogAction` would
 * close it before the request ended and leave a failure with nobody to tell.
 */

import type { ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { GuestInvitationWithdraw } from '../GuestInvitationWithdraw';

const cancelGuestInvitation = vi.hoisted(() => vi.fn());
vi.mock('@/lib/api', () => ({
  api: { gameNights: { cancelGuestInvitation } },
}));
vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({
    t: (key: string, params?: { email?: string }) =>
      params?.email ? `${key}|${params.email}` : key,
  }),
}));

const NIGHT_ID = '11111111-1111-4111-8111-111111111111';
const INVITATION_ID = '55555555-5555-4555-8555-555555555555';

function renderWithdraw(accepted = false) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
  return render(
    <GuestInvitationWithdraw
      gameNightId={NIGHT_ID}
      invitationId={INVITATION_ID}
      email="carla@example.com"
      accepted={accepted}
    />,
    { wrapper }
  );
}

const openDialog = () => fireEvent.click(screen.getByTestId('guest-invitation-withdraw'));
const confirmButton = () => screen.getByTestId('guest-invitation-withdraw-confirm');

beforeEach(() => {
  vi.clearAllMocks();
  cancelGuestInvitation.mockResolvedValue(undefined);
});

describe('GuestInvitationWithdraw (#4721)', () => {
  it('shows only a button until it is asked, named after the guest', () => {
    renderWithdraw();

    expect(screen.queryByRole('alertdialog')).toBeNull();
    expect(screen.getByTestId('guest-invitation-withdraw')).toHaveAttribute(
      'aria-label',
      'gameNightDetail.participants.withdraw.triggerAria|carla@example.com'
    );
  });

  it('asks for confirmation, and says what a pending invitation loses', async () => {
    renderWithdraw(false);

    openDialog();

    const dialog = await screen.findByRole('alertdialog');
    expect(dialog).toHaveTextContent(
      'gameNightDetail.participants.withdraw.descriptionPending|carla@example.com'
    );
    expect(dialog).not.toHaveTextContent('descriptionAccepted');
    expect(cancelGuestInvitation).not.toHaveBeenCalled();
  });

  it('says that an accepted guest loses the seat', async () => {
    renderWithdraw(true);

    openDialog();

    expect(await screen.findByRole('alertdialog')).toHaveTextContent(
      'gameNightDetail.participants.withdraw.descriptionAccepted|carla@example.com'
    );
  });

  it('withdraws that invitation of that night on confirmation, and closes once it worked', async () => {
    renderWithdraw();
    openDialog();
    await screen.findByRole('alertdialog');

    fireEvent.click(confirmButton());

    await waitFor(() => expect(screen.queryByRole('alertdialog')).toBeNull());
    expect(cancelGuestInvitation).toHaveBeenCalledTimes(1);
    expect(cancelGuestInvitation).toHaveBeenCalledWith(NIGHT_ID, INVITATION_ID);
  });

  it('stays open and says so when the withdrawal fails', async () => {
    cancelGuestInvitation.mockRejectedValue(new Error('409'));
    renderWithdraw();
    openDialog();
    await screen.findByRole('alertdialog');

    fireEvent.click(confirmButton());

    const error = await screen.findByTestId('guest-invitation-withdraw-error');
    expect(error).toHaveAttribute('role', 'alert');
    expect(error).toHaveTextContent('gameNightDetail.participants.withdraw.error');
    expect(screen.getByRole('alertdialog')).toBeInTheDocument();
    // It can be tried again.
    expect(confirmButton()).toBeEnabled();
  });

  it('while the request is in flight, cannot be confirmed twice or dismissed, and shows it', async () => {
    let finish!: () => void;
    cancelGuestInvitation.mockReturnValue(new Promise<void>(resolve => (finish = resolve)));
    renderWithdraw();
    openDialog();
    await screen.findByRole('alertdialog');

    fireEvent.click(confirmButton());

    await waitFor(() => expect(confirmButton()).toBeDisabled());
    expect(confirmButton()).toHaveTextContent('gameNightDetail.participants.withdraw.confirming');
    fireEvent.click(confirmButton());
    fireEvent.keyDown(screen.getByRole('alertdialog'), { key: 'Escape' });
    expect(screen.getByRole('alertdialog')).toBeInTheDocument();
    expect(cancelGuestInvitation).toHaveBeenCalledTimes(1);

    finish();
    await waitFor(() => expect(screen.queryByRole('alertdialog')).toBeNull());
  });

  it('does nothing when the organizer cancels', async () => {
    renderWithdraw();
    openDialog();
    await screen.findByRole('alertdialog');

    fireEvent.click(screen.getByText('gameNightDetail.participants.withdraw.cancel'));

    await waitFor(() => expect(screen.queryByRole('alertdialog')).toBeNull());
    expect(cancelGuestInvitation).not.toHaveBeenCalled();
  });

  it('gives the focus back to the row button when the dialog closes, so the list keeps its place', async () => {
    renderWithdraw();
    const trigger = screen.getByTestId('guest-invitation-withdraw');
    openDialog();
    await screen.findByRole('alertdialog');

    fireEvent.click(screen.getByText('gameNightDetail.participants.withdraw.cancel'));

    await waitFor(() => expect(screen.queryByRole('alertdialog')).toBeNull());
    await waitFor(() => expect(trigger).toHaveFocus());
  });

  it('forgets an earlier failure when it is opened again', async () => {
    cancelGuestInvitation.mockRejectedValueOnce(new Error('409'));
    renderWithdraw();
    openDialog();
    await screen.findByRole('alertdialog');
    fireEvent.click(confirmButton());
    await screen.findByTestId('guest-invitation-withdraw-error');
    fireEvent.click(screen.getByText('gameNightDetail.participants.withdraw.cancel'));
    await waitFor(() => expect(screen.queryByRole('alertdialog')).toBeNull());

    openDialog();

    await screen.findByRole('alertdialog');
    expect(screen.queryByTestId('guest-invitation-withdraw-error')).toBeNull();
  });
});
