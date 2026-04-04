'use client';

/**
 * StatusBadge Component (Issue #3182)
 *
 * Visual badge for proposal status with appropriate colors and icons.
 */

import { CheckCircle, Clock, FileEdit, XCircle } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';

type Status = 'Draft' | 'PendingReview' | 'Approved' | 'Rejected';

interface StatusBadgeProps {
  status: Status;
}

/* eslint-disable security/detect-object-injection -- Safe status config Record access */
const statusConfig: Record<
  Status,
  {
    variant: 'secondary' | 'default' | 'destructive';
    label: string;
    customClass: string;
    Icon: typeof CheckCircle;
  }
> = {
  Draft: {
    variant: 'secondary',
    label: 'Draft',
    customClass: '',
    Icon: FileEdit,
  },
  PendingReview: {
    variant: 'default',
    label: 'Pending Review',
    customClass: 'bg-yellow-100 text-yellow-800 dark:bg-yellow-950 dark:text-yellow-200',
    Icon: Clock,
  },
  Approved: {
    variant: 'default',
    label: 'Approved',
    customClass: 'bg-green-100 text-green-800 dark:bg-green-950 dark:text-green-200',
    Icon: CheckCircle,
  },
  Rejected: {
    variant: 'destructive',
    label: 'Rejected',
    customClass: '',
    Icon: XCircle,
  },
};

export function StatusBadge({ status }: StatusBadgeProps) {
  const config = statusConfig[status];
  const Icon = config.Icon;

  return (
    <Badge variant={config.variant} className={config.customClass}>
      <Icon className="h-3 w-3" aria-hidden="true" />
      {config.label}
    </Badge>
  );
}
/* eslint-enable security/detect-object-injection */
