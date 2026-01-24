'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { useStartReview, useReleaseReview } from '@/hooks/queries';
import { Button } from '@/components/ui/primitives/button';
import { Lock, Unlock, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { ReviewLockTimer } from './ReviewLockTimer';
import { ReviewConflictDialog } from './ReviewConflictDialog';

/**
 * Review Action Buttons Component
 *
 * Unified component for review lock management:
 * - Start Review button (when not locked)
 * - Release Review button + Timer (when locked by current admin)
 * - Locked status message (when locked by another admin)
 *
 * Handles:
 * - 409 Conflict errors with ReviewConflictDialog
 * - Timer expiration with toast notification
 * - Keyboard shortcuts (Escape to release when locked by current admin)
 * - Optimistic UI updates
 *
 * Issue #2748: Frontend - Admin Review Lock UI
 */

export interface ReviewActionButtonsProps {
  /** Share request ID to lock/unlock */
  shareRequestId: string;
  /** Current lock status */
  lockStatus: {
    isLocked: boolean;
    isLockedByCurrentAdmin: boolean;
    lockedByAdminName: string | null;
    lockExpiresAt: string | null;
  };
  /** Callback fired after successful lock state change */
  onAction?: () => void;
  /** Enable Escape key shortcut to release review (default: true) */
  enableKeyboardShortcut?: boolean;
}

export function ReviewActionButtons({
  shareRequestId,
  lockStatus,
  onAction,
  enableKeyboardShortcut = true,
}: ReviewActionButtonsProps): JSX.Element {
  const router = useRouter();
  const { mutate: startReview, isPending: isStarting } = useStartReview();
  const { mutate: releaseReview, isPending: isReleasing } = useReleaseReview();
  const [showConflictDialog, setShowConflictDialog] = useState(false);
  const [conflictDetails, setConflictDetails] = useState<{
    adminName: string;
    adminId: string;
  } | null>(null);

  const handleStartReview = () => {
    startReview(
      { shareRequestId },
      {
        onSuccess: () => {
          onAction?.();
        },
        onError: (error: any) => {
          if (error.status === 409) {
            setConflictDetails({
              adminName: error.data?.lockedByAdminName ?? 'Another admin',
              adminId: error.data?.lockedByAdminId ?? 'unknown',
            });
            setShowConflictDialog(true);
          }
        },
      }
    );
  };

  const handleReleaseReview = () => {
    releaseReview(
      { shareRequestId },
      {
        onSuccess: () => {
          onAction?.();
          router.push('/admin/share-requests');
        },
      }
    );
  };

  const handleTimerExpired = () => {
    toast.warning('Your review lock has expired', {
      description: 'Please start a new review to continue.',
    });
    onAction?.();
  };

  // Keyboard shortcut: Escape to release review (Issue #2748)
  useEffect(() => {
    if (!enableKeyboardShortcut || !lockStatus.isLockedByCurrentAdmin || isReleasing) {
      return;
    }

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault();
        handleReleaseReview();
      }
    };

    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [enableKeyboardShortcut, lockStatus.isLockedByCurrentAdmin, isReleasing]);

  // Not locked - show Start Review button
  if (!lockStatus.isLocked) {
    return (
      <>
        <Button className="w-full" onClick={handleStartReview} disabled={isStarting} data-testid="start-review-button">
          {isStarting ? (
            <>
              <Loader2 className="w-4 h-4 mr-2 animate-spin" />
              Starting...
            </>
          ) : (
            <>
              <Lock className="w-4 h-4 mr-2" />
              Start Review
            </>
          )}
        </Button>
        <ReviewConflictDialog
          open={showConflictDialog}
          onClose={() => setShowConflictDialog(false)}
          conflictDetails={conflictDetails}
        />
      </>
    );
  }

  // Locked by current admin - show Timer + Release button
  if (lockStatus.isLockedByCurrentAdmin && lockStatus.lockExpiresAt) {
    return (
      <div className="space-y-3">
        <ReviewLockTimer expiresAt={lockStatus.lockExpiresAt} onExpired={handleTimerExpired} />
        <Button
          variant="outline"
          className="w-full"
          onClick={handleReleaseReview}
          disabled={isReleasing}
          data-testid="release-review-button"
        >
          {isReleasing ? (
            <>
              <Loader2 className="w-4 h-4 mr-2 animate-spin" />
              Releasing...
            </>
          ) : (
            <>
              <Unlock className="w-4 h-4 mr-2" />
              Release Review
            </>
          )}
        </Button>
      </div>
    );
  }

  // Locked by another admin - show info message
  return (
    <div className="p-3 rounded-lg bg-muted text-sm" data-testid="locked-by-other-message">
      <p className="font-medium">Currently being reviewed by:</p>
      <p className="text-muted-foreground mt-1">{lockStatus.lockedByAdminName}</p>
    </div>
  );
}