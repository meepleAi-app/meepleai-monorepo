/**
 * VerificationSuccess Component (Issue #3076)
 *
 * Displays success message after email verification.
 * Features:
 * - Success animation/icon
 * - Auto-redirect countdown to dashboard
 * - Manual redirect button
 * - Accessible design (WCAG 2.1 AA)
 *
 * @example
 * ```tsx
 * <VerificationSuccess
 *   email="user@example.com"
 *   redirectUrl="/dashboard"
 *   autoRedirectSeconds={3}
 *   onRedirect={handleRedirect}
 * />
 * ```
 */

'use client';

import { useEffect, useState, useRef } from 'react';

import { CheckCircle2, ArrowRight } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { useTranslation } from '@/hooks/useTranslation';

export interface VerificationSuccessProps {
  /** The verified email address */
  email?: string;

  /** URL to redirect to after success */
  redirectUrl?: string;

  /** Seconds before auto-redirect (0 = no auto-redirect) */
  autoRedirectSeconds?: number;

  /** Callback when redirect is triggered (manual or auto) */
  onRedirect?: () => void;

  /** Optional test ID for testing */
  'data-testid'?: string;
}

export function VerificationSuccess({
  email,
  redirectUrl = '/dashboard',
  autoRedirectSeconds = 3,
  onRedirect,
  'data-testid': testId,
}: VerificationSuccessProps) {
  const { t } = useTranslation();
  const [countdown, setCountdown] = useState(autoRedirectSeconds);
  const timerRef = useRef<NodeJS.Timeout | null>(null);
  const hasRedirectedRef = useRef(false);

  /**
   * Handle countdown and auto-redirect
   */
  useEffect(() => {
    if (autoRedirectSeconds <= 0) return;

    timerRef.current = setInterval(() => {
      setCountdown(prev => {
        if (prev <= 1 && !hasRedirectedRef.current) {
          hasRedirectedRef.current = true;
          // Clear timer before redirect
          if (timerRef.current) {
            clearInterval(timerRef.current);
          }
          // Trigger redirect
          if (onRedirect) {
            onRedirect();
          } else {
            window.location.href = redirectUrl;
          }
          return 0;
        }
        return prev - 1;
      });
    }, 1000);

    return () => {
      if (timerRef.current) {
        clearInterval(timerRef.current);
      }
    };
  }, [autoRedirectSeconds, onRedirect, redirectUrl]);

  /**
   * Handle manual redirect button click
   */
  const handleManualRedirect = () => {
    if (hasRedirectedRef.current) return;
    hasRedirectedRef.current = true;

    if (timerRef.current) {
      clearInterval(timerRef.current);
    }

    if (onRedirect) {
      onRedirect();
    } else {
      window.location.href = redirectUrl;
    }
  };

  return (
    <div
      className="flex flex-col items-center text-center space-y-6"
      data-testid={testId}
      role="status"
      aria-live="polite"
    >
      {/* Success Icon with Animation */}
      <div className="relative">
        <div className="w-20 h-20 bg-green-500/10 dark:bg-green-500/20 rounded-full flex items-center justify-center animate-in zoom-in-50 duration-300">
          <CheckCircle2
            className="w-10 h-10 text-green-500 dark:text-green-400"
            aria-hidden="true"
          />
        </div>
        {/* Pulse animation ring */}
        <div className="absolute inset-0 w-20 h-20 bg-green-500/20 rounded-full animate-ping" />
      </div>

      {/* Title */}
      <div className="space-y-2">
        <h2 className="text-2xl font-bold text-foreground">
          {t('auth.emailVerification.success.title')}
        </h2>
        <p className="text-muted-foreground">
          {t('auth.emailVerification.success.description')}
        </p>
      </div>

      {/* Email Display (optional) */}
      {email && (
        <div className="bg-green-500/5 dark:bg-green-500/10 border border-green-500/20 rounded-lg px-4 py-3 w-full">
          <p className="text-sm text-green-700 dark:text-green-300">
            {email}
          </p>
        </div>
      )}

      {/* Countdown or Redirect Button */}
      <div className="w-full space-y-3">
        <Button
          className="w-full"
          onClick={handleManualRedirect}
          data-testid="continue-to-dashboard-button"
        >
          {t('auth.emailVerification.success.continueButton')}
          <ArrowRight className="w-4 h-4 ml-2" aria-hidden="true" />
        </Button>

        {/* Countdown Message */}
        {countdown > 0 && autoRedirectSeconds > 0 && (
          <p className="text-sm text-muted-foreground" aria-live="polite">
            {t('auth.emailVerification.success.redirectCountdown', { seconds: countdown })}
          </p>
        )}
      </div>

      {/* Additional Info */}
      <p className="text-xs text-muted-foreground">
        {t('auth.emailVerification.success.welcomeMessage')}
      </p>
    </div>
  );
}
