'use client';

/**
 * OnboardingReminderBanner
 * Issue #326: Reminder banner for users who skipped onboarding wizard.
 *
 * Shows a subtle, dismissible banner at the top of the app shell
 * encouraging users to complete their profile setup.
 */

import { useEffect, useState } from 'react';

import { X } from 'lucide-react';
import Link from 'next/link';

interface OnboardingReminderBannerProps {
  onDismiss?: () => void;
}

export function OnboardingReminderBanner({ onDismiss }: OnboardingReminderBannerProps) {
  const [dismissed, setDismissed] = useState(true); // Start hidden to avoid SSR mismatch

  useEffect(() => {
    try {
      setDismissed(localStorage.getItem('onboarding_banner_dismissed') === 'true');
    } catch {
      setDismissed(false);
    }
  }, []);

  if (dismissed) return null;

  const handleDismiss = () => {
    setDismissed(true);
    try {
      localStorage.setItem('onboarding_banner_dismissed', 'true');
    } catch {
      // localStorage not available
    }
    onDismiss?.();
  };

  return (
    <div
      className="flex items-center justify-between gap-4 border-b border-amber-200 bg-amber-50 px-4 py-2 text-sm text-amber-900 dark:border-amber-800 dark:bg-amber-950/50 dark:text-amber-200"
      role="status"
      aria-label="Onboarding reminder"
      data-testid="onboarding-reminder-banner"
    >
      <p>
        <span className="font-medium">Complete your profile setup</span>
        {' — '}
        <Link
          href="/accept-invite"
          className="underline hover:text-amber-700 dark:hover:text-amber-100"
        >
          Finish the onboarding wizard
        </Link>{' '}
        to get personalized game recommendations.
      </p>
      <button
        type="button"
        onClick={handleDismiss}
        className="shrink-0 rounded p-1 hover:bg-amber-100 dark:hover:bg-amber-900"
        aria-label="Dismiss onboarding reminder"
        data-testid="dismiss-onboarding-banner"
      >
        <X className="h-4 w-4" />
      </button>
    </div>
  );
}
