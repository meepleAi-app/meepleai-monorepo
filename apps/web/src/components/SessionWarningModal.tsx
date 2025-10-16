/**
 * SessionWarningModal Component (AUTH-05)
 *
 * Displays a warning modal when user's session is about to expire.
 * Shows countdown timer and allows user to extend session or log out.
 *
 * Features:
 * - WCAG 2.1 AA compliant (uses AccessibleModal, AccessibleButton)
 * - Real-time countdown display
 * - "Stay Logged In" button extends session
 * - "Log Out Now" button redirects to login
 * - Keyboard accessible (ESC to close, Tab navigation)
 * - Screen reader friendly
 *
 * @example
 * ```tsx
 * const { remainingMinutes, isNearExpiry } = useSessionCheck();
 *
 * if (isNearExpiry) {
 *   return (
 *     <SessionWarningModal
 *       remainingMinutes={remainingMinutes!}
 *       onStayLoggedIn={handleExtend}
 *       onLogOut={handleLogOut}
 *     />
 *   );
 * }
 * ```
 */

import { useEffect, useState } from 'react';
import { AccessibleModal } from './accessible/AccessibleModal';
import { AccessibleButton } from './accessible/AccessibleButton';

export interface SessionWarningModalProps {
  /** Minutes remaining until session expires */
  remainingMinutes: number;

  /** Callback when user clicks "Stay Logged In" */
  onStayLoggedIn: () => void;

  /** Callback when user clicks "Log Out Now" */
  onLogOut: () => void;
}

/**
 * SessionWarningModal component
 */
export function SessionWarningModal({
  remainingMinutes: initialRemainingMinutes,
  onStayLoggedIn,
  onLogOut,
}: SessionWarningModalProps) {
  const [remainingMinutes, setRemainingMinutes] = useState(initialRemainingMinutes);

  // Update countdown every minute
  useEffect(() => {
    setRemainingMinutes(initialRemainingMinutes);

    const intervalId = setInterval(() => {
      setRemainingMinutes((prev) => Math.max(0, prev - 1));
    }, 60 * 1000); // 1 minute

    return () => clearInterval(intervalId);
  }, [initialRemainingMinutes]);

  // Auto-logout when time expires
  useEffect(() => {
    if (remainingMinutes <= 0) {
      onLogOut();
    }
  }, [remainingMinutes, onLogOut]);

  return (
    <AccessibleModal
      isOpen={true}
      onClose={onLogOut}
      title="Session Expiring Soon"
      description="Your session is about to expire due to inactivity"
      closeOnBackdropClick={false}
      showCloseButton={false}
      size="md"
    >
      <div className="space-y-6">
        {/* Warning Icon */}
        <div className="flex justify-center">
          <div
            className="
              flex
              items-center
              justify-center
              w-16
              h-16
              bg-amber-100
              dark:bg-amber-900/30
              rounded-full
            "
            aria-hidden="true"
          >
            <svg
              className="w-8 h-8 text-amber-600 dark:text-amber-400"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
              />
            </svg>
          </div>
        </div>

        {/* Countdown Timer */}
        <div className="text-center">
          <div className="text-4xl font-bold text-amber-600 dark:text-amber-400" aria-live="polite" aria-atomic="true">
            {remainingMinutes}
          </div>
          <div className="mt-1 text-sm text-slate-600 dark:text-slate-400">
            {remainingMinutes === 1 ? 'minute remaining' : 'minutes remaining'}
          </div>
        </div>

        {/* Message */}
        <p className="text-center text-slate-700 dark:text-slate-300">
          You haven&apos;t been active for a while. For your security, your session will expire soon.
          <br />
          <span className="text-sm text-slate-600 dark:text-slate-400">
            Click &quot;Stay Logged In&quot; to continue your session.
          </span>
        </p>

        {/* Actions */}
        <div className="flex flex-col-reverse sm:flex-row gap-3">
          <AccessibleButton
            variant="secondary"
            onClick={onLogOut}
            className="flex-1"
            ariaLabel="Log out now"
          >
            Log Out Now
          </AccessibleButton>

          <AccessibleButton
            variant="primary"
            onClick={onStayLoggedIn}
            className="flex-1"
            ariaLabel="Stay logged in and extend session"
          >
            Stay Logged In
          </AccessibleButton>
        </div>

        {/* Help Text */}
        <p className="text-xs text-center text-slate-500 dark:text-slate-500">
          Sessions expire after 30 days of inactivity. Regular activity keeps your session active.
        </p>
      </div>
    </AccessibleModal>
  );
}
