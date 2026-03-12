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
    className: 'border-amber-300 bg-amber-50 text-amber-900',
  },
  Accepted: {
    variant: 'outline' as const,
    className: 'border-green-300 bg-green-50 text-green-900',
  },
  Expired: {
    variant: 'outline' as const,
    className: 'border-red-300 bg-red-50 text-red-900',
  },
} as const;

export interface InvitationStatusBadgeProps {
  status: 'Pending' | 'Accepted' | 'Expired';
}

export function InvitationStatusBadge({ status }: InvitationStatusBadgeProps) {
  const config = statusConfig[status];
  return (
    <Badge variant={config.variant} className={config.className}>
      {status}
    </Badge>
  );
}
