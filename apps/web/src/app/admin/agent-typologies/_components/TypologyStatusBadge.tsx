/**
 * Typology Status Badge Component (Issue #3179)
 *
 * Visual status indicator for agent typologies.
 * Maps status to badge variant and color.
 */

import { Badge } from '@/components/ui/data-display/badge';

export type TypologyStatus = 'Draft' | 'PendingReview' | 'Approved' | 'Rejected';

interface TypologyStatusBadgeProps {
  status: TypologyStatus;
}

export function TypologyStatusBadge({ status }: TypologyStatusBadgeProps) {
  switch (status) {
    case 'Draft':
      return <Badge variant="secondary">Draft</Badge>;
    case 'PendingReview':
      return <Badge variant="outline">Pending Review</Badge>;
    case 'Approved':
      return <Badge variant="default">Approved</Badge>;
    case 'Rejected':
      return <Badge variant="destructive">Rejected</Badge>;
    default:
      return <Badge variant="secondary">{status}</Badge>;
  }
}
