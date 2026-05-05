/**
 * Cookie consent storage contract (GDPR, ePrivacy Art. 5(3)).
 *
 * Source of truth for both `CookieConsentBanner` (first-visit prompt)
 * and `/cookie-settings` (explicit preferences page).
 */

export const CONSENT_KEY = 'meepleai-cookie-consent';
export const CONSENT_VERSION = '1.0';

export interface CookieConsent {
  version: string;
  essential: true;
  analytics: boolean;
  functional: boolean;
  timestamp: string;
}

export function getStoredConsent(): CookieConsent | null {
  if (typeof window === 'undefined') return null;
  try {
    const stored = localStorage.getItem(CONSENT_KEY);
    if (!stored) return null;
    const consent = JSON.parse(stored) as CookieConsent;
    if (consent.version !== CONSENT_VERSION) return null;
    return consent;
  } catch {
    return null;
  }
}

export function setStoredConsent(next: {
  analytics: boolean;
  functional: boolean;
}): CookieConsent | null {
  if (typeof window === 'undefined') return null;
  const consent: CookieConsent = {
    version: CONSENT_VERSION,
    essential: true,
    analytics: next.analytics,
    functional: next.functional,
    timestamp: new Date().toISOString(),
  };
  try {
    localStorage.setItem(CONSENT_KEY, JSON.stringify(consent));
    return consent;
  } catch {
    // Storage quota exceeded or unavailable — record not persisted.
    return null;
  }
}

export function clearStoredConsent(): void {
  if (typeof window === 'undefined') return;
  try {
    localStorage.removeItem(CONSENT_KEY);
  } catch {
    // no-op
  }
}
