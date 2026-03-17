/**
 * InvitationRow Component (Issue #132)
 *
 * Table row displaying a single invitation with status badge,
 * role, dates, resend and revoke action buttons.
 */

'use client';

import { RefreshCwIcon, XCircleIcon } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/overlays/alert-dialog-primitives';
import { Button } from '@/components/ui/primitives/button';
import type { InvitationDto } from '@/lib/api/schemas/invitation.schemas';

import { InvitationStatusBadge } from './InvitationStatusBadge';

export interface InvitationRowProps {
  invitation: InvitationDto;
  onResend?: (id: string) => void;
  onRevoke?: (id: string) => void;
}

function formatDate(iso: string): string {
  try {
    return new Date(iso).toLocaleDateString('en-US', {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
    });
  } catch {
    return iso;
  }
}

export function InvitationRow({ invitation, onResend, onRevoke }: InvitationRowProps) {
  const isPending = invitation.status === 'Pending';

  return (
    <tr className="border-b border-slate-100 dark:border-zinc-800/60 hover:bg-slate-50/50 dark:hover:bg-zinc-700/30">
      <td className="p-3 align-middle">{invitation.email}</td>
      <td className="p-3 align-middle">
        <Badge variant="secondary">{invitation.role}</Badge>
      </td>
      <td className="p-3 align-middle">
        <InvitationStatusBadge status={invitation.status} />
      </td>
      <td className="p-3 align-middle text-sm text-muted-foreground">
        {formatDate(invitation.createdAt)}
      </td>
      <td className="p-3 align-middle text-sm text-muted-foreground">
        {formatDate(invitation.expiresAt)}
      </td>
      <td className="p-3 align-middle">
        <div className="flex items-center gap-1">
          {isPending && onResend && (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => onResend(invitation.id)}
              aria-label={`Resend invitation to ${invitation.email}`}
              className="gap-1"
            >
              <RefreshCwIcon className="h-3 w-3" />
              Resend
            </Button>
          )}
          {isPending && onRevoke && (
            <AlertDialog>
              <AlertDialogTrigger asChild>
                <Button
                  variant="ghost"
                  size="sm"
                  className="gap-1 text-destructive hover:text-destructive"
                  aria-label={`Revoke invitation for ${invitation.email}`}
                >
                  <XCircleIcon className="h-3 w-3" />
                  Revoke
                </Button>
              </AlertDialogTrigger>
              <AlertDialogContent>
                <AlertDialogHeader>
                  <AlertDialogTitle>Revoke Invitation</AlertDialogTitle>
                  <AlertDialogDescription>
                    This will revoke the invitation sent to <strong>{invitation.email}</strong>. The
                    invitation link will no longer be valid.
                  </AlertDialogDescription>
                </AlertDialogHeader>
                <AlertDialogFooter>
                  <AlertDialogCancel>Cancel</AlertDialogCancel>
                  <AlertDialogAction
                    onClick={() => onRevoke(invitation.id)}
                    className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
                  >
                    Revoke
                  </AlertDialogAction>
                </AlertDialogFooter>
              </AlertDialogContent>
            </AlertDialog>
          )}
        </div>
      </td>
    </tr>
  );
}
