/**
 * i18n auth.2fa Keys Validation Tests
 *
 * Issue #3172: TwoFactorSetup / TwoFactorDisable / TwoFactorRecoveryCodes reference the
 * `auth.2fa.*` namespace which was missing from BOTH locale catalogs (the real namespace
 * shipped was `auth.twoFactor`). Italian users saw the hardcoded English defaultMessage.
 *
 * These tests assert every used `auth.2fa.*` key exists (flattened) in it.json and en.json.
 */

import itMessages from '@/locales/it.json';
import enMessages from '@/locales/en.json';
import { flattenMessages } from '@/locales';

// Every auth.2fa.* key referenced by the setup/disable/recovery-codes components.
const AUTH_2FA_KEYS = [
  'auth.2fa.setupTitle',
  'auth.2fa.setupSubtitle',
  'auth.2fa.step1',
  'auth.2fa.step2',
  'auth.2fa.cantScan',
  'auth.2fa.secretKey',
  'auth.2fa.enterCode',
  'auth.2fa.verifyAndEnable',
  'auth.2fa.verifying',
  'auth.2fa.disableTitle',
  'auth.2fa.disableSubtitle',
  'auth.2fa.disableWarningTitle',
  'auth.2fa.disableWarning',
  'auth.2fa.currentPassword',
  'auth.2fa.enterPassword',
  'auth.2fa.codeOrBackup',
  'auth.2fa.enterCodeOrBackup',
  'auth.2fa.codeInvalid',
  'auth.2fa.codeRequired',
  'auth.2fa.disableButton',
  'auth.2fa.disabling',
  'auth.2fa.backupCodesTitle',
  'auth.2fa.backupCodesWarningTitle',
  'auth.2fa.backupCodesWarning',
  'auth.2fa.backupCodesList',
  'auth.2fa.copyCodes',
  'auth.2fa.downloadCodes',
  'auth.2fa.savedCodes',
  'auth.2fa.lowBackupCodesWarning',
];

describe('auth.2fa i18n key coverage (#3172)', () => {
  const itFlat = flattenMessages(itMessages as Record<string, unknown>);
  const enFlat = flattenMessages(enMessages as Record<string, unknown>);

  it.each(AUTH_2FA_KEYS)('IT catalog has "%s"', key => {
    expect(itFlat[key]).toBeTruthy();
  });

  it.each(AUTH_2FA_KEYS)('EN catalog has "%s"', key => {
    expect(enFlat[key]).toBeTruthy();
  });
});
