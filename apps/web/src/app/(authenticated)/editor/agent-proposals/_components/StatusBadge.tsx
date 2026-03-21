'use client';

/**
 * StatusBadge Component (Issue #3182)
 *
 * Visual badge for proposal status with appropriate colors.
 */

import { Badge } from '@/components/ui/data-display/badge';

type Status = 'Draft' | 'PendingReview' | 'Approved' | 'Rejected';

interface StatusBadgeProps {
  status: Status;
}

export function StatusBadge({ status }: StatusBadgeProps) {
  const variants: Record<Status, 'secondary' | 'default' | 'destructive'> = {
    Draft: 'secondary',
    PendingReview: 'default',
    Approved: 'default',
    Rejected: 'destructive',
  };

  const labels: Record<Status, string> = {
    Draft: 'Draft',
    PendingReview: 'Pending Review',
    Approved: 'Approved',
    Rejected: 'Rejected',
  };

  const customClasses: Record<Status, string> = {
    Draft: '',
    PendingReview: 'bg-yellow-100 text-yellow-800 dark:bg-yellow-950 dark:text-yellow-200',
    Approved: 'bg-green-100 text-green-800 dark:bg-green-950 dark:text-green-200',
    Rejected: '',
  };

  const variant = variants[status];
  const label = labels[status];
  const customClass = customClasses[status];

  return (
    <Badge variant={variant} className={customClass}>
      {label}
    </Badge>
  );
}
