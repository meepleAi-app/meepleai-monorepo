'use client';

/**
 * IntlProvider - React Intl Provider Component
 *
 * This component wraps the application with react-intl's IntlProvider,
 * providing internationalization support for Italian (primary) and other languages.
 *
 * Issue #990: BGAI-049 - i18n setup (React Intl, it.json)
 *
 * Features:
 * - Type-safe translations
 * - Automatic locale detection (browser language)
 * - Fallback to Italian (default)
 * - Flattened message format for react-intl
 *
 * @see https://formatjs.io/docs/react-intl
 */

import { ReactNode, useEffect, useMemo, useState } from 'react';

import { IntlProvider as ReactIntlProvider } from 'react-intl';

import { createErrorContext } from '@/lib/errors';
import { logger } from '@/lib/logger';
import { DEFAULT_LOCALE, type Locale, getMessages, flattenMessages } from '@/locales';

interface IntlProviderProps {
  children: ReactNode;
  locale?: Locale;
}

/**
 * Get browser locale with fallback
 */
function getBrowserLocale(): Locale {
  // Always return default locale in test environment
  if (process.env.NODE_ENV === 'test') {
    return DEFAULT_LOCALE;
  }

  if (typeof window === 'undefined') {
    return DEFAULT_LOCALE;
  }

  try {
    const browserLang = window.navigator.language?.split('-')[0]?.toLowerCase();
    return browserLang === 'it' || browserLang === 'en' ? (browserLang as Locale) : DEFAULT_LOCALE;
  } catch {
    return DEFAULT_LOCALE;
  }
}

/**
 * IntlProvider Component
 *
 * Wraps children with react-intl's IntlProvider, providing i18n support.
 *
 * @example
 * ```tsx
 * <IntlProvider>
 *   <App />
 * </IntlProvider>
 * ```
 *
 * @example With specific locale
 * ```tsx
 * <IntlProvider locale="it">
 *   <App />
 * </IntlProvider>
 * ```
 */
export function IntlProvider({ children, locale }: IntlProviderProps) {
  // #3263: detect the browser locale only AFTER mount. On the server and on the
  // first client (hydration) render we use the same value — an explicit prop, or
  // DEFAULT_LOCALE — so the SSR HTML matches the first client render and a
  // non-Italian browser no longer triggers a hydration mismatch. The browser
  // locale is then adopted by the effect below (a brief, intentional swap).
  const [detectedLocale, setDetectedLocale] = useState<Locale | null>(null);

  useEffect(() => {
    if (!locale) {
      setDetectedLocale(getBrowserLocale());
    }
  }, [locale]);

  const currentLocale = locale ?? detectedLocale ?? DEFAULT_LOCALE;

  // Get messages for current locale and flatten for react-intl
  const flatMessages = useMemo(() => {
    const nestedMessages = getMessages(currentLocale);
    return flattenMessages(nestedMessages);
  }, [currentLocale]);

  return (
    <ReactIntlProvider
      messages={flatMessages}
      locale={currentLocale}
      defaultLocale={DEFAULT_LOCALE}
      onError={err => {
        // Only log missing translation errors in development
        if (process.env.NODE_ENV === 'development') {
          if (err.code === 'MISSING_TRANSLATION') {
            logger.warn(`Missing translation: ${err.message}`);
          } else {
            logger.error(
              'Intl error',
              err instanceof Error ? err : new Error(String(err)),
              createErrorContext('IntlProvider', 'onError', {
                locale: currentLocale,
                errorCode: err.code,
              })
            );
          }
        }
      }}
    >
      {children}
    </ReactIntlProvider>
  );
}
