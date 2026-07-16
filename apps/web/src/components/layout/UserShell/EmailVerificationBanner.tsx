'use client';

/**
 * EmailVerificationBanner — F1 #1974 (audit 2026-06-07).
 *
 * Renders a non-blocking warning band on the authenticated shell when the
 * current user is in the registration grace period (BE field
 * `EmailVerified == false`). Pre-fix the FE silently dropped the BE flag
 * because `AuthUserSchema` didn't expose it; users were confused why some
 * privileged actions returned errors after a successful registration.
 *
 * Contract:
 *   - Reads `useCurrentUser()` — if loading or no user, renders null.
 *   - Hidden when `user.emailVerified !== false` (covers `true`, `undefined`,
 *     and any legacy payload that omits the field). The banner intentionally
 *     only shows when the BE explicitly tells us the email is unverified.
 *   - Locally dismissible per-session via `sessionStorage` so a user can
 *     temporarily hide the banner; it returns on the next page load.
 *   - Primary CTA links to `/email-verification` (the existing pending page).
 */

import { useEffect, useState } from 'react';

import { Mail, X } from 'lucide-react';
import Link from 'next/link';

import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import { useTranslation } from '@/hooks/useTranslation';
import { cn } from '@/lib/utils';

const DISMISS_STORAGE_KEY = 'meepleai.emailVerifyBanner.dismissed';

export function EmailVerificationBanner() {
  const { t } = useTranslation();
  const { data: user, isLoading } = useCurrentUser();
  const [dismissed, setDismissed] = useState(false);

  // Hydrate dismissal state from sessionStorage on mount (post-hydration to
  // avoid SSR/CSR mismatch). useEffect → only runs on the client.
  useEffect(() => {
    try {
      if (sessionStorage.getItem(DISMISS_STORAGE_KEY) === '1') {
        setDismissed(true);
      }
    } catch {
      // sessionStorage unavailable (private mode, etc.) — just don't persist.
    }
  }, []);

  const handleDismiss = () => {
    setDismissed(true);
    try {
      sessionStorage.setItem(DISMISS_STORAGE_KEY, '1');
    } catch {
      // ignore
    }
  };

  if (isLoading || !user) return null;
  // Only render when the BE explicitly says the email is NOT verified.
  // `undefined` (legacy payload, OAuth flow) leaves the banner hidden.
  if (user.emailVerified !== false) return null;
  if (dismissed) return null;

  return (
    <div
      data-testid="email-verification-banner"
      role="status"
      className={cn(
        'flex items-center gap-3 border-b border-[hsl(var(--c-warning)/0.3)] bg-[hsl(var(--c-warning)/0.1)] px-4 py-2'
      )}
    >
      <Mail aria-hidden="true" className="h-4 w-4 shrink-0 text-[hsl(var(--c-warning))]" />
      <div className="min-w-0 flex-1">
        <p className="font-display text-[13px] font-bold text-foreground">
          {t('auth.emailVerification.banner.title')}
        </p>
        <p className="truncate text-xs text-muted-foreground">
          {t('auth.emailVerification.banner.description')}
        </p>
      </div>
      <Link
        href="/email-verification"
        className="rounded-md bg-[hsl(var(--c-warning))] px-3 py-1 text-xs font-bold text-white hover:bg-[hsl(var(--c-warning)/0.9)]"
        data-testid="email-verification-banner-cta"
      >
        {t('auth.emailVerification.banner.cta')}
      </Link>
      <button
        type="button"
        onClick={handleDismiss}
        aria-label={t('auth.emailVerification.banner.dismissAriaLabel')}
        className="rounded-md p-1 text-muted-foreground hover:bg-muted hover:text-foreground"
        data-testid="email-verification-banner-dismiss"
      >
        <X className="h-3.5 w-3.5" aria-hidden="true" />
      </button>
    </div>
  );
}
