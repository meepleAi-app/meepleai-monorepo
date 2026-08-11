/**
 * i18n missing-key regression (#3193)
 *
 * Each dot-path below is consumed through a RAW translator (`useTranslation()`)
 * but was absent from BOTH locale catalogs, so react-intl rendered either the
 * raw dot-path id (when no defaultMessage was passed) or a hardcoded
 * `defaultMessage` in the wrong locale. This guard fails if any key regresses
 * out of either catalog.
 */

import enMessages from '@/locales/en.json';
import itMessages from '@/locales/it.json';

const REQUIRED_KEYS = [
  // Rendered as the raw id in an aria-label for every user (NotificationCenter).
  'notificationCenter.preferences',
  // Invite-only request-access form (/register) — was fully English for IT users.
  'auth.requestAccess.successTitle',
  'auth.requestAccess.successMessage',
  'auth.requestAccess.email',
  'auth.requestAccess.emailPlaceholder',
  'auth.requestAccess.submitting',
  'auth.requestAccess.submit',
  // 2FA disable validation error — was English for IT users.
  'validation.passwordRequired',
  // 2FA setup/recovery toasts — were English for IT users.
  'common.copied',
  'common.downloaded',
  // Session summary error state + gamebook aria — defaulted to Italian, broke EN.
  'common.errorTitle',
  'common.retry',
  'common.selected',
] as const;

function getNested(obj: unknown, path: string): unknown {
  return path
    .split('.')
    .reduce<unknown>(
      (acc, key) =>
        acc && typeof acc === 'object' ? (acc as Record<string, unknown>)[key] : undefined,
      obj
    );
}

describe('i18n missing-key regression (#3193)', () => {
  it.each(REQUIRED_KEYS)('IT catalog defines "%s"', key => {
    expect(getNested(itMessages, key)).toBeTruthy();
  });

  it.each(REQUIRED_KEYS)('EN catalog defines "%s"', key => {
    expect(getNested(enMessages, key)).toBeTruthy();
  });
});
