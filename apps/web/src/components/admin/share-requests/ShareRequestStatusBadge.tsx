import type { JSX } from 'react';

import { Badge } from '@/components/ui/data-display/badge';
import type { ShareRequestStatus } from '@/lib/api/schemas/admin-share-requests.schemas';

import { STATUS_LABELS } from './share-request-labels';

const VARIANTS: Record<ShareRequestStatus, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  Pending: 'secondary',
  InReview: 'default',
  ChangesRequested: 'outline',
  Approved: 'default',
  Rejected: 'destructive',
  Withdrawn: 'outline',
};

export function ShareRequestStatusBadge({ status }: { status: ShareRequestStatus }): JSX.Element {
  return <Badge variant={VARIANTS[status]}>{STATUS_LABELS[status]}</Badge>;
}
