/**
 * i18n Test Messages Constants
 *
 * Centralized translations for test environment.
 * Use these constants in both testMessages and test assertions
 * to avoid magic strings and ensure consistency.
 *
 * Issue #3077: 2FA component test support
 */

// =============================================================================
// OAuth Messages
// =============================================================================

export const oauthMessages = {
  'auth.oauth.separator': 'Or continue with',
  'auth.oauth.google': 'Continue with Google',
  'auth.oauth.discord': 'Continue with Discord',
  'auth.oauth.github': 'Continue with GitHub',
} as const;

// =============================================================================
// 2FA Verification Messages
// =============================================================================

export const twoFactorVerificationMessages = {
  'auth.2fa.verificationTitle': 'Two-Factor Authentication',
  'auth.2fa.verificationSubtitle': 'Enter the 6-digit code from your authenticator app',
  'auth.2fa.codeLabel': 'Verification Code',
  'auth.2fa.rememberDevice': 'Trust this device for 30 days',
  'auth.2fa.verifying': 'Verifying...',
  'auth.2fa.verify': 'Verify',
  'auth.2fa.useBackupCode': 'Use a backup code instead',
} as const;

// =============================================================================
// 2FA Setup Messages
// =============================================================================

export const twoFactorSetupMessages = {
  'auth.2fa.setupTitle': 'Set Up Two-Factor Authentication',
  'auth.2fa.setupSubtitle': 'Scan this QR code with your authenticator app (Google Authenticator, Authy, etc.)',
  'auth.2fa.step1': 'Step 1: Scan QR Code',
  'auth.2fa.step2': 'Step 2: Verify & Enable',
  'auth.2fa.cantScan': "Can't scan? Enter manually",
  'auth.2fa.secretKey': 'Secret Key',
  'auth.2fa.enterCode': 'Enter the 6-digit code from your app',
  'auth.2fa.verifyAndEnable': 'Verify & Enable',
} as const;

// =============================================================================
// 2FA Disable Messages
// =============================================================================

export const twoFactorDisableMessages = {
  'auth.2fa.disableTitle': 'Disable Two-Factor Authentication',
  'auth.2fa.disableSubtitle': 'Enter your password and a code to disable 2FA',
  'auth.2fa.disableWarningTitle': 'Warning: Your account will be less secure',
  'auth.2fa.disableWarning':
    'Disabling two-factor authentication removes an important security layer from your account. Anyone with your password will be able to access your account.',
  'auth.2fa.currentPassword': 'Current Password',
  'auth.2fa.enterPassword': 'Enter your password',
  'auth.2fa.codeOrBackup': 'TOTP Code or Backup Code',
  'auth.2fa.enterCodeOrBackup': '000000 or XXXX-XXXX',
  'auth.2fa.disableButton': 'Disable 2FA',
  'auth.2fa.disabling': 'Disabling...',
  'auth.2fa.codeRequired': 'Please enter a 6-digit code or backup code',
  'auth.2fa.codeInvalid': 'Code is too long',
} as const;

// =============================================================================
// 2FA Backup/Recovery Codes Messages
// =============================================================================

export const twoFactorBackupCodesMessages = {
  'auth.2fa.backupCodesTitle': 'Save Your Backup Codes',
  'auth.2fa.backupCodesWarningTitle': 'Important: Save these codes now!',
  'auth.2fa.backupCodesWarning':
    'These codes are your only way to access your account if you lose your authenticator device. Each code can only be used once.',
  'auth.2fa.backupCodesList': 'Backup codes',
  'auth.2fa.copyCodes': 'Copy Codes',
  'auth.2fa.downloadCodes': 'Download',
  'auth.2fa.savedCodes': "I've Saved My Codes",
} as const;

// =============================================================================
// Common Messages
// =============================================================================

export const commonMessages = {
  'common.cancel': 'Cancel',
  'common.confirm': 'Confirm',
  'common.close': 'Close',
  'common.copy': 'Copy',
  'common.copied': 'Copied!',
  'common.downloaded': 'Downloaded!',
} as const;

// =============================================================================
// Validation Messages
// =============================================================================

export const validationMessages = {
  'validation.passwordRequired': 'Password is required',
  'validation.required': 'This field is required',
} as const;

// =============================================================================
// All Test Messages (combined)
// =============================================================================

export const allTestMessages: Record<string, string> = {
  ...oauthMessages,
  ...twoFactorVerificationMessages,
  ...twoFactorSetupMessages,
  ...twoFactorDisableMessages,
  ...twoFactorBackupCodesMessages,
  ...commonMessages,
  ...validationMessages,
};

// =============================================================================
// Type-safe message getter
// =============================================================================

type AllMessageKeys =
  | keyof typeof oauthMessages
  | keyof typeof twoFactorVerificationMessages
  | keyof typeof twoFactorSetupMessages
  | keyof typeof twoFactorDisableMessages
  | keyof typeof twoFactorBackupCodesMessages
  | keyof typeof commonMessages
  | keyof typeof validationMessages;

/**
 * Get a translated message by key (type-safe)
 * Use this in tests to avoid magic strings
 *
 * @example
 * expect(screen.getByText(msg('auth.2fa.disableTitle'))).toBeInTheDocument();
 */
export function msg(key: AllMessageKeys): string {
  return allTestMessages[key];
}
