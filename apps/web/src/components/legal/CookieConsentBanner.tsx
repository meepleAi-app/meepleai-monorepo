/**
 * CookieConsentBanner - GDPR-compliant cookie consent UI
 *
 * Displays a bottom banner on first visit asking for cookie consent.
 * Required by ePrivacy Directive Art. 5(3) for non-essential cookies.
 *
 * Consent levels:
 * - Essential: Always active (no consent needed)
 * - Analytics: Requires explicit consent
 * - Functional: Requires explicit consent
 */

'use client';

import { useState, useEffect, useCallback } from 'react';

import Link from 'next/link';

import { Button } from '@/components/ui/primitives/button';
import { useTranslation } from '@/hooks/useTranslation';
import { cn } from '@/lib/utils';

const CONSENT_KEY = 'meepleai-cookie-consent';
const CONSENT_VERSION = '1.0';

export interface CookieConsent {
  version: string;
  essential: true; // Always true
  analytics: boolean;
  functional: boolean;
  timestamp: string;
}

/**
 * Read stored cookie consent
 */
function getStoredConsent(): CookieConsent | null {
  if (typeof window === 'undefined') return null;
  try {
    const stored = localStorage.getItem(CONSENT_KEY);
    if (!stored) return null;
    const consent = JSON.parse(stored) as CookieConsent;
    // Invalidate if version changed
    if (consent.version !== CONSENT_VERSION) return null;
    return consent;
  } catch {
    return null;
  }
}

/**
 * Save cookie consent
 */
function saveConsent(consent: CookieConsent) {
  try {
    localStorage.setItem(CONSENT_KEY, JSON.stringify(consent));
  } catch {
    // localStorage not available
  }
}

/**
 * Hook to access cookie consent state
 */
export function useCookieConsent() {
  const [consent, setConsent] = useState<CookieConsent | null>(null);

  useEffect(() => {
    setConsent(getStoredConsent());
  }, []);

  const updateConsent = useCallback(
    (partial: Partial<Omit<CookieConsent, 'version' | 'essential' | 'timestamp'>>) => {
      const newConsent: CookieConsent = {
        version: CONSENT_VERSION,
        essential: true,
        analytics: partial.analytics ?? consent?.analytics ?? false,
        functional: partial.functional ?? consent?.functional ?? false,
        timestamp: new Date().toISOString(),
      };
      saveConsent(newConsent);
      setConsent(newConsent);
      return newConsent;
    },
    [consent]
  );

  const resetConsent = useCallback(() => {
    try {
      localStorage.removeItem(CONSENT_KEY);
    } catch {
      // localStorage not available
    }
    setConsent(null);
  }, []);

  return { consent, updateConsent, resetConsent, hasConsented: consent !== null };
}

interface CookieConsentBannerProps {
  className?: string;
  /** Force show even if user already consented (for "Manage Cookies" flow) */
  forceShow?: boolean;
  /** Called when banner is dismissed after forceShow */
  onDismiss?: () => void;
}

