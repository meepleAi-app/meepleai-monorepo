'use client';

/**
 * Approval Status Badge Component
 *
 * Displays game approval status with semantic colors and accessibility.
 * Issue #3482: Admin approval status management UI
 */

import { Badge } from '@/components/ui/data-display/badge';
import type { ApprovalStatus } from '@/lib/api/schemas/admin.schemas';

interface ApprovalStatusBadgeProps {
  status: ApprovalStatus;
  className?: string;
}

const STATUS_CONFIG: Record<
  ApprovalStatus,
  { label: string; variant: 'default' | 'secondary' | 'destructive' | 'outline' }
> = {
  Draft: { label: 'Bozza', variant: 'secondary' },
  PendingReview: { label: 'In Revisione', variant: 'outline' },
  Approved: { label: 'Approvato', variant: 'default' },
  Rejected: { label: 'Rifiutato', variant: 'destructive' },
};

export function ApprovalStatusBadge({ status, className }: ApprovalStatusBadgeProps) {
   
  const config = STATUS_CONFIG[status];

  if (!config) {
    return <Badge className={className}>Sconosciuto ({status})</Badge>;
  }

  return (
    <Badge variant={config.variant} className={className}>
      {config.label}
    </Badge>
  );
}
