import { Badge } from '@/components/ui/data-display/badge';
import { Lock, LockOpen, Clock } from 'lucide-react';
import { formatDistanceToNow } from 'date-fns';
import type { LockStatusDto } from '@/lib/api/schemas/admin-share-requests.schemas';

/**
 * Lock Status Badge Component
 *
 * Displays review lock status with admin name and expiration time.
 *
 * States:
 * - Not Locked: Available for review (green)
 * - Locked by You: You are reviewing (blue)
 * - Locked by Other: Another admin is reviewing (red)
 * - Lock Expiring Soon: < 5 minutes remaining (yellow warning)
 *
 * Issue #2745: Frontend - Admin Review Interface
 */

interface LockStatusBadgeProps {
  lockStatus: LockStatusDto;
  className?: string;
}

export function LockStatusBadge({ lockStatus, className }: LockStatusBadgeProps): JSX.Element {
  if (!lockStatus.isLocked) {
    return (
      <Badge variant="outline" className={`border-green-300 bg-green-50 text-green-700 ${className ?? ''}`}>
        <LockOpen className="mr-1 h-3 w-3" />
        Available
      </Badge>
    );
  }

  if (lockStatus.isLockedByCurrentAdmin) {
    const isExpiringSoon = lockStatus.lockExpiresAt
      ? new Date(lockStatus.lockExpiresAt).getTime() - Date.now() < 5 * 60 * 1000 // 5 minutes
      : false;

    if (isExpiringSoon && lockStatus.lockExpiresAt) {
      return (
        <Badge variant="outline" className={`border-yellow-300 bg-yellow-50 text-yellow-700 ${className ?? ''}`}>
          <Clock className="mr-1 h-3 w-3" />
          Expires in {formatDistanceToNow(new Date(lockStatus.lockExpiresAt))}
        </Badge>
      );
    }

    return (
      <Badge variant="outline" className={`border-blue-300 bg-blue-50 text-blue-700 ${className ?? ''}`}>
        <Lock className="mr-1 h-3 w-3" />
        Reviewing
      </Badge>
    );
  }

  return (
    <Badge variant="outline" className={`border-red-300 bg-red-50 text-red-700 ${className ?? ''}`}>
      <Lock className="mr-1 h-3 w-3" />
      Locked by {lockStatus.lockedByAdminName ?? 'Another Admin'}
    </Badge>
  );
}