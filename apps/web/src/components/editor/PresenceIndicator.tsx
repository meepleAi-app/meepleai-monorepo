/**
 * Presence Indicator (Issue #2055)
 *
 * Shows the current lock status and other editors:
 * - Green: You have the lock
 * - Orange: Someone else is editing (with their email)
 * - Gray: No lock / Read-only mode
 *
 * Also shows lock expiration countdown.
 */

'use client';

import * as React from 'react';

import type { EditorLock } from '@/lib/api/schemas';
import { cn } from '@/lib/utils';
import type { LockAcquisitionStatus } from '@/stores/RuleSpecLockStore';

export interface PresenceIndicatorProps {
  /** Current lock status */
  lockStatus: EditorLock | null;
  /** Lock acquisition status */
  acquisitionStatus: LockAcquisitionStatus;
  /** Lock error message */
  lockError?: string | null;
  /** Optional className */
  className?: string;
}

export function PresenceIndicator({
  lockStatus,
  acquisitionStatus,
  lockError,
  className,
}: PresenceIndicatorProps) {
  const [timeRemaining, setTimeRemaining] = React.useState<string | null>(null);

  // Calculate time remaining until lock expires
  React.useEffect(() => {
    if (!lockStatus?.expiresAt || !lockStatus.isCurrentUserLock) {
      setTimeRemaining(null);
      return;
    }

    const expiresAtValue = lockStatus.expiresAt;

    const updateTimeRemaining = () => {
      const expiresAt = new Date(expiresAtValue).getTime();
      const now = Date.now();
      const remaining = expiresAt - now;

      if (remaining <= 0) {
        setTimeRemaining('Scaduto');
        return;
      }

      const minutes = Math.floor(remaining / 60000);
      const seconds = Math.floor((remaining % 60000) / 1000);
      setTimeRemaining(`${minutes}:${seconds.toString().padStart(2, '0')}`);
    };

    updateTimeRemaining();
    const interval = setInterval(updateTimeRemaining, 1000);

    return () => clearInterval(interval);
  }, [lockStatus?.expiresAt, lockStatus?.isCurrentUserLock]);

  // Determine status color and icon
  const getStatusConfig = () => {
    switch (acquisitionStatus) {
      case 'acquiring':
        return {
          color: 'text-yellow-600 bg-yellow-50 border-yellow-200',
          dotColor: 'bg-yellow-500',
          icon: (
            <svg className="animate-spin h-4 w-4" viewBox="0 0 24 24" fill="none">
              <circle
                className="opacity-25"
                cx="12"
                cy="12"
                r="10"
                stroke="currentColor"
                strokeWidth="4"
              />
              <path
                className="opacity-75"
                fill="currentColor"
                d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
              />
            </svg>
          ),
          label: 'Acquisizione lock...',
        };

      case 'acquired':
        return {
          color: 'text-green-600 bg-green-50 border-green-200',
          dotColor: 'bg-green-500',
          icon: (
            <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z"
              />
            </svg>
          ),
          label: 'Stai modificando',
        };

      case 'conflict':
        return {
          color: 'text-orange-600 bg-orange-50 border-orange-200',
          dotColor: 'bg-orange-500',
          icon: (
            <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
              />
            </svg>
          ),
          label: lockStatus?.lockedByUserEmail
            ? `In modifica da ${lockStatus.lockedByUserEmail}`
            : 'Conflitto rilevato',
        };

      case 'failed':
        return {
          color: 'text-red-600 bg-red-50 border-red-200',
          dotColor: 'bg-red-500',
          icon: (
            <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M6 18L18 6M6 6l12 12"
              />
            </svg>
          ),
          label: lockError ?? 'Impossibile acquisire lock',
        };

      case 'idle':
      default:
        return {
          color: 'text-gray-600 bg-gray-50 border-gray-200',
          dotColor: 'bg-gray-400',
          icon: (
            <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M8 11V7a4 4 0 118 0m-4 8v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2z"
              />
            </svg>
          ),
          label: 'Sola lettura',
        };
    }
  };

  const config = getStatusConfig();

  return (
    <div
      className={cn(
        'inline-flex items-center gap-2 px-3 py-1.5 rounded-full border text-sm',
        config.color,
        className
      )}
    >
      {/* Status dot (pulsing animation for acquired) */}
      <span
        className={cn(
          'w-2 h-2 rounded-full',
          config.dotColor,
          acquisitionStatus === 'acquired' && 'animate-pulse'
        )}
      />

      {/* Icon */}
      {config.icon}

      {/* Label */}
      <span className="font-medium">{config.label}</span>

      {/* Time remaining (only for acquired locks) */}
      {timeRemaining && acquisitionStatus === 'acquired' && (
        <span className="text-xs opacity-75 ml-1">({timeRemaining})</span>
      )}
    </div>
  );
}

/**
 * Compact version for toolbar
 */
export function PresenceIndicatorCompact({
  lockStatus,
  acquisitionStatus,
  className,
}: Omit<PresenceIndicatorProps, 'lockError'>) {
  const isLocked = acquisitionStatus === 'acquired';
  const isOtherUser = acquisitionStatus === 'conflict' && lockStatus?.lockedByUserEmail;

  return (
    <div
      className={cn('inline-flex items-center gap-1.5', className)}
      title={
        isLocked
          ? 'Hai il controllo esclusivo'
          : isOtherUser
            ? `In modifica da ${lockStatus?.lockedByUserEmail}`
            : 'Nessun lock attivo'
      }
    >
      <span
        className={cn(
          'w-2.5 h-2.5 rounded-full',
          isLocked ? 'bg-green-500 animate-pulse' : isOtherUser ? 'bg-orange-500' : 'bg-gray-400'
        )}
      />
      {isOtherUser && (
        <span className="text-xs text-orange-600 max-w-[100px] truncate">
          {lockStatus?.lockedByUserEmail}
        </span>
      )}
    </div>
  );
}

export default PresenceIndicator;
