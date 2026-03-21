'use client';

/**
 * Invitation Expired Page
 *
 * Displayed when a user clicks an expired, invalid, or already-used
 * invitation link. Provides a clear message and path to request
 * new access.
 *
 * Features:
 * - Friendly expiry message
 * - Link to request access form
 * - Link back to login
 * - WCAG 2.1 AA compliant
 * - Uses AuthLayout for consistent auth page styling
 */

import { Clock } from 'lucide-react';
import Link from 'next/link';

import { AuthLayout } from '@/components/layouts';
import { Button } from '@/components/ui/button';

export default function InvitationExpiredPage() {
  return (
    <AuthLayout title="Invitation Expired" data-testid="invitation-expired-page">
      <div className="flex flex-col items-center text-center space-y-4">
        {/* Icon */}
        <div className="flex h-12 w-12 items-center justify-center rounded-full bg-amber-100 dark:bg-amber-900/30">
          <Clock className="h-6 w-6 text-amber-600 dark:text-amber-400" aria-hidden="true" />
        </div>

        {/* Description */}
        <p className="text-muted-foreground">
          This invitation link has expired or is no longer valid.
        </p>
        <p className="text-sm text-muted-foreground">
          Invitations are valid for 7 days. You can request new access below.
        </p>

        {/* Actions */}
        <div className="flex flex-col gap-2 pt-4 w-full">
          <Button asChild className="w-full">
            <Link href="/register">Request Access</Link>
          </Button>
          <Button variant="ghost" asChild className="w-full">
            <Link href="/login">Back to Login</Link>
          </Button>
        </div>
      </div>
    </AuthLayout>
  );
}
