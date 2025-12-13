/**
 * Mock for next-intl in test environment
 *
 * This mock is used by Vitest to resolve `next-intl` imports at transform time.
 * It provides a `useTranslations` hook that uses the test-i18n utility.
 */

import { t } from '../test-i18n';

/**
 * Mock useTranslations hook that returns a translator function
 * using the test-i18n utility for language-agnostic testing.
 *
 * Supports interpolation parameters like `t('key', { count: 5, max: 100 })`
 * which replaces `{count}` and `{max}` placeholders in the translation.
 *
 * @param namespace - The translation namespace (e.g., 'comments', 'questionInput')
 * @returns A function that translates keys within the namespace
 */
export function useTranslations(namespace: string) {
  return (key: string, values?: Record<string, string | number>) => {
    let translation = t(`${namespace}.${key}`);
    if (values) {
      Object.entries(values).forEach(([k, v]) => {
        translation = translation.replace(new RegExp(`\\{${k}\\}`, 'g'), String(v));
      });
    }
    return translation;
  };
}

/**
 * Mock useLocale hook
 */
export function useLocale() {
  return 'it'; // Default to Italian for tests
}

/**
 * Mock useMessages hook
 */
export function useMessages() {
  return {};
}

/**
 * Mock useNow hook
 */
export function useNow() {
  return new Date();
}

/**
 * Mock useTimeZone hook
 */
export function useTimeZone() {
  return 'Europe/Rome';
}

/**
 * Mock useFormatter hook
 */
export function useFormatter() {
  return {
    number: (value: number) => String(value),
    dateTime: (value: Date) => value.toISOString(),
    relativeTime: (value: Date) => 'now',
    list: (items: string[]) => items.join(', '),
  };
}

/**
 * Mock NextIntlClientProvider component
 */
export function NextIntlClientProvider({ children }: { children: React.ReactNode }) {
  return children;
}
