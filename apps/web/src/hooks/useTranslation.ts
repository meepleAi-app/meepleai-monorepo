/**
 * useTranslation - Custom hook for i18n translations
 *
 * This hook provides a type-safe and convenient API for accessing translations
 * in components. It wraps react-intl's useIntl hook with additional utilities.
 *
 * Issue #990: BGAI-049 - i18n setup (React Intl, it.json)
 *
 * @example Basic usage
 * ```tsx
 * function MyComponent() {
 *   const { t, formatMessage } = useTranslation();
 *   return <div>{t('common.loading')}</div>;
 * }
 * ```
 *
 * @example With interpolation
 * ```tsx
 * function SessionWarning() {
 *   const { t } = useTranslation();
 *   return <div>{t('auth.session.expiringSoon', { minutes: 5 })}</div>;
 * }
 * ```
 *
 * @example With rich text formatting
 * ```tsx
 * function FormattedMessage() {
 *   const { formatMessage } = useTranslation();
 *   return (
 *     <FormattedMessage
 *       id="app.tagline"
 *       defaultMessage="Your intelligent board game assistant"
 *     />
 *   );
 * }
 * ```
 *
 * @see https://formatjs.io/docs/react-intl/api
 */

import { useIntl } from 'react-intl';

import type { MessageDescriptor, IntlShape } from 'react-intl';

/**
 * Translation function type
 *
 * Supports two signatures:
 * - t(id): Translate with just the key
 * - t(id, defaultMessage): Translate with fallback
 * - t(id, values): Translate with interpolation values
 */
export type TranslationFunction = {
  (id: string): string;
  (id: string, defaultMessage: string): string;
  (id: string, values: Record<string, string | number | boolean | Date | null | undefined>): string;
};

/**
 * Hook return type
 */
export interface UseTranslationReturn {
  /**
   * Simple translation function
   * @param id - Message ID (e.g., 'common.loading')
   * @param values - Optional interpolation values
   * @returns Translated string
   */
  t: TranslationFunction;

  /**
   * Format message with descriptor
   * @param descriptor - Message descriptor object
   * @param values - Optional interpolation values
   * @returns Translated string
   */
  formatMessage: (
    descriptor: MessageDescriptor,
    values?: Record<string, string | number | boolean | Date | null | undefined>
  ) => string;

  /**
   * Current locale (e.g., 'it', 'en')
   */
  locale: string;

  /**
   * Format number according to locale
   */
  formatNumber: IntlShape['formatNumber'];

  /**
   * Format date according to locale
   */
  formatDate: IntlShape['formatDate'];

  /**
   * Format time according to locale
   */
  formatTime: IntlShape['formatTime'];

  /**
   * Format relative time (e.g., "2 hours ago")
   */
  formatRelativeTime: IntlShape['formatRelativeTime'];
}

/**
 * Custom hook for accessing i18n translations
 *
 * Wraps react-intl's useIntl hook with a simpler API and additional utilities.
 *
 * @returns Translation utilities
 */
export function useTranslation(): UseTranslationReturn {
  const intl = useIntl();

  /**
   * Simple translation function that accepts a dot-notated message ID
   * Supports optional default message or interpolation values
   */
  const t: TranslationFunction = ((
    id: string,
    valuesOrDefault?: string | Record<string, string | number | boolean | Date | null | undefined>
  ) => {
    // If second arg is a string, treat it as defaultMessage
    if (typeof valuesOrDefault === 'string') {
      return intl.formatMessage({ id, defaultMessage: valuesOrDefault });
    }
    // Otherwise treat it as interpolation values
    return intl.formatMessage({ id }, valuesOrDefault);
  }) as TranslationFunction;

  return {
    t,
    formatMessage: intl.formatMessage,
    locale: intl.locale,
    formatNumber: intl.formatNumber,
    formatDate: intl.formatDate,
    formatTime: intl.formatTime,
    formatRelativeTime: intl.formatRelativeTime,
  };
}

/**
 * Re-export commonly used react-intl components for convenience
 */
export { FormattedMessage, FormattedDate, FormattedTime, FormattedNumber } from 'react-intl';
