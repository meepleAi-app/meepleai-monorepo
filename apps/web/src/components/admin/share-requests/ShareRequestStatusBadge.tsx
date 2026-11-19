import type { JSX } from 'react';

import { Badge } from '@/components/ui/data-display/badge';
import type { ShareRequestStatus } from '@/lib/api/schemas/admin-share-requests.schemas';

import { STATUS_LABELS } from './share-request-labels';

// `secondary` is the green of the theme: it marks the approved request, `default` (the primary orange) the one in
// review, the outline the ones waiting on someone.
const VARIANTS: Record<ShareRequestStatus, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  Pending: 'outline',
  InReview: 'default',
  ChangesRequested: 'outline',
  Approved: 'secondary',
  Rejected: 'destructive',
  Withdrawn: 'outline',
};

export function ShareRequestStatusBadge({ status }: { status: ShareRequestStatus }): JSX.Element {
  return <Badge variant={VARIANTS[status]}>{STATUS_LABELS[status]}</Badge>;
}
