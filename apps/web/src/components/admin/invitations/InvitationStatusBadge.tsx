/**
 * InvitationStatusBadge Component (Issue #132)
 *
 * Visual status indicator for user invitations.
 * Displays Pending (amber), Accepted (green), or Expired (red) states.
 */

import { Badge } from '@/components/ui/data-display/badge';

const statusConfig = {
  Pending: {
    variant: 'outline' as const,
    className:
      'border-amber-300 bg-amber-50 text-amber-900 dark:border-amber-700 dark:bg-amber-900/30 dark:text-amber-300',
  },
  Accepted: {
    variant: 'outline' as const,
    className:
      'border-green-300 bg-green-50 text-green-900 dark:border-green-700 dark:bg-green-900/30 dark:text-green-300',
  },
  Expired: {
    variant: 'outline' as const,
    className:
      'border-slate-300 bg-slate-50 text-slate-700 dark:border-zinc-600 dark:bg-zinc-800/50 dark:text-zinc-400',
  },
  Revoked: {
    variant: 'outline' as const,
    className:
      'border-red-300 bg-red-50 text-red-900 dark:border-red-700 dark:bg-red-900/30 dark:text-red-300',
  },
} as const;

export interface InvitationStatusBadgeProps {
  status: 'Pending' | 'Accepted' | 'Expired' | 'Revoked';
}

export function InvitationStatusBadge({ status }: InvitationStatusBadgeProps) {
  const config = statusConfig[status];
  return (
    <Badge variant={config.variant} className={config.className}>
      {status}
    </Badge>
  );
}
