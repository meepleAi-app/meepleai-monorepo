/**
 * Rate Limit Banner Component
 *
 * Displays a user-friendly banner when API rate limits are encountered.
 * Features countdown timer and automatic dismissal when time expires.
 *
 * @example
 * ```tsx
 * const { isRateLimited, message, remainingSeconds } = useRateLimitHandler();
 *
 * {isRateLimited && (
 *   <RateLimitBanner
 *     message={message}
 *     remainingSeconds={remainingSeconds}
 *   />
 * )}
 * ```
 */

import React from 'react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { cn } from '@/lib/utils';

export interface RateLimitBannerProps {
  /** User-friendly message describing the rate limit */
  message: string | null;
  /** Seconds remaining in countdown (optional, for additional display) */
  remainingSeconds?: number;
  /** Additional CSS classes */
  className?: string;
  /** Whether to show the countdown timer separately */
  showCountdown?: boolean;
  /** Callback when the banner is dismissed (if dismissible) */
  onDismiss?: () => void;
  /** Whether the banner can be dismissed */
  dismissible?: boolean;
}

/**
 * Banner component for displaying rate limit messages with countdown
 */
export const RateLimitBanner: React.FC<RateLimitBannerProps> = ({
  message,
  remainingSeconds = 0,
  className,
  showCountdown = true,
  onDismiss,
  dismissible = false,
}) => {
  if (!message || remainingSeconds <= 0) {
    return null;
  }

  return (
    <Alert
      variant="destructive"
      className={cn('relative', className)}
      role="alert"
      aria-live="polite"
      aria-atomic="true"
    >
      <div className="flex items-start justify-between">
        <div className="flex-1">
          <AlertTitle>Rate Limit Exceeded</AlertTitle>
          <AlertDescription>
            <p>{message}</p>
            {showCountdown && remainingSeconds > 0 && (
              <p className="mt-2 font-mono text-xs" aria-live="polite" aria-atomic="true">
                Retry available in: <span className="font-bold">{remainingSeconds}s</span>
              </p>
            )}
          </AlertDescription>
        </div>
        {dismissible && onDismiss && (
          <button
            onClick={onDismiss}
            className="ml-4 text-destructive hover:text-destructive/80 focus:outline-none focus:ring-2 focus:ring-destructive focus:ring-offset-2"
            aria-label="Dismiss rate limit notification"
          >
            <svg
              xmlns="http://www.w3.org/2000/svg"
              width="16"
              height="16"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            >
              <line x1="18" y1="6" x2="6" y2="18"></line>
              <line x1="6" y1="6" x2="18" y2="18"></line>
            </svg>
          </button>
        )}
      </div>
    </Alert>
  );
};

RateLimitBanner.displayName = 'RateLimitBanner';
