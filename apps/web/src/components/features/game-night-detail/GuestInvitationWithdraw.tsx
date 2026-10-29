'use client';

/**
 * GuestInvitationWithdraw — the organizer withdraws an invitation sent by email (#4721).
 *
 * A small button on the guest's row opens a confirmation. Confirming cancels the invitation: its link stops
 * working and, if the guest had already accepted, they lose the seat. Nobody is emailed. Only pending and
 * accepted invitations get the button — declined and expired ones are history (the server refuses them too).
 *
 * The confirm button is a plain button, not `AlertDialogAction`: that one is a Radix `Close`, so with an async
 * handler the dialog would close before the request ends, and a failure would have nobody to tell. The dialog
 * closes only when the withdrawal worked; if it fails it stays open and says so.
 */

import { useState, type JSX } from 'react';

import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/overlays/alert-dialog-primitives';
import { Button } from '@/components/ui/primitives/button';
import { useCancelGuestInvitation } from '@/hooks/queries/useGameNights';
import { useTranslation } from '@/hooks/useTranslation';

export interface GuestInvitationWithdrawProps {
  readonly gameNightId: string;
  readonly invitationId: string;
  readonly email: string;
  /** The guest already said yes: withdrawing also takes their seat, and the dialog says so. */
  readonly accepted: boolean;
}

export function GuestInvitationWithdraw({
  gameNightId,
  invitationId,
  email,
  accepted,
}: GuestInvitationWithdrawProps): JSX.Element {
  const { t } = useTranslation();
  const cancel = useCancelGuestInvitation(gameNightId);
  const [open, setOpen] = useState(false);
  const [failed, setFailed] = useState(false);

  async function confirm(): Promise<void> {
    setFailed(false);
    try {
      await cancel.mutateAsync(invitationId);
      setOpen(false);
    } catch {
      setFailed(true);
    }
  }

  return (
    <>
      <Button
        type="button"
        variant="ghost"
        size="sm"
        data-testid="guest-invitation-withdraw"
        aria-label={t('gameNightDetail.participants.withdraw.triggerAria', { email })}
        onClick={() => {
          setFailed(false);
          setOpen(true);
        }}
      >
        {t('gameNightDetail.participants.withdraw.trigger')}
      </Button>

      <AlertDialog
        open={open}
        // A request in flight cannot be dismissed: closing now would hide its outcome.
        onOpenChange={next => {
          if (!cancel.isPending) setOpen(next);
        }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t('gameNightDetail.participants.withdraw.title')}</AlertDialogTitle>
            <AlertDialogDescription>
              {accepted
                ? t('gameNightDetail.participants.withdraw.descriptionAccepted', { email })
                : t('gameNightDetail.participants.withdraw.descriptionPending', { email })}
            </AlertDialogDescription>
          </AlertDialogHeader>

          {failed ? (
            <p
              role="alert"
              data-testid="guest-invitation-withdraw-error"
              className="text-sm text-destructive"
            >
              {t('gameNightDetail.participants.withdraw.error')}
            </p>
          ) : null}

          <AlertDialogFooter>
            <AlertDialogCancel disabled={cancel.isPending}>
              {t('gameNightDetail.participants.withdraw.cancel')}
            </AlertDialogCancel>
            <Button
              type="button"
              variant="destructive"
              data-testid="guest-invitation-withdraw-confirm"
              disabled={cancel.isPending}
              onClick={() => void confirm()}
            >
              {cancel.isPending
                ? t('gameNightDetail.participants.withdraw.confirming')
                : t('gameNightDetail.participants.withdraw.confirm')}
            </Button>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
