/**
 * InvitationRow Component (Issue #132)
 *
 * Table row displaying a single invitation with status badge,
 * role, dates, and resend action button.
 */

'use client';

import { RefreshCwIcon } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Button } from '@/components/ui/primitives/button';
import type { InvitationDto } from '@/lib/api/schemas/invitation.schemas';

import { InvitationStatusBadge } from './InvitationStatusBadge';

export interface InvitationRowProps {
  invitation: InvitationDto;
  onResend?: (id: string) => void;
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

export function InvitationRow({ invitation, onResend }: InvitationRowProps) {
  const canResend = invitation.status === 'Pending' || invitation.status === 'Expired';

  return (
    <tr className="border-b border-border/50 transition-colors hover:bg-muted/50">
      <td className="p-2 align-middle">{invitation.email}</td>
      <td className="p-2 align-middle">
        <Badge variant="secondary">{invitation.role}</Badge>
      </td>
      <td className="p-2 align-middle">
        <InvitationStatusBadge status={invitation.status} />
      </td>
      <td className="p-2 align-middle text-sm text-muted-foreground">
        {formatDate(invitation.createdAt)}
      </td>
      <td className="p-2 align-middle text-sm text-muted-foreground">
        {formatDate(invitation.expiresAt)}
      </td>
      <td className="p-2 align-middle">
        {canResend && onResend && (
          <Button
            variant="ghost"
            size="sm"
            onClick={() => onResend(invitation.id)}
            aria-label={`Resend invitation to ${invitation.email}`}
          >
            <RefreshCwIcon className="mr-1 h-3 w-3" />
            Resend
          </Button>
        )}
      </td>
    </tr>
  );
}
