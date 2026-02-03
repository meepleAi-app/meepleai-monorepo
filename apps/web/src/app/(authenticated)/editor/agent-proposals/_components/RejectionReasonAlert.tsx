'use client';

/**
 * RejectionReasonAlert Component (Issue #3182)
 *
 * Displays rejection reason when proposal is rejected.
 */

import { AlertCircle } from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';

interface RejectionReasonAlertProps {
  reason: string;
}

export function RejectionReasonAlert({ reason }: RejectionReasonAlertProps) {
  return (
    <Alert variant="destructive">
      <AlertCircle className="h-4 w-4" />
      <AlertTitle>Rejected</AlertTitle>
      <AlertDescription>{reason}</AlertDescription>
    </Alert>
  );
}
