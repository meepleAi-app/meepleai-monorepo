/**
 * ImpersonationBanner Component (Issue #3349)
 *
 * Displays a fixed banner when an admin is impersonating a user.
 * Shows impersonated user info and End Impersonation button.
 *
 * Features:
 * - Fixed position at top of screen
 * - Clear visual indicator of impersonation state
 * - One-click end impersonation
 * - User info display
 *
 * @example
 * ```tsx
 * // In your layout:
 * <ImpersonationBanner
 *   isImpersonating={true}
 *   impersonatedUser={{ id: '123', displayName: 'John Doe', email: 'john@example.com' }}
 *   onEndImpersonation={handleEndImpersonation}
 * />
 * ```
 */

'use client';

import * as React from 'react';

import { UserCog, X, AlertTriangle } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface ImpersonatedUser {
  id: string;
  displayName: string;
  email: string;
}

export interface ImpersonationBannerProps extends React.HTMLAttributes<HTMLDivElement> {
  /** Whether admin is currently impersonating a user */
  isImpersonating: boolean;

  /** The user being impersonated */
  impersonatedUser?: ImpersonatedUser | null;

  /** Session ID for the impersonation (needed to end it) */
  sessionId?: string;

  /** Callback when End Impersonation button is clicked */
  onEndImpersonation: () => void;

  /** Loading state for ending impersonation */
  isLoading?: boolean;
}

// ============================================================================
// Component
// ============================================================================

const ImpersonationBanner = React.forwardRef<HTMLDivElement, ImpersonationBannerProps>(
  (
    { className, isImpersonating, impersonatedUser, onEndImpersonation, isLoading = false, ...props },
    ref
  ) => {
    if (!isImpersonating || !impersonatedUser) {
      return null;
    }

    return (
      <div
        ref={ref}
        role="alert"
        aria-live="polite"
        className={cn(
          'fixed top-0 left-0 right-0 z-[100] flex items-center justify-between px-4 py-2 text-sm',
          'bg-amber-500 text-amber-950 dark:bg-amber-600 dark:text-amber-50',
          'shadow-lg transition-all duration-300 ease-in-out',
          className
        )}
        {...props}
      >
        <div className="flex items-center gap-3">
          <div className="flex items-center gap-2">
            <AlertTriangle className="h-4 w-4" aria-hidden="true" />
            <UserCog className="h-5 w-5" aria-hidden="true" />
          </div>
          <div className="flex flex-col sm:flex-row sm:items-center sm:gap-2">
            <span className="font-semibold">Impersonating:</span>
            <span className="font-medium">{impersonatedUser.displayName}</span>
            <span className="text-amber-800 dark:text-amber-200 hidden sm:inline">
              ({impersonatedUser.email})
            </span>
          </div>
        </div>

        <div className="flex items-center gap-2">
          <span className="text-xs text-amber-800 dark:text-amber-200 hidden md:inline">
            Actions are logged with your admin ID
          </span>
          <Button
            variant="destructive"
            size="sm"
            onClick={onEndImpersonation}
            disabled={isLoading}
            className="h-7 px-3 bg-amber-900 hover:bg-amber-950 text-white"
          >
            {isLoading ? (
              <>
                <span className="animate-spin mr-1">...</span>
                Ending...
              </>
            ) : (
              <>
                <X className="mr-1 h-3 w-3" />
                End Impersonation
              </>
            )}
          </Button>
        </div>
      </div>
    );
  }
);

ImpersonationBanner.displayName = 'ImpersonationBanner';

export { ImpersonationBanner };