export function CookieConsentBanner({ className, forceShow, onDismiss }: CookieConsentBannerProps) {
  const { t } = useTranslation();
  const { consent, updateConsent, hasConsented } = useCookieConsent();
  const [showDetails, setShowDetails] = useState(false);
  const [mounted, setMounted] = useState(false);

  // Local state for checkboxes — only persisted when user clicks "Save preferences"
  // Pre-fill from existing consent when re-managing
  const [localAnalytics, setLocalAnalytics] = useState(consent?.analytics ?? false);
  const [localFunctional, setLocalFunctional] = useState(consent?.functional ?? false);

  useEffect(() => {
    setMounted(true);
  }, []);

  // Sync local state when consent changes (e.g. forceShow with existing consent)
  useEffect(() => {
    if (consent) {
      setLocalAnalytics(consent.analytics);
      setLocalFunctional(consent.functional);
    }
  }, [consent]);

  // Don't render anything until mounted (avoid hydration mismatch)
  // Don't render if user already consented (unless forceShow)
  if (!mounted || (hasConsented && !forceShow)) return null;

  const handleAcceptAll = () => {
    updateConsent({ analytics: true, functional: true });
    onDismiss?.();
  };

  const handleAcceptEssentialOnly = () => {
    updateConsent({ analytics: false, functional: false });
    onDismiss?.();
  };

  const handleSavePreferences = () => {
    updateConsent({ analytics: localAnalytics, functional: localFunctional });
    onDismiss?.();
  };

  return (
    <div
      className={cn(
        'fixed bottom-0 inset-x-0 z-50 p-4 md:p-6',
        'bg-background/95 backdrop-blur-sm border-t shadow-lg',
        'animate-in slide-in-from-bottom duration-300',
        className
      )}
      role="dialog"
      aria-label={t('legal.cookieBanner.ariaLabel')}
      data-testid="cookie-consent-banner"
    >
      <div className="max-w-4xl mx-auto">
        <div className="flex flex-col gap-4">
          {/* Main message */}
          <div>
            <h3 className="font-semibold text-foreground mb-1">{t('legal.cookieBanner.title')}</h3>
            <p className="text-sm text-muted-foreground">
              {t('legal.cookieBanner.description')}{' '}
              <Link
                href="/cookies"
                className="text-blue-600 dark:text-blue-400 underline hover:text-blue-800"
              >
                {t('legal.cookieBanner.readPolicy')}
              </Link>
            </p>
          </div>

          {/* Detailed preferences (expandable) */}
          {showDetails && (
            <div className="space-y-3 py-3 border-y border-slate-200 dark:border-slate-700">
              <label className="flex items-center gap-3 text-sm">
                <input
                  type="checkbox"
                  checked={true}
                  disabled
                  className="rounded border-slate-300"
                  aria-label={t('legal.cookieBanner.essentialAriaLabel')}
                />
                <div>
                  <span className="font-medium text-foreground">
                    {t('legal.cookieBanner.essential')}
                  </span>
                  <span className="text-muted-foreground ml-1">
                    ({t('legal.cookieBanner.essentialAlways')})
                  </span>
                </div>
              </label>

              <label className="flex items-center gap-3 text-sm cursor-pointer">
                <input
                  type="checkbox"
                  checked={localAnalytics}
                  onChange={e => setLocalAnalytics(e.target.checked)}
                  className="rounded border-slate-300"
                  aria-label={t('legal.cookieBanner.analyticsAriaLabel')}
                />
                <div>
                  <span className="font-medium text-foreground">
                    {t('legal.cookieBanner.analytics')}
                  </span>
                  <span className="text-muted-foreground ml-1">
                    — {t('legal.cookieBanner.analyticsDescription')}
                  </span>
                </div>
              </label>

              <label className="flex items-center gap-3 text-sm cursor-pointer">
                <input
                  type="checkbox"
                  checked={localFunctional}
                  onChange={e => setLocalFunctional(e.target.checked)}
                  className="rounded border-slate-300"
                  aria-label={t('legal.cookieBanner.functionalAriaLabel')}
                />
                <div>
                  <span className="font-medium text-foreground">
                    {t('legal.cookieBanner.functional')}
                  </span>
                  <span className="text-muted-foreground ml-1">
                    — {t('legal.cookieBanner.functionalDescription')}
                  </span>
                </div>
              </label>
            </div>
          )}

          {/* Action buttons */}
          <div className="flex flex-col sm:flex-row gap-2 sm:items-center sm:justify-end">
            {!showDetails ? (
              <>
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => setShowDetails(true)}
                  className="text-muted-foreground"
                >
                  {t('legal.cookieBanner.customize')}
                </Button>
                <Button variant="outline" size="sm" onClick={handleAcceptEssentialOnly}>
                  {t('legal.cookieBanner.essentialOnly')}
                </Button>
                <Button size="sm" onClick={handleAcceptAll}>
                  {t('legal.cookieBanner.acceptAll')}
                </Button>
              </>
            ) : (
              <>
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => setShowDetails(false)}
                  className="text-muted-foreground"
                >
                  {t('legal.cookieBanner.back')}
                </Button>
                <Button size="sm" onClick={handleSavePreferences}>
                  {t('legal.cookieBanner.savePreferences')}
                </Button>
              </>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
