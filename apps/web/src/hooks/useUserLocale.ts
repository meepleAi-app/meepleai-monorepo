/**
 * useUserLocale Hook - User Locale Preference with Fallback Chain
 *
 * Issue #2254: Get locale from user preferences instead of hardcoding
 *
 * Provides user's preferred locale with intelligent fallback:
 * 1. User preference from profile (Language field)
 * 2. Browser language (navigator.language)
 * 3. Default locale ('it' for Italian)
 *
 * @example
 * ```tsx
 * function MyComponent() {
 *   const locale = useUserLocale();
 *   const i18n = getSomeI18n(locale);
 *   return <div>{i18n.welcome}</div>;
 * }
 * ```
 */

import { useState, useEffect } from 'react';

import { api } from '@/lib/api';
import { logger } from '@/lib/logger';

/**
 * Supported locales in the application
 */
export type SupportedLocale = 'it' | 'en' | 'es' | 'fr' | 'de';

/**
 * Default locale when all fallbacks fail
 */
const DEFAULT_LOCALE: SupportedLocale = 'it';

/**
 * Supported locales list for validation
 */
const SUPPORTED_LOCALES: readonly SupportedLocale[] = ['it', 'en', 'es', 'fr', 'de'] as const;

/**
 * Get browser's preferred language
 * Uses navigator.language with fallback to navigator.languages[0]
 *
 * @returns Browser language code (e.g., 'en-US', 'it-IT') or null
 */
function getBrowserLanguage(): string | null {
  if (typeof window === 'undefined') return null;

  // Modern browsers support navigator.languages (array of preferences)
  if (Array.isArray(navigator.languages) && navigator.languages.length > 0) {
    return navigator.languages[0];
  }

  // Fallback to single language property
  return navigator.language || null;
}

/**
 * Type guard to check if a value is a supported locale
 *
 * @param locale - Value to check
 * @returns True if locale is supported
 */
function isSupportedLocale(locale: unknown): locale is SupportedLocale {
  return typeof locale === 'string' && SUPPORTED_LOCALES.includes(locale as SupportedLocale);
}

/**
 * Normalize language code to supported locale
 * Extracts primary language from codes like 'en-US', 'it-IT'
 *
 * @param languageCode - Full language code (e.g., 'en-US')
 * @returns Normalized locale (e.g., 'en') or null if not supported
 */
function normalizeToSupportedLocale(languageCode: string | null): SupportedLocale | null {
  if (!languageCode) return null;

  // Extract primary language (e.g., 'en' from 'en-US')
  const primaryLanguage = languageCode.split('-')[0].toLowerCase();

  // Type-safe check using type guard
  if (isSupportedLocale(primaryLanguage)) {
    return primaryLanguage;
  }

  return null;
}

/**
 * Hook to retrieve user's locale with intelligent fallback
 *
 * Fallback chain:
 * 1. User preference from profile (UserProfile.Language)
 * 2. Browser language (navigator.language)
 * 3. Default locale ('it')
 *
 * @returns Current locale code ('it', 'en', 'es', 'fr', 'de')
 */
export function useUserLocale(): SupportedLocale {
  const [locale, setLocale] = useState<SupportedLocale>(DEFAULT_LOCALE);

  useEffect(() => {
    async function loadUserLocale() {
      try {
        // Step 1: Try to get user preference from profile
        // Uses centralized API client for circuit breaker, deduplication, metrics
        const profile = await api.auth.getProfile();
        const userLanguage = profile?.language;

        if (isSupportedLocale(userLanguage)) {
          setLocale(userLanguage);
          return;
        }
      } catch (error) {
        // User not logged in or API error - continue to fallbacks
        // Note: This is expected for anonymous users, so we log as debug
        logger.debug('User locale not available from profile, using browser fallback', {
          component: 'useUserLocale',
          metadata: { error: error instanceof Error ? error.message : String(error) },
        });
      }

      // Step 2: Try browser language
      const browserLanguage = getBrowserLanguage();
      const normalizedBrowserLocale = normalizeToSupportedLocale(browserLanguage);

      if (normalizedBrowserLocale) {
        setLocale(normalizedBrowserLocale);
        return;
      }

      // Step 3: Use default locale
      setLocale(DEFAULT_LOCALE);
    }

    void loadUserLocale();
  }, []);

  return locale;
}

/**
 * Synchronous function to get browser locale without user profile
 * Useful for server-side rendering or when user is not logged in
 *
 * @returns Browser locale or default locale
 */
export function getBrowserLocale(): SupportedLocale {
  const browserLanguage = getBrowserLanguage();
  const normalized = normalizeToSupportedLocale(browserLanguage);
  return normalized || DEFAULT_LOCALE;
}
