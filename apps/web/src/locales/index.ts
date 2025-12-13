/* eslint-disable security/detect-object-injection -- Safe typed Record access with Locale keys and message paths */
/**
 * i18n Locales Configuration
 *
 * This file exports all available locales and provides type-safe message definitions.
 *
 * Issue #990: BGAI-049 - i18n setup (React Intl, it.json)
 *
 * @see https://formatjs.io/docs/react-intl
 */

import itMessages from './it.json';
import enMessages from './en.json';

/**
 * Available locales in the application
 */
export const LOCALES = {
  IT: 'it',
  EN: 'en',
} as const;

/**
 * Default locale for the application
 */
export const DEFAULT_LOCALE = LOCALES.IT;

/**
 * Type-safe locale keys
 */
export type Locale = (typeof LOCALES)[keyof typeof LOCALES];

/**
 * Message catalog type inferred from Italian messages
 */
export type Messages = typeof itMessages;

/**
 * All available message catalogs
 */
export const messages: Record<Locale, Messages | Record<string, unknown>> = {
  [LOCALES.IT]: itMessages,
  [LOCALES.EN]: enMessages,
};

/**
 * Get messages for a specific locale
 * Falls back to default locale if not found or empty
 */
export function getMessages(locale: Locale): Messages | Record<string, unknown> {
  const localeMessages = messages[locale];

  // Fallback to default locale if messages don't exist or are empty
  if (!localeMessages || Object.keys(localeMessages).length === 0) {
    return messages[DEFAULT_LOCALE];
  }

  return localeMessages;
}

/**
 * Flatten nested message object for react-intl
 * Converts { common: { loading: "..." } } to { "common.loading": "..." }
 */
export function flattenMessages(
  nestedMessages: Record<string, unknown>,
  prefix = ''
): Record<string, string> {
  return Object.keys(nestedMessages).reduce(
    (messages, key) => {
      const value = nestedMessages[key];
      const prefixedKey = prefix ? `${prefix}.${key}` : key;

      if (typeof value === 'string') {
        messages[prefixedKey] = value;
      } else if (typeof value === 'object' && value !== null) {
        Object.assign(messages, flattenMessages(value as Record<string, unknown>, prefixedKey));
      }

      return messages;
    },
    {} as Record<string, string>
  );
}
