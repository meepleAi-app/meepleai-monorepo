/**
 * LegalLocaleToggle - Language selector for legal pages
 *
 * Allows users to switch between Italian and English on legal pages.
 * Stores preference in localStorage and overrides the app-level locale
 * via a nested IntlProvider backed by a shared React Context.
 */

'use client';

import { useState, useEffect, useCallback, createContext, useContext, type ReactNode } from 'react';

import { IntlProvider as ReactIntlProvider } from 'react-intl';

import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';
import { type Locale, LOCALES, getMessages, flattenMessages } from '@/locales';

const STORAGE_KEY = 'meepleai-legal-locale';

interface LegalLocaleToggleProps {
  className?: string;
}

interface LegalLocaleProviderProps {
  children: ReactNode;
}

interface LegalLocaleContextValue {
  locale: Locale;
  setLocale: (locale: Locale) => void;
  toggleLocale: () => void;
}

const LegalLocaleContext = createContext<LegalLocaleContextValue | null>(null);

/**
 * Read stored locale preference
 */
function getStoredLocale(): Locale | null {
  if (typeof window === 'undefined') return null;
  try {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored === 'it' || stored === 'en') return stored as Locale;
  } catch {
    // localStorage not available
  }
  return null;
}

/**
 * Custom hook for legal page locale management.
 * When used inside a LegalLocaleProvider, returns the shared context.
 * When used standalone, creates independent state (not recommended).
 */
export function useLegalLocale(): LegalLocaleContextValue {
  const ctx = useContext(LegalLocaleContext);
  if (ctx) return ctx;

  // Fallback for usage outside provider — should not happen in practice
  throw new Error('useLegalLocale must be used within a LegalLocaleProvider');
}

/**
 * Language toggle button for legal pages.
 * Must be used within a LegalLocaleProvider.
 */
export function LegalLocaleToggle({ className }: LegalLocaleToggleProps) {
  const { locale, setLocale } = useLegalLocale();

  return (
    <div
      className={cn('inline-flex items-center rounded-lg border p-0.5 bg-muted/50', className)}
      role="radiogroup"
      aria-label="Select language"
    >
      <Button
        variant={locale === LOCALES.IT ? 'default' : 'ghost'}
        size="sm"
        className={cn(
          'h-7 px-3 text-xs font-medium rounded-md',
          locale === LOCALES.IT
            ? 'bg-foreground text-background shadow-sm'
            : 'text-muted-foreground hover:text-foreground'
        )}
        onClick={() => setLocale(LOCALES.IT)}
        role="radio"
        aria-checked={locale === LOCALES.IT}
        aria-label="Italiano"
      >
        IT
      </Button>
      <Button
        variant={locale === LOCALES.EN ? 'default' : 'ghost'}
        size="sm"
        className={cn(
          'h-7 px-3 text-xs font-medium rounded-md',
          locale === LOCALES.EN
            ? 'bg-foreground text-background shadow-sm'
            : 'text-muted-foreground hover:text-foreground'
        )}
        onClick={() => setLocale(LOCALES.EN)}
        role="radio"
        aria-checked={locale === LOCALES.EN}
        aria-label="English"
      >
        EN
      </Button>
    </div>
  );
}

/**
 * Provider that wraps legal pages with a locale-specific IntlProvider.
 * Single source of truth for locale state — both LegalLocaleToggle and
 * content consumers share the same React Context.
 */
export function LegalLocaleProvider({ children }: LegalLocaleProviderProps) {
  // SSR-safe: always start with IT to avoid hydration mismatch,
  // then sync from localStorage in useEffect
  const [locale, setLocaleState] = useState<Locale>(LOCALES.IT);

  // Sync from localStorage on client mount
  useEffect(() => {
    const stored = getStoredLocale();
    if (stored) {
      setLocaleState(stored);
    }
  }, []);

  const setLocale = useCallback((newLocale: Locale) => {
    setLocaleState(newLocale);
    try {
      localStorage.setItem(STORAGE_KEY, newLocale);
    } catch {
      // localStorage not available
    }
  }, []);

  const toggleLocale = useCallback(() => {
    setLocaleState(prev => {
      const newLocale = prev === LOCALES.IT ? LOCALES.EN : LOCALES.IT;
      try {
        localStorage.setItem(STORAGE_KEY, newLocale);
      } catch {
        // localStorage not available
      }
      return newLocale;
    });
  }, []);

  const messages = getMessages(locale);
  const flatMessages = flattenMessages(messages);

  return (
    <LegalLocaleContext.Provider value={{ locale, setLocale, toggleLocale }}>
      <ReactIntlProvider
        messages={flatMessages}
        locale={locale}
        defaultLocale={LOCALES.IT}
        onError={() => {
          // Suppress missing translation errors in legal context
        }}
      >
        {children}
      </ReactIntlProvider>
    </LegalLocaleContext.Provider>
  );
}
